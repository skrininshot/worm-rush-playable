using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WormRush.View
{
    /// <summary>
    /// A button-shaped group: a halo behind it, the edge (the "3D" lip under the face), the face with a vertical
    /// gradient and a beam of light that can sweep across it, the label.
    /// </summary>
    public sealed class PillButton
    {
        public RectTransform Root;
        public Text Label;
        public Image Glow;
        public Image Face;
        public RectTransform Shine;
        public float Width;
        public float Height;
    }

    /// <summary>
    /// Builds uGUI pieces from code so no scene or prefab wiring is needed: the built-in font, one procedural
    /// sprite atlas (circle, 9-sliced pill, square, four-point star, ring, soft glow, light beam, sunburst), text
    /// with outline, gradient buttons. All images share the atlas, so they batch together. Owns what it creates.
    /// </summary>
    public sealed class UiKit : IDisposable
    {
        const int AtlasSize = 512;
        const int CellSize = 64;
        const int RaysCellSize = 256;
        const int PillBorder = 31;              // pill corner radius in texels: half of CellSize minus the 1 texel margin
        const float PixelsPerUnit = 100f;       // equals CanvasScaler's default reference pixels per unit
        const float RingThickness = 3.5f;       // texels
        const float StarCoreGain = 2.4f;
        const float GlowPower = 2f;
        const float BeamPower = 1.6f;
        const float SquareInset = 2f;           // texels of margin so bilinear sampling never reads a neighbour cell
        const int RayCount = 12;
        const float RayContrast = 1.4f;       // low = soft wedge edges
        const float RayFadePower = 0.8f;
        const float GlowWidthFactor = 1.7f;     // halo size relative to the button
        const float GlowHeightFactor = 3.2f;
        const float BeamWidthFactor = 0.22f;    // light beam relative to the face
        const float BeamHeightFactor = 2.2f;
        const float BeamTiltDegrees = 20f;
        const float GlossAlpha = 0.2f;          // lighter top half of a button face

        // The pointing hand: its own cell, painted in colour (white skin, dark outline), tip position in texels from the cell centre.
        const int HandCellSize = 128;
        const int HandCellX = 256;
        const float HandOutline = 3.5f;
        const float HandBlend = 8f;             // smooth-union radius between finger, fist and thumb
        public const float HandTipX = -6f;
        public const float HandTipY = 45.5f + HandOutline;
        public const float HandCell = HandCellSize;
        static readonly Color32 HandOutlineColor = new Color32(0x1c, 0x20, 0x40, 0xff);

        public readonly Font Font;
        public readonly Sprite Circle;
        public readonly Sprite Pill;
        public readonly Sprite Square;
        public readonly Sprite Star;
        public readonly Sprite Ring;
        public readonly Sprite Glow;
        public readonly Sprite Beam;
        public readonly Sprite Rays;
        public readonly Sprite Hand;

        readonly List<Object> _owned = new List<Object>();

        public UiKit()
        {
            Font = LoadFont();

            Texture2D atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false)
            {
                name = "WormRush.UiAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            Color32[] pixels = new Color32[AtlasSize * AtlasSize];

            float half = CellSize * 0.5f - 1f;
            Rect pill = Paint(pixels, 0, 0, CellSize, (x, y) => 0.5f - (Mathf.Sqrt(x * x + y * y) - PillBorder));
            Rect circle = Paint(pixels, CellSize, 0, CellSize, (x, y) => half + 0.5f - Mathf.Sqrt(x * x + y * y));
            Rect star = Paint(pixels, CellSize * 2, 0, CellSize, (x, y) =>
            {
                float d = Mathf.Sqrt(Mathf.Abs(x) / half) + Mathf.Sqrt(Mathf.Abs(y) / half);
                return (1f - d) * StarCoreGain;
            });
            Rect ring = Paint(pixels, CellSize * 3, 0, CellSize, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                return Mathf.Min(r - (half - RingThickness), half + 0.5f - r);
            });
            Rect glow = Paint(pixels, 0, CellSize, CellSize, (x, y) =>
                Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) / half), GlowPower));
            Rect beam = Paint(pixels, CellSize, CellSize, CellSize, (x, y) =>
                Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x) / half), BeamPower) * Mathf.Clamp01(half + 0.5f - Mathf.Abs(y)));
            Rect square = Paint(pixels, CellSize * 2, CellSize, CellSize, (x, y) =>
                Mathf.Min(half + 1f - SquareInset + 0.5f - Mathf.Abs(x), half + 1f - SquareInset + 0.5f - Mathf.Abs(y)));
            float raysHalf = RaysCellSize * 0.5f - 1f;
            Rect rays = Paint(pixels, 0, CellSize * 2, RaysCellSize, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                float lobe = Mathf.Clamp01(Mathf.Sin(Mathf.Atan2(y, x) * RayCount) * RayContrast);
                return lobe * Mathf.Pow(Mathf.Clamp01(1f - r / raysHalf), RayFadePower);
            });

            Rect hand = PaintHand(pixels, HandCellX, 0);

            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            _owned.Add(atlas);

            Vector4 pillBorder = new Vector4(PillBorder, PillBorder, PillBorder, PillBorder);
            Pill = MakeSprite(atlas, pill, pillBorder);
            Circle = MakeSprite(atlas, circle, Vector4.zero);
            Star = MakeSprite(atlas, star, Vector4.zero);
            Ring = MakeSprite(atlas, ring, Vector4.zero);
            Glow = MakeSprite(atlas, glow, Vector4.zero);
            Beam = MakeSprite(atlas, beam, Vector4.zero);
            Square = MakeSprite(atlas, square, Vector4.zero);
            Rays = MakeSprite(atlas, rays, Vector4.zero);
            Hand = MakeSprite(atlas, hand, Vector4.zero);
        }

        static Font LoadFont()
        {
            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (ArgumentException)
            {
                // Unity 6 ships LegacyRuntime.ttf; an OS font keeps the editor usable if that ever changes.
                return Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i] != null) Object.Destroy(_owned[i]);
            _owned.Clear();
        }

        /// <summary>
        /// Paints one square cell of the atlas. coverage(dx, dy) is the pixel's alpha (clamped to 0..1) at an
        /// offset from the cell centre; the pixels are white so an Image's colour tints them.
        /// </summary>
        static Rect Paint(Color32[] pixels, int originX, int originY, int size, Func<float, float, float> coverage)
        {
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(coverage(x - centre, y - centre));
                    pixels[(originY + y) * AtlasSize + originX + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            return new Rect(originX, originY, size, size);
        }

        /// <summary>
        /// A hand with the index finger raised (a pointing cursor), painted as layers back to front: finger and palm
        /// as one smooth shape, three curled fingers, the thumb. Every layer has its own dark outline, so the
        /// fingers read as separate. Signed distances keep the edges anti-aliased and the outline an exact offset.
        /// Transparent outside, but dark, so bilinear sampling leaves no pale halo.
        /// </summary>
        static Rect PaintHand(Color32[] pixels, int originX, int originY)
        {
            float centre = (HandCellSize - 1) * 0.5f;
            for (int y = 0; y < HandCellSize; y++)
            {
                for (int x = 0; x < HandCellSize; x++)
                {
                    float px = x - centre;
                    float py = y - centre;

                    // Premultiplied accumulation of (outline, skin) pairs, back to front.
                    float red = 0f;
                    float green = 0f;
                    float blue = 0f;
                    float alpha = 0f;
                    for (int layer = 0; layer < HandLayers; layer++)
                    {
                        float d = HandLayerDistance(layer, px, py);
                        Over(ref red, ref green, ref blue, ref alpha, HandOutlineColor.r / 255f, HandOutlineColor.g / 255f,
                            HandOutlineColor.b / 255f, Mathf.Clamp01(0.5f - (d - HandOutline)));
                        Over(ref red, ref green, ref blue, ref alpha, 1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                    }

                    float inverse = alpha > 1e-4f ? 1f / alpha : 0f;
                    Color32 outline = HandOutlineColor;
                    byte r = alpha > 1e-4f ? (byte)Mathf.RoundToInt(red * inverse * 255f) : outline.r;
                    byte g = alpha > 1e-4f ? (byte)Mathf.RoundToInt(green * inverse * 255f) : outline.g;
                    byte b = alpha > 1e-4f ? (byte)Mathf.RoundToInt(blue * inverse * 255f) : outline.b;
                    pixels[(originY + y) * AtlasSize + originX + x] = new Color32(r, g, b, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            return new Rect(originX, originY, HandCellSize, HandCellSize);
        }

        static void Over(ref float red, ref float green, ref float blue, ref float alpha, float r, float g, float b, float a)
        {
            red = red * (1f - a) + r * a;
            green = green * (1f - a) + g * a;
            blue = blue * (1f - a) + b * a;
            alpha = alpha * (1f - a) + a;
        }

        const int HandLayers = 5;

        static float HandLayerDistance(int layer, float x, float y)
        {
            switch (layer)
            {
                case 0:
                {
                    float finger = CapsuleDistance(x, y, HandTipX, -4f, HandTipX, HandTipY - HandOutline - 9.5f, 9.5f);
                    float palm = BoxDistance(x, y, 4f, -24f, 27f, 24f, 13f);
                    return SmoothMin(finger, palm, HandBlend);
                }
                case 1:
                    return CircleDistance(x, y, 31f, -9f, 7.5f);
                case 2:
                    return CircleDistance(x, y, 22f, -4f, 8f);
                case 3:
                    return CircleDistance(x, y, 12.5f, -1f, 8f);
                default:
                    return CapsuleDistance(x, y, -25f, -11f, -14f, -22f, 7.5f);
            }
        }

        static float CircleDistance(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx;
            float dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float CapsuleDistance(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax;
            float pay = y - ay;
            float bax = bx - ax;
            float bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h;
            float dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float BoxDistance(float x, float y, float cx, float cy, float halfX, float halfY, float r)
        {
            float qx = Mathf.Abs(x - cx) - (halfX - r);
            float qy = Mathf.Abs(y - cy) - (halfY - r);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }

        Sprite MakeSprite(Texture2D atlas, Rect rect, Vector4 border)
        {
            Sprite sprite = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = "WormRush.UiSprite";
            _owned.Add(sprite);
            return sprite;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Anchors a rect to one point of its parent (0..1 on both axes), pivot in the middle.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public Text NewText(string name, Transform parent, string value, int size, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        public static void AddOutline(Text text, Color color, float distance)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        public static void AddShadow(Text text, Color color, Vector2 offset)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = offset;
        }

        /// <summary>
        /// A pill with a top-to-bottom gradient and a darker lip <paramref name="lip"/> units below it, a halo
        /// (<paramref name="glow"/>, invisible until a pose lights it) and a beam of light clipped to the face.
        /// Width follows the label; <paramref name="padX"/> is the space on each side.
        /// </summary>
        public PillButton NewPillButton(string name, Transform parent, string label, int fontSize, float padX, float height,
            Color top, Color bottom, Color edge, Color textColor, float lip, Color glow, Color shine)
        {
            RectTransform root = NewRect(name, parent);
            Text text = NewText("Label", root, label, fontSize, textColor);
            float width = Mathf.Ceil(text.preferredWidth) + padX * 2f;
            root.sizeDelta = new Vector2(width, height);

            // Children are created in draw order: halo, lip, face (with the beam inside it), label.
            Image halo = NewImage("Glow", root, Glow, new Color(glow.r, glow.g, glow.b, 0f));
            Place(halo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(width * GlowWidthFactor, height * GlowHeightFactor));
            halo.rectTransform.SetSiblingIndex(0);

            Image lipImage = NewImage("Lip", root, Pill, edge);
            lipImage.rectTransform.SetSiblingIndex(1);
            Place(lipImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -lip), new Vector2(width, height));

            Image face = NewImage("Face", root, Pill, Color.white);
            face.rectTransform.SetSiblingIndex(2);
            Stretch(face.rectTransform);
            VerticalGradient gradient = face.gameObject.AddComponent<VerticalGradient>();
            gradient.Set(top, bottom);

            // The beam only shows inside the face's shape (stencil mask from the face's own alpha).
            Mask mask = face.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Gloss: the top half a little lighter, clipped to the pill by the same mask.
            Image gloss = NewImage("Gloss", face.rectTransform, Square, new Color(1f, 1f, 1f, GlossAlpha));
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = Vector2.zero;
            gloss.rectTransform.offsetMax = Vector2.zero;
            Image beam = NewImage("Shine", face.rectTransform, Beam, shine);
            Place(beam.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(width * BeamWidthFactor, height * BeamHeightFactor));
            beam.rectTransform.localRotation = Quaternion.Euler(0f, 0f, BeamTiltDegrees);
            beam.gameObject.SetActive(false);

            Stretch(text.rectTransform);

            return new PillButton
            {
                Root = root,
                Label = text,
                Glow = halo,
                Face = face,
                Shine = beam.rectTransform,
                Width = width,
                Height = height,
            };
        }
    }

    /// <summary>Tints a graphic's vertices from <c>Top</c> to <c>Bottom</c>, multiplied with its own colour.</summary>
    public sealed class VerticalGradient : BaseMeshEffect
    {
        Color _top = Color.white;
        Color _bottom = Color.white;

        public void Set(Color top, Color bottom)
        {
            _top = top;
            _bottom = bottom;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            Rect rect = ((RectTransform)transform).rect;
            UIVertex vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = rect.height > 0f ? Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y) : 1f;
                vertex.color = (Color32)((Color)vertex.color * Color.Lerp(_bottom, _top, t));
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
