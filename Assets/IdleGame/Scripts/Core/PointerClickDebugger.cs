using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IdleTime.Core
{
    // Developer-only click probe: on every left-click it asks the EventSystem what UI is
    // under the cursor (across ALL canvases) and logs the stack in the exact order Unity
    // routes the click — topmost first. The top entry is the GameObject that actually
    // receives the press, so this answers "is my button firing, or is something on a
    // higher canvas eating the click?" without guesswork.
    //
    // Built for the portal-nav-vs-inventory question: portal nav buttons live on a
    // sortingOrder 9998 canvas with invisible (Color.clear) but raycastable backgrounds,
    // so they can shadow gameplay UI (sortingOrder 0) at the screen edges. If the top
    // line of a click log is a "PortalButton_*" while you were aiming at the inventory
    // toggle, the nav HUD is the culprit. If the top line IS the toggle and nothing
    // opens, the click reaches it and the handler is the problem.
    //
    // Auto-bootstraps once and survives scene loads (DontDestroyOnLoad), so there's no
    // per-scene setup — it just starts logging in the editor / dev builds. Toggle the
    // logging on/off live with the toggle key (default F4).
    public class PointerClickDebugger : MonoBehaviour
    {
        [Header("Hotkeys (editor / development builds only)")]
        [Tooltip("Turns click logging on/off at runtime.")]
        [SerializeField] Key toggleKey = Key.F4;

        [Header("Options")]
        [Tooltip("Start with logging enabled.")]
        [SerializeField] bool loggingEnabled = true;

        [Tooltip("Also log when a click hits no UI at all (reached the world instead).")]
        [SerializeField] bool logEmptyHits = true;

        static readonly List<RaycastResult> Hits = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            if (FindAnyObjectByType<PointerClickDebugger>() != null) return;
            var go = new GameObject("Pointer Click Debugger");
            go.AddComponent<PointerClickDebugger>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;

            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame)
            {
                loggingEnabled = !loggingEnabled;
                Debug.Log($"[ClickDebug] logging {(loggingEnabled ? "ENABLED" : "disabled")}.");
            }

            if (!loggingEnabled) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            LogClick(mouse.position.ReadValue());
        }

        void LogClick(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null)
            {
                Debug.LogWarning($"[ClickDebug] click at {screenPos} but no EventSystem.current — nothing can receive UI input.");
                return;
            }

            var pointer = new PointerEventData(es) { position = screenPos };
            Hits.Clear();
            es.RaycastAll(pointer, Hits);

            if (Hits.Count == 0)
            {
                if (logEmptyHits)
                    Debug.Log($"[ClickDebug] click at {screenPos:F0}: no UI hit — click reached the world.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[ClickDebug] click at {screenPos:F0}: {Hits.Count} UI hit(s), topmost first —");
            for (int i = 0; i < Hits.Count; i++)
            {
                RaycastResult r = Hits[i];
                GameObject go = r.gameObject;
                Canvas canvas = go != null ? go.GetComponentInParent<Canvas>() : null;
                int order = canvas != null ? canvas.rootCanvas.sortingOrder : 0;
                bool interactable = IsInteractable(go);
                string marker = i == 0 ? "►" : " ";
                sb.AppendLine(
                    $"  {marker} [{i}] {Path(go)}  | canvas='{(canvas != null ? canvas.rootCanvas.name : "?")}' order={order} " +
                    $"| raycastTarget-graphic, button={(interactable ? "YES" : "no")}");
            }

            GameObject top = Hits[0].gameObject;
            sb.Append($"  → click goes to: {(top != null ? top.name : "null")}");
            Debug.Log(sb.ToString());
        }

        // Does this object (or an ancestor) carry an enabled, interactable Selectable
        // (Button/Toggle/etc.)? Tells you whether the thing receiving the click is even
        // meant to act on it.
        static bool IsInteractable(GameObject go)
        {
            if (go == null) return false;
            var selectable = go.GetComponentInParent<Selectable>();
            return selectable != null && selectable.IsInteractable() && selectable.enabled;
        }

        static string Path(GameObject go)
        {
            if (go == null) return "null";
            var sb = new StringBuilder(go.name);
            for (Transform t = go.transform.parent; t != null; t = t.parent)
                sb.Insert(0, t.name + "/");
            return sb.ToString();
        }
    }
}
