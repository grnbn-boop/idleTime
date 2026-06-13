using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace IdleTime.Core
{
    // Developer-only one-shot probe for "the button receives the click but the handler
    // never runs." It dumps, after the scene settles, every UI Button's onClick wiring —
    // the serialized persistent target object + method — plus how many UIManagers exist.
    //
    // Built for the dragged-in-UI-prefab case: re-parenting / re-stamping a UI prefab can
    // leave a button's onClick pointing at a MISSING object (silently does nothing) or at
    // a stale UIManager instance, even after the UIManager's own fields were rewired.
    // Read the log:
    //   • persistentCalls=0           → the button has no wiring at all.
    //   • target=<Missing>            → the onClick points at a destroyed/unlinked object — re-drag it.
    //   • target on a different obj   → points at the wrong/old UIManager instance.
    //   • UIManager count != 1        → duplicate or absent manager (drag-in left two copies).
    public class UIWiringInspector : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            var go = new GameObject("UI Wiring Inspector");
            go.AddComponent<UIWiringInspector>();
        }

        // Wait one frame so every scene object's Awake/OnEnable has run before we inspect.
        void Start() => Invoke(nameof(Dump), 0f);

        void Dump()
        {
            var managers = FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new StringBuilder();
            sb.AppendLine($"[UIWiring] UIManager instances in scene: {managers.Length}");
            foreach (var m in managers)
                sb.AppendLine($"    • UIManager on '{Path(m.gameObject)}' (activeInHierarchy={m.gameObject.activeInHierarchy})");

            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"[UIWiring] Buttons found: {buttons.Length}. onClick wiring —");
            foreach (var b in buttons)
            {
                int count = b.onClick.GetPersistentEventCount();
                sb.AppendLine($"  '{b.name}': persistentCalls={count}");
                for (int i = 0; i < count; i++)
                {
                    Object target = b.onClick.GetPersistentTarget(i);
                    string method = b.onClick.GetPersistentMethodName(i);
                    string targetDesc =
                        target == null ? "<Missing / null>" :
                        $"{target.GetType().Name} on '{(target is Component c ? Path(c.gameObject) : target.name)}'";
                    sb.AppendLine($"        [{i}] {targetDesc} . {method}()");
                }
            }

            Debug.Log(sb.ToString());
        }

        static string Path(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            for (Transform t = go.transform.parent; t != null; t = t.parent)
                sb.Insert(0, t.name + "/");
            return sb.ToString();
        }
    }
}
