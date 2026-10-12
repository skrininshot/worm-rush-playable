using UnityEngine;
using WormRush.View;

namespace WormRush.Flow
{
    /// <summary>
    /// Puts the playable into whatever scene is open, from code: no scene wiring, nothing to configure by hand.
    /// Press Play in any scene that has a camera and the game runs.
    /// </summary>
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run()
        {
            if (Object.FindFirstObjectByType<PlayableRunner>() != null) return;

            GameObject root = new GameObject("WormRush");
            PlayableRunner runner = root.AddComponent<PlayableRunner>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            root.AddComponent<DebugOverlay>().Runner = runner;
#endif
        }
    }
}
