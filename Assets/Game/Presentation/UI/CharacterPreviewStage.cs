using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Renders an interactive 3D turntable preview of a character GLB model into a UI RawImage
    /// using an isolated offscreen camera and RenderTexture.
    /// </summary>
    public sealed class CharacterPreviewStage : MonoBehaviour, IDragHandler
    {
        RawImage _target;
        RenderTexture _rt;
        GameObject _stageRoot;
        Transform _turntable;
        Camera _cam;
        GameObject _currentModel;
        string _loadedKey;
        float _yaw = 180f;
        bool _dragging;

        public static CharacterPreviewStage Attach(RawImage target, int width = 512, int height = 640)
        {
            var stage = target.gameObject.AddComponent<CharacterPreviewStage>();
            stage._target = target;
            stage.InitStage(width, height);
            return stage;
        }

        void InitStage(int width, int height)
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;

            _rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2,
                name = "CharacterPreviewRT"
            };
            _rt.Create();
            _target.texture = _rt;

            _stageRoot = new GameObject("CharacterPreviewStage");
            _stageRoot.transform.position = new Vector3(5000f, -500f, 5000f);

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(_stageRoot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.02f, 3.35f);
            camGo.transform.LookAt(_stageRoot.transform.position + new Vector3(0f, 0.94f, 0f));
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 0f);
            _cam.fieldOfView = 30f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 20f;
            _cam.targetTexture = _rt;
            _cam.allowHDR = false;

            var keyLightGo = new GameObject("KeyLight");
            keyLightGo.transform.SetParent(_stageRoot.transform, false);
            keyLightGo.transform.localRotation = Quaternion.Euler(28f, 145f, 0f);
            var keyLight = keyLightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.35f;
            keyLight.color = new Color(1f, 0.96f, 0.90f);

            var rimLightGo = new GameObject("RimLight");
            rimLightGo.transform.SetParent(_stageRoot.transform, false);
            rimLightGo.transform.localRotation = Quaternion.Euler(15f, -35f, 0f);
            var rimLight = rimLightGo.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.intensity = 0.65f;
            rimLight.color = new Color(0.65f, 0.80f, 1f);

            var ttGo = new GameObject("Turntable");
            ttGo.transform.SetParent(_stageRoot.transform, false);
            _turntable = ttGo.transform;
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        public void ShowCharacter(string charId, CharacterQuality quality)
        {
            if (_turntable == null) return;
            string key = $"{charId}_{(int)quality}";
            if (_loadedKey == key && _currentModel != null) return;
            _loadedKey = key;

            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }

            var prefab = CharacterCatalog.LoadModelPrefab(charId, quality);
            if (prefab == null) return;

            _currentModel = Instantiate(prefab, _turntable, false);
            _currentModel.name = "PreviewModel";
            _currentModel.transform.localPosition = Vector3.zero;
            _currentModel.transform.localRotation = Quaternion.identity;
            foreach (var col in _currentModel.GetComponentsInChildren<Collider>(true)) Destroy(col);

            var renderers = _currentModel.GetComponentsInChildren<Renderer>(true);
            Bounds b = default;
            bool has = false;
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            float sourceHeight = has ? Mathf.Max(0.01f, b.size.y) : 1f;
            float scale = 1.8f / sourceHeight;
            _currentModel.transform.localScale *= scale;
            float bottom = has ? (b.min.y - _turntable.position.y) * scale : 0f;
            _currentModel.transform.localPosition = new Vector3(0f, -bottom, 0f);

            var anim = _currentModel.GetComponentInChildren<Animation>(true);
            if (anim != null)
            {
                var idle = anim.GetClip("idle1");
                if (idle != null)
                {
                    idle.wrapMode = WrapMode.Loop;
                    anim.Play("idle1");
                }
            }
        }

        public void SetVisible(bool visible)
        {
            if (_stageRoot != null) _stageRoot.SetActive(visible);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _dragging = true;
            _yaw -= eventData.delta.x * 0.55f;
            if (_turntable != null) _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        void Update()
        {
            if (_turntable == null || _stageRoot == null || !_stageRoot.activeSelf) return;
            if (!_dragging)
            {
                _yaw += Time.unscaledDeltaTime * 24f;
                _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            }
            _dragging = false;
        }

        void OnDestroy()
        {
            if (_stageRoot != null) Destroy(_stageRoot);
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
        }
    }
}
