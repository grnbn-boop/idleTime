using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using IdleTime.Core;

namespace IdleTime.Interactions
{
    // The single seam between "the player entered an active portal" and "the world
    // changes." Wired for OPTION A — one scene per room: a portal carries the build
    // name of the room to travel to (forward portals → the next room, return portals →
    // the previous room, since map traversal is just "which scene name does this portal
    // point at"). The managers (PlayerManager, Inventory, Equipment, SaveManager,
    // ScreenFader, this app's other DontDestroyOnLoad singletons) and the static
    // RoomProgress all persist across the load, so progress/inventory carry over and a
    // room you've cleared stays cleared.
    //
    // Setup to make a destination live: create the room as its own .unity scene and add
    // it to Build Settings (File ▸ Build Settings ▸ Add Open Scenes). Until then, Go()
    // logs a clear warning rather than throwing, so half-authored maps don't break play.
    //
    // (Options B "one big scene, teleport" and C "additive scenes" remain valid future
    // pivots — A and C share the per-scene room layout, so this is a free upgrade path.)
    public static class LevelLoader
    {
        const float FadeOutDuration = 0.4f;
        const float FadeInDuration = 0.4f;

        public static void Go(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                Debug.Log("[LevelLoader] Portal entered, but no destination is set " +
                          "(stub). Set 'Destination Scene Name' on the PortalController.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogWarning($"[LevelLoader] Scene '{destination}' is not in Build Settings — " +
                                 "create the room scene and add it via File ▸ Build Settings.");
                return;
            }

            // Fade out → load → fade in. ScreenFader is DontDestroyOnLoad, so the single
            // coroutine keeps running across the scene load (its host object survives).
            var fader = ScreenFader.Instance;
            if (fader != null)
                fader.StartCoroutine(Transition(destination, fader));
            else
                SceneManager.LoadScene(destination);   // no fader in scene → straight cut
        }

        static IEnumerator Transition(string destination, ScreenFader fader)
        {
            yield return fader.Fade(1f, FadeOutDuration);

            AsyncOperation op = SceneManager.LoadSceneAsync(destination);
            while (op != null && !op.isDone) yield return null;

            // One frame so the new room's objects run Awake/Start (portals derive their
            // open state, the nav HUD rebuilds) before we reveal them.
            yield return null;

            yield return fader.Fade(0f, FadeInDuration);
        }
    }
}
