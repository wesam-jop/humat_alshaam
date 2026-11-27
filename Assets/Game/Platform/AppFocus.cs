using System;
using UnityEngine;

namespace Adventure3D.Platform
{
    /// <summary>
    /// Focus / background notifications. Input listens to this to release held buttons and touches,
    /// so a sprint or stick press never stays stuck after an alt-tab, a phone call or the home button.
    /// </summary>
    public static class AppFocus
    {
        public static event Action<bool> FocusChanged;
        public static bool HasFocus { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            FocusChanged = null;
            HasFocus = true;
            Application.focusChanged -= OnFocus;
            Application.focusChanged += OnFocus;
        }

        static void OnFocus(bool focus) => Raise(focus);

        /// <summary>Also called by tests to simulate focus loss.</summary>
        public static void Raise(bool focus)
        {
            HasFocus = focus;
            FocusChanged?.Invoke(focus);
        }
    }

    /// <summary>Safe area helper (notches, rounded corners, home indicator).</summary>
    public static class SafeArea
    {
        public static Rect Current
        {
            get
            {
                var r = Screen.safeArea;
                if (r.width <= 0f || r.height <= 0f) r = new Rect(0f, 0f, Screen.width, Screen.height);
                return r;
            }
        }
    }
}
