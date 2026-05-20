// =============================================================
// 描述：数据持久化文件夹快捷打开工具
// 作者：HCFlower
// 创建时间：2026-05-20
// 版本：1.0.0
// =============================================================
using UnityEditor;
using UnityEngine;
using System.IO;

namespace FFramework.Editor
{
    /// <summary>
    /// 数据持久化文件夹快捷打开工具。
    /// 提供菜单入口，一键打开 Application.persistentDataPath/SaveData 文件夹，
    /// 方便查看和调试 Model 保存的 JSON 数据文件。
    /// </summary>
    public static class OpenDataPersistenceFolder
    {
        /// <summary>
        /// 打开当前数据持久化根目录
        /// </summary>
        [MenuItem("FFramework/打开数据持久化文件夹", priority = 110)]
        public static void OpenSaveDataFolder()
        {
            string saveDataPath = Path.Combine(Application.persistentDataPath, "SaveData");

            if (Directory.Exists(saveDataPath))
            {
                EditorUtility.RevealInFinder(saveDataPath);
                Debug.Log($"[OpenDataPersistenceFolder] 已打开数据持久化文件夹: {saveDataPath}");
            }
            else
            {
                // 文件夹不存在时，询问是否创建
                bool create = EditorUtility.DisplayDialog(
                    "数据持久化文件夹不存在",
                    $"存档文件夹不存在：\n{saveDataPath}\n\n是否创建并打开？",
                    "创建并打开",
                    "取消"
                );

                if (create)
                {
                    Directory.CreateDirectory(saveDataPath);
                    EditorUtility.RevealInFinder(saveDataPath);
                    Debug.Log($"[OpenDataPersistenceFolder] 已创建并打开数据持久化文件夹: {saveDataPath}");
                }
            }
        }

        /// <summary>
        /// 验证菜单项是否可用（仅在非播放模式下可用）
        /// </summary>
        [MenuItem("FFramework/打开数据持久化文件夹", validate = true)]
        private static bool ValidateOpenSaveDataFolder()
        {
            return !EditorApplication.isPlaying;
        }
    }
}
