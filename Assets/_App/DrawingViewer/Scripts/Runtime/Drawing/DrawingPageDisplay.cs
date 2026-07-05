using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Renders a single drawing page as a textured quad in world space.
    /// Uses Quad mesh with Unlit/Texture material for optimal stereo rendering performance.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class DrawingPageDisplay : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private BoxCollider _boxCollider;

        [Header("Display Settings")]
        [SerializeField] private float _defaultAspectRatio = 1.414f;

        /// <summary>
        /// Currently displayed texture.
        /// </summary>
        public Texture2D CurrentTexture { get; private set; }

        /// <summary>
        /// Whether this display is currently showing content.
        /// </summary>
        public bool IsLoaded => CurrentTexture != null;

        /// <summary>
        /// The page index this display is showing (-1 if not set).
        /// </summary>
        public int PageIndex { get; set; } = -1;

        /// <summary>
        /// Current panel width in meters.
        /// </summary>
        public float PanelWidth { get; private set; }

        /// <summary>
        /// Current panel height in meters.
        /// </summary>
        public float PanelHeight { get; private set; }

        private void Awake()
        {
            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();

            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();

            if (_boxCollider == null)
                _boxCollider = GetComponent<BoxCollider>();

            // Ensure we have a quad mesh
            if (_meshFilter.sharedMesh == null)
            {
                _meshFilter.sharedMesh = CreateQuadMesh();
            }
        }

        /// <summary>
        /// Sets the texture to display on this panel.
        /// </summary>
        public void SetTexture(Texture2D texture, float panelWidth = -1f)
        {
            if (texture == null)
            {
                Clear();
                return;
            }

            if (panelWidth <= 0f)
            {
                var settings = DrawingViewerApp.Singleton?.Settings;
                panelWidth = settings != null ? settings.PanelDefaultWidth : 1.2f;
            }

            CurrentTexture = texture;
            PanelWidth = panelWidth;

            // Calculate height from texture aspect ratio
            float aspectRatio = (float)texture.width / texture.height;
            PanelHeight = panelWidth / aspectRatio;

            // Update material
            if (_meshRenderer.sharedMaterial == null || _meshRenderer.sharedMaterial.shader == null)
            {
                _meshRenderer.sharedMaterial = CreateDrawingMaterial(texture);
            }
            else
            {
                _meshRenderer.sharedMaterial.mainTexture = texture;
            }

            // Update quad scale
            transform.localScale = new Vector3(PanelWidth, PanelHeight, 1f);

            // Update collider
            if (_boxCollider != null)
            {
                _boxCollider.size = new Vector3(1f, 1f, 0.01f);
                // The collider scales with the transform, so it will match the panel size
            }

            // Make visible
            _meshRenderer.enabled = true;
        }

        /// <summary>
        /// Clears the displayed texture.
        /// </summary>
        public void Clear()
        {
            CurrentTexture = null;
            PageIndex = -1;

            if (_meshRenderer.material != null)
            {
                _meshRenderer.material.mainTexture = null;
            }
            _meshRenderer.enabled = false;
        }

        /// <summary>
        /// Updates the scale while maintaining aspect ratio.
        /// </summary>
        public void SetScale(float width)
        {
            if (CurrentTexture == null) return;

            PanelWidth = width;
            PanelHeight = width / ((float)CurrentTexture.width / CurrentTexture.height);
            transform.localScale = new Vector3(PanelWidth, PanelHeight, 1f);
        }

        /// <summary>
        /// Gets the aspect ratio of the current texture (or default if none loaded).
        /// </summary>
        public float GetAspectRatio()
        {
            if (CurrentTexture != null)
                return (float)CurrentTexture.width / CurrentTexture.height;
            return _defaultAspectRatio;
        }

        /// <summary>
        /// Creates a simple quad mesh for panel rendering.
        /// </summary>
        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh
            {
                name = "DrawingPanelQuad",
                vertices = new Vector3[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3( 0.5f, -0.5f, 0),
                    new Vector3(-0.5f,  0.5f, 0),
                    new Vector3( 0.5f,  0.5f, 0),
                },
                triangles = new int[] { 0, 1, 2, 1, 3, 2 },
                uv = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                },
                normals = new Vector3[]
                {
                    Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material CreateDrawingMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("UI/Default");

            var material = new Material(shader);
            if (texture != null)
                material.mainTexture = texture;
            material.renderQueue = 2000;
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            return material;
        }
    }
}
