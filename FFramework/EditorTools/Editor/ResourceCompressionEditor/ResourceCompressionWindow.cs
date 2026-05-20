// =============================================================
// 描述：资源压缩工具独立面板入口
// 作者：HCFlower
// 创建时间：2026-05-20
// 版本：1.0.0
// =============================================================
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FFramework.Editor
{
    public class ResourceCompressionWindow : EditorWindow
    {
        [MenuItem("FFramework/资源压缩工具")]
        public static void ShowWindow()
        {
            var window = GetWindow<ResourceCompressionWindow>("资源压缩工具");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexGrow = 1;
            root.Add(ResourceCompressionGUI.CreateCompressionView());
        }
    }
}
