using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleTime.Interactions
{
    // The single seam between "the player entered an active portal" and "the world
    // changes." Everything else in the portal system is agnostic to how you expand
    // out of the test bed — only this file needs to change when you pick an approach.
    //
    // ───────────────────────────────────────────────────────────────────────────
    //  EXPANSION OPTIONS (pick one when there's a second level to go to)
    // ───────────────────────────────────────────────────────────────────────────
    //
    //  A. ONE SCENE PER LEVEL  (recommended starting point)
    //     Each level is its own .unity file; the portal carries the next scene name.
    //         SceneManager.LoadScene(destination);
    //     + Cleanest separation, smallest scenes, plays nicely with the existing
    //       death→reload-active-scene flow and DontDestroyOnLoad managers.
    //     + Matches IdleOn's discrete "screens."
    //     - Brief load hitch; no cross-scene object references (use the manager
    //       singletons, which is already the pattern here).
    //
    //  B. ONE BIG SCENE, MOVE THE PLAYER  (location-based)
    //     All rooms laid out in one scene; the portal teleports the player + camera
    //     to the next room's anchor Transform.
    //         player.position = destinationAnchor.position; // + snap camera
    //     + No load hitch, trivial state, whole world visible at once.
    //     - Everything loads/simulates at once (memory + all spawners running); one
    //       giant scene is unwieldy for source control as content grows.
    //
    //  C. ADDITIVE SCENES  (best long-term, more plumbing)
    //     A persistent bootstrap scene holds the managers; level scenes load/unload
    //     additively for seamless transitions.
    //         SceneManager.LoadSceneAsync(destination, LoadSceneMode.Additive);
    //         // then unload the old one
    //     + Seamless + modular. Upgrade path from A: A and C share the per-scene
    //       layout, so starting with A costs nothing later.
    //
    //  RECOMMENDATION: start with A. The portal already passes a string destination,
    //  so going live is a one-line change below. Move to C if/when you want seamless
    //  transitions. Wrap whichever you choose in a ScreenFader fade for polish (the
    //  project already has one — see DeathSequenceController for the pattern).
    // ───────────────────────────────────────────────────────────────────────────
    public static class LevelLoader
    {
        public static void Go(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                Debug.Log("[LevelLoader] Portal entered, but no destination is set yet " +
                          "(stub). Set 'Destination Scene Name' on the PortalController, " +
                          "then enable Option A below.");
                return;
            }

            // --- OPTION A (uncomment when the destination scene exists & is in Build Settings) ---
            // if (Application.CanStreamedLevelBeLoaded(destination))
            //     SceneManager.LoadScene(destination);
            // else
            //     Debug.LogWarning($"[LevelLoader] Scene '{destination}' is not in Build Settings.");

            Debug.Log($"[LevelLoader] (stub) Would travel to '{destination}'. " +
                      "Uncomment Option A in LevelLoader.cs to make it live.");
        }
    }
}
