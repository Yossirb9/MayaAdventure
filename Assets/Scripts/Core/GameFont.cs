using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// The UI font: Secular One (SIL Open Font License, Resources/Fonts), which has Hebrew, Latin and digits,
    /// so the HUD never depends on what fonts the phone happens to have.
    /// </summary>
    public static class GameFont
    {
        static Font font;
        static bool bundled;

        /// <summary>
        /// Every string is rasterised at one of these sizes and scaled to the size asked for. A dynamic font
        /// keeps one glyph atlas per size; with a dozen sizes the phone's atlas overflowed and letters went missing.
        /// </summary>
        public static readonly int[] Sizes = { 24, 36, 52, 76 };

        public const string Charset =
            "אבגדהוזחטיכךלמםנןסעפףצץקרשת" +
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            " !?.,:;'\"-–/()+=%·…*#&<>[]_";

        public static int BaseSize(int size)
        {
            foreach (int b in Sizes)
                if (b >= size * 0.92f) return b;
            return Sizes[Sizes.Length - 1];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            font = null;
            tmp = null;
        }

        static TMPro.TMP_FontAsset tmp;

        /// <summary>TextMeshPro SDF font made from the same font at runtime (dynamic atlas, Hebrew glyphs preloaded).</summary>
        public static TMPro.TMP_FontAsset Tmp()
        {
            if (tmp != null) return tmp;
            // Pre-baked static atlas (Assets/Resources/Fonts, made by Maya > Bake UI Font): nothing is rasterised on the phone.
            tmp = Resources.Load<TMPro.TMP_FontAsset>("Fonts/SecularOne SDF");
            if (tmp != null) return tmp;
            var f = Get(out _);
            try
            {
                tmp = TMPro.TMP_FontAsset.CreateFontAsset(f, 72, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                                                          TMPro.AtlasPopulationMode.Dynamic, true);
                if (tmp != null) tmp.TryAddCharacters(Charset, out _);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            return tmp;
        }

        /// <summary>The shared font, plus the style to draw it with (the bundled font is already heavy).</summary>
        public static Font Get(out FontStyle style)
        {
            if (font == null)
            {
                font = Resources.Load<Font>("Fonts/SecularOne");
                bundled = font != null;
                if (!bundled)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Tahoma", "Roboto", "Noto Sans Hebrew", "sans-serif" }, 40);
                // Rasterise every glyph up front so the atlas never rebuilds in the middle of drawing the HUD.
                var fs = bundled ? FontStyle.Normal : FontStyle.Bold;
                foreach (int size in Sizes) font.RequestCharactersInTexture(Charset, size, fs);
            }
            style = bundled ? FontStyle.Normal : FontStyle.Bold;
            return font;
        }
    }
}
