#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using WormRush.Core;
using WormRush.Flow;

namespace WormRush.View
{
    /// <summary>
    /// Editor/dev-build only readout: FPS, draw batches, counts. Press F1 to hide it.
    /// Not part of the playable: the real HUD is PlayableUi. R restarts the run.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        const float SmoothingPerFrame = 0.05f;
        const float FontScale = 0.022f;   // of screen height
        const float TopOffset = 0.07f;    // of screen height: clear of the length counter

        public PlayableRunner Runner;

        float _smoothedDelta = 1f / 60f;
        bool _visible = true;
        GUIStyle _style;

        void Update()
        {
            _smoothedDelta += (Time.unscaledDeltaTime - _smoothedDelta) * SmoothingPerFrame;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible || Runner == null || Runner.Sim == null) return;

            if (_style == null) _style = new GUIStyle(GUI.skin.label) { richText = true };
            _style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * FontScale));

            WormSim sim = Runner.Sim;
            int alive = 0;
            for (int i = 0; i < sim.Worms.Count; i++)
                if (sim.Worms[i].Alive) alive++;

            string text =
                "FPS " + Mathf.RoundToInt(1f / _smoothedDelta) +
                "\n" + Runner.Flow.State + "   worms alive " + alive + "   food " + sim.FoodCount +
                "\nkills " + sim.PlayerKills + "   store opens " + Runner.StoreOpens +
#if UNITY_EDITOR
                "\nbatches " + UnityEditor.UnityStats.batches + "   set-pass " + UnityEditor.UnityStats.setPassCalls +
#endif
                (sim.Started ? "" : "\nclick to start, then move the mouse") +
                (Runner.Flow.State == FlowState.EndCard ? "\nend card: click = store" : "") +
                "\nR restart   F1 hide";
            GUI.Label(new Rect(12f, Screen.height * TopOffset, Screen.width - 24f, Screen.height * 0.4f), text, _style);
        }
    }
}
#endif
