using System.Collections.Generic;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Input;
using Adventure3D.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Handles CS 1.6 thrown grenades (HE, Flashbang, Smoke) with GLB world models, bounce physics,
    /// authentic CS 1.6 sounds, radial HE damage, Flashbang screen blind, Smoke clouds, and C4 world visual/beeps.
    /// </summary>
    public sealed class TacticalGrenadeSystem : MonoBehaviour
    {
        enum GrenadeType { He = 0, Flash = 1, Smoke = 2 }

        sealed class ActiveGrenade
        {
            public GrenadeType Type;
            public GameObject Go;
            public Vector3 Pos, Vel;
            public float Fuse;
            public ushort OwnerId;
            public Team OwnerTeam;
            public bool Bounced;
        }

        sealed class ActiveSmoke
        {
            public GameObject Go;
            public float Life;
        }

        readonly List<ActiveGrenade> _grenades = new List<ActiveGrenade>();
        readonly List<ActiveSmoke> _smokes = new List<ActiveSmoke>();

        GameSession _session;
        LocalPlayerInput _input;
        Camera _camera;
        Image _flashOverlay;
        float _flashAlpha;
        GameObject _c4WorldGo;
        float _c4BeepTimer;

        public static TacticalGrenadeSystem Create(GameSession session, LocalPlayerInput input, Camera camera, Transform uiRoot)
        {
            var go = new GameObject("TacticalGrenadeSystem");
            var sys = go.AddComponent<TacticalGrenadeSystem>();
            sys._session = session;
            sys._input = input;
            sys._camera = camera;

            var flashGo = UiKit.NewRect("FlashbangOverlay", uiRoot);
            UiKit.Stretch(flashGo);
            sys._flashOverlay = flashGo.gameObject.AddComponent<Image>();
            sys._flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            sys._flashOverlay.raycastTarget = false;

            if (input != null) input.GrenadeThrowRequested += sys.TryThrowLocal;
            return sys;
        }

        void OnDestroy()
        {
            if (_input != null) _input.GrenadeThrowRequested -= TryThrowLocal;
        }

        void TryThrowLocal(int kind)
        {
            var local = _session != null ? _session.LocalAvatar : null;
            if (local == null || !local.Alive || _camera == null) return;
            ref var c = ref local.Combat;
            if (kind == 0) { if (c.HeGrenades == 0) return; c.HeGrenades--; }
            else if (kind == 1) { if (c.Flashbangs == 0) return; c.Flashbangs--; }
            else if (kind == 2) { if (c.SmokeGrenades == 0) return; c.SmokeGrenades--; }
            else return;

            var type = (GrenadeType)kind;
            string modelName = type == GrenadeType.He ? "w_hegrenade" : type == GrenadeType.Flash ? "w_flashbang" : "w_smokegrenade";
            var prefab = Resources.Load<GameObject>("Adventure3D/Weapons/World/" + modelName);
            GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (prefab == null) go.transform.localScale = Vector3.one * 0.16f;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);

            Vector3 origin = _camera.transform.position + _camera.transform.forward * 0.6f;
            go.transform.position = origin;
            var pin = Resources.Load<AudioClip>("Adventure3D/Audio/Weapons/pinpull");
            if (pin != null) AudioSource.PlayClipAtPoint(pin, origin, 0.7f);

            _grenades.Add(new ActiveGrenade
            {
                Type = type,
                Go = go,
                Pos = origin,
                Vel = _camera.transform.forward * 16f + Vector3.up * 3.5f,
                Fuse = 1.6f,
                OwnerId = local.PlayerId,
                OwnerTeam = local.Combat.Team,
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateGrenades(dt);
            UpdateSmokes(dt);
            UpdateFlashOverlay(dt);
            UpdateC4World(dt);
        }

        void UpdateGrenades(float dt)
        {
            for (int i = _grenades.Count - 1; i >= 0; i--)
            {
                var g = _grenades[i];
                g.Fuse -= dt;
                g.Vel += Vector3.down * 20.32f * dt;
                Vector3 next = g.Pos + g.Vel * dt;
                if (Physics.Raycast(g.Pos, (next - g.Pos).normalized, out var hit, (next - g.Pos).magnitude + 0.12f, ~0, QueryTriggerInteraction.Ignore))
                {
                    g.Pos = hit.point + hit.normal * 0.1f;
                    g.Vel = Vector3.Reflect(g.Vel, hit.normal) * 0.45f;
                    if (!g.Bounced)
                    {
                        g.Bounced = true;
                        var bounce = Resources.Load<AudioClip>("Adventure3D/Audio/Weapons/he_bounce-1");
                        if (bounce != null) AudioSource.PlayClipAtPoint(bounce, g.Pos, 0.6f);
                    }
                }
                else
                {
                    g.Pos = next;
                }
                if (g.Go != null)
                {
                    g.Go.transform.position = g.Pos;
                    g.Go.transform.Rotate(Vector3.right * 360f * dt);
                }
                if (g.Fuse <= 0f)
                {
                    Detonate(g);
                    if (g.Go != null) Destroy(g.Go);
                    _grenades.RemoveAt(i);
                }
            }
        }

        void Detonate(ActiveGrenade g)
        {
            if (g.Type == GrenadeType.He)
            {
                var clip = Resources.Load<AudioClip>("Adventure3D/Audio/Weapons/hegrenade-1");
                if (clip != null) AudioSource.PlayClipAtPoint(clip, g.Pos, 1f);
                if (_session != null && _session.IsAuthority)
                {
                    foreach (var p in _session.World.Players)
                    {
                        if (!p.Alive) continue;
                        float d = Vector3.Distance(p.State.Position + Vector3.up, g.Pos);
                        if (d > 8.5f) continue;
                        int dmg = Mathf.RoundToInt(Mathf.Lerp(98f, 10f, d / 8.5f));
                        if (p.Combat.Armor > 0) dmg = Mathf.RoundToInt(dmg * 0.58f);
                        p.Combat.Health = (byte)Mathf.Max(1, p.Combat.Health - dmg);
                    }
                }
            }
            else if (g.Type == GrenadeType.Flash)
            {
                var clip = Resources.Load<AudioClip>("Adventure3D/Audio/Weapons/flashbang-1");
                if (clip != null) AudioSource.PlayClipAtPoint(clip, g.Pos, 1f);
                if (_camera != null)
                {
                    float d = Vector3.Distance(_camera.transform.position, g.Pos);
                    if (d < 22f)
                    {
                        float dot = Vector3.Dot(_camera.transform.forward, (g.Pos - _camera.transform.position).normalized);
                        float factor = Mathf.Clamp01(1f - d / 22f) * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(dot));
                        _flashAlpha = Mathf.Max(_flashAlpha, factor);
                    }
                }
            }
            else
            {
                var clip = Resources.Load<AudioClip>("Adventure3D/Audio/Weapons/sg_explode");
                if (clip != null) AudioSource.PlayClipAtPoint(clip, g.Pos, 0.9f);
                var cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(cloud.GetComponent<Collider>());
                cloud.transform.position = g.Pos + Vector3.up * 1.2f;
                cloud.transform.localScale = Vector3.one * 5.2f;
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.75f, 0.78f, 0.8f, 0.72f);
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
                cloud.GetComponent<Renderer>().sharedMaterial = mat;
                _smokes.Add(new ActiveSmoke { Go = cloud, Life = 12f });
            }
        }

        void UpdateSmokes(float dt)
        {
            for (int i = _smokes.Count - 1; i >= 0; i--)
            {
                _smokes[i].Life -= dt;
                if (_smokes[i].Life <= 0f)
                {
                    if (_smokes[i].Go != null) Destroy(_smokes[i].Go);
                    _smokes.RemoveAt(i);
                }
            }
        }

        void UpdateFlashOverlay(float dt)
        {
            if (_flashOverlay == null) return;
            _flashAlpha = Mathf.Max(0f, _flashAlpha - dt * 0.45f);
            _flashOverlay.color = new Color(1f, 1f, 1f, _flashAlpha);
        }

        void UpdateC4World(float dt)
        {
            if (_session == null) return;
            if (_session.BombPlanted)
            {
                if (_c4WorldGo == null)
                {
                    var c4Prefab = Resources.Load<GameObject>("Adventure3D/Weapons/World/w_c4");
                    _c4WorldGo = c4Prefab != null ? Instantiate(c4Prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _c4WorldGo.transform.position = _session.BombPosition + Vector3.up * 0.05f;
                    foreach (var col in _c4WorldGo.GetComponentsInChildren<Collider>(true)) Destroy(col);
                    _c4BeepTimer = 0.1f;
                }
                _c4BeepTimer -= dt;
                if (_c4BeepTimer <= 0f)
                {
                    float frac = Mathf.Clamp01(_session.BombTimer / 45f);
                    _c4BeepTimer = Mathf.Lerp(0.18f, 1.0f, frac);
                    int idx = frac < 0.2f ? 5 : frac < 0.4f ? 4 : frac < 0.6f ? 3 : frac < 0.8f ? 2 : 1;
                    var beep = Resources.Load<AudioClip>("Adventure3D/Audio/Weapons/c4_beep" + idx);
                    if (beep != null) AudioSource.PlayClipAtPoint(beep, _session.BombPosition, 0.85f);
                }
            }
            else if (_c4WorldGo != null)
            {
                Destroy(_c4WorldGo);
                _c4WorldGo = null;
            }
        }
    }
}
