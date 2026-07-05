using System.Collections.Generic;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Object pool for DrawingPageDisplay instances.
    /// Recycles page display objects to avoid instantiation/destruction overhead.
    /// </summary>
    public class DrawingPagePool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject _panelPrefab;
        [SerializeField] private Transform _poolParent;

        /// <summary>
        /// Available (inactive) page displays.
        /// </summary>
        private Queue<DrawingPageDisplay> _available = new Queue<DrawingPageDisplay>();

        /// <summary>
        /// Currently active (visible) page displays.
        /// </summary>
        private List<DrawingPageDisplay> _active = new List<DrawingPageDisplay>();

        /// <summary>
        /// Total created count.
        /// </summary>
        private int _totalCreated;

        private void Awake()
        {
            if (_poolParent == null)
            {
                var parentGo = new GameObject("PagePool_Parent");
                parentGo.transform.SetParent(transform);
                _poolParent = parentGo.transform;
            }
        }

        /// <summary>
        /// Gets a page display from the pool or creates a new one.
        /// </summary>
        public DrawingPageDisplay GetPageDisplay()
        {
            DrawingPageDisplay display;

            if (_available.Count > 0)
            {
                display = _available.Dequeue();
                display.gameObject.SetActive(true);
            }
            else
            {
                var settings = DrawingViewerApp.Singleton?.Settings;
                int maxPages = settings != null ? settings.MaxLoadedPages : 10;

                if (_totalCreated >= maxPages)
                {
                    // Pool exhausted - recycle the least recently used active display
                    Debug.LogWarning($"[DrawingPagePool] Max pages ({maxPages}) reached. Recycling oldest page.");
                    if (_active.Count > 0)
                    {
                        display = _active[0];
                        _active.RemoveAt(0);
                        display.Clear();
                    }
                    else
                    {
                        display = _available.Dequeue();
                        display.gameObject.SetActive(true);
                    }
                }
                else
                {
                    display = CreateNewDisplay();
                }
            }

            _active.Add(display);
            return display;
        }

        /// <summary>
        /// Returns a page display to the pool.
        /// </summary>
        public void ReturnPageDisplay(DrawingPageDisplay display)
        {
            if (display == null) return;

            display.Clear();
            display.gameObject.SetActive(false);
            display.transform.SetParent(_poolParent);

            _active.Remove(display);

            if (!_available.Contains(display))
            {
                _available.Enqueue(display);
            }
        }

        /// <summary>
        /// Returns all active page displays to the pool.
        /// </summary>
        public void ClearAll()
        {
            foreach (var display in _active.ToArray())
            {
                ReturnPageDisplay(display);
            }
            _active.Clear();
        }

        /// <summary>
        /// Creates a new page display from the prefab.
        /// </summary>
        private DrawingPageDisplay CreateNewDisplay()
        {
            GameObject go;

            if (_panelPrefab != null)
            {
                go = Instantiate(_panelPrefab, _poolParent);
            }
            else
            {
                go = CreateDefaultPanel();
            }

            go.name = $"DrawingPanel_{_totalCreated}";
            var display = go.GetComponent<DrawingPageDisplay>();

            if (display == null)
                display = go.AddComponent<DrawingPageDisplay>();

            _totalCreated++;
            return display;
        }

        /// <summary>
        /// Creates a default drawing panel GameObject when no prefab is assigned.
        /// </summary>
        private GameObject CreateDefaultPanel()
        {
            var go = new GameObject("DrawingPanel_Default");
            go.transform.SetParent(_poolParent);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var bc = go.AddComponent<BoxCollider>();

            // Create quad mesh
            var mesh = new Mesh
            {
                name = "PanelQuad",
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
                    new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(0, 1), new Vector2(1, 1),
                },
                normals = new Vector3[]
                {
                    Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                }
            };
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            // Default material
            mr.sharedMaterial = DrawingPageDisplay.CreateDrawingMaterial(null);

            bc.size = new Vector3(1f, 1f, 0.01f);

            return go;
        }

        /// <summary>
        /// Gets the number of active displays.
        /// </summary>
        public int ActiveCount => _active.Count;

        /// <summary>
        /// Gets the number of available (pooled) displays.
        /// </summary>
        public int AvailableCount => _available.Count;

        /// <summary>
        /// Gets the total number of displays created.
        /// </summary>
        public int TotalCreated => _totalCreated;
    }
}
