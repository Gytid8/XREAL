using System;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Platform file picker used to import drawings into persistent storage.
    /// Android launches a system image picker; Editor uses a file dialog.
    /// </summary>
    public class DrawingFilePicker : MonoBehaviour
    {
        private static DrawingFilePicker _instance;
        private TaskCompletionSource<string> _pickTask;

        public static DrawingFilePicker Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var app = DrawingViewerApp.Singleton;
                if (app == null)
                    return null;

                _instance = app.GetComponent<DrawingFilePicker>();
                if (_instance == null)
                    _instance = app.gameObject.AddComponent<DrawingFilePicker>();

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            gameObject.name = nameof(DrawingFilePicker);
        }

        /// <summary>
        /// Opens the platform picker and returns the copied file path in persistent storage.
        /// Returns null when the user cancels or import fails.
        /// </summary>
        public Task<string> PickAndImportImageAsync()
        {
            if (_pickTask != null && !_pickTask.Task.IsCompleted)
            {
                Debug.LogWarning("[DrawingFilePicker] Picker already open.");
                return _pickTask.Task;
            }

            _pickTask = new TaskCompletionSource<string>();

#if UNITY_EDITOR
            PickImageInEditor();
#elif UNITY_ANDROID
            PickImageOnAndroid();
#else
            _pickTask.TrySetResult(null);
#endif

            return _pickTask.Task;
        }

#if UNITY_EDITOR
        private void PickImageInEditor()
        {
            string path = EditorUtility.OpenFilePanel(
                "选择图纸",
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "png,jpg,jpeg,pdf");

            CompletePick(path);
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void PickImageOnAndroid()
        {
            try
            {
                DrawingViewerAndroidFileBridge.StartImagePicker(gameObject.name, nameof(OnAndroidPickCompleted));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DrawingFilePicker] Android picker failed: {ex.Message}");
                _pickTask?.TrySetResult(null);
            }
        }

        // Called from Android via UnitySendMessage.
        public void OnAndroidPickCompleted(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                _pickTask?.TrySetResult(null);
                return;
            }

            CompletePick(payload);
        }
#endif

        private void CompletePick(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                _pickTask?.TrySetResult(null);
                return;
            }

            string importedPath = FileImportHelper.ImportFile(sourcePath);
            _pickTask?.TrySetResult(importedPath);
        }
    }
}
