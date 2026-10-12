using UnityEngine;
using UnityEngine.UI;
using WormRush.Config;
using WormRush.Core;

namespace WormRush.View
{
    /// <summary>
    /// Everything drawn in screen space: length counter, persistent CTA, tutorial hand, "Block their path!",
    /// the end card. Built from code on one canvas (Screen Space - Camera, so the render check sees it too) and
    /// driven by <see cref="Present"/> from a PlayableFlow; it holds no game logic. Layout is in CSS-like units
    /// (390 x 844 reference), so the numbers below follow reference/worm-rush-playable.html.
    /// </summary>
    public sealed class PlayableUi
    {
        // The UI is laid out for this resolution; width and height weigh equally, so portrait and landscape both read.
        const float ReferenceWidth = 390f;
        const float ReferenceHeight = 844f;
        const float MatchWidthOrHeight = 0.5f;
        const float PlaneDistance = 1f;

        // Length counter (top centre).
        const int LengthFontSize = 20;
        const float LengthTop = 26f;

        // "Block their path!" (top centre).
        const int BlockFontSize = 22;
        const float BlockTop = 62f;

        // Persistent CTA (bottom centre).
        const int CtaFontSize = 17;
        const float CtaPadX = 30f;
        const float CtaHeight = 46f;
        const float CtaLip = 4f;
        const float CtaBottom = 18f;

        // Tutorial hand: a track, a dot sliding along it, the label above.
        const int TutorialFontSize = 24;
        const float TutorialAnchorY = 0.28f;   // of screen height, from the bottom
        const float TutorialTrackHalf = 70f;
        const float TutorialTrackThickness = 4f;
        const float TutorialLabelOffset = 44f;
        const float TutorialDotRadius = 18f;
        const float TutorialDotPulse = 3f;
        const float TutorialSlideSpeed = 2.5f;
        const float TutorialPulseSpeed = 5f;
        const float TutorialTrackAlpha = 0.5f;

        // End card.
        const int EndTitleFontSize = 44;
        const int EndSubtitleFontSize = 18;
        const int EndButtonFontSize = 24;
        const float EndTitleY = 67f;
        const float EndSubtitleY = 8f;
        const float EndButtonY = -63f;
        const float EndSubtitleWidth = 320f;
        const float EndSubtitleHeight = 48f;
        const float EndSubtitleAlpha = 0.9f;
        const float EndButtonPadX = 46f;
        const float EndButtonHeight = 62f;
        const float EndButtonLip = 5f;
        const float EndPopSeconds = 0.5f;
        const float EndPopOvershoot = 1.15f;
        const float EndPopFromScale = 0.3f;
        const float EndPopPeakAt = 0.7f;
        const float EndPulseSeconds = 1f;
        const float EndPulseAmount = 0.08f;

        const float OutlineDistance = 2.5f;

        readonly PlayableConfig _config;
        readonly Camera _camera;
        readonly UiKit _kit;
        readonly GameObject _root;
        readonly Canvas _canvas;

        readonly Text _length;
        readonly Text _blockHint;
        readonly RectTransform _cta;
        readonly RectTransform _tutorial;
        readonly CanvasGroup _tutorialGroup;
        readonly RectTransform _tutorialDot;
        readonly RectTransform _endCard;
        readonly Text _endTitle;
        readonly Text _endSubtitle;
        readonly RectTransform _endButton;

        int _lastLength = -1;
        bool _endShown;
        float _endShownAt;

        public static PlayableUi Create(PlayableConfig config, Camera camera)
        {
            return new PlayableUi(config, camera);
        }

        PlayableUi(PlayableConfig config, Camera camera)
        {
            _config = config;
            _camera = camera;
            _kit = new UiKit();

            _root = new GameObject("WormRush UI", typeof(RectTransform), typeof(Canvas));
            _canvas = _root.GetComponent<Canvas>();
            Canvas canvas = _canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = PlaneDistance;
            Transform root = _root.transform;

            Color shadow = new Color(0f, 0f, 0f, 0.7f);
            Color stroke = Color.black;

            _length = _kit.NewText("Length", root, "", LengthFontSize, Color.white);
            UiKit.Place(_length.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -LengthTop), new Vector2(240f, 30f));
            UiKit.AddShadow(_length, shadow, new Vector2(0f, -2f));

            _blockHint = _kit.NewText("BlockHint", root, config.BlockPathText, BlockFontSize, config.HintColor);
            UiKit.Place(_blockHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BlockTop), new Vector2(360f, 30f));
            UiKit.AddOutline(_blockHint, stroke, OutlineDistance);

            // Tutorial hand.
            _tutorial = UiKit.NewRect("Tutorial", root);
            UiKit.Place(_tutorial, new Vector2(0.5f, TutorialAnchorY), Vector2.zero, Vector2.zero);
            _tutorialGroup = _tutorial.gameObject.AddComponent<CanvasGroup>();
            _tutorialGroup.interactable = false;
            _tutorialGroup.blocksRaycasts = false;
            Image track = _kit.NewImage("Track", _tutorial, _kit.Pill, new Color(1f, 1f, 1f, TutorialTrackAlpha));
            UiKit.Place(track.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(TutorialTrackHalf * 2f, TutorialTrackThickness));
            Image dot = _kit.NewImage("Dot", _tutorial, _kit.Circle, Color.white);
            _tutorialDot = dot.rectTransform;
            UiKit.Place(_tutorialDot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * TutorialDotRadius * 2f);
            Text tutorialLabel = _kit.NewText("Label", _tutorial, config.TutorialText, TutorialFontSize, Color.white);
            UiKit.Place(tutorialLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, TutorialLabelOffset),
                new Vector2(360f, 34f));
            UiKit.AddOutline(tutorialLabel, stroke, OutlineDistance);

            // Persistent CTA.
            PillButton cta = _kit.NewPillButton("Cta", root, config.CtaText, CtaFontSize, CtaPadX, CtaHeight,
                config.CtaTopColor, config.CtaBottomColor, config.CtaEdgeColor, config.CtaTextColor, CtaLip);
            _cta = cta.Root;
            UiKit.Place(_cta, new Vector2(0.5f, 0f), new Vector2(0f, CtaBottom + CtaHeight * 0.5f), _cta.sizeDelta);

            // End card, on top of everything.
            _endCard = UiKit.NewRect("EndCard", root);
            UiKit.Stretch(_endCard);
            Image dim = _kit.NewImage("Dim", _endCard, null, config.EndDimColor);
            UiKit.Stretch(dim.rectTransform);

            _endTitle = _kit.NewText("Title", _endCard, "", EndTitleFontSize, Color.white);
            UiKit.Place(_endTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, EndTitleY), new Vector2(420f, 60f));
            UiKit.AddShadow(_endTitle, config.EndTitleShadowColor, new Vector2(0f, -4f));
            UiKit.AddShadow(_endTitle, new Color(0f, 0f, 0f, 0.65f), new Vector2(0f, -8f));

            _endSubtitle = _kit.NewText("Subtitle", _endCard, "", EndSubtitleFontSize, new Color(1f, 1f, 1f, EndSubtitleAlpha));
            _endSubtitle.fontStyle = FontStyle.Normal;
            _endSubtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(_endSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, EndSubtitleY),
                new Vector2(EndSubtitleWidth, EndSubtitleHeight));

            PillButton endButton = _kit.NewPillButton("Button", _endCard, config.EndButtonText, EndButtonFontSize,
                EndButtonPadX, EndButtonHeight, config.EndButtonTopColor, config.EndButtonBottomColor,
                config.EndButtonEdgeColor, config.EndButtonTextColor, EndButtonLip);
            _endButton = endButton.Root;
            UiKit.Place(_endButton, new Vector2(0.5f, 0.5f), new Vector2(0f, EndButtonY), _endButton.sizeDelta);

            _tutorial.gameObject.SetActive(false);
            _blockHint.gameObject.SetActive(false);
            _endCard.gameObject.SetActive(false);
        }

        /// <summary>
        /// True when the pointer is on the persistent CTA button. Always false while the end card owns the screen
        /// (there a tap anywhere is the CTA).
        /// </summary>
        public bool IsOverCta(Vector2 screenPoint)
        {
            return _cta.gameObject.activeSelf &&
                   RectTransformUtility.RectangleContainsScreenPoint(_cta, screenPoint, _camera);
        }

        /// <summary>
        /// Syncs the screen with the flow. <paramref name="time"/> drives the pop and pulse animations;
        /// the size is the render target in pixels and sets the UI scale.
        /// </summary>
        public void Present(PlayableFlow flow, WormSim sim, float time, float screenWidth, float screenHeight)
        {
            _canvas.scaleFactor = ViewMath.UiScale(screenWidth, screenHeight, ReferenceWidth, ReferenceHeight, MatchWidthOrHeight);

            int length = Mathf.FloorToInt(sim.Player.Length * 10f);
            if (length != _lastLength)
            {
                _lastLength = length;
                _length.text = _config.LengthLabel + length;
            }

            _cta.gameObject.SetActive(flow.CtaVisible);
            _blockHint.gameObject.SetActive(flow.BlockHintVisible);

            bool tutorial = flow.TutorialVisible;
            _tutorial.gameObject.SetActive(tutorial);
            if (tutorial) AnimateTutorial(flow.TutorialAlpha, sim.Time);

            bool end = flow.State == FlowState.EndCard;
            if (end && !_endShown) ShowEndCard(flow.EndKind, time);
            if (!end && _endShown) _endCard.gameObject.SetActive(false);
            _endShown = end;
            if (end) AnimateEndCard(time - _endShownAt);
        }

        void AnimateTutorial(float alpha, float simTime)
        {
            _tutorialGroup.alpha = alpha;
            float x = Mathf.Sin(simTime * TutorialSlideSpeed) * TutorialTrackHalf;
            float radius = TutorialDotRadius + Mathf.Sin(simTime * TutorialPulseSpeed) * TutorialDotPulse;
            _tutorialDot.anchoredPosition = new Vector2(x, 0f);
            _tutorialDot.sizeDelta = Vector2.one * radius * 2f;
        }

        void ShowEndCard(EndKind kind, float time)
        {
            bool won = kind != EndKind.SoClose;
            switch (kind)
            {
                case EndKind.Awesome:
                    _endTitle.text = _config.EndAwesomeTitle;
                    break;
                case EndKind.NiceMoves:
                    _endTitle.text = _config.EndNiceMovesTitle;
                    break;
                default:
                    _endTitle.text = _config.EndSoCloseTitle;
                    break;
            }

            _endSubtitle.text = won ? _config.EndWinSubtitle : _config.EndLoseSubtitle;
            _endShownAt = time;
            _endCard.gameObject.SetActive(true);
        }

        /// <summary>Title pops in (scale 0.3 to overshoot to 1, ease-out), the button pulses, as in the reference CSS.</summary>
        void AnimateEndCard(float elapsed)
        {
            float t = Mathf.Clamp01(elapsed / EndPopSeconds);
            float scale;
            if (t < EndPopPeakAt)
                scale = Mathf.Lerp(EndPopFromScale, EndPopOvershoot, EaseOut(t / EndPopPeakAt));
            else
                scale = Mathf.Lerp(EndPopOvershoot, 1f, EaseOut((t - EndPopPeakAt) / (1f - EndPopPeakAt)));
            _endTitle.rectTransform.localScale = Vector3.one * scale;

            Color color = _endTitle.color;
            color.a = EaseOut(t);
            _endTitle.color = color;

            float phase = Mathf.Repeat(elapsed / EndPulseSeconds, 1f);
            float pulse = 1f + EndPulseAmount * (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f));
            _endButton.localScale = Vector3.one * pulse;
        }

        static float EaseOut(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv;
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            _kit.Dispose();
        }
    }
}
