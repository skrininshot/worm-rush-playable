using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WormRush.Config;
using WormRush.Core;

namespace WormRush.View
{
    /// <summary>
    /// Everything drawn in screen space: length counter, persistent CTA, tutorial hand, "Block their path!",
    /// the end card, and the confetti over all of it. Built from code on one canvas (Screen Space - Camera, so
    /// the render check sees it too) and driven by <see cref="Present"/> from a PlayableFlow; it holds no game
    /// logic, and every animation decision is a <see cref="UiJuice"/> pose. Layout is in CSS-like units
    /// (390 x 844 reference), so the numbers below follow reference/worm-rush-playable.html.
    /// </summary>
    public sealed class PlayableUi
    {
        // The UI is laid out for this resolution; width and height weigh equally, so portrait and landscape both read.
        const float ReferenceWidth = 390f;
        const float ReferenceHeight = 844f;
        const float MatchWidthOrHeight = 0.5f;
        const float PlaneDistance = 1f;
        // The world's instanced layers sort by distance, which depends on the 2D renderer's sort axis and so on where
        // the camera is; an explicit sorting order keeps the UI above them wherever the player is.
        const int CanvasSortingOrder = 100;
        // A frame longer than this (a tab coming back, a breakpoint) advances the UI by this much, no more.
        const float MaxFrameSeconds = 0.05f;
        // The UI's own random stream (confetti), apart from the sim's seeded one.
        const int JuiceSeedMask = 0x5549;

        // Edge elements keep clear of the screen edge: a phone's camera cut-out, rounded corners and home bar
        // sit in the outer ~50 units, so nothing interactive or readable lives there.
        // Length counter (top centre): its centre line, from the top edge.
        const int LengthFontSize = 20;
        const float LengthTop = 74f;

        // "Block their path!" (top centre), under the counter.
        const int BlockFontSize = 22;
        const float BlockTop = 112f;

        // Persistent CTA (bottom centre), 1.5x the first version; CtaBottom is the gap under its lip.
        const int CtaFontSize = 26;
        const float CtaPadX = 45f;
        const float CtaHeight = 69f;
        const float CtaLip = 6f;
        const float CtaBottom = 54f;

        // Tutorial finger: a hand dragging round a circle with a trail, the label above the circle.
        const int TutorialFontSize = 24;
        const float TutorialAnchorY = 0.34f;   // of screen height, from the bottom: the circle's centre
        const float TutorialLabelGap = 44f;    // label sits this far above the top of the circle
        const float TutorialHandSize = 110f;   // the hand sprite's box, UI units
        const float TutorialTiltDegrees = 9f;  // the hand leans with its sideways position, for a little life
        const float TutorialShadowUpX = 12f;   // shadow offset while hovering ...
        const float TutorialShadowUpY = -16f;
        const float TutorialShadowDownX = 3f;  // ... and while pressed
        const float TutorialShadowDownY = -4f;
        const float TutorialShadowAlpha = 0.4f;
        const float TutorialTouchSize = 74f;   // soft light under the fingertip
        const float TutorialTouchAlpha = 0.45f;
        const float TutorialRingStart = 0.3f;  // ripple: grows from this fraction of its size to TutorialRingEnd
        const float TutorialRingEnd = 1.15f;
        const float TutorialDotMin = 8f;
        const float TutorialDotMax = 24f;
        const float TutorialTrailAlpha = 0.6f;

        // Restart button (top right). The hit area is a little bigger than the disc, for a thumb.
        const float RestartHitSize = 44f;
        const float RestartDiscSize = 34f;
        const float RestartMarginSide = 22f;   // screen edge to the hit area
        const float RestartMarginTop = 52f;    // level with the length counter, clear of the camera cut-out
        const float RestartSpinSeconds = 0.45f;
        const float RestartSpinDegrees = 360f; // clockwise, eased out
        const float RestartPressScale = 0.82f; // the disc dips to this on a press and comes back

        // End card.
        // Title and button are ~1.4x the first version. The title shrinks to fit when the line is wider than
        // the screen less EndSideMargin on each side (NICE MOVES! is the long one).
        const int EndTitleFontSize = 62;
        const int EndTitleMinFontSize = 24;
        const int EndTitleWrapBelowFontSize = 55; // 1.25x the first version: a title that would shrink below this wraps
        const float TitleLineHeight = 1.02f;   // of the font size
        const int EndSubtitleFontSize = 20;
        const int EndButtonFontSize = 34;
        const float EndTitleY = 98f;
        const float EndSubtitleY = 30f;
        const float EndButtonY = -62f;
        const float EndSideMargin = 32f;
        const float EndSubtitleWidth = 330f;
        const float EndSubtitleHeight = 56f;
        const float EndSubtitleAlpha = 0.9f;
        const float EndButtonPadX = 62f;
        const float EndButtonHeight = 86f;
        const float EndButtonLip = 7f;
        const int MaxTitleLetters = 16;
        const float LetterBoxFactor = 1.45f;   // of the font size
        const float TitleShadowNear = -6f;
        const float TitleShadowFar = -11f;
        const float FallbackAdvance = 0.6f;    // of the font size, when the font has no metrics for a character

        // Button light: the beam's colour and how far it travels past the face on each side.
        const float ShineAlpha = 0.65f;
        const float ShineTravelPad = 0.2f;     // of the button width

        const float OutlineDistance = 2.5f;
        // Button labels: white with a dark rim and a short drop, so they pop off the gradient (see UiKit.AddRim).
        const float ButtonTextOutline = 1.5f;
        const float EndButtonTextOutline = 2f;
        const float ButtonTextDrop = 1f;
        const float EndButtonTextDrop = 1.5f;

        // World popups ("+1 KILL!", "+3"): the Text is rasterised at this size and scaled to match the world, so a
        // zooming camera never makes the font atlas regenerate. The outline follows the reference (5 px on 26).
        const int PopupRasterFontSize = 48;
        const float PopupOutlineFraction = 0.1f;
        const float PopupBoxWidth = 360f;
        const float PopupBoxHeight = 64f;

        // Confetti images are one size and scaled, so moving them never rebuilds a mesh.
        const float FxNativeSize = 100f;

        readonly PlayableConfig _config;
        readonly Camera _camera;
        readonly UiKit _kit;
        readonly UiJuice _juice;
        readonly GameObject _root;
        readonly Canvas _canvas;

        readonly Text _length;
        readonly Text _blockHint;
        readonly PillButton _cta;
        readonly RectTransform _tutorial;
        readonly CanvasGroup _tutorialGroup;
        readonly RectTransform _tutorialHand;
        readonly RectTransform _tutorialShadow;
        readonly Image _tutorialHandImage;
        readonly Image _tutorialShadowImage;
        readonly Image _tutorialTouch;
        readonly Image _tutorialRing;
        readonly Image[] _tutorialTrail;
        readonly TutorialGesture _gesture;
        readonly RectTransform _endCard;
        readonly Image _dim;
        readonly Image _flash;
        readonly Image _rays;
        readonly Text[] _letters;
        readonly float[] _letterX;
        float _uiWidth = ReferenceWidth;
        float _titleCenterY = EndTitleY;
        float _titleHalfHeight = EndTitleFontSize * 0.6f;
        readonly float[] _letterY;
        readonly Text _endSubtitle;
        readonly PillButton _endButton;
        readonly RectTransform _restart;
        readonly RectTransform _restartIcon;

        readonly Text[] _popups;
        readonly int[] _popupKeys;

        readonly Image[] _fxImages;
        readonly RectTransform[] _fxRects;

        int _lastLength = -1;
        int _letterCount;
        bool _endShown;
        bool _endWon;
        bool _hintShown;
        float _hintShownAt;
        bool _tutorialShown;
        float _tutorialShownAt;
        bool _restartSpinPending;
        float _restartSpinAt = float.NegativeInfinity;
        float _lastTime;
        bool _hasTime;
        Vector2 _pointer;
        bool _pointerIsMouse;

        public static PlayableUi Create(PlayableConfig config, Camera camera)
        {
            return new PlayableUi(config, camera);
        }

        PlayableUi(PlayableConfig config, Camera camera)
        {
            _config = config;
            _camera = camera;
            _kit = new UiKit();
            _juice = new UiJuice(config.UiJuice, config.Seed ^ JuiceSeedMask);

            _root = new GameObject("WormRush UI", typeof(RectTransform), typeof(Canvas));
            _canvas = _root.GetComponent<Canvas>();
            Canvas canvas = _canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = PlaneDistance;
            canvas.sortingOrder = CanvasSortingOrder;
            Transform root = _root.transform;

            Color shadow = new Color(0f, 0f, 0f, 0.7f);
            Color stroke = Color.black;
            Color shine = new Color(1f, 1f, 1f, ShineAlpha);

            // World popups first, so every HUD element and the end card draw on top of them.
            int popupCount = Mathf.Max(1, config.Juice.PopupCapacity);
            _popups = new Text[popupCount];
            _popupKeys = new int[popupCount];
            for (int i = 0; i < popupCount; i++)
            {
                Text popup = _kit.NewText("Popup", root, "", PopupRasterFontSize, Color.white);
                UiKit.Place(popup.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PopupBoxWidth, PopupBoxHeight));
                UiKit.AddOutline(popup, config.PopupOutlineColor, PopupRasterFontSize * PopupOutlineFraction);
                popup.gameObject.SetActive(false);
                _popups[i] = popup;
            }

            _length = _kit.NewText("Length", root, "", LengthFontSize, Color.white);
            UiKit.Place(_length.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -LengthTop), new Vector2(240f, 30f));
            UiKit.AddShadow(_length, shadow, new Vector2(0f, -2f));

            _blockHint = _kit.NewText("BlockHint", root, config.BlockPathText, BlockFontSize, config.HintColor);
            UiKit.Place(_blockHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BlockTop), new Vector2(360f, 30f));
            UiKit.AddOutline(_blockHint, stroke, OutlineDistance);

            // Tutorial finger. Children are in draw order: label, trail, touch light, ripple, hand shadow, hand.
            _gesture = new TutorialGesture(config.UiJuice);
            _tutorial = UiKit.NewRect("Tutorial", root);
            UiKit.Place(_tutorial, new Vector2(0.5f, TutorialAnchorY), Vector2.zero, Vector2.zero);
            _tutorialGroup = _tutorial.gameObject.AddComponent<CanvasGroup>();
            _tutorialGroup.interactable = false;
            _tutorialGroup.blocksRaycasts = false;

            Text tutorialLabel = _kit.NewText("Label", _tutorial, config.TutorialText, TutorialFontSize, Color.white);
            UiKit.Place(tutorialLabel.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, config.UiJuice.TutorialRadius + TutorialLabelGap), new Vector2(360f, 34f));
            UiKit.AddOutline(tutorialLabel, stroke, OutlineDistance);

            _tutorialTrail = new Image[_gesture.TrailDots];
            for (int i = 0; i < _tutorialTrail.Length; i++)
            {
                Image dot = _kit.NewImage("Trail", _tutorial, _kit.Circle, Color.white);
                UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * TutorialDotMax);
                _tutorialTrail[i] = dot;
            }

            _tutorialTouch = _kit.NewImage("Touch", _tutorial, _kit.Glow, Color.white);
            UiKit.Place(_tutorialTouch.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * TutorialTouchSize);
            _tutorialRing = _kit.NewImage("Ripple", _tutorial, _kit.Ring, Color.white);
            UiKit.Place(_tutorialRing.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.one * config.UiJuice.TutorialRingSize);

            _tutorialShadowImage = _kit.NewImage("HandShadow", _tutorial, _kit.Hand, new Color(0f, 0f, 0f, TutorialShadowAlpha));
            _tutorialShadow = _tutorialShadowImage.rectTransform;
            _tutorialHandImage = _kit.NewImage("Hand", _tutorial, _kit.Hand, Color.white);
            _tutorialHand = _tutorialHandImage.rectTransform;
            // The fingertip is the pivot, so position, scale and tilt all happen at the point that touches.
            Vector2 tipPivot = new Vector2(0.5f + UiKit.HandTipX / UiKit.HandCell, 0.5f + UiKit.HandTipY / UiKit.HandCell);
            UiKit.Place(_tutorialShadow, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * TutorialHandSize);
            _tutorialShadow.pivot = tipPivot;
            UiKit.Place(_tutorialHand, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * TutorialHandSize);
            _tutorialHand.pivot = tipPivot;

            // Persistent CTA.
            _cta = _kit.NewPillButton("Cta", root, config.CtaText, CtaFontSize, CtaPadX, CtaHeight,
                config.CtaTopColor, config.CtaBottomColor, config.CtaEdgeColor, config.CtaTextColor, CtaLip,
                config.CtaTopColor, shine);
            UiKit.Place(_cta.Root, new Vector2(0.5f, 0f), new Vector2(0f, CtaBottom + CtaHeight * 0.5f), _cta.Root.sizeDelta);
            UiKit.AddRim(_cta.Label, config.CtaTextOutlineColor, ButtonTextOutline, ButtonTextDrop);

            // End card.
            _endCard = UiKit.NewRect("EndCard", root);
            UiKit.Stretch(_endCard);
            _dim = _kit.NewImage("Dim", _endCard, _kit.Square, config.EndDimColor);
            UiKit.Stretch(_dim.rectTransform);

            _rays = _kit.NewImage("Rays", _endCard, _kit.Rays, Color.white);
            UiKit.Place(_rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, EndTitleY),
                Vector2.one * config.UiJuice.RaysSize);

            // The title is one Text per letter so each can drop, tilt and ride the wave on its own.
            _letters = new Text[MaxTitleLetters];
            _letterX = new float[MaxTitleLetters];
            _letterY = new float[MaxTitleLetters];
            for (int i = 0; i < _letters.Length; i++)
            {
                Text letter = _kit.NewText("Letter", _endCard, "", EndTitleFontSize, Color.white);
                UiKit.Place(letter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, EndTitleY),
                    Vector2.one * (EndTitleFontSize * LetterBoxFactor));
                UiKit.AddShadow(letter, config.EndTitleShadowColor, new Vector2(0f, TitleShadowNear));
                UiKit.AddShadow(letter, new Color(0f, 0f, 0f, 0.65f), new Vector2(0f, TitleShadowFar));
                letter.gameObject.SetActive(false);
                _letters[i] = letter;
            }

            _endSubtitle = _kit.NewText("Subtitle", _endCard, "", EndSubtitleFontSize, new Color(1f, 1f, 1f, EndSubtitleAlpha));
            _endSubtitle.fontStyle = FontStyle.Normal;
            _endSubtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(_endSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, EndSubtitleY),
                new Vector2(EndSubtitleWidth, EndSubtitleHeight));

            _endButton = _kit.NewPillButton("Button", _endCard, config.EndButtonText, EndButtonFontSize,
                EndButtonPadX, EndButtonHeight, config.EndButtonTopColor, config.EndButtonBottomColor,
                config.EndButtonEdgeColor, config.EndButtonTextColor, EndButtonLip, config.EndButtonTopColor, shine);
            UiKit.Place(_endButton.Root, new Vector2(0.5f, 0.5f), new Vector2(0f, EndButtonY), _endButton.Root.sizeDelta);
            UiKit.AddRim(_endButton.Label, config.EndButtonTextOutlineColor, EndButtonTextOutline, EndButtonTextDrop);

            _flash = _kit.NewImage("Flash", _endCard, _kit.Square, new Color(1f, 1f, 1f, 0f));
            UiKit.Stretch(_flash.rectTransform);

            // Restart: over the end card (it is not part of it, a tap there must not go to the store), under the confetti.
            if (config.ShowRestartButton)
            {
                _restart = UiKit.NewRect("Restart", root);
                UiKit.Place(_restart, new Vector2(1f, 1f),
                    new Vector2(-(RestartMarginSide + RestartHitSize * 0.5f), -(RestartMarginTop + RestartHitSize * 0.5f)),
                    Vector2.one * RestartHitSize);
                Image disc = _kit.NewImage("Disc", _restart, _kit.Circle, config.RestartBackColor);
                UiKit.Place(disc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * RestartDiscSize);
                Image icon = _kit.NewImage("Icon", _restart, _kit.Restart, config.RestartIconColor);
                UiKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * RestartDiscSize);
                _restartIcon = icon.rectTransform;
            }

            // Confetti, stars and rings: over everything, on a canvas of their own so they re-batch alone.
            RectTransform fx = UiKit.NewRect("Fx", root);
            UiKit.Stretch(fx);
            fx.gameObject.AddComponent<Canvas>();
            int fxCount = Mathf.Max(1, config.UiJuice.FxCapacity);
            _fxImages = new Image[fxCount];
            _fxRects = new RectTransform[fxCount];
            for (int i = 0; i < fxCount; i++)
            {
                Image image = _kit.NewImage("Piece", fx, _kit.Square, Color.white);
                UiKit.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * FxNativeSize);
                image.gameObject.SetActive(false);
                _fxImages[i] = image;
                _fxRects[i] = image.rectTransform;
            }

            _tutorial.gameObject.SetActive(false);
            _blockHint.gameObject.SetActive(false);
            _endCard.gameObject.SetActive(false);
        }

        /// <summary>Reactions and sounds of the UI itself; the caller plays <c>Sounds</c> and then clears the list.</summary>
        public List<SoundRequest> Sounds
        {
            get { return _juice.Sounds; }
        }

        /// <summary>
        /// True when the pointer is on the persistent CTA button. Always false while the end card owns the screen
        /// (there a tap anywhere is the CTA).
        /// </summary>
        public bool IsOverCta(Vector2 screenPoint)
        {
            return _cta.Root.gameObject.activeSelf &&
                   RectTransformUtility.RectangleContainsScreenPoint(_cta.Root, screenPoint, _camera);
        }

        /// <summary>True when the pointer is on the restart button (always false when it is switched off).</summary>
        public bool IsOverRestart(Vector2 screenPoint)
        {
            return _restart != null && RectTransformUtility.RectangleContainsScreenPoint(_restart, screenPoint, _camera);
        }

        /// <summary>The restart button was pressed: it dips and its arrow spins once.</summary>
        public void PressRestart()
        {
            _restartSpinPending = true;
        }

        /// <summary>Where the pointer is, for the hover state of the end button. Call before <see cref="Present"/>.</summary>
        public void SetPointer(Vector2 screenPoint, bool isMouse)
        {
            _pointer = screenPoint;
            _pointerIsMouse = isMouse;
        }

        /// <summary>A press on a button (the CTA, or anywhere on the end card): squash, burst and tap sound.</summary>
        public void Press(Vector2 screenPoint, float screenWidth, float screenHeight)
        {
            Vector2 ui = ScreenToUi(screenPoint, screenWidth, screenHeight);
            _juice.Press(ui.x, ui.y, _endShown ? UiTarget.EndButton : UiTarget.Cta);
        }

        /// <summary>Game sounds the HUD bumps with (eating, kills, death): pass what JuiceState asked for.</summary>
        public void React(List<SoundRequest> requests)
        {
            for (int i = 0; i < requests.Count; i++) _juice.React(requests[i].Id, requests[i].Step);
        }

        /// <summary>
        /// Syncs the screen with the flow. <paramref name="time"/> is a clock in seconds (its differences drive
        /// the animations); the size is the render target in pixels and sets the UI scale.
        /// </summary>
        public void Present(PlayableFlow flow, WormSim sim, float time, float screenWidth, float screenHeight)
        {
            _canvas.scaleFactor = ScaleFor(screenWidth, screenHeight);
            float dt = _hasTime ? Mathf.Clamp(time - _lastTime, 0f, MaxFrameSeconds) : 0f;
            _lastTime = time;
            _hasTime = true;

            float uiHeight = screenHeight / _canvas.scaleFactor;
            _uiWidth = screenWidth / _canvas.scaleFactor;
            SetAnchor(ref _juice.Cta, 0f, -uiHeight * 0.5f + CtaBottom + CtaHeight * 0.5f, _cta);

            int length = Mathf.FloorToInt(sim.Player.Length * 10f);
            if (length != _lastLength)
            {
                _lastLength = length;
                _length.text = _config.LengthLabel + length;
            }

            bool end = flow.State == FlowState.EndCard;
            if (end && !_endShown) ShowEndCard(flow.EndKind);
            if (!end && _endShown)
            {
                _endCard.gameObject.SetActive(false);
                _juice.EndEndCard();
            }
            _endShown = end;

            bool hover = end && _pointerIsMouse &&
                         RectTransformUtility.RectangleContainsScreenPoint(_endButton.Root, _pointer, _camera);
            _juice.SetHover(hover);
            _juice.Step(dt, flow.CtaVisible);

            _length.rectTransform.localScale = Vector3.one * _juice.CounterScale;
            _length.color = Color.Lerp(Color.white, _config.HintColor, _juice.CounterGlow);

            _cta.Root.gameObject.SetActive(flow.CtaVisible);
            if (flow.CtaVisible)
            {
                _juice.CtaPose(out ButtonPose pose);
                ApplyPose(_cta, pose);
            }

            bool hintVisible = flow.BlockHintVisible;
            if (hintVisible && !_hintShown) _hintShownAt = time;
            _hintShown = hintVisible;
            _blockHint.gameObject.SetActive(hintVisible);
            if (hintVisible) _blockHint.rectTransform.localScale = Vector3.one * _juice.HintScale(time - _hintShownAt, time);

            bool tutorial = flow.TutorialVisible;
            if (tutorial && !_tutorialShown) _tutorialShownAt = time;
            _tutorialShown = tutorial;
            _tutorial.gameObject.SetActive(tutorial);
            if (tutorial) AnimateTutorial(flow.TutorialAlpha, time - _tutorialShownAt);

            if (end) AnimateEndCard();
            PresentRestart(time);
            PresentFx();
        }

        void PresentRestart(float time)
        {
            if (_restart == null) return;

            if (_restartSpinPending)
            {
                _restartSpinPending = false;
                _restartSpinAt = time;
            }

            float t = (time - _restartSpinAt) / RestartSpinSeconds;
            bool spinning = t >= 0f && t < 1f;
            float angle = spinning ? -RestartSpinDegrees * UiMotion.EaseOut(t) : 0f;
            _restartIcon.localRotation = Quaternion.Euler(0f, 0f, angle);
            _restart.localScale = Vector3.one * (spinning ? 1f - (1f - RestartPressScale) * UiMotion.Bump(t) : 1f);
        }

        void SetAnchor(ref UiAnchor anchor, float x, float y, PillButton button)
        {
            anchor.X = x;
            anchor.Y = y;
            anchor.HalfWidth = button.Width * 0.5f;
            anchor.HalfHeight = button.Height * 0.5f;
        }

        /// <summary>The finger's loop at <paramref name="seconds"/> since the hint appeared: trail, touch light, ripple, hand.</summary>
        void AnimateTutorial(float alpha, float seconds)
        {
            _tutorialGroup.alpha = alpha;
            _gesture.Sample(seconds, out HandPose pose);

            for (int i = 0; i < _tutorialTrail.Length; i++)
            {
                Image dot = _tutorialTrail[i];
                float x = 0f;
                float y = 0f;
                float strength = 0f;
                bool shown = pose.TrailFade > 0f && _gesture.TrailPoint(seconds, i, out x, out y, out strength);
                if (!shown)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }

                dot.rectTransform.anchoredPosition = new Vector2(x, y);
                dot.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(TutorialDotMin, TutorialDotMax, strength);
                dot.color = new Color(1f, 1f, 1f, TutorialTrailAlpha * strength * pose.TrailFade);
                if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
            }

            Vector2 tip = new Vector2(pose.X, pose.Y);
            _tutorialTouch.rectTransform.anchoredPosition = tip;
            _tutorialTouch.color = new Color(1f, 1f, 1f, TutorialTouchAlpha * pose.Press * pose.Alpha);

            bool ring = pose.Ring >= 0f;
            if (_tutorialRing.gameObject.activeSelf != ring) _tutorialRing.gameObject.SetActive(ring);
            if (ring)
            {
                _tutorialRing.rectTransform.anchoredPosition = tip;
                _tutorialRing.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(TutorialRingStart, TutorialRingEnd, UiMotion.EaseOut(pose.Ring));
                _tutorialRing.color = new Color(1f, 1f, 1f, 1f - pose.Ring);
            }

            // Hovering, the shadow sits far off; pressed, it tucks in under the finger.
            Vector2 shadowOffset = Vector2.Lerp(new Vector2(TutorialShadowUpX, TutorialShadowUpY),
                new Vector2(TutorialShadowDownX, TutorialShadowDownY), pose.Press);
            float lean = -pose.X / Mathf.Max(1f, _config.UiJuice.TutorialRadius) * TutorialTiltDegrees;
            Quaternion tilt = Quaternion.Euler(0f, 0f, lean);
            Vector3 scale = Vector3.one * pose.Scale;

            _tutorialHand.anchoredPosition = tip;
            _tutorialHand.localScale = scale;
            _tutorialHand.localRotation = tilt;
            _tutorialHandImage.color = new Color(1f, 1f, 1f, pose.Alpha);

            _tutorialShadow.anchoredPosition = tip + shadowOffset;
            _tutorialShadow.localScale = scale;
            _tutorialShadow.localRotation = tilt;
            _tutorialShadowImage.color = new Color(0f, 0f, 0f, TutorialShadowAlpha * pose.Alpha);
        }

        void ShowEndCard(EndKind kind)
        {
            _endWon = kind != EndKind.SoClose;
            string title;
            switch (kind)
            {
                case EndKind.Awesome:
                    title = _config.EndAwesomeTitle;
                    break;
                case EndKind.NiceMoves:
                    title = _config.EndNiceMovesTitle;
                    break;
                default:
                    title = _config.EndSoCloseTitle;
                    break;
            }

            LayOutTitle(title);
            _endSubtitle.text = _endWon ? _config.EndWinSubtitle : _config.EndLoseSubtitle;

            Color rays = _endWon ? _config.HintColor : _config.EndTitleShadowColor;
            rays.a = 0f;
            _rays.color = rays;

            _endCard.gameObject.SetActive(true);
            _endButton.Root.localScale = Vector3.zero;

            // The anchors the burst comes out of: where the title and the button sit on this layout.
            _juice.Title = new UiAnchor { X = 0f, Y = _titleCenterY, HalfWidth = _juice.Title.HalfWidth, HalfHeight = _titleHalfHeight };
            SetAnchor(ref _juice.EndButton, 0f, EndButtonY, _endButton);
            _juice.BeginEndCard(_endWon);
        }

        /// <summary>
        /// One Text per character, centred as a line; each remembers its resting x and y. The font shrinks from
        /// <see cref="EndTitleFontSize"/> until the line fits the screen width less the side margins; a title that
        /// would have to go below <see cref="EndTitleWrapBelowFontSize"/> breaks at its first space instead.
        /// </summary>
        void LayOutTitle(string title)
        {
            _letterCount = Mathf.Min(title.Length, _letters.Length);
            float maxWidth = Mathf.Max(1f, _uiWidth - EndSideMargin * 2f);

            int size = FitTitle(title, 0, _letterCount, maxWidth);
            int space = title.IndexOf(' ');
            bool wrap = size < EndTitleWrapBelowFontSize && space > 0 && space < _letterCount - 1;
            float halfWidth;
            if (wrap)
            {
                size = Mathf.Min(FitTitle(title, 0, space, maxWidth), FitTitle(title, space + 1, _letterCount, maxWidth));
                float lineHeight = size * TitleLineHeight;
                // The lower line sits where a one-line title would, so the subtitle below keeps its distance.
                float a = PlaceTitleLine(title, 0, space, size, lineHeight);
                float b = PlaceTitleLine(title, space + 1, _letterCount, size, 0f);
                _letterX[space] = 0f;
                _letterY[space] = 0f;
                halfWidth = Mathf.Max(a, b) * 0.5f;
                _titleCenterY = EndTitleY + lineHeight * 0.5f;
                _titleHalfHeight = lineHeight * 1.1f;
            }
            else
            {
                halfWidth = PlaceTitleLine(title, 0, _letterCount, size, 0f) * 0.5f;
                _titleCenterY = EndTitleY;
                _titleHalfHeight = size * 0.6f;
            }

            for (int i = 0; i < _letters.Length; i++)
            {
                bool used = i < _letterCount;
                _letters[i].gameObject.SetActive(used);
                if (!used) continue;
                _letters[i].fontSize = size;
                _letters[i].text = title[i] == ' ' ? string.Empty : title[i].ToString();
            }

            _juice.Title = new UiAnchor { HalfWidth = halfWidth };
        }

        /// <summary>The largest font size, at most <see cref="EndTitleFontSize"/>, at which these characters fit the width.</summary>
        int FitTitle(string title, int from, int to, float maxWidth)
        {
            float total = MeasureTitle(title, from, to, EndTitleFontSize);
            if (total <= maxWidth) return EndTitleFontSize;
            return Mathf.Max(EndTitleMinFontSize, Mathf.FloorToInt(EndTitleFontSize * maxWidth / total));
        }

        /// <summary>Centres one line of the title at height <paramref name="y"/>; returns its width.</summary>
        float PlaceTitleLine(string title, int from, int to, int size, float y)
        {
            float total = MeasureTitle(title, from, to, size);
            float x = -total * 0.5f;
            for (int i = from; i < to; i++)
            {
                float advance = _letterX[i];
                _letterX[i] = x + advance * 0.5f;
                _letterY[i] = y;
                x += advance;
            }

            return total;
        }

        /// <summary>Width of title[from..to) at this font size; leaves each character's advance in <c>_letterX</c>.</summary>
        float MeasureTitle(string title, int from, int to, int size)
        {
            _kit.Font.RequestCharactersInTexture(title, size, FontStyle.Bold);
            float total = 0f;
            for (int i = from; i < to; i++)
            {
                float advance = _kit.Font.GetCharacterInfo(title[i], out CharacterInfo info, size, FontStyle.Bold)
                    ? info.advance
                    : size * FallbackAdvance;
                _letterX[i] = advance;
                total += advance;
            }

            return total;
        }

        /// <summary>The end card, a pose at a time: veil, rays, letters, subtitle, button, flash.</summary>
        void AnimateEndCard()
        {
            Color dim = _config.EndDimColor;
            dim.a *= _juice.DimAmount;
            _dim.color = dim;

            _juice.RaysPose(out float raysScale, out float raysRotation, out float raysAlpha);
            Color raysColor = _rays.color;
            raysColor.a = raysAlpha;
            _rays.color = raysColor;
            _rays.rectTransform.localScale = Vector3.one * raysScale;
            _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, raysRotation);

            for (int i = 0; i < _letterCount; i++)
            {
                _juice.LetterPose(i, out LetterPose pose);
                RectTransform rect = _letters[i].rectTransform;
                rect.anchoredPosition = new Vector2(_letterX[i], EndTitleY + _letterY[i] + pose.Y);
                rect.localRotation = Quaternion.Euler(0f, 0f, pose.Rotation);
                rect.localScale = Vector3.one * pose.Scale;
                Color color = Color.Lerp(Color.white, _config.HintColor, pose.Gold);
                color.a = pose.Alpha;
                _letters[i].color = color;
            }

            _juice.SubtitlePose(out float subtitleAlpha, out float subtitleRise);
            Color subtitle = _endSubtitle.color;
            subtitle.a = EndSubtitleAlpha * subtitleAlpha;
            _endSubtitle.color = subtitle;
            _endSubtitle.rectTransform.anchoredPosition = new Vector2(0f, EndSubtitleY - subtitleRise);

            _juice.EndButtonPose(out ButtonPose button);
            ApplyPose(_endButton, button);

            Color flash = _flash.color;
            flash.a = _juice.FlashAlpha;
            _flash.color = flash;
        }

        /// <summary>Squash and stretch, wobble, halo and the sweeping beam of a button.</summary>
        static void ApplyPose(PillButton button, in ButtonPose pose)
        {
            button.Root.localScale = new Vector3(pose.ScaleX, pose.ScaleY, 1f);
            button.Root.localRotation = Quaternion.Euler(0f, 0f, pose.Rotation);

            Color glow = button.Glow.color;
            glow.a = pose.GlowAlpha;
            button.Glow.color = glow;

            bool shining = pose.Shine >= 0f;
            if (button.Shine.gameObject.activeSelf != shining) button.Shine.gameObject.SetActive(shining);
            if (shining)
            {
                float travel = button.Width * (0.5f + ShineTravelPad);
                button.Shine.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, pose.Shine), 0f);
            }
        }

        void PresentFx()
        {
            UiFxPool pool = _juice.Fx;
            for (int i = 0; i < _fxImages.Length; i++)
            {
                Image image = _fxImages[i];
                if (i >= pool.Count)
                {
                    if (image.gameObject.activeSelf) image.gameObject.SetActive(false);
                    continue;
                }

                ref UiPiece piece = ref pool.Items[i];
                UiFxPool.Look(piece, out float width, out float height, out float alpha);

                image.sprite = SpriteFor(piece.Kind);
                Color color = ColorFor(piece.ColorSlot);
                color.a *= alpha;
                image.color = color;

                RectTransform rect = _fxRects[i];
                rect.anchoredPosition = new Vector2(piece.X, piece.Y);
                rect.localRotation = Quaternion.Euler(0f, 0f, piece.Rotation);
                rect.localScale = new Vector3(width / FxNativeSize, height / FxNativeSize, 1f);
                if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            }
        }

        Sprite SpriteFor(UiFxKind kind)
        {
            switch (kind)
            {
                case UiFxKind.Star:
                    return _kit.Star;
                case UiFxKind.Ring:
                    return _kit.Ring;
                case UiFxKind.Dot:
                    return _kit.Circle;
                default:
                    return _kit.Square;
            }
        }

        Color ColorFor(int slot)
        {
            if (slot == UiFxPool.ColorWhite) return Color.white;
            if (slot == UiFxPool.ColorGold) return _config.HintColor;
            return _config.PrimaryColor(slot);
        }

        Vector2 ScreenToUi(Vector2 screenPoint, float screenWidth, float screenHeight)
        {
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            return new Vector2(screenPoint.x - screenWidth * 0.5f, screenPoint.y - screenHeight * 0.5f) / scale;
        }

        /// <summary>UI units to pixels for a render target of this size.</summary>
        public static float ScaleFor(float screenWidth, float screenHeight)
        {
            return ViewMath.UiScale(screenWidth, screenHeight, ReferenceWidth, ReferenceHeight, MatchWidthOrHeight);
        }

        /// <summary>
        /// Puts the juice popups over the world, at the world position they were spawned at (after the camera has
        /// moved this frame). Call after <see cref="Present"/>, which sets the UI scale.
        /// </summary>
        public void PresentPopups(JuiceState juice, float screenWidth, float screenHeight)
        {
            PopupPool pool = juice.Popups;
            float uiScale = _canvas.scaleFactor;
            float pixelsPerWorld = screenHeight / (2f * _camera.orthographicSize);
            int shown = Mathf.Min(pool.Count, _popups.Length);

            for (int i = 0; i < _popups.Length; i++)
            {
                Text text = _popups[i];
                if (i >= shown)
                {
                    if (text.gameObject.activeSelf) text.gameObject.SetActive(false);
                    continue;
                }

                ref Popup popup = ref pool.Items[i];
                int key = (int)popup.Kind * 100000 + popup.Value + 1;
                if (_popupKeys[i] != key)
                {
                    _popupKeys[i] = key;
                    text.text = popup.Kind == PopupKind.Kill
                        ? _config.KillPopupText
                        : string.Format(_config.ValuePopupFormat, popup.Value);
                }

                Color color = popup.Kind == PopupKind.Kill ? _config.KillPopupColor : _config.ValuePopupColor;
                color.a *= pool.Alpha(popup);
                text.color = color;

                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(popup.X, popup.Y, 0f));
                Vector2 offset = new Vector2(screen.x - screenWidth * 0.5f, screen.y - screenHeight * 0.5f) / uiScale;
                text.rectTransform.anchoredPosition = offset;

                float pixelSize = _config.Juice.PopupFontSize * popup.Scale * pixelsPerWorld;
                text.rectTransform.localScale = Vector3.one * (pixelSize / (PopupRasterFontSize * uiScale));
                if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
            }
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            _kit.Dispose();
        }
    }
}
