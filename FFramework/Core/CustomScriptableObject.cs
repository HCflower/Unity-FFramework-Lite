// =============================================================
// 描述：自定义ScriptableObject(用于扩展编辑器功能)
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.0
// =============================================================
using UnityEngine;

namespace FFramework.Core
{
    /// <summary>
    /// 自定义ScriptableObject(用于扩展编辑器功能)
    /// </summary>
    public class CustomScriptableObject : ScriptableObject
    {
#if UNITY_EDITOR
        [Button("保存资源", ButtonColor.Green)]
        public void SaveAsset()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
        }
#endif
    }
}
