using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Gamepad / keyboard focus rules: when a screen opens its first control is selected; when a
    /// dialog closes the control that was selected before it gets focus back.
    /// </summary>
    public static class UiFocus
    {
        static readonly Stack<GameObject> Saved = new Stack<GameObject>();

        public static void Select(Selectable s)
        {
            var es = EventSystem.current;
            if (es == null || s == null) return;
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(s.gameObject);
        }

        public static void PushDialog(Selectable first)
        {
            var es = EventSystem.current;
            Saved.Push(es != null ? es.currentSelectedGameObject : null);
            Select(first);
        }

        public static void PopDialog(Selectable fallback)
        {
            GameObject previous = Saved.Count > 0 ? Saved.Pop() : null;
            if (previous != null && previous.activeInHierarchy && previous.TryGetComponent<Selectable>(out var sel) && sel.interactable)
                Select(sel);
            else
                Select(fallback);
        }

        public static void Clear() => Saved.Clear();

        /// <summary>If focus was lost (mouse click on empty space), give it back so a gamepad keeps working.</summary>
        public static void KeepFocus(Selectable fallback)
        {
            var es = EventSystem.current;
            if (es == null || fallback == null) return;
            var cur = es.currentSelectedGameObject;
            if (cur == null || !cur.activeInHierarchy) Select(fallback);
        }

        /// <summary>Wires explicit up/down navigation for a vertical list of controls.</summary>
        public static void ChainVertical(IList<Selectable> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = items[(i - 1 + items.Count) % items.Count],
                    selectOnDown = items[(i + 1) % items.Count],
                };
                items[i].navigation = nav;
            }
        }
    }
}
