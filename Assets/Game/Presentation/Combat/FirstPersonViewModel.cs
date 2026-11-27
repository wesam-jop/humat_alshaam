using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Input;
using Adventure3D.Networking;
using UnityEngine;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// The player's arms and weapon on screen. Rendered by a second camera on its own layer so the gun
    /// never clips into walls. Procedural animation: look sway, walk / sprint bob, jump and landing
    /// motion, recoil kick (weapon and camera), reload (magazine out / in), weapon switch, aim down
    /// sights (with sniper scope), muzzle flash and light. Shots are shown immediately: on the
    /// authority from combat events, on a client by local prediction of the fire rate.
    /// </summary>
    [DefaultExecutionOrder(990)]
    public sealed class FirstPersonViewModel : MonoBehaviour
    {
        GameSession _session;
        LocalPlayerInput _input;
        FirstPersonCameraRig _rig;
        Camera _vmCamera;
        AudioSource _audio;

        Transform _pivot, _kick;
        ViewModelRig _model;
        byte _modelWeapon = 255;
        SpriteRenderer _flash;
        Light _flashLight;
        float _flashTimer;

        Vector3 _swayRot, _swayVel;
        float _bobPhase;
        float _bobWeight;
        Vector3 _kickPos, _kickRot, _camKick;
        float _landDip;
        float _aimBlend;
        float _switchBlend = 1f;   // 0 = lowered, 1 = raised
        bool _switching;
        byte _pendingWeapon = 255;
        float _reloadTime;
        bool _wasReloading;
        bool _wasAlive;
        float _predCooldown;
        bool _predTriggerWasDown;
        float _vertical;

        public bool ScopeActive { get; private set; }
        /// <summary>0 = hip fire, 1 = fully aimed down sights.</summary>
        public float AimBlend => _aimBlend;
        public float CurrentSpreadVisual { get; private set; }

        public static FirstPersonViewModel Create(FirstPersonCameraRig rig, GameSession session, LocalPlayerInput input)
        {
            var go = new GameObject("FirstPersonViewModel");
            go.transform.SetParent(rig.transform, false);
            var vm = go.AddComponent<FirstPersonViewModel>();
            vm._session = session;
            vm._input = input;
            vm._rig = rig;
            vm.Init();
            return vm;
        }

        void Init()
        {
            int layerMask = 1 << ViewModelBuilder.ViewModelLayer;
            _rig.Camera.cullingMask &= ~layerMask;

            var camGo = new GameObject("ViewModelCamera");
            camGo.transform.SetParent(transform, false);
            _vmCamera = camGo.AddComponent<Camera>();
            _vmCamera.clearFlags = CameraClearFlags.Depth;
            _vmCamera.cullingMask = layerMask;
            _vmCamera.depth = _rig.Camera.depth + 1;
            _vmCamera.fieldOfView = 62f;
            _vmCamera.nearClipPlane = 0.01f;
            _vmCamera.farClipPlane = 5f;

            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(transform, false);
            _kick = new GameObject("Kick").transform;
            _kick.SetParent(_pivot, false);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.playOnAwake = false;

            var flashGo = new GameObject("MuzzleFlash");
            _flash = flashGo.AddComponent<SpriteRenderer>();
            _flash.sprite = CombatFx.FlashSprite;
            _flash.color = new Color(1f, 0.82f, 0.45f, 1f);
            flashGo.layer = ViewModelBuilder.ViewModelLayer;
            flashGo.SetActive(false);
            var lightGo = new GameObject("MuzzleLight");
            _flashLight = lightGo.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.color = new Color(1f, 0.7f, 0.35f);
            _flashLight.range = 6f;
            _flashLight.intensity = 0f;
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0.1f, -0.05f, 0.8f);
        }

        /// <summary>Called by CombatEffects for the local player's authoritative shots.</summary>
        public void OnLocalShot(byte weaponIndex)
        {
            if (!_session.IsAuthority) return; // clients already played the predicted shot
            PlayShot(weaponIndex);
        }

        void PlayShot(byte weaponIndex)
        {
            var w = WeaponCatalog.Get(weaponIndex);
            float aimFactor = Mathf.Lerp(1f, 0.45f, _aimBlend);
            _kickPos += new Vector3(Random.Range(-0.004f, 0.004f), 0.006f, -0.03f - w.ViewKick * 0.01f) * aimFactor;
            _kickRot += new Vector3(-w.ViewKick * 2.2f, Random.Range(-1f, 1f) * w.ViewKick * 0.6f, Random.Range(-1.5f, 1.5f)) * aimFactor;
            _camKick += new Vector3(-w.ViewKick * 0.55f, Random.Range(-0.25f, 0.25f) * w.ViewKick, 0f);
            if (!w.IsKnife)
            {
                _flashTimer = 0.045f;
                if (_model != null && _model.Bolt != null) _model.Bolt.localPosition = _model.BoltRest + new Vector3(0f, 0f, -0.03f);
            }
            else _kickRot += new Vector3(25f, -35f, 20f);
            if (_model != null && _model.Anim != null)
            {
                string clip = _model.Anim.GetClip("shoot1") != null ? "shoot1"
                    : _model.Anim.GetClip("slash1") != null ? "slash1" : null;
                if (clip != null)
                {
                    _model.Anim.GetClip(clip).wrapMode = WrapMode.ClampForever;
                    _model.Anim.Rewind(clip);
                    _model.Anim.Play(clip, PlayMode.StopAll);
                }
            }
            _audio.PlayOneShot(ProceduralAudio.Shot(w.Id, w.Class), 0.9f);
        }

        void LateUpdate()
        {
            var local = _session != null ? _session.LocalAvatar : null;
            bool alive = local != null && local.Alive;
            if (!alive)
            {
                if (_model != null) _model.Root.SetActive(false);
                _rig.KickEuler = Vector3.zero;
                _rig.FovOverride = -1f;
                ScopeActive = false;
                _wasAlive = false;
                return;
            }
            if (!_wasAlive)
            {
                _switchBlend = 0f; // raise the weapon after respawn
                _wasAlive = true;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            var c = local.Combat;
            byte weaponIndex = c.ActiveWeapon;
            var w = WeaponCatalog.Get(weaponIndex);

            // ---- weapon change: lower, rebuild, raise
            if (_model == null) { BuildModel(weaponIndex); _switchBlend = 0f; }
            else if (weaponIndex != _modelWeapon && !_switching) { _switching = true; _pendingWeapon = weaponIndex; }
            if (_switching)
            {
                _switchBlend = Mathf.MoveTowards(_switchBlend, 0f, dt / 0.15f);
                if (_switchBlend <= 0f) { BuildModel(_pendingWeapon); _switching = false; _audio.PlayOneShot(ProceduralAudio.EquipFor(WeaponCatalog.Get(_pendingWeapon).Id), 0.6f); }
            }
            else _switchBlend = Mathf.MoveTowards(_switchBlend, 1f, dt / Mathf.Max(0.15f, w.DrawTime));
            _model.Root.SetActive(true);

            // ---- client-side shot prediction (fire rate and edge, from the latest server state)
            bool reloading = c.Reloading;
            if (!_session.IsAuthority)
            {
                _predCooldown -= dt;
                bool trigger = _input != null && _input.FireHeld;
                bool pressed = trigger && !_predTriggerWasDown;
                _predTriggerWasDown = trigger;
                bool hasAmmo = !w.UsesAmmo || c.ActiveClip > 0;
                if (trigger && hasAmmo && !reloading && _switchBlend > 0.95f && _predCooldown <= 0f && (w.Automatic || pressed))
                {
                    _predCooldown = w.FireInterval;
                    PlayShot(weaponIndex);
                }
            }

            // ---- reload animation and sound
            if (reloading && !_wasReloading)
            {
                _reloadTime = 0f;
                _audio.PlayOneShot(ProceduralAudio.ReloadFor(w.Id), 0.8f);
                if (_model.Anim != null && _model.Anim.GetClip("reload") != null)
                {
                    _model.Anim.GetClip("reload").wrapMode = WrapMode.ClampForever;
                    _model.Anim.Play("reload", PlayMode.StopAll);
                }
            }
            _wasReloading = reloading;
            if (reloading) _reloadTime += dt;
            float reloadPhase = reloading ? Mathf.Clamp01(_reloadTime / Mathf.Max(0.1f, w.ReloadTime)) : 1f;
            float reloadWeight = reloading ? Mathf.Sin(reloadPhase * Mathf.PI) : 0f;

            // ---- aim down sights
            bool wantAim = ((_input != null && _input.AimHeld) || c.Aiming) && !reloading && !w.IsKnife && !_switching;
            _aimBlend = Mathf.MoveTowards(_aimBlend, wantAim ? 1f : 0f, dt / 0.16f);
            if (_input != null) _input.AimSensitivityScale = w.HasScope ? 0.3f : 0.65f;
            bool wasScoped = ScopeActive;
            ScopeActive = w.HasScope && _aimBlend > 0.9f;
            if (ScopeActive && !wasScoped) _audio.PlayOneShot(ProceduralAudio.Zoom, 0.6f);
            _rig.FovOverride = _aimBlend > 0.01f ? Mathf.Lerp(-1f, w.HasScope ? 18f : 58f, _aimBlend) : -1f;
            if (_rig.FovOverride > 0f && _rig.FovOverride < 18f) _rig.FovOverride = 18f;
            if (_aimBlend <= 0.01f) _rig.FovOverride = -1f;

            // ---- sway from looking
            Vector2 look = _input != null ? _input.LastLookDelta : Vector2.zero;
            float swayScale = Mathf.Lerp(1f, 0.25f, _aimBlend);
            Vector3 swayTarget = new Vector3(look.y * 1.6f, -look.x * 1.6f, -look.x * 2.2f) * swayScale;
            swayTarget = Vector3.ClampMagnitude(swayTarget, 7f);
            _swayRot = Vector3.SmoothDamp(_swayRot, swayTarget, ref _swayVel, 0.08f, Mathf.Infinity, dt);

            // ---- bob from movement
            var s = local.State;
            float speed = s.HorizontalSpeed;
            bool sprinting = s.Sprinting && !(_input != null && _input.FireHeld);
            _bobWeight = Mathf.MoveTowards(_bobWeight, s.Grounded ? Mathf.Clamp01(speed / 4.5f) : 0f, dt * 4f);
            _bobPhase += dt * Mathf.Lerp(6f, sprinting ? 13f : 9.5f, Mathf.Clamp01(speed / 7f));
            float bobAmp = (sprinting ? 0.022f : 0.011f) * _bobWeight * Mathf.Lerp(1f, 0.15f, _aimBlend);
            Vector3 bob = new Vector3(Mathf.Sin(_bobPhase) * bobAmp, -Mathf.Abs(Mathf.Cos(_bobPhase)) * bobAmp, 0f);
            if (s.Landed) _landDip = Mathf.Clamp(s.LandingSpeed * 0.006f, 0.01f, 0.07f);
            _landDip = Mathf.MoveTowards(_landDip, 0f, dt * 0.25f);
            _vertical = Mathf.Lerp(_vertical, Mathf.Clamp(-s.VerticalSpeed * 0.004f, -0.03f, 0.03f), dt * 10f);

            // ---- recoil spring
            float recover = 1f - Mathf.Exp(-14f * dt);
            _kickPos = Vector3.Lerp(_kickPos, Vector3.zero, recover);
            _kickRot = Vector3.Lerp(_kickRot, Vector3.zero, recover * 0.9f);
            _camKick = Vector3.Lerp(_camKick, Vector3.zero, 1f - Mathf.Exp(-9f * dt));
            _rig.KickEuler = _camKick;
            if (_model.Bolt != null) _model.Bolt.localPosition = Vector3.Lerp(_model.Bolt.localPosition, _model.BoltRest, recover);

            // ---- compose
            Vector3 pos = Vector3.Lerp(_model.HipPosition, _model.AimPosition, Mathf.SmoothStep(0f, 1f, _aimBlend));
            pos += bob + new Vector3(0f, -_landDip + _vertical, 0f);
            Quaternion rot = Quaternion.Euler(_swayRot);
            if (sprinting && _aimBlend < 0.1f && !reloading)
            {
                pos += new Vector3(-0.04f, -0.03f, -0.02f) * _bobWeight;
                rot *= Quaternion.Euler(12f * _bobWeight, -28f * _bobWeight, 8f * _bobWeight);
            }
            if (reloading && !_model.IsGlb)
            {
                pos += new Vector3(-0.02f, -0.05f, 0f) * reloadWeight;
                rot *= Quaternion.Euler(18f * reloadWeight, -10f * reloadWeight, 32f * reloadWeight);
                AnimateMagazine(reloadPhase);
            }
            else ResetMagazine();
            if (_model.Anim != null && !_model.Anim.isPlaying && _model.Anim.GetClip("idle1") != null)
            {
                _model.Anim.GetClip("idle1").wrapMode = WrapMode.Loop;
                _model.Anim.Play("idle1");
            }
            float lower = _model.IsGlb ? 0f : 1f - Mathf.SmoothStep(0f, 1f, _switchBlend);
            pos += new Vector3(0f, -0.22f * lower, -0.05f * lower);
            rot *= Quaternion.Euler(35f * lower, 0f, 0f);

            _pivot.localPosition = pos + _kickPos;
            _pivot.localRotation = rot * Quaternion.Euler(_kickRot);
            _model.Root.SetActive(!ScopeActive);

            // ---- muzzle flash
            if (_flashTimer > 0f && _model.Muzzle != null && !ScopeActive)
            {
                _flashTimer -= dt;
                _flash.gameObject.SetActive(true);
                _flash.transform.SetParent(_model.Muzzle, false);
                _flash.transform.localPosition = new Vector3(0f, 0f, 0.04f);
                _flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                _flash.transform.localScale = Vector3.one * Random.Range(0.11f, 0.16f);
                _flashLight.intensity = 3.5f;
            }
            else
            {
                _flash.gameObject.SetActive(false);
                _flashLight.intensity = Mathf.MoveTowards(_flashLight.intensity, 0f, dt * 80f);
            }

            CurrentSpreadVisual = CombatRules.CurrentSpread(local, w);
        }

        void BuildModel(byte weaponIndex)
        {
            if (_flash != null) _flash.transform.SetParent(transform, false);
            if (_model != null) Destroy(_model.Root);
            _model = ViewModelBuilder.Build(WeaponCatalog.Get(weaponIndex), _kick);
            _modelWeapon = weaponIndex;
        }

        void AnimateMagazine(float phase)
        {
            if (_model.Magazine == null) return;
            // 0-0.35 drop the magazine, 0.35-0.65 new magazine comes in, 0.65-1 seat it.
            float outAmount = phase < 0.35f ? phase / 0.35f : phase < 0.65f ? 1f : 1f - (phase - 0.65f) / 0.35f;
            Vector3 offset = _model.Class == WeaponClass.Shotgun
                ? new Vector3(0f, 0f, -0.06f * Mathf.Sin(phase * Mathf.PI * 4f))
                : new Vector3(0f, -0.18f * outAmount, -0.02f * outAmount);
            _model.Magazine.localPosition = _model.MagazineRest + offset;
            if (_model.LeftHand != null)
                _model.LeftHand.localPosition = _model.LeftHandRest + new Vector3(0.01f, -0.12f * outAmount, -0.1f * outAmount);
        }

        void ResetMagazine()
        {
            if (_model.Magazine != null) _model.Magazine.localPosition = _model.MagazineRest;
            if (_model.LeftHand != null) _model.LeftHand.localPosition = _model.LeftHandRest;
        }
    }
}
