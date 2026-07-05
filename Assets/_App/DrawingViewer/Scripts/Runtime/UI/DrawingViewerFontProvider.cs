using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    public static class DrawingViewerFontProvider
    {
        private const string ChineseFontResourcePath = "Fonts/DrawingViewerChinese SDF";

        public const string PreloadCharacters =
            "\u672a\u6253\u5f00\u56fe\u7eb8\u9009\u62e9\u6587\u4ef6\u5237\u65b0\u5173\u95ed\u9875\u7801\u4e0a\u4f20\u5220\u9664\u672c\u5730\u5185\u7f6e\u6682\u65e0\u70b9\u51fb\u53f3\u4e0a\u89d2\u8fd4\u56de\u4e3b\u83dc\u5355\u56fe\u50cf\u8bc6\u522b\u5668\u56fe\u7eb8\u67e5\u770b\u5668\u6f14\u793a\u6a21\u5f0f\u6a21\u578b\u672a\u5c31\u7eea\u65ad\u8def\u5668\u9694\u79bb\u5f00\u5173\u8d1f\u8377\u5f00\u5173\u91cd\u7f6e\u6392\u7248\u5de5\u5177\u7bb1\u5de6\u952e\u6309\u4f4f\u62d6\u52a8\u53f3\u952e\u8f6c\u5934\u83dc\u5355\u968f\u5934\u90e8\u8f6c\u52a8\u62d6\u62fd\u7a7a\u767d\u5904\u53ef\u79fb\u52a8\u4f4d\u7f6e\u53cc\u9875\u5bf9\u6bd4\u5355\u9875\u5c55\u5f00\u5df2\u5bfc\u5165\u623f\u5c4b\u5efa\u7b51\u7ed3\u6784\u673a\u68b0\u7535\u6c14\u7cfb\u7edf\u5e73\u9762\u7acb\u9762\u6837\u672c\u300c\u300d\u00b7ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789./-+()[]：: \n";

        private static TMP_FontAsset _chineseFont;
        private static bool _warnedMissing;
        private static bool _preloaded;

        public static TMP_FontAsset ChineseFont
        {
            get
            {
                if (_chineseFont == null)
                {
                    _chineseFont = Resources.Load<TMP_FontAsset>(ChineseFontResourcePath);
                    if (_chineseFont == null && !_warnedMissing)
                    {
                        _warnedMissing = true;
                        Debug.LogWarning(
                            "[DrawingViewerFontProvider] Chinese font not found. Run Drawing Viewer > Setup Chinese Font.");
                    }
                }

                return _chineseFont;
            }
        }

        public static TMP_FontAsset GetFont()
        {
            var font = ChineseFont;
            if (font != null)
            {
                EnsureCharactersLoaded(font, PreloadCharacters);
                return font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        public static void PreloadText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var font = GetFont();
            if (font != null)
                EnsureCharactersLoaded(font, text);
        }

        public static void PreloadDocumentNames(IEnumerable<string> names)
        {
            if (names == null)
                return;

            var builder = new StringBuilder();
            foreach (var name in names)
            {
                if (!string.IsNullOrEmpty(name))
                    builder.Append(name);
            }

            PreloadText(builder.ToString());
        }

        public static void Apply(TextMeshProUGUI text)
        {
            if (text == null) return;

            var font = GetFont();
            if (font == null) return;

            text.font = font;
            if (!string.IsNullOrEmpty(text.text))
                EnsureCharactersLoaded(font, text.text);

            text.ForceMeshUpdate(true);
        }

        public static void Apply(TextMeshPro text)
        {
            if (text == null) return;

            var font = GetFont();
            if (font == null) return;

            text.font = font;
            if (!string.IsNullOrEmpty(text.text))
                EnsureCharactersLoaded(font, text.text);

            text.ForceMeshUpdate(true);
        }

        public static void ApplyAllInChildren(Transform root)
        {
            if (root == null) return;

            var font = GetFont();
            if (font == null) return;

            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.font = font;
                if (!string.IsNullOrEmpty(text.text))
                    EnsureCharactersLoaded(font, text.text);
                text.ForceMeshUpdate(true);
            }

            foreach (var text in root.GetComponentsInChildren<TextMeshPro>(true))
            {
                text.font = font;
                if (!string.IsNullOrEmpty(text.text))
                    EnsureCharactersLoaded(font, text.text);
                text.ForceMeshUpdate(true);
            }
        }

        private static void EnsureCharactersLoaded(TMP_FontAsset font, string characters)
        {
            if (font == null || string.IsNullOrEmpty(characters))
                return;

            if (!_preloaded)
            {
                font.TryAddCharacters(PreloadCharacters, out _);
                _preloaded = true;
            }

            if (!font.TryAddCharacters(characters, out string missing) && !string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning(
                    $"[DrawingViewerFontProvider] Missing glyphs in font: {missing}");
            }
        }
    }
}
