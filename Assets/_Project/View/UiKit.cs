using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WormRush.View
{
    /// <summary>A button-shaped group: edge (the "3D" lip under the face), face with a vertical gradient, label.</summary>
    public sealed class PillButton
    {
        public RectTransform Root;
        public Text Label;
    }

    /// <summary>
    /// Builds uGUI pieces from code so no scene or prefab wiring is needed: the built-in font, two procedural
    /// sprites (circle and a 9-sliced pill), text with outline, gradient buttons. Owns what it creates.
    /// </summary>
    public sealed class UiKit : IDisposable
    {
        const int SpriteSize = 64;
        const int PillBorder = 31;        // pill corner radius in texels: half of SpriteSize minus the 1 texel margin
        const float PixelsPerUnit = 100f; // equals CanvasScaler's default reference pixels per unit

        public readonly Font Font;
        public readonly Sprite Circle;
        public readonly Sprite Pill;

        readonly List<Object> _owned = new List<Object>();

        public UiKit()
        {
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            float half = SpriteSize * 0.5f - 1f;
            Circle = MakeSprite((x, y) => half + 0.5f - Mathf.Sqrt(x * x + y * y), Vector4.zero);
            Pill = MakeSprite((x, y) => 0.5f - (Mathf.Sqrt(x * x + y * y) - PillBorder),
                new Vector4(PillBorder, PillBorder, PillBorder, PillBorder));
        }

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i] != null) Object.Destroy(_owned[i]);
            _owned.Clear();
        }

        /// <summary>coverage(dx, dy) is the pixel's alpha (0..1, clamped) at an offset from the texture centre.</summary>
        Sprite MakeSprite(Func<float, float, float> coverage, Vector4 border)
        {
            Texture2D texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                name = "WormRush.UiSprite",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Color32[] pixels = new Color32[SpriteSize * SpriteSize];
            float centre = (SpriteSize - 1) * 0.5f;
            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    float a = Mathf.Clamp01(coverage(x - centre, y - centre));
                    pixels[y * SpriteSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f),
                PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = "WormRush.UiSprite";
            _owned.Add(texture);
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
        /// A pill with a top-to-bottom gradient and a darker lip <paramref name="lip"/> units below it.
        /// Width follows the label; <paramref name="padX"/> is the space on each side.
        /// </summary>
        public PillButton NewPillButton(string name, Transform parent, string label, int fontSize, float padX, float height,
            Color top, Color bottom, Color edge, Color textColor, float lip)
        {
            RectTransform root = NewRect(name, parent);
            Text text = NewText("Label", root, label, fontSize, textColor);
            float width = Mathf.Ceil(text.preferredWidth) + padX * 2f;
            root.sizeDelta = new Vector2(width, height);

            // Children are created in draw order: lip, face, label.
            Image lipImage = NewImage("Lip", root, Pill, edge);
            lipImage.rectTransform.SetSiblingIndex(0);
            Place(lipImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -lip), new Vector2(width, height));

            Image face = NewImage("Face", root, Pill, Color.white);
            face.rectTransform.SetSiblingIndex(1);
            Stretch(face.rectTransform);
            VerticalGradient gradient = face.gameObject.AddComponent<VerticalGradient>();
            gradient.Set(top, bottom);

            Stretch(text.rectTransform);

            return new PillButton { Root = root, Label = text };
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
