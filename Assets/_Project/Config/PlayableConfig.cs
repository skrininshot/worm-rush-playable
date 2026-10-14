using UnityEngine;
using WormRush.Core;

namespace WormRush.Config
{
    /// <summary>
    /// Everything a producer may want to change: simulation tunables (<see cref="Sim"/>), camera, colours, look of the
    /// worms and food. One asset, loaded from Resources so a bare scene needs no wiring.
    /// Defaults reproduce reference/worm-rush-playable.html.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayableConfig", menuName = "Worm Rush/Playable Config")]
    public sealed class PlayableConfig : ScriptableObject
    {
        public const string ResourcePath = "PlayableConfig";

        [Header("Simulation")]
        public SimConfig Sim = new SimConfig();
        [Tooltip("Arena seed. 0 picks a new one every run.")]
        public int Seed = 0;

        [Header("Run")]
        public int TargetFrameRate = 60;

        [Header("Store links (the Web build picks one by user agent)")]
        public string StoreUrlIos = "https://apps.apple.com/";
        public string StoreUrlAndroid = "https://play.google.com/store/apps";

        [Header("Juice (shake, particles, popups, sounds)")]
        public JuiceConfig Juice = new JuiceConfig();
        public string KillPopupText = "+1 NICE!";
        public string ValuePopupFormat = "+{0}";
        public Color KillPopupColor = new Color32(0xff, 0xd2, 0x3f, 0xff);
        public Color ValuePopupColor = Color.white;
        public Color PopupOutlineColor = Color.black;

        [Header("UI juice (buttons, end card, confetti)")]
        public UiJuiceConfig UiJuice = new UiJuiceConfig();

        [Header("Texts")]
        public string CtaText = "PLAY NOW";
        public string LengthLabel = "Length: ";
        public string TutorialText = "DRAG TO MOVE";
        public string BlockPathText = "Block their path!";
        public string EndAwesomeTitle = "AWESOME!";
        public string EndNiceMovesTitle = "NICE MOVES!";
        public string EndSoCloseTitle = "SO CLOSE!";
        public string EndWinSubtitle = "Grow into the longest worm in the arena!";
        public string EndLoseSubtitle = "Come back and take your revenge!";
        public string EndButtonText = "PLAY NOW";

        [Header("UI colours")]
        [Tooltip("PLAY NOW buttons: lime gradient, white label with a dark outline (yellow with dark text read flat).")]
        public Color CtaTopColor = new Color32(0x8a, 0xf5, 0x3c, 0xff);
        public Color CtaBottomColor = new Color32(0x1f, 0xb8, 0x2d, 0xff);
        public Color CtaEdgeColor = new Color32(0x0b, 0x6a, 0x1b, 0xff);
        public Color CtaTextColor = Color.white;
        public Color CtaTextOutlineColor = new Color32(0x0a, 0x4d, 0x18, 0xff);
        public Color EndButtonTopColor = new Color32(0x8a, 0xf5, 0x3c, 0xff);
        public Color EndButtonBottomColor = new Color32(0x1f, 0xb8, 0x2d, 0xff);
        public Color EndButtonEdgeColor = new Color32(0x0b, 0x6a, 0x1b, 0xff);
        public Color EndButtonTextColor = Color.white;
        public Color EndButtonTextOutlineColor = new Color32(0x0a, 0x4d, 0x18, 0xff);
        public Color EndDimColor = new Color32(0x08, 0x0c, 0x1c, 0xc7);
        public Color EndTitleShadowColor = new Color32(0xff, 0x5d, 0x8f, 0xff);
        public Color HintColor = new Color32(0xff, 0xd2, 0x3f, 0xff);

        [Header("Restart button (top right)")]
        [Tooltip("A small button that starts the 25 s run again, so the playable can be hosted and replayed. Switch off for ad networks that supply their own replay.")]
        public bool ShowRestartButton = true;
        public Color RestartBackColor = new Color(0f, 0f, 0f, 0.45f);
        public Color RestartIconColor = new Color(1f, 1f, 1f, 0.95f);

        [Header("Camera and input")]
        [Tooltip("World units visible across the shorter screen side at worm length 0.")]
        public float ViewSpan = 340f;
        [Tooltip("The view widens by this fraction per unit of player length.")]
        public float ZoomOutPerLength = 0.002f;
        public float CameraDepth = -10f;
        [Tooltip("Pointer closer than this to the screen centre does not change the heading.")]
        public float PointerDeadZonePixels = 6f;
        [Tooltip("Things this far outside the screen are still drawn (grid, food, segments).")]
        public float CullMargin = 60f;
        [Tooltip("Depth distance between draw layers: arena, food, worms.")]
        public float LayerDepthStep = 1f;

        [Header("Arena")]
        public Color OutsideColor = new Color32(0x04, 0x06, 0x0d, 0xff);
        public Color ArenaColor = new Color32(0x0b, 0x10, 0x20, 0xff);
        public Color ArenaBorderColor = new Color32(0xff, 0x3d, 0x6e, 0xff);
        public float ArenaBorderWidth = 8f;
        public Color GridDotColor = new Color32(0x18, 0x21, 0x3d, 0xff);
        public float GridSpacing = 60f;
        public float GridDotRadius = 3f;

        [Header("Palette (indexed by ColorId)")]
        public Color[] Palette =
        {
            new Color32(0xff, 0x5d, 0x8f, 0xff),
            new Color32(0xff, 0xd2, 0x3f, 0xff),
            new Color32(0x3e, 0xe6, 0xa8, 0xff),
            new Color32(0x4c, 0xc9, 0xf0, 0xff),
            new Color32(0xb3, 0x88, 0xff, 0xff),
            new Color32(0xff, 0x8c, 0x42, 0xff),
        };
        public Color[] StripePalette =
        {
            new Color32(0xff, 0xd1, 0xe0, 0xff),
            new Color32(0xff, 0xf4, 0xc2, 0xff),
            new Color32(0xd6, 0xff, 0xf0, 0xff),
            new Color32(0xe0, 0xfb, 0xff, 0xff),
            new Color32(0xef, 0xe3, 0xff, 0xff),
            new Color32(0xff, 0xe0, 0xc9, 0xff),
        };

        [Header("Food")]
        public float FoodGlowAlpha = 0.25f;
        public float FoodGlowScale = 2.2f;
        public float FoodPulseAmount = 0.15f;
        public float FoodPulseSpeed = 3f;

        [Header("Worms")]
        [Tooltip("The body alternates between the two palette colours every this many segments.")]
        public int StripeLength = 4;
        public Color ShadowColor = new Color(0f, 0f, 0f, 0.35f);
        public Vector2 ShadowOffset = new Vector2(3f, -4f);
        public Color EyeColor = Color.white;
        public Color PupilColor = new Color32(0x11, 0x11, 0x11, 0xff);
        [Tooltip("Eye geometry, in worm radii.")]
        public float EyeForward = 0.35f;
        public float EyeSide = 0.45f;
        public float EyeRadius = 0.38f;
        public float PupilRadius = 0.2f;
        public float PupilForward = 0.14f;

        [Header("Rendering")]
        [Tooltip("Instanced circle shader. Assigned by the editor tool; kept as a reference so builds include it.")]
        public Shader CircleShader;

        public static PlayableConfig Load()
        {
            PlayableConfig config = Resources.Load<PlayableConfig>(ResourcePath);
            return config != null ? config : CreateInstance<PlayableConfig>();
        }

        public Color PrimaryColor(int colorId)
        {
            return Palette[Mathf.Abs(colorId) % Palette.Length];
        }

        public Color SecondaryColor(int colorId)
        {
            return StripePalette[Mathf.Abs(colorId) % StripePalette.Length];
        }

        void OnValidate()
        {
            if (Palette != null && Palette.Length > 0)
            {
                Sim.ColorCount = Palette.Length;
                UiJuice.ColorSlots = Palette.Length;
            }
            if (StripeLength < 1) StripeLength = 1;
        }
    }
}
