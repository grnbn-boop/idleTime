using UnityEngine;

namespace IdleTime.Core
{
    // Creates the persistent "brain" of the game — the GameSystems rig of manager
    // singletons — exactly once at launch, no matter which scene you press Play on.
    // After this, the rig DontDestroyOnLoads itself and survives every room load, so
    // individual room scenes never need a copy of GameSystems in them.
    //
    // Setup: make GameSystems a prefab and place it at  Resources/GameSystems.prefab
    // (any folder named "Resources" works). Then remove GameSystems from your scenes.
    //
    // Safe to adopt gradually: if a scene still contains a GameSystems, the singletons'
    // own dedup (Destroy(transform.root...)) tears the duplicate down, so nothing breaks
    // while you migrate room scenes one at a time.
    public static class GameBootstrap
    {
        // Path is relative to a Resources/ folder, without extension.
        const string RigResourcePath = "GameSystems";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureGameSystems()
        {
            // Already alive (e.g. a scene still embeds the rig)? Leave it be.
            if (PlayerManager.Instance != null) return;

            var prefab = Resources.Load<GameObject>(RigResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[Bootstrap] No '{RigResourcePath}' prefab under a Resources/ folder yet — " +
                    "falling back to the GameSystems placed in the scene. Move the prefab to " +
                    "Resources/ to stop placing it per scene.");
                return;
            }

            var rig = Object.Instantiate(prefab);
            rig.name = prefab.name;   // drop the "(Clone)" suffix
        }
    }
}
