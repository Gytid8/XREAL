#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Unity.XR.XREAL.DrawingViewer.Editor
{
    /// <summary>
    /// Creates required ScriptableObject assets and sample content for the Drawing Viewer.
    /// Run from: Drawing Viewer > Create All Resources
    /// </summary>
    public class DrawingViewerResourceSetup
    {
        private const string ResourcesPath = "Assets/_App/DrawingViewer/Resources";
        private const string StreamingPath = "Assets/_App/DrawingViewer/StreamingAssets/Drawings";
        private const string TexturesPath = "Assets/_App/DrawingViewer/Textures";
        private const string SettingsAssetPath = "Assets/_App/DrawingViewer/Resources/DrawingViewerSettings.asset";

        [MenuItem("Drawing Viewer/Create All Resources")]
        public static void CreateAllResources()
        {
            EnsureDirectories();
            CreateSettingsAsset();
            CreateSampleDrawing();
            CreateIcons();
            AssetDatabase.Refresh();

            Debug.Log("====================================");
            Debug.Log("[ResourceSetup] All resources created!");
            Debug.Log("====================================");

            EditorUtility.DisplayDialog("Resources Created",
                "Drawing Viewer resources have been created:\n\n" +
                "  - DrawingViewerSettings.asset\n" +
                "  - sample-drawing-01.png\n" +
                "  - Icons (zoom, page, file, reset)\n\n" +
                "You can now setup the Main Scene.",
                "OK");
        }

        [MenuItem("Drawing Viewer/Create Settings Asset")]
        public static void CreateSettingsAsset()
        {
            EnsureDirectories();

            DrawingViewerSettings settings;

            if (File.Exists(SettingsAssetPath))
            {
                settings = AssetDatabase.LoadAssetAtPath<DrawingViewerSettings>(SettingsAssetPath);
                if (settings != null)
                {
                    Debug.Log("[ResourceSetup] Settings asset already exists. Updating...");
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    return;
                }
            }

            settings = ScriptableObject.CreateInstance<DrawingViewerSettings>();
            settings.ApplyDefaults();

            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ResourceSetup] DrawingViewerSettings created at: {SettingsAssetPath}");
        }

        [MenuItem("Drawing Viewer/Create Sample Drawings")]
        public static void CreateSampleDrawing()
        {
            EnsureDirectories();

            string samplePath = Path.Combine(StreamingPath, "sample-drawing-01.png");

            if (File.Exists(samplePath))
            {
                Debug.Log("[ResourceSetup] Sample drawing already exists.");
                return;
            }

            // Create an engineering drawing-like sample image
            int width = 1024;
            int height = 724; // ~A4 aspect
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // White background
            Color white = new Color(0.95f, 0.95f, 0.95f);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, white);

            // Draw a border frame
            Color frameColor = Color.black;
            DrawRect(texture, 40, 40, width - 80, height - 80, frameColor, false);
            DrawRect(texture, 38, 38, width - 76, height - 76, frameColor, false);

            // Draw title block
            int tbx = width - 360;
            int tby = 50;
            DrawRect(texture, tbx, tby, 310, 120, frameColor, false);
            DrawRect(texture, tbx, tby + 60, 310, 1, frameColor, true);
            DrawRect(texture, tbx, tby + 90, 310, 1, frameColor, true);
            DrawRect(texture, tbx + 150, tby + 90, 1, 30, frameColor, true);

            // Draw some "mechanical part" geometry lines
            Color lineColor = new Color(0, 0, 0.3f);
            int cx = width / 2;
            int cy = height / 2;

            // Outer rectangle
            DrawRect(texture, cx - 250, cy - 150, 500, 300, lineColor, false);

            // Inner features
            DrawRect(texture, cx - 100, cy - 80, 200, 160, lineColor, false);
            DrawCircle(texture, cx - 200, cy, 40, lineColor);
            DrawCircle(texture, cx + 200, cy, 40, lineColor);
            DrawCircle(texture, cx, cy - 50, 30, lineColor);
            DrawCircle(texture, cx, cy + 50, 30, lineColor);

            // Dimension lines
            Color dimColor = new Color(0.2f, 0.2f, 0.6f);
            // Horizontal dimension
            DrawLine(texture, cx - 250, cy - 200, cx + 250, cy - 200, dimColor);
            DrawLine(texture, cx - 250, cy - 205, cx - 250, cy - 190, dimColor);
            DrawLine(texture, cx + 250, cy - 205, cx + 250, cy - 190, dimColor);
            DrawArrow(texture, cx - 250, cy - 200, false, dimColor);
            DrawArrow(texture, cx + 250, cy - 200, true, dimColor);

            // Vertical dimension
            DrawLine(texture, cx + 300, cy - 150, cx + 300, cy + 150, dimColor);
            DrawLine(texture, cx + 295, cy - 150, cx + 310, cy - 150, dimColor);
            DrawLine(texture, cx + 295, cy + 150, cx + 310, cy + 150, dimColor);
            DrawArrow(texture, cx + 300, cy - 150, false, dimColor);
            DrawArrow(texture, cx + 300, cy + 150, true, dimColor);

            // Center lines
            Color centerColor = new Color(0.6f, 0.1f, 0.1f);
            DrawLine(texture, cx - 280, cy, cx + 280, cy, centerColor, true); // dash-dot
            DrawLine(texture, cx, cy - 200, cx, cy + 200, centerColor, true);

            texture.Apply();

            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(samplePath, pngData);
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(samplePath);

            // Configure texture import settings for Android
            var importer = AssetImporter.GetAtPath(samplePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;

                var androidSettings = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    format = TextureImporterFormat.ASTC_6x6,
                    maxTextureSize = 2048,
                    compressionQuality = 100
                };
                importer.SetPlatformTextureSettings(androidSettings);
                importer.SaveAndReimport();
            }

            Debug.Log($"[ResourceSetup] Sample drawing created: {samplePath}");
        }

        [MenuItem("Drawing Viewer/Create Icons")]
        public static void CreateIcons()
        {
            EnsureDirectories();

            CreateSimpleIcon("IconZoomIn", Color.green, "+");
            CreateSimpleIcon("IconZoomOut", Color.red, "-");
            CreateSimpleIcon("IconNextPage", Color.cyan, ">");
            CreateSimpleIcon("IconPrevPage", Color.cyan, "<");
            CreateSimpleIcon("IconReset", Color.yellow, "R");

            AssetDatabase.Refresh();
        }

        private static void CreateSimpleIcon(string name, Color color, string symbol)
        {
            string path = Path.Combine(TexturesPath, $"{name}.png");

            if (File.Exists(path))
            {
                // Update existing
                AssetDatabase.DeleteAsset(path);
            }

            int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            // Circle background
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - size / 2f;
                    float dy = y - size / 2f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist < size / 2f - 2)
                    {
                        texture.SetPixel(x, y, color);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }

            texture.Apply();
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(path, pngData);
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            Debug.Log($"[ResourceSetup] Icon created: {name}.png");
        }

        #region Drawing Helpers

        private static void DrawRect(Texture2D tex, int x, int y, int w, int h, Color c, bool fill)
        {
            if (fill)
            {
                for (int py = y; py < y + h && py < tex.height; py++)
                    for (int px = x; px < x + w && px < tex.width; px++)
                        if (px >= 0 && py >= 0)
                            tex.SetPixel(px, py, c);
            }
            else
            {
                for (int px = x; px < x + w && px < tex.width; px++)
                {
                    if (px >= 0 && y >= 0 && y < tex.height) tex.SetPixel(px, y, c);
                    if (px >= 0 && y + h >= 0 && y + h < tex.height) tex.SetPixel(px, y + h, c);
                }
                for (int py = y; py < y + h && py < tex.height; py++)
                {
                    if (py >= 0 && x >= 0 && x < tex.width) tex.SetPixel(x, py, c);
                    if (py >= 0 && x + w >= 0 && x + w < tex.width) tex.SetPixel(x + w, py, c);
                }
            }
        }

        private static void DrawCircle(Texture2D tex, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= tex.width || y < 0 || y >= tex.height) continue;
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(dist - r) < 1.5f)
                    {
                        tex.SetPixel(x, y, c);
                    }
                }
            }
        }

        private static void DrawLine(Texture2D tex, int x1, int y1, int x2, int y2, Color c, bool dashed = false)
        {
            int dx = Mathf.Abs(x2 - x1);
            int dy = Mathf.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            int steps = 0;
            while (true)
            {
                bool draw = !dashed || (steps / 8) % 3 != 0;

                if (draw && x1 >= 0 && x1 < tex.width && y1 >= 0 && y1 < tex.height)
                    tex.SetPixel(x1, y1, c);

                steps++;
                if (x1 == x2 && y1 == y2) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x1 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y1 += sy;
                }
            }
        }

        private static void DrawArrow(Texture2D tex, int x, int y, bool pointingLeft, Color c)
        {
            int direction = pointingLeft ? -1 : 1;
            DrawLine(tex, x, y, x + direction * 10, y - 5, c);
            DrawLine(tex, x, y, x + direction * 10, y + 5, c);
        }

        #endregion

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(ResourcesPath))
                Directory.CreateDirectory(ResourcesPath);

            if (!Directory.Exists(StreamingPath))
                Directory.CreateDirectory(StreamingPath);

            if (!Directory.Exists(TexturesPath))
                Directory.CreateDirectory(TexturesPath);
        }
    }
}
#endif
