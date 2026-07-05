#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Unity.XR.XREAL.DrawingViewer.Editor
{
    /// <summary>
    /// Creates a TMP font asset that supports Chinese characters.
    /// Menu: Drawing Viewer > Setup Chinese Font
    /// </summary>
    public static class DrawingViewerChineseFontSetup
    {
        private const string SourceFontDir = "Assets/_App/DrawingViewer/Fonts";
        private const string SourceFontPath = SourceFontDir + "/ChineseSource.ttf";
        private const string FontAssetPath = "Assets/_App/DrawingViewer/Resources/Fonts/DrawingViewerChinese SDF.asset";

        public const string PreloadCharacters =
            "未打开图纸选择文件刷新关闭页码上传删除本地内置暂无点击右上角ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789./-+()[]：: ";

        [InitializeOnLoadMethod]
        private static void AutoCreateFontOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(FontAssetPath))
                    SetupChineseFontIfMissing();
            };
        }

        [MenuItem("Drawing Viewer/Setup Chinese Font")]
        public static void SetupChineseFont()
        {
            CreateChineseFontAsset(showDialogs: true);
        }

        public static void SetupChineseFontIfMissing()
        {
            if (File.Exists(FontAssetPath))
                return;

            CreateChineseFontAsset(showDialogs: false);
        }

        private static void CreateChineseFontAsset(bool showDialogs)
        {
            try
            {
                Directory.CreateDirectory(SourceFontDir);
                Directory.CreateDirectory("Assets/_App/DrawingViewer/Resources/Fonts");

                CleanupLegacySourceFonts();

                if (!TryPrepareSourceFont())
                {
                    if (showDialogs)
                    {
                        EditorUtility.DisplayDialog(
                            "未找到中文字体",
                            "未能导入中文字体。\n\n请确认系统存在 C:\\Windows\\Fonts\\simhei.ttf，\n" +
                            "或手动将 .ttf 复制到 Assets/_App/DrawingViewer/Fonts/ChineseSource.ttf 后重试。",
                            "OK");
                    }
                    else
                    {
                        Debug.LogWarning("[DrawingViewerChineseFontSetup] Chinese font missing. Run Drawing Viewer > Setup Chinese Font.");
                    }
                    return;
                }

                var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
                if (sourceFont == null)
                {
                    if (showDialogs)
                        EditorUtility.DisplayDialog("字体导入失败", "无法加载 " + SourceFontPath, "OK");
                    return;
                }

                if (File.Exists(FontAssetPath))
                    AssetDatabase.DeleteAsset(FontAssetPath);

                var fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    42,
                    5,
                    GlyphRenderMode.SDFAA,
                    2048,
                    2048,
                    AtlasPopulationMode.Dynamic);

                if (fontAsset == null)
                {
                    if (showDialogs)
                    {
                        EditorUtility.DisplayDialog(
                            "字体创建失败",
                            "请检查 ChineseSource.ttf 的 Import Settings 是否勾选 Include Font Data。",
                            "OK");
                    }
                    return;
                }

                fontAsset.name = "DrawingViewerChinese SDF";

                if (fontAsset.fallbackFontAssetTable == null)
                    fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();

                var defaultFont = TMP_Settings.defaultFontAsset;
                if (defaultFont != null && !fontAsset.fallbackFontAssetTable.Contains(defaultFont))
                    fontAsset.fallbackFontAssetTable.Add(defaultFont);

                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

                if (fontAsset.atlasTextures != null)
                {
                    foreach (var atlas in fontAsset.atlasTextures)
                    {
                        if (atlas == null) continue;
                        atlas.name = "DrawingViewerChinese SDF Atlas";
                        AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                    }
                }

                if (fontAsset.material != null)
                {
                    fontAsset.material.name = "DrawingViewerChinese SDF Atlas Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                if (!fontAsset.TryAddCharacters(PreloadCharacters, out string missing) && !string.IsNullOrEmpty(missing))
                    Debug.LogWarning("[DrawingViewerChineseFontSetup] Missing glyphs: " + missing);

                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "中文字体已配置",
                        "已创建：\n" + FontAssetPath + "\n\n运行 Play 后中文应正常显示。",
                        "OK");
                }

                Debug.Log("[DrawingViewerChineseFontSetup] Chinese TMP font created at " + FontAssetPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[DrawingViewerChineseFontSetup] Failed to create Chinese font: " + ex);
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "字体创建失败",
                        "创建中文字体时出错：\n" + ex.Message,
                        "OK");
                }
            }
        }

        private static void CleanupLegacySourceFonts()
        {
            string legacyTtc = SourceFontDir + "/ChineseSource.ttc";
            if (File.Exists(legacyTtc))
                AssetDatabase.DeleteAsset(legacyTtc);
        }

        private static bool TryPrepareSourceFont()
        {
            if (!File.Exists(SourceFontPath))
            {
                string systemFont = FindSystemChineseFontPath();
                if (string.IsNullOrEmpty(systemFont))
                    return false;

                File.Copy(systemFont, SourceFontPath, true);
            }

            AssetDatabase.ImportAsset(SourceFontPath, ImportAssetOptions.ForceUpdate);
            EnsureFontImportSettings(SourceFontPath);
            return File.Exists(SourceFontPath);
        }

        private static string FindSystemChineseFontPath()
        {
            string fontsFolder = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows),
                "Fonts");

            string[] candidates =
            {
                "simhei.ttf",
                "msyh.ttf",
                "simfang.ttf",
                "simsun.ttc",
                "msyh.ttc"
            };

            foreach (var fileName in candidates)
            {
                string fullPath = Path.Combine(fontsFolder, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        private static void EnsureFontImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TrueTypeFontImporter;
            if (importer == null)
                return;

            importer.includeFontData = true;
            importer.fontRenderingMode = FontRenderingMode.Smooth;
            importer.SaveAndReimport();
        }
    }
}
#endif
