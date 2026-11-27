using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>Holds the logical Arabic text and writes its shaped, visually ordered form to a Text.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class ArabicText : MonoBehaviour
    {
        [SerializeField, TextArea] string logical;
        [SerializeField] string key;
        Text _text;

        public Text Target => _text != null ? _text : (_text = GetComponent<Text>());
        public string Logical => logical;

        void Awake()
        {
            if (!string.IsNullOrEmpty(key)) logical = Strings.Get(key);
            Apply();
        }

        public void SetText(string value)
        {
            logical = value ?? "";
            Apply();
        }

        public void SetKey(string stringKey)
        {
            key = stringKey;
            SetText(Strings.Get(stringKey));
        }

        void Apply() => Target.text = ArabicShaper.Process(logical);
    }

    /// <summary>Keeps a RectTransform inside Screen.safeArea (notches, rounded corners).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect _applied;
        Vector2Int _screen;

        void OnEnable() => Apply();
        void Update()
        {
            if (Screen.safeArea != _applied || _screen.x != Screen.width || _screen.y != Screen.height) Apply();
        }

        void Apply()
        {
            var rt = (RectTransform)transform;
            var safe = Platform.SafeArea.Current;
            _applied = Screen.safeArea;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
