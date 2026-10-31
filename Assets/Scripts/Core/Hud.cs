using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MayaAdventure
{
    /// <summary>
    /// HUD and menus, written immediate-mode style (Text / Panel / Fill each frame) but drawn with pooled uGUI
    /// elements and TextMeshPro SDF text: sharp at every size, one canvas, far cheaper on phones than IMGUI.
    /// All strings are logical Hebrew, converted by <see cref="Rtl"/>.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        GUIStyle style, panel;
        FontStyle textStyle = FontStyle.Bold;
        string message, title, hint;
        float messageUntil, titleUntil, hintUntil;
        float playStartTime = -1f;
        float fade, fadeTarget, fadeSpeed = 2f;
        Texture2D starIcon, coinIcon, redCoinIcon, heartIcon, meter, gradient;
        int meterHealth = -1;

        public void Show(string text, float seconds = 2.5f)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        string caption, captionSub;
        float captionFrom, captionUntil;

        /// <summary>Big trailer-style caption near the bottom (title + optional subtitle).</summary>
        public void Caption(string text, float seconds, string sub = null)
        {
            caption = text;
            captionSub = sub;
            captionFrom = Time.unscaledTime;
            captionUntil = Time.unscaledTime + seconds;
        }

        void DrawCaption(float w, float h)
        {
            if (caption == null || Time.unscaledTime > captionUntil) return;
            float age = Time.unscaledTime - captionFrom, left = captionUntil - Time.unscaledTime;
            float a = Mathf.Clamp01(age * 4f) * Mathf.Clamp01(left * 3f);
            float slide = (1f - Mathf.Clamp01(age * 4f)) * 40f;
            Fill(new Rect(0, h - 250f, w, 250f), new Color(0.02f, 0.03f, 0.1f, 0.35f * a));
            Text(new Rect(0, h - 230f + slide, w, 120f), caption, 92, new Color(1f, 0.86f, 0.25f, a), TextAnchor.MiddleCenter, 5);
            if (!string.IsNullOrEmpty(captionSub))
                Text(new Rect(0, h - 120f + slide, w, 60f), captionSub, 40, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, 3);
        }

        /// <summary>Big level-name banner.</summary>
        public void ShowTitle(string text, float seconds)
        {
            title = text;
            titleUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Small hint at the bottom; call every frame while it should stay visible.</summary>
        string speechWho, speechText, missionText;
        float speechUntil, missionUntil;

        /// <summary>A character talking to Maya (call every frame while it talks).</summary>
        public void Speech(string who, string text)
        {
            speechWho = who;
            speechText = text;
            speechUntil = Time.unscaledTime + 0.15f;
        }

        /// <summary>Announces the world's next star ("mission") in a banner for a few seconds.</summary>
        public void Mission(string title, float seconds)
        {
            missionText = title;
            missionUntil = Time.unscaledTime + seconds;
        }

        void DrawSpeech(float w, float h)
        {
            if (speechText == null || Time.unscaledTime > speechUntil || ScriptedInput.Active) return;
            const float boxWidth = 1180f, lineHeight = 50f;
            var lines = Wrap(speechText, 38, boxWidth - 80f);
            float boxHeight = 84f + lines.Count * lineHeight;
            float top = h - 120f - boxHeight;
            float left = w * 0.5f - boxWidth * 0.5f;
            Panel(new Rect(left, top, boxWidth, boxHeight), 0.96f);
            Text(new Rect(left + 30f, top + 10f, boxWidth - 60f, 52f), speechWho, 36, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleRight, 3);
            for (int i = 0; i < lines.Count; i++)
                Text(new Rect(left, top + 64f + i * lineHeight, boxWidth, lineHeight), lines[i], 38, Color.white, TextAnchor.MiddleCenter, 2);
        }

        void DrawMission(float w)
        {
            if (missionText == null || Time.unscaledTime > missionUntil || ScriptedInput.Active) return;
            float a = Mathf.Clamp01((missionUntil - Time.unscaledTime) * 2f) * Mathf.Clamp01((Time.unscaledTime - (missionUntil - 6f)) * 3f);
            // Under the world's name while that is still showing.
            bool nameShowing = title != null && Time.unscaledTime < titleUntil;
            var r = new Rect(w * 0.5f - 520f, nameShowing ? 305f : 170f, 1040f, 120f);
            PanelTinted(r, new Color(1f, 1f, 1f, 0.95f * a));
            Text(new Rect(r.x, r.y + 8f, r.width, 46f), "המשימה הבאה", 32, new Color(1f, 0.85f, 0.35f, a), TextAnchor.MiddleCenter, 2);
            Text(new Rect(r.x, r.y + 52f, r.width, 60f), missionText, 46, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, 3);
        }

        public void Hint(string text)
        {
            hint = text;
            hintUntil = Time.unscaledTime + 0.15f;
        }

        public void Fade(float target, float seconds)
        {
            fadeTarget = target;
            fadeSpeed = 1f / Mathf.Max(0.05f, seconds);
        }

        public void OnGameStarted() => playStartTime = Time.unscaledTime;


        // ---------- textures ----------

        void EnsureStyle()
        {
            if (style != null) return;
            style = new GUIStyle();
            var round = RoundRect(64, 18);
            panelSprite = Sprite.Create(round, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
            buttonTex = ButtonTexture();
            buttonSprite = Sprite.Create(buttonTex, new Rect(0, 0, 128, 96), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(34, 34, 34, 34));
            playIcon = Icon(64, (x, y) => PlayMask(x, y), Color.white, new Color(0.85f, 0.9f, 1f));
            gearIcon = Icon(64, (x, y) => GearMask(x, y), Color.white, new Color(0.85f, 0.9f, 1f));
            padIcon = Icon(64, (x, y) => PadMask(x, y), Color.white, new Color(0.85f, 0.9f, 1f));
            BuildCanvas();
            starIcon = Icon(64, (x, y) => StarMask(x, y), new Color(1f, 0.85f, 0.2f), new Color(0.85f, 0.5f, 0.05f));
            coinIcon = Icon(64, (x, y) => CoinMask(x, y), new Color(1f, 0.82f, 0.25f), new Color(0.8f, 0.5f, 0.05f));
            redCoinIcon = Icon(64, (x, y) => CoinMask(x, y), new Color(1f, 0.35f, 0.3f), new Color(0.6f, 0.08f, 0.08f));
            heartIcon = Icon(64, (x, y) => HeartMask(x, y), new Color(1f, 0.4f, 0.55f), new Color(0.7f, 0.12f, 0.25f));
            gradient = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++)
            {
                float t = i / 63f;
                gradient.SetPixel(0, i, Color.Lerp(new Color(0.02f, 0.04f, 0.12f, 0.75f), new Color(0.05f, 0.15f, 0.35f, 0.15f), t));
            }
            gradient.Apply();
        }

        static Texture2D RoundRect(int size, float radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d);
                float edge = Mathf.Clamp01(radius - d - 2.5f);
                var c = Color.Lerp(new Color(1f, 1f, 1f, 0.55f), new Color(0.05f, 0.08f, 0.2f, 0.55f), edge);
                c.a *= a;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        delegate float Mask(float x, float y);

        static Texture2D Icon(int n, Mask mask, Color fill, Color rim)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float m = mask(u, v);                // signed: > 0 inside
                float a = Mathf.Clamp01(m * n * 0.5f);
                float inner = Mathf.Clamp01((m - 0.12f) * n * 0.5f);
                var c = Color.Lerp(rim, fill, inner);
                c = Color.Lerp(c, Color.white, Mathf.Clamp01(v * 0.5f - u * 0.2f) * inner * 0.45f); // shine
                c.a = a;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        static float StarMask(float x, float y)
        {
            // Five sharp points: the edge radius falls linearly from each tip to the notch between tips.
            float a = Mathf.Atan2(x, y);
            float r = Mathf.Sqrt(x * x + y * y);
            float seg = Mathf.Repeat(a / (Mathf.PI * 2f / 5f), 1f);
            float tip = 1f - Mathf.Abs(seg - 0.5f) * 2f; // 0 at a tip... flip so 1 = tip
            tip = 1f - tip;
            float edge = Mathf.Lerp(0.4f, 0.97f, tip * tip);
            return (edge - r) * 0.8f;
        }

        static float CoinMask(float x, float y)
        {
            float r = Mathf.Sqrt(x * x / 0.7f + y * y);
            return 0.92f - r - (Mathf.Abs(r - 0.6f) < 0.06f ? 0.11f : 0f);
        }

        static float HeartMask(float x, float y)
        {
            y = -y * 1.1f + 0.15f;
            x *= 1.15f;
            float a = x * x + y * y - 0.5f;
            return -(a * a * a - x * x * y * y * y) * 4f;
        }

        Texture2D Meter(int health)
        {
            if (meter != null && meterHealth == health) return meter;
            meterHealth = health;
            const int n = 96;
            if (meter == null) meter = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color on = health > 5 ? new Color(0.3f, 0.85f, 1f) : health > 2 ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Repeat(Mathf.Atan2(u, v), Mathf.PI * 2f);
                int seg = Mathf.FloorToInt(ang / (Mathf.PI * 2f) * GameManager.MaxHealth);
                float segPos = Mathf.Repeat(ang / (Mathf.PI * 2f) * GameManager.MaxHealth, 1f);
                Color c;
                if (r > 0.98f) c = Color.clear;
                else if (r > 0.9f) c = new Color(1f, 1f, 1f, 0.95f);
                else if (r < 0.32f) c = new Color(1f, 1f, 1f, 0.95f);
                else if (segPos < 0.05f || segPos > 0.95f) c = new Color(0.1f, 0.12f, 0.2f, 0.9f);
                else c = seg < health ? Color.Lerp(on, Color.white, (1f - r) * 0.35f) : new Color(0.15f, 0.18f, 0.28f, 0.75f);
                c.a *= Mathf.Clamp01((0.98f - r) * n * 0.5f);
                meter.SetPixel(x, y, c);
            }
            meter.Apply();
            return meter;
        }

        // ---------- drawing ----------

        // ---------- retained drawing: pooled uGUI elements reused in call order every frame ----------

        Canvas canvas;
        RectTransform layer;
        Sprite panelSprite;
        TMP_FontAsset tmpFont;
        Material textMaterial;
        TextMeshProUGUI measure;
        readonly List<Image> images = new List<Image>();
        readonly List<RawImage> raws = new List<RawImage>();
        readonly List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();
        int usedImages, usedRaws, usedTexts, order;

        void BuildCanvas()
        {
            var go = new GameObject("HUD Canvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // 1080 units tall, like the old layout
            layer = (RectTransform)new GameObject("Layer", typeof(RectTransform)).transform;
            layer.SetParent(go.transform, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;

            tmpFont = GameFont.Tmp();
            if (tmpFont != null)
            {
                // One shared material: dark outline + soft drop shadow, like the old 8-way outline but in one draw.
                textMaterial = new Material(tmpFont.material);
                textMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
                textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                textMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.03f, 0.05f, 0.14f, 0.92f));
                textMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
                textMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                textMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0.05f, 0.45f));
                textMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                textMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            }
            measure = NewText();
            measure.gameObject.SetActive(false);
        }

        TextMeshProUGUI NewText()
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(layer, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (tmpFont != null) t.font = tmpFont;
            if (textMaterial != null) t.fontSharedMaterial = textMaterial;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = false;
            t.isRightToLeftText = false; // strings arrive already in visual order (Rtl.Visual)
            // No kerning / mark positioning: those tables expect logical order and misplace letters in reversed text.
            t.fontFeatures = new List<UnityEngine.TextCore.OTL_FeatureTag>();
            return t;
        }

        void Place(RectTransform rt, Rect r)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = new Vector2(r.width, r.height);
            if (rt.GetSiblingIndex() != order) rt.SetSiblingIndex(order);
            order++;
        }

        void BeginFrame()
        {
            usedImages = usedRaws = usedTexts = order = 0;
        }

        void EndFrame()
        {
            for (int i = usedImages; i < images.Count; i++) if (images[i].enabled) images[i].enabled = false;
            for (int i = usedRaws; i < raws.Count; i++) if (raws[i].enabled) raws[i].enabled = false;
            for (int i = usedTexts; i < texts.Count; i++) if (texts[i].enabled) texts[i].enabled = false;
        }

        Image NextImage()
        {
            if (usedImages == images.Count)
            {
                var go = new GameObject("Image", typeof(RectTransform));
                go.transform.SetParent(layer, false);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                images.Add(img);
            }
            var image = images[usedImages++];
            image.enabled = true;
            return image;
        }

        void Text(Rect r, string s, int size, Color c, TextAnchor anchor, int outline = 3)
        {
            if (usedTexts == texts.Count) texts.Add(NewText());
            var t = texts[usedTexts++];
            t.enabled = true;
            Place(t.rectTransform, r);
            t.text = Rtl.Visual(s);
            t.fontSize = size;
            t.color = c;
            t.alignment = Align(anchor);
        }

        static TextAlignmentOptions Align(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center,
        };

        void Fill(Rect r, Color c)
        {
            var img = NextImage();
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = c;
            Place(img.rectTransform, r);
        }

        void Panel(Rect r, float alpha = 1f) => PanelTinted(r, new Color(1f, 1f, 1f, alpha));

        void PanelTinted(Rect r, Color c)
        {
            var img = NextImage();
            img.sprite = panelSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = c;
            Place(img.rectTransform, r);
        }

        void DrawTex(Rect r, Texture tex, Color c)
        {
            if (usedRaws == raws.Count)
            {
                var go = new GameObject("Icon", typeof(RectTransform));
                go.transform.SetParent(layer, false);
                var raw = go.AddComponent<RawImage>();
                raw.raycastTarget = false;
                raws.Add(raw);
            }
            var ri = raws[usedRaws++];
            ri.enabled = true;
            ri.texture = tex;
            ri.color = c;
            Place(ri.rectTransform, r);
        }

        void DrawTex(Rect r, Texture tex) => DrawTex(r, tex, Color.white);

        /// <summary>Glossy rounded button: lighter top, bright rim (9-sliced, tinted).</summary>
        void ButtonImage(Rect r, Color c)
        {
            var img = NextImage();
            img.sprite = buttonSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = c;
            Place(img.rectTransform, r);
        }

        static Texture2D ButtonTexture()
        {
            const int w = 128, h = 96;
            const float radius = 30f;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (w - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (h - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d);
                float rim = Mathf.Clamp01(1f - Mathf.Abs(radius - d - 3f) / 2.5f);
                float v = y / (float)h; // 0 bottom, 1 top
                float shade = Mathf.Lerp(0.72f, 1.15f, v) + (v > 0.55f ? 0.12f : 0f);
                var c = new Color(shade, shade, shade, a);
                c = Color.Lerp(c, new Color(1.6f, 1.6f, 1.6f, a), rim * 0.85f);
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        static float PlayMask(float x, float y) => Mathf.Min(Mathf.Min(x + 0.55f, 0.6f - (x * 0.5f + Mathf.Abs(y))), 1f) * 0.6f;

        static float GearMask(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float teeth = Mathf.Cos(a * 8f) > 0.3f ? 0.92f : 0.72f;
            return Mathf.Min(teeth - r, r - 0.3f);
        }

        static float PadMask(float x, float y)
        {
            // Rounded body with two grips; buttons are holes.
            float body = 0.42f - Mathf.Max(Mathf.Abs(x) * 0.62f, Mathf.Abs(y + 0.05f));
            float grips = 0.3f - Vector2.Distance(new Vector2(Mathf.Abs(x), y), new Vector2(0.55f, -0.25f));
            float shape = Mathf.Max(body, grips);
            float holeL = Vector2.Distance(new Vector2(x, y), new Vector2(-0.38f, 0.05f)) - 0.1f;
            float holeR = Vector2.Distance(new Vector2(x, y), new Vector2(0.38f, 0.05f)) - 0.1f;
            return Mathf.Min(shape, Mathf.Min(holeL, holeR));
        }

        /// <summary>Touch buttons are drawn into the same canvas (screen pixels, origin bottom-left).</summary>
        public void TouchCircle(Vector2 screenCenter, float radius, Color fill, Texture circle, Texture ring, string text, int size)
        {
            float k = 1080f / Screen.height;
            var r = new Rect((screenCenter.x - radius) * k, (Screen.height - screenCenter.y - radius) * k, radius * 2f * k, radius * 2f * k);
            DrawTex(r, circle, fill);
            DrawTex(r, ring, new Color(1f, 1f, 1f, 0.75f));
            if (!string.IsNullOrEmpty(text)) Text(r, text, Mathf.RoundToInt(size * k), Color.white, TextAnchor.MiddleCenter, 2);
        }

        TouchControls touch;

        /// <summary>Layout width in 1080-tall units: the canvas' own size (it can also render into a texture, e.g. the trailer).</summary>
        float CanvasWidth() => layer != null && layer.rect.width > 1f ? layer.rect.width * 1080f / Mathf.Max(1f, layer.rect.height)
                                                                       : Screen.width / (Screen.height / 1080f);
        static string firstError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetErrors() => firstError = null;

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        static void OnLog(string message, string stack, LogType type)
        {
            if (firstError != null || (type != LogType.Exception && type != LogType.Error)) return;
            string where = stack;
            int nl = where.IndexOf('\n');
            if (nl > 0) where = where.Substring(0, nl);
            firstError = (message.Length > 160 ? message.Substring(0, 160) : message) + "  @ " + where;
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            EnsureStyle();
            BeginFrame();
            float w = CanvasWidth(), h = 1080f;
            Draw(gm, w, h);
            DrawCaption(w, h);
            if (touch == null) touch = GetComponent<TouchControls>();
            if (touch != null) touch.DrawUi(this);
            if (fade > 0.001f) Fill(new Rect(0, 0, w, h), new Color(0.02f, 0.02f, 0.06f, fade));
            if (firstError != null && !ScriptedInput.Active)
                Text(new Rect(20f, h - 60f, w - 40f, 50f), "Error: " + firstError, 22, new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleLeft, 2);
            EndFrame();
        }

        void Draw(GameManager gm, float w, float h)
        {

            switch (gm.State)
            {
                case GameState.Title:
                    DrawTitle(gm, w, h);
                    break;
                case GameState.Playing:
                    DrawStatus(gm, w);
                    DrawBanner(w);
                    DrawMessage(w, h);
                    DrawMission(w);
                    DrawHint(w, h);
                    DrawSpeech(w, h);
                    DrawHints(w, h);
                    break;
                case GameState.Paused:
                    DrawStatus(gm, w);
                    Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0.05f, 0.5f));
                    Text(new Rect(0, 150, w, 120), page == Page.Settings ? "הגדרות" : page == Page.Shop ? "בגדים למאיה" : page == Page.Missions ? "משימות" : "הפסקה",
                         88, new Color(1f, 0.86f, 0.28f), TextAnchor.MiddleCenter, 4);
                    if (page == Page.Shop) Text(new Rect(0, 270, w, 60), $"יש לך {Outfits.Wallet} מטבעות", 38, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter, 2);
                    else if (page == Page.Missions) DrawMissionList(gm, w);
                    else Text(new Rect(0, 270, w, 60), $"{gm.LevelName}: {gm.LevelStars}/{gm.LevelStarTotal} כוכבים", 38, Color.white, TextAnchor.MiddleCenter, 2);
                    DrawButtons();
                    break;
                case GameState.Won:
                    DrawStatus(gm, w);
                    DrawPanel(w, h, "כל הכבוד מאיה!", new[]
                    {
                        $"אספת את כל {gm.TotalStars} הכוכבים בכל {Levels.Count} השלבים",
                        $"מטבעות: {gm.Coins}",
                        TouchControls.Active ? "געו במסך כדי להמשיך לשחק" : "Enter - להמשיך לשחק חופשי",
                        "N - משחק חדש",
                    });
                    break;
                case GameState.GameOver:
                    DrawPanel(w, h, "המשחק נגמר", new[] { $"כוכבים: {gm.Stars}/{gm.TotalStars}", TouchControls.Active ? "געו במסך כדי לנסות שוב" : "Enter - לנסות שוב" });
                    break;
            }
        }

        /// <summary>Centre of the title menu column (the right part of the screen; Maya stands on the left).</summary>
        static float TitleColumn(float w) => w * 0.64f;

        void DrawTitle(GameManager gm, float w, float h)
        {
            float cx = TitleColumn(w);
            float t = Time.unscaledTime;
            float bob = Mathf.Sin(t * 1.6f) * 6f;
            // Soft dark edges keep the text readable over the bright world.
            Fill(new Rect(0, 0, w, 90), new Color(0.02f, 0.05f, 0.15f, 0.25f));
            // Logo: "מאיה" big, "ומסע הכוכבים" under it, stars dancing around.
            var gold = new Color(1f, 0.84f, 0.2f);
            Text(new Rect(cx - 420f, 18f + bob * 0.4f, 840f, 170f), "מאיה", 168, gold, TextAnchor.MiddleCenter, 5);
            Text(new Rect(cx - 520f, 160f + bob * 0.4f, 1040f, 140f), "ומסע הכוכבים", 112, gold, TextAnchor.MiddleCenter, 5);
            float spin = Mathf.Sin(t * 2.2f) * 8f;
            DrawTex(new Rect(cx + 175f, 30f - bob + spin * 0.3f, 110f, 110f), starIcon);
            DrawTex(new Rect(cx - 285f, 70f + bob, 72f, 72f), starIcon);
            DrawTex(new Rect(cx - 245f, 25f + bob * 0.5f, 44f, 44f), starIcon);
            DrawTex(new Rect(cx - 330f, 296f, 40f, 40f), starIcon);
            DrawTex(new Rect(cx + 290f, 296f, 40f, 40f), starIcon);
            Text(new Rect(cx - 300f, 290f, 600f, 52f), $"{Levels.Count} עולמות · {gm.TotalStars} כוכבי כוח · יש לך {gm.Stars}", 34, Color.white, TextAnchor.MiddleCenter);

            if (page == Page.Help)
            {
                Panel(new Rect(w * 0.5f - 680f, 350f, 1360f, 430f), 0.92f);
                string[] lines = TouchControls.Active ? new[]
                {
                    "ג'ויסטיק בצד שמאל - תנועה,  גרירה בצד ימין - סיבוב מצלמה,  צביטה - זום",
                    "קפיצה (3 קפיצות ברצף בזמן ריצה = קפיצה משולשת!)",
                    "מכה / הלמות: על הקרקע ובמים - מכה (3 ברצף = בעיטה),  באוויר - הלמות קרקע",
                    "קפיצה כשנוגעים בקיר באוויר = קפיצת קיר",
                    "מכה / הלמות על צינור ירוק = כניסה לצינור",
                    "במים: קפיצה - לשחות למעלה,  החזקת מכה / הלמות - לצלול",
                    "אספי כוכבים כדי לפתוח שערים לעולמות חדשים",
                } : new[]
                {
                    "WASD / חיצים - תנועה,  עכבר - סיבוב מצלמה,  גלגלת - זום,  Shift - הליכה איטית",
                    "רווח - קפיצה (3 קפיצות ברצף בזמן ריצה = קפיצה משולשת!)",
                    "Ctrl / E / קליק: על הקרקע ובמים - מכה (3 ברצף = בעיטה),  באוויר - הלמות קרקע",
                    "רווח כשנוגעים בקיר באוויר - קפיצת קיר",
                    "Ctrl / E על צינור ירוק - כניסה לצינור",
                    "במים: רווח - לשחות למעלה,  החזקת Ctrl / E - לצלול",
                    "אספי כוכבים כדי לפתוח שערים לעולמות חדשים",
                };
                for (int i = 0; i < lines.Length; i++)
                    Text(new Rect(w * 0.5f - 660f, 370 + i * 56, 1320f, 54f), lines[i], 31, new Color(0.92f, 0.96f, 1f), TextAnchor.MiddleCenter, 2);
            }
            else if (page == Page.Shop)
            {
                Text(new Rect(cx - 500f, 300f, 1000f, 60f), $"בגדים למאיה · יש לך {Outfits.Wallet} מטבעות", 38, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter, 3);
            }
            else if (page == Page.ConfirmNew)
            {
                Text(new Rect(cx - 500f, 360f, 1000f, 70f), "להתחיל משחק חדש? כל הכוכבים שאספת יימחקו", 40, new Color(1f, 0.8f, 0.8f), TextAnchor.MiddleCenter, 3);
            }
            // The trailer's end card shows only the logo and its caption.
            if (!(ScriptedInput.Active && caption != null && Time.unscaledTime < captionUntil)) DrawButtons();
        }

        // ---------- title and pause menus ----------

        enum Page { Main, Settings, Help, ConfirmNew, Shop, Missions }

        /// <summary>Pause menu: the world's stars with a tick for each one already collected.</summary>
        void DrawMissionList(GameManager gm, float w)
        {
            var stars = gm.LevelStarsList();
            float y = 260f;
            foreach (var s in stars)
            {
                bool got = gm.HasStar(s.Id);
                bool current = gm.Mission == s;
                var col = got ? new Color(0.7f, 0.95f, 0.7f) : current ? new Color(1f, 0.85f, 0.35f) : Color.white;
                Text(new Rect(w * 0.5f - 500f, y, 1000f, 56f), got ? s.Title + " - נאסף" : current ? "המשימה: " + s.Title : s.Title, 40, col, TextAnchor.MiddleCenter, 2);
                y += 60f;
            }
        }

        struct MenuButton
        {
            public Rect Rect;
            public string Label;
            public System.Action Action;
            public Texture2D Icon;
            public bool Primary;
        }

        Texture2D playIcon, padIcon, gearIcon, backIcon, buttonTex;
        Sprite buttonSprite;

        Page page;
        int selected;
        GameState lastState = GameState.Title;
        Vector3 lastMouse;
        readonly System.Collections.Generic.List<MenuButton> buttons = new System.Collections.Generic.List<MenuButton>();

        void Go(Page p)
        {
            page = p;
            selected = 0;
            menuShownAt = Time.unscaledTime;
            glow.Clear();
        }

        void SettingChanged()
        {
            GameSettings.Save();
            GameManager.I.ApplySettings();
        }

        void BuildButtons(GameManager gm, float w)
        {
            buttons.Clear();
            bool title = gm.State == GameState.Title;
            float bw = title ? 620f : 660f, bh = title ? 88f : 90f, gap = 20f;
            float y = page == Page.Help ? 800f : page == Page.ConfirmNew ? 450f : page == Page.Missions ? 600f : page == Page.Shop ? (title ? 380f : 350f) : title ? 372f : 380f;
            if (page == Page.Shop) { bh = 76f; gap = 14f; }
            if (page == Page.Settings) { bh = 68f; gap = 10f; y -= 20f; }
            float cx = title && page != Page.Help ? TitleColumn(w) : w * 0.5f;
            void Add(string label, System.Action action, Texture2D icon = null, bool primary = false)
            {
                float wide = primary ? 40f : 0f;
                buttons.Add(new MenuButton { Rect = new Rect(cx - (bw + wide) * 0.5f, y, bw + wide, bh + (primary ? 8f : 0f)), Label = label, Action = action, Icon = icon, Primary = primary });
                y += bh + gap + (primary ? 8f : 0f);
            }
            switch (page)
            {
                case Page.Main when title:
                    if (gm.HasProgress) Add($"המשך מהפעם הקודמת  ({gm.Stars}/{gm.TotalStars})", gm.ContinueGame, starIcon, true);
                    Add("משחק חדש", () => { if (gm.HasProgress) Go(Page.ConfirmNew); else gm.StartNewGame(); }, playIcon, !gm.HasProgress);
                    Add("בגדים למאיה", () => Go(Page.Shop), starIcon);
                    Add("איך משחקים", () => Go(Page.Help), padIcon);
                    Add("הגדרות", () => Go(Page.Settings), gearIcon);
                    break;
                case Page.Main:
                    Add("המשך לשחק", gm.Resume);
                    Add("משימות בעולם הזה", () => Go(Page.Missions));
                    Add("בגדים למאיה", () => Go(Page.Shop));
                    Add("חזרה לנקודת הביקורת", gm.RespawnFromMenu);
                    Add("הגדרות", () => Go(Page.Settings));
                    Add("לתפריט הראשי", gm.QuitToTitle);
                    break;
                case Page.Settings:
                    Add($"צלילים: {(GameSettings.Sound ? "פועלים" : "כבויים")}", () => { GameSettings.Sound = !GameSettings.Sound; SettingChanged(); });
                    Add($"מוזיקה: {(GameSettings.Music ? "פועלת" : "כבויה")}", () => { GameSettings.Music = !GameSettings.Music; SettingChanged(); });
                    Add($"גרפיקה: {GameSettings.QualityNames[GameSettings.Quality]}", () => { GameSettings.Quality = (GameSettings.Quality + 1) % 3; SettingChanged(); });
                    Add($"מצלמה: {GameSettings.ZoomNames[GameSettings.CameraZoom]}", () => { GameSettings.CameraZoom = (GameSettings.CameraZoom + 1) % 3; SettingChanged(); });
                    Add($"כפתורי מגע: {GameSettings.ButtonSizeNames[GameSettings.ButtonSize]}", () => { GameSettings.ButtonSize = (GameSettings.ButtonSize + 1) % 3; SettingChanged(); });
                    Add($"צד הכפתורים: {(GameSettings.LeftHanded ? "שמאל" : "ימין")}", () => { GameSettings.LeftHanded = !GameSettings.LeftHanded; SettingChanged(); });
                    Add($"רטט: {(GameSettings.Vibration ? "פועל" : "כבוי")}", () => { GameSettings.Vibration = !GameSettings.Vibration; SettingChanged(); Haptics.Pulse(40); });
                    Add("חזרה", () => Go(Page.Main));
                    break;
                case Page.Help:
                case Page.Missions:
                    Add("חזרה", () => Go(Page.Main));
                    break;
                case Page.Shop:
                    foreach (var item in Outfits.Items)
                    {
                        var it = item;
                        string label = !Outfits.Owns(it.Id) ? $"{it.Name} · {it.Price} מטבעות"
                                     : Outfits.Wears(it.Id) ? $"{it.Name} · לבושה עכשיו" : $"{it.Name} · ללבוש";
                        Add(label, () =>
                        {
                            if (!Outfits.Owns(it.Id))
                            {
                                if (Outfits.Buy(it.Id)) { gm.Sfx.Play(gm.Sfx.Star, 0.6f); Show($"קנית {it.Name}!", 2f); }
                                else Show($"חסרים עוד {it.Price - Outfits.Wallet} מטבעות", 2f);
                            }
                            else Outfits.Wear(it.Id, !Outfits.Wears(it.Id));
                        });
                    }
                    Add("חזרה", () => Go(Page.Main));
                    break;
                case Page.ConfirmNew:
                    Add("כן, משחק חדש", gm.StartNewGame);
                    Add("ביטול", () => Go(Page.Main));
                    break;
            }
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, buttons.Count - 1));
        }

        readonly List<float> glow = new List<float>();

        void DrawButtons()
        {
            while (glow.Count < buttons.Count) glow.Add(0f);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                bool on = i == selected;
                // Buttons slide in one after another, and the selected one swells and breathes.
                glow[i] = Mathf.MoveTowards(glow[i], on ? 1f : 0f, dt * 6f);
                float appear = Mathf.Clamp01((Time.unscaledTime - menuShownAt) * 5f - i * 0.35f);
                float ease = 1f - (1f - appear) * (1f - appear);
                float pulse = on ? Mathf.Sin(Time.unscaledTime * 4f) * 4f : 0f;
                float grow = glow[i] * 14f + pulse;
                var r = new Rect(b.Rect.x - grow + (1f - ease) * 120f, b.Rect.y - grow * 0.25f, b.Rect.width + grow * 2f, b.Rect.height + grow * 0.5f);
                var tint = Color.Lerp(new Color(1f, 1f, 1f, 0.9f), new Color(1f, 0.8f, 0.3f, 1f), glow[i]);
                tint.a *= ease;
                // Gold for the main action, deep blue for the rest; a soft halo on the selected one.
                var baseCol = b.Primary ? new Color(1f, 0.72f, 0.16f) : new Color(0.12f, 0.32f, 0.72f);
                var col = Color.Lerp(baseCol, b.Primary ? new Color(1f, 0.85f, 0.35f) : new Color(0.2f, 0.48f, 0.95f), glow[i]);
                col.a = ease * 0.96f;
                if (glow[i] > 0.01f)
                    ButtonImage(new Rect(r.x - 8f, r.y - 8f, r.width + 16f, r.height + 16f), new Color(1f, 0.95f, 0.6f, 0.35f * glow[i] * ease));
                ButtonImage(r, col);
                if (b.Icon != null)
                    DrawTex(new Rect(r.x + 26f, r.y + r.height * 0.5f - 30f, 60f, 60f), b.Icon, new Color(1f, 1f, 1f, ease));
                var tc = Color.white;
                tc.a = ease;
                Text(new Rect(r.x + (b.Icon != null ? 70f : 0f), r.y, r.width - (b.Icon != null ? 90f : 0f), r.height), b.Label, Mathf.RoundToInt(40 + glow[i] * 3f), tc, TextAnchor.MiddleCenter, 3);
            }
        }

        float menuShownAt;

        void Update()
        {
            fade = Mathf.MoveTowards(fade, fadeTarget, Time.unscaledDeltaTime * fadeSpeed);
            var gm = GameManager.I;
            if (gm == null) return;
            if (gm.State != lastState)
            {
                lastState = gm.State;
                Go(Page.Main);
            }
            if (gm.State != GameState.Title && gm.State != GameState.Paused) return;
            EnsureStyle();
            float scale = Screen.height / 1080f;
            BuildButtons(gm, CanvasWidth());
            if (buttons.Count == 0) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected = (selected + 1) % buttons.Count;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected = (selected + buttons.Count - 1) % buttons.Count;
            if (gm.State == GameState.Title && Input.GetKeyDown(KeyCode.Escape) && page != Page.Main) { Go(Page.Main); return; }

            Vector2? press = null;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) press = Input.GetTouch(i).position;
            if (gm.State == GameState.Title) TitlePointer(gm, scale);
            if (!TouchControls.Active)
            {
                var mouse = Input.mousePosition;
                if ((mouse - lastMouse).sqrMagnitude > 1f)
                {
                    lastMouse = mouse;
                    var g = new Vector2(mouse.x / scale, (Screen.height - mouse.y) / scale);
                    for (int i = 0; i < buttons.Count; i++)
                        if (buttons[i].Rect.Contains(g)) selected = i;
                }
                if (Input.GetMouseButtonDown(0)) press = mouse;
            }
            if (press.HasValue)
            {
                var g = new Vector2(press.Value.x / scale, (Screen.height - press.Value.y) / scale);
                for (int i = 0; i < buttons.Count; i++)
                    if (buttons[i].Rect.Contains(g)) { selected = i; Activate(i); return; }
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.JoystickButton0))
                Activate(selected);
#endif
        }

        bool titleDrag;
        Vector2 dragStart, dragLast;
        float dragTravel;

        /// <summary>Title screen: drag outside the buttons spins Maya; a short tap on her makes her react.</summary>
        void TitlePointer(GameManager gm, float scale)
        {
            bool down, held;
            Vector2 pos;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pos = t.position;
                down = t.phase == TouchPhase.Began;
                held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            }
            else
            {
                pos = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
            }
            if (down)
            {
                var g = new Vector2(pos.x / scale, (Screen.height - pos.y) / scale);
                foreach (var b in buttons)
                    if (b.Rect.Contains(g)) return; // the menu handles it
                titleDrag = true;
                dragStart = dragLast = pos;
                dragTravel = 0f;
                return;
            }
            if (!titleDrag) return;
            if (held)
            {
                float dx = pos.x - dragLast.x;
                dragTravel += Mathf.Abs(dx) + Mathf.Abs(pos.y - dragLast.y);
                if (dragTravel > 12f) gm.TitleSpin(dx);
                dragLast = pos;
                return;
            }
            titleDrag = false;
            if (dragTravel <= 12f) gm.TitleTap(dragStart);
        }

        void Activate(int i)
        {
            if (i < 0 || i >= buttons.Count) return;
            var gm = GameManager.I;
            gm.Sfx.Play(gm.Sfx.Coin, 0.5f);
            buttons[i].Action?.Invoke();
        }

        void DrawStatus(GameManager gm, float w)
        {
            // Right: stars, coins, lives (with icons)
            int rows = gm.ShowRedCoins ? 4 : 3;
            Panel(new Rect(w - 420f, 20f, 400f, 30f + rows * 62f));
            float y = 34f;
            Row(w, ref y, starIcon, $"{gm.Stars}/{gm.TotalStars}", new Color(1f, 0.9f, 0.35f));
            Row(w, ref y, coinIcon, $"{gm.Coins}", new Color(1f, 0.95f, 0.7f));
            Row(w, ref y, heartIcon, $"{gm.Lives}", new Color(1f, 0.7f, 0.82f));
            if (gm.ShowRedCoins) Row(w, ref y, redCoinIcon, $"{gm.RedCoins}/{gm.TotalRedCoins}", new Color(1f, 0.5f, 0.45f));

            // Left: power meter + level name
            DrawTex(new Rect(24f, 18f, 128f, 128f), Meter(Mathf.Clamp(gm.Health, 0, GameManager.MaxHealth)));
            Text(new Rect(24f, 18f, 128f, 128f), gm.Health.ToString(), 40, Color.white, TextAnchor.MiddleCenter, 2);
            Text(new Rect(165f, 40f, 700f, 44f), gm.LevelName, 36, Color.white, TextAnchor.MiddleLeft, 3);
            Text(new Rect(165f, 86f, 700f, 40f), $"כוכבים בעולם הזה: {gm.LevelStars}/{gm.LevelStarTotal}", 28, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleLeft, 2);

            // Boss health bar, top centre, while fighting a boss.
            var boss = Enemy.Boss;
            if (boss != null && !boss.Dead && boss.BossMaxHp > 0)
            {
                float bw = 560f, bx = w * 0.5f - bw * 0.5f;
                Panel(new Rect(bx, 16f, bw, 96f));
                Text(new Rect(bx, 20f, bw, 44f), boss.BossName, 34, new Color(1f, 0.75f, 0.3f), TextAnchor.MiddleCenter, 3);
                float slot = (bw - 60f) / boss.BossMaxHp;
                for (int i = 0; i < boss.BossMaxHp; i++)
                {
                    var r = new Rect(bx + 30f + i * slot + 4f, 70f, slot - 8f, 24f);
                    Fill(r, new Color(0.1f, 0.05f, 0.1f, 0.7f));
                    if (i < boss.BossHp) Fill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), new Color(0.95f, 0.25f, 0.35f));
                }
            }

            // Power-up timer under the meter.
            var player = gm.Player;
            if (player != null && player.Power != PowerKind.None)
            {
                float k = Mathf.Clamp01(player.PowerTimeLeft / Mathf.Max(0.1f, player.PowerDuration));
                bool wing = player.Power == PowerKind.Wing;
                var col = wing ? new Color(1f, 0.45f, 0.5f) : new Color(0.8f, 0.86f, 0.95f);
                if (player.PowerTimeLeft < 3f && Mathf.Repeat(Time.unscaledTime * 4f, 1f) > 0.5f) col.a = 0.4f;
                Panel(new Rect(24f, 156f, 420f, 70f));
                Fill(new Rect(40f, 204f, 388f, 12f), new Color(0f, 0f, 0f, 0.4f));
                Fill(new Rect(40f, 204f, 388f * k, 12f), col);
                Text(new Rect(40f, 160f, 388f, 42f), $"{(wing ? "כובע כנפיים" : "כובע מתכת")} {Mathf.CeilToInt(player.PowerTimeLeft)}", 30, col, TextAnchor.MiddleCenter, 2);
            }
        }

        void Row(float w, ref float y, Texture2D icon, string value, Color c)
        {
            DrawTex(new Rect(w - 400f, y, 54f, 54f), icon);
            Text(new Rect(w - 330f, y, 290f, 54f), value, 44, c, TextAnchor.MiddleLeft, 3);
            y += 62f;
        }

        void DrawBanner(float w)
        {
            if (title == null || Time.unscaledTime > titleUntil) return;
            float left = titleUntil - Time.unscaledTime;
            float a = Mathf.Clamp01(left * 1.5f) * Mathf.Clamp01((3f - left) * 3f + 0.2f);
            Panel(new Rect(w * 0.5f - 420f, 170f, 840f, 120f), a);
            Text(new Rect(0, 170, w, 120), title, 76, new Color(1f, 0.88f, 0.35f, a), TextAnchor.MiddleCenter, 4);
        }

        void DrawMessage(float w, float h)
        {
            if (message == null || Time.unscaledTime > messageUntil) return;
            float a = Mathf.Clamp01((messageUntil - Time.unscaledTime) * 2f);
            Text(new Rect(0, 320, w, 100), message, 62, new Color(1f, 0.92f, 0.35f, a), TextAnchor.MiddleCenter, 4);
        }

        void DrawHint(float w, float h)
        {
            if (hint == null || Time.unscaledTime > hintUntil || ScriptedInput.Active) return;
            // Wrap in logical order first, then Text() turns each line right-to-left, so long hints never overflow the box.
            const float boxWidth = 1120f, lineHeight = 50f;
            var lines = Wrap(hint, 36, boxWidth - 60f);
            float boxHeight = 30f + lines.Count * lineHeight;
            float top = h - 110f - boxHeight;
            Panel(new Rect(w * 0.5f - boxWidth * 0.5f, top, boxWidth, boxHeight), 0.95f);
            for (int i = 0; i < lines.Count; i++)
                Text(new Rect(w * 0.5f - boxWidth * 0.5f, top + 15f + i * lineHeight, boxWidth, lineHeight), lines[i], 36, Color.white, TextAnchor.MiddleCenter, 2);
        }

        readonly System.Collections.Generic.List<string> wrapped = new System.Collections.Generic.List<string>();
        string wrappedSource;

        System.Collections.Generic.List<string> Wrap(string text, int size, float maxWidth)
        {
            if (text == wrappedSource) return wrapped;
            wrappedSource = text;
            wrapped.Clear();
            measure.fontSize = size;
            string line = "";
            foreach (var word in text.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && measure.GetPreferredValues(Rtl.Visual(candidate)).x > maxWidth)
                {
                    wrapped.Add(line);
                    line = word;
                }
                else line = candidate;
            }
            if (line.Length > 0) wrapped.Add(line);
            return wrapped;
        }

        void DrawHints(float w, float h)
        {
            if (playStartTime < 0f) return;
            float age = Time.unscaledTime - playStartTime;
            if (age > 14f || TouchControls.Active || ScriptedInput.Active) return;
            float a = Mathf.Clamp01(14f - age);
            Text(new Rect(0, h - 80, w, 50), "רווח - קפיצה   ·   Ctrl/E/קליק - מכה, באוויר: הלמות קרקע   ·   Esc - הפסקה", 30,
                 new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, 2);
        }

        void DrawPanel(float w, float h, string heading, string[] lines)
        {
            Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0.05f, 0.45f));
            Panel(new Rect(w * 0.5f - 620f, 240f, 1240f, 220f + lines.Length * 64f));
            Text(new Rect(0, 262, w, 120), heading, 88, new Color(1f, 0.86f, 0.28f), TextAnchor.MiddleCenter, 4);
            for (int i = 0; i < lines.Length; i++)
                Text(new Rect(0, 420 + i * 64, w, 60), lines[i], 38, Color.white, TextAnchor.MiddleCenter, 2);
        }
    }
}
