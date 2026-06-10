using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IdleTime.UI
{
    // Attach to any UI element (with a raycast-target Graphic) to show a tooltip on
    // hover. Designers can type a static string in the Inspector; code can instead set
    // ContentProvider for live content (e.g. a stat breakdown that changes as gear/skills
    // change). Requires an EventSystem + GraphicRaycaster in the scene (standard UI setup).
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, TextArea] string content;

        // When set, takes precedence over the serialized string and is re-evaluated on
        // every hover — use for values that change at runtime.
        public Func<string> ContentProvider;

        public void SetContent(string text)
        {
            content = text;
            ContentProvider = null;
        }

        string Resolve() => ContentProvider != null ? ContentProvider() : content;

        public void OnPointerEnter(PointerEventData eventData)
        {
            string text = Resolve();
            if (!string.IsNullOrEmpty(text))
                TooltipManager.Instance?.Show(text);
        }

        public void OnPointerExit(PointerEventData eventData) => TooltipManager.Instance?.Hide();

        // If the element is hidden/destroyed while hovered, don't leave a stuck tooltip.
        void OnDisable() => TooltipManager.Instance?.Hide();
    }
}
