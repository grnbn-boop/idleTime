using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using IdleTime.CameraRig;
using IdleTime.Interactions;
using IdleTime.Player;

namespace IdleTime.UI
{
    // An off-screen waypoint HUD: shows a clickable button for every portal the player
    // has UNLOCKED in the current room (killed enough monsters to meet the kill quota).
    // The button tracks the portal's real world position projected to screen space — it
    // sits over the portal while it's on-screen, and once the portal scrolls out of view
    // it hugs the screen edge/corner in the portal's direction, so it always points to
    // where the portal actually is. Clicking it walks the player toward that portal —
    // the portal's own trigger (PortalController.OnTriggerEnter2D) then fires the level
    // transition, so this is purely a "walk me there" shortcut and never bypasses the gate.
    //
    // Mirrors TooltipManager's self-building convention: it builds its own Canvas +
    // buttons at runtime, so the only scene setup is dropping this component onto any
    // GameObject. Scene-local (not DontDestroyOnLoad) — one room's HUD lists that room's
    // portals; a fresh scene rebuilds it.
    //
    // It catches portals two ways so ordering never matters: it scans for already-active
    // portals when it enables (covers the death→reload flow, where RoomProgress is static
    // and a portal re-activates in its own OnEnable, possibly before this runs) AND it
    // subscribes to PortalController.OnPortalActivated for portals unlocked while playing.
    public class PortalNavHUD : MonoBehaviour
    {
        const string PointerSpriteAssetPath = "Assets/IdleGame/Art/UI/Icons.png";
        const string PointerSpriteName = "pointer";

        [Header("Master Switches")]
        [Tooltip("Untick to fully disable the HUD: canvas off + raycaster off, so it cannot " +
                 "render or catch any clicks. Toggleable live in Play Mode to prove it isn't interfering.")]
        [SerializeField] bool hudEnabled = true;

        [Tooltip("Untick to keep the direction arrows visible but stop their buttons from catching " +
                 "clicks — isolates whether the HUD's GraphicRaycaster is stealing input from other UI.")]
        [SerializeField] bool buttonsCatchClicks = true;

        [Header("Tracking")]
        [Tooltip("Camera the indicator projects through. Falls back to Camera.main.")]
        [SerializeField] Camera worldCamera;

        [Header("Placement")]
        [SerializeField] int sortingOrder = 9998;        // above gameplay UI, below fader (9999) + tooltip (10000)
        [SerializeField] Vector2 buttonSize = new Vector2(72f, 72f);
        [Tooltip("Extra gap kept between the button edge and the screen edge when it's clamped to a border.")]
        [SerializeField] float edgeMargin = 12f;
        [Tooltip("How often to rescan for already-active portals, in case an activation happened before this HUD existed.")]
        [SerializeField] float rescanInterval = 0.25f;

        [Header("Appearance")]
        [Tooltip("Optional portal image. If empty, the portal's own sprite is used.")]
        [SerializeField] Sprite portalIcon;
        [Tooltip("Pointer sprite used for the direction arrow. Auto-loads the 'pointer' sprite from Icons.png in the editor if empty.")]
        [SerializeField] Sprite pointerIcon;
        [SerializeField] Vector2 portalImageSize = new Vector2(48f, 48f);
        [SerializeField] Vector2 pointerSize = new Vector2(28f, 28f);
        [SerializeField] float imagePointerGap = 12f;
        [SerializeField] Color portalTint = new Color(1f, 1f, 1f, 0.76f);
        [SerializeField] Color pointerTint = new Color(1f, 1f, 1f, 0.92f);
        [Tooltip("Fallback direction glyph used only if the pointer sprite cannot be found.")]
        [SerializeField] string fallbackLabel = ">";

        [Header("Runtime Debug")]
        [SerializeField] Camera resolvedCamera;
        [SerializeField] int trackedPortalCount;
        [SerializeField] int visibleIndicatorCount;

        RectTransform container;
        Canvas canvas;
        GraphicRaycaster raycaster;
        readonly Dictionary<PortalController, Indicator> buttons = new();
        ClickToMove2D player;
        Camera cam;
        float rescanTimer;

        class Indicator
        {
            public RectTransform root;
            public RectTransform portalImage;
            public RectTransform arrow;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapInitialScene() => EnsureSceneHud();

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureSceneHud();

        static void EnsureSceneHud()
        {
            // Include inactive so a hand-placed-but-disabled HUD GameObject suppresses the
            // auto-spawn — otherwise unticking a placed instance would just get a fresh
            // active one created here, and you could never turn the HUD off.
            if (FindObjectsByType<PortalNavHUD>(FindObjectsInactive.Include).Length > 0) return;
            new GameObject("Portal Nav HUD").AddComponent<PortalNavHUD>();
        }

#if UNITY_EDITOR
        // Drop a real, inspectable HUD GameObject into the open scene so its toggles can be
        // flipped by hand. With one present, the runtime bootstrap above won't spawn another.
        [MenuItem("IdleTime/Add Portal Nav HUD To Scene")]
        static void AddToScene()
        {
            if (FindObjectsByType<PortalNavHUD>(FindObjectsInactive.Include).Length > 0)
            {
                Debug.Log("[PortalNavHUD] Scene already contains a Portal Nav HUD.");
                return;
            }
            var go = new GameObject("Portal Nav HUD");
            go.AddComponent<PortalNavHUD>();
            Undo.RegisterCreatedObjectUndo(go, "Add Portal Nav HUD");
            Selection.activeGameObject = go;
        }
#endif

        void Awake() => Build();

        void OnEnable()
        {
            PortalController.OnPortalActivated += HandlePortalActivated;
            ScanExistingPortals();
        }

        void OnDisable()
        {
            PortalController.OnPortalActivated -= HandlePortalActivated;
        }

        // Push the master toggles onto the built canvas. Disabling the Canvas hides the
        // whole HUD and stops it rendering; disabling the GraphicRaycaster stops its
        // buttons catching clicks while leaving the arrows visible. Cheap to call each
        // frame, and OnValidate routes inspector edits here live during Play Mode.
        void ApplySwitches()
        {
            if (canvas != null) canvas.enabled = hudEnabled;
            if (raycaster != null) raycaster.enabled = hudEnabled && buttonsCatchClicks;
        }

        void OnValidate()
        {
            if (Application.isPlaying) ApplySwitches();
        }

        // Position after the camera has settled for the frame (CameraFollow2D moves in
        // LateUpdate; a one-frame lag on an indicator is imperceptible).
        void LateUpdate()
        {
            ApplySwitches();
            if (!hudEnabled) return;   // master off: don't position or rescan

            Camera viewCamera = ResolveCamera();
            trackedPortalCount = buttons.Count;
            visibleIndicatorCount = 0;
            if (viewCamera == null) return;

            rescanTimer -= Time.unscaledDeltaTime;
            if (rescanTimer <= 0f)
            {
                rescanTimer = Mathf.Max(0.05f, rescanInterval);
                ScanExistingPortals();
            }

            foreach (var pair in buttons)
            {
                PortalController portal = pair.Key;
                if (portal == null) continue;
                Indicator indicator = pair.Value;
                if (indicator?.root == null) continue;

                Vector3 screenPoint = viewCamera.WorldToScreenPoint(portal.transform.position);

                // The portal is its own affordance while it's in view — only show the
                // edge indicator once it has scrolled off-screen.
                bool onScreen = IsOnScreen(screenPoint);
                if (indicator.root.gameObject.activeSelf == onScreen)
                    indicator.root.gameObject.SetActive(!onScreen);

                if (!onScreen)
                {
                    Vector3 edgePoint = ClampToScreen(screenPoint);
                    indicator.root.position = edgePoint;
                    LayoutIndicator(indicator, screenPoint, edgePoint);
                    visibleIndicatorCount++;
                }
            }
        }

        void LayoutIndicator(Indicator indicator, Vector3 portalScreenPoint, Vector3 edgePoint)
        {
            RectTransform arrow = indicator.arrow;
            if (arrow == null) return;

            Vector2 direction = (Vector2)(portalScreenPoint - edgePoint);
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
            direction.Normalize();

            float centreDistance =
                ProjectedHalfSize(portalImageSize, direction) +
                imagePointerGap +
                ProjectedHalfSize(pointerSize, direction);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            arrow.anchoredPosition = direction * (centreDistance * 0.5f);
            if (indicator.portalImage != null)
                indicator.portalImage.anchoredPosition = -direction * (centreDistance * 0.5f);
        }

        static float ProjectedHalfSize(Vector2 size, Vector2 direction) =>
            (Mathf.Abs(direction.x) * size.x + Mathf.Abs(direction.y) * size.y) * 0.5f;

        // The view camera, cached. Falls back past Camera.main because the gameplay
        // camera may not be tagged MainCamera (ClickToMove2D / CameraFollow2D both run
        // off explicit references), in which case Camera.main is null and the indicator
        // would never position. Re-resolves if the cached camera is destroyed (e.g. a
        // fresh camera after a scene reload).
        Camera ResolveCamera()
        {
            if (worldCamera != null && worldCamera.isActiveAndEnabled) return CacheCamera(worldCamera);
            if (cam != null && cam.isActiveAndEnabled) return CacheCamera(cam);

            var followRig = FindAnyObjectByType<CameraFollow2D>();
            if (followRig != null && followRig.TryGetComponent(out Camera followCamera))
                return CacheCamera(followCamera);

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return CacheCamera(Camera.main);

            foreach (var candidate in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
            {
                if (candidate.isActiveAndEnabled && candidate.targetTexture == null)
                    return CacheCamera(candidate);
            }

            resolvedCamera = null;
            return null;
        }

        Camera CacheCamera(Camera camera)
        {
            cam = camera;
            resolvedCamera = camera;
            return camera;
        }

        static bool IsOnScreen(Vector3 screenPoint) =>
            screenPoint.z > 0f &&
            screenPoint.x >= 0f && screenPoint.x <= Screen.width &&
            screenPoint.y >= 0f && screenPoint.y <= Screen.height;

        // Project + clamp the portal's world point to the screen. Component-wise clamp:
        // while the point is on-screen the button sits on the portal; when it's past an
        // edge the button slides along that edge at the portal's true X or Y, and tucks
        // into the corner when it's off on both axes.
        Vector3 ClampToScreen(Vector3 screenPoint)
        {
            // Behind the camera (rare for the 2D rig): mirror so the indicator still
            // points the right way instead of inverting.
            if (screenPoint.z < 0f)
            {
                screenPoint.x = Screen.width - screenPoint.x;
                screenPoint.y = Screen.height - screenPoint.y;
            }

            float halfExtent = (Mathf.Max(portalImageSize.x, portalImageSize.y) + imagePointerGap + Mathf.Max(pointerSize.x, pointerSize.y)) * 0.5f;
            float marginX = halfExtent + edgeMargin;
            float marginY = halfExtent + edgeMargin;
            screenPoint.x = Mathf.Clamp(screenPoint.x, marginX, Screen.width - marginX);
            screenPoint.y = Mathf.Clamp(screenPoint.y, marginY, Screen.height - marginY);
            screenPoint.z = 0f;
            return screenPoint;
        }

        // ── HUD construction ──────────────────────────────────────────────────────

        void Build()
        {
            var canvasGO = new GameObject("PortalNavCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvasGO.AddComponent<CanvasScaler>();
            raycaster = canvasGO.AddComponent<GraphicRaycaster>();   // buttons need their own raycaster

            ApplySwitches();

            // Full-screen container; buttons are positioned manually in screen space.
            var containerGO = new GameObject("Portals", typeof(RectTransform));
            containerGO.transform.SetParent(canvasGO.transform, false);
            container = containerGO.GetComponent<RectTransform>();
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.offsetMin = container.offsetMax = Vector2.zero;
        }

        // ── Portal tracking ───────────────────────────────────────────────────────

        void ScanExistingPortals()
        {
            foreach (var portal in FindObjectsByType<PortalController>(FindObjectsInactive.Exclude))
                if (portal.IsActive) AddButton(portal);
        }

        void HandlePortalActivated(PortalController portal) => AddButton(portal);

        void AddButton(PortalController portal)
        {
            if (portal == null || buttons.ContainsKey(portal)) return;

            var buttonGO = new GameObject($"PortalButton_{portal.RoomId}", typeof(RectTransform));
            buttonGO.transform.SetParent(container, false);
            var rect = buttonGO.GetComponent<RectTransform>();
            rect.sizeDelta = CalculateIndicatorBounds();
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = buttonGO.AddComponent<Image>();
            image.color = Color.clear;

            Sprite icon = ResolveIcon(portal);
            RectTransform portalImage = null;
            if (icon != null)
                portalImage = AddIcon(buttonGO.transform, icon);

            RectTransform arrow = AddArrow(buttonGO.transform);

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = image;   // press/highlight tint
            button.onClick.AddListener(() => WalkTo(portal));

            // Hover tooltip via the existing tooltip system. Use a provider so a portal
            // destroyed mid-hover resolves safely.
            var trigger = buttonGO.AddComponent<TooltipTrigger>();
            trigger.ContentProvider = () => portal != null ? $"Travel to {portal.DisplayName}" : "";

            buttons[portal] = new Indicator { root = rect, portalImage = portalImage, arrow = arrow };
        }

        Vector2 CalculateIndicatorBounds()
        {
            float halfExtent = Mathf.Max(portalImageSize.x, portalImageSize.y) + imagePointerGap + Mathf.Max(pointerSize.x, pointerSize.y);
            return new Vector2(Mathf.Max(buttonSize.x, halfExtent), Mathf.Max(buttonSize.y, halfExtent));
        }

        Sprite ResolveIcon(PortalController portal)
        {
            if (portalIcon != null) return portalIcon;
            var sr = portal.GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.sprite : null;
        }

        RectTransform AddIcon(Transform parent, Sprite icon)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(parent, false);
            var rect = iconGO.GetComponent<RectTransform>();
            rect.sizeDelta = portalImageSize;

            var image = iconGO.AddComponent<Image>();
            image.sprite = icon;
            image.color = portalTint;
            image.preserveAspect = true;
            image.raycastTarget = false;   // the button background handles the click
            return rect;
        }

        RectTransform AddArrow(Transform parent)
        {
            var arrowGO = new GameObject("DirectionPointer", typeof(RectTransform));
            arrowGO.transform.SetParent(parent, false);
            var rect = arrowGO.GetComponent<RectTransform>();
            rect.sizeDelta = pointerSize;

            Sprite pointer = ResolvePointerIcon();
            if (pointer != null)
            {
                var image = arrowGO.AddComponent<Image>();
                image.sprite = pointer;
                image.color = pointerTint;
                image.preserveAspect = true;
                image.raycastTarget = false;
                return rect;
            }

            var label = arrowGO.AddComponent<TMPro.TextMeshProUGUI>();
            label.text = fallbackLabel;
            label.color = pointerTint;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.fontSize = pointerSize.y;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.raycastTarget = false;
            return rect;
        }

        Sprite ResolvePointerIcon()
        {
            if (pointerIcon != null) return pointerIcon;

#if UNITY_EDITOR
            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(PointerSpriteAssetPath))
            {
                if (asset is Sprite sprite && sprite.name == PointerSpriteName)
                {
                    pointerIcon = sprite;
                    return pointerIcon;
                }
            }
#endif

            return null;
        }

        // ── Action ────────────────────────────────────────────────────────────────

        void WalkTo(PortalController portal)
        {
            if (portal == null) return;
            if (player == null) player = FindAnyObjectByType<ClickToMove2D>();
            if (player == null) return;

            player.SetMoveTarget(portal.transform.position.x);
        }
    }
}
