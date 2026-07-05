using UnityEditor;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer.Editor
{
    /// <summary>
    /// 编辑器工具：在原点创建立方体并添加 Rigidbody。
    /// </summary>
    public static class CreatePrimitive
    {
        [MenuItem("GameObject/Create Cube With Rigidbody At Origin", false, 0)]
        private static void CreateCubeWithRigidbodyAtOrigin()
        {
            // 在原点创建立方体
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = Vector3.zero;
            cube.name = "Cube_Origin";

            // 添加 Rigidbody
            cube.AddComponent<Rigidbody>();

            // 选中新创建的对象
            Selection.activeGameObject = cube;

            Debug.Log("已在原点创建立方体，并添加 Rigidbody 组件。");
        }
    }
}
