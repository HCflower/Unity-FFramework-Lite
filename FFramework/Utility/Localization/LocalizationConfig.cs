// =============================================================
// 描述：本地化配置资产 - 定义支持的语言列表、默认语言、字体映射、CSV 分组
// 作者：HCFlower
// 创建时间：2026-05-18
// 版本：2.1.0
// 修改：
//   v2.0.0 - 新增 CsvGroupEntry 列表，支持多 CSV 按组管理
//   v2.1.0 - 字体查询添加 Dictionary 缓存，O(1) 查找；OnEnable/OnValidate 自动重建缓存
// =============================================================
using System.Collections.Generic;
using UnityEngine;

namespace FFramework.Utility
{
    /// <summary>
    /// 本地化配置资产
    /// 在 Project 窗口中右键 → Create → FFramework → Localization → LocalizationConfig 创建
    /// </summary>
    [CreateAssetMenu(menuName = "FFramework/Localization/LocalizationConfig", fileName = "LocalizationConfig")]
    public class LocalizationConfig : ScriptableObject
    {
        #region Inspector 配置

        [Header("本地化基本配置")]
        [SerializeField, Tooltip("默认语言名称，与 CSV 标题列对应，如 ChineseSimplified")]
        private string defaultLanguage = "ChineseSimplified";

        [Header("CSV 数据文件组（按需加载/卸载）")]
        [SerializeField, Tooltip("定义所有 CSV 数据文件组，每个组包含 groupId 和对应的 CSV 文件引用")]
        private List<CsvGroupEntry> csvGroups = new List<CsvGroupEntry>();

        [Header("字体映射（语言名称 → 对应字体）")]
        [SerializeField, Tooltip("为每种语言配置对应的 UGUI 字体和 TMP 字体")]
        private List<LanguageFontMapping> fontMappings = new List<LanguageFontMapping>();

        #endregion

        #region 字体查找缓存

        /// <summary>字体映射缓存（语言代码 → 映射条目，O(1) 查找）</summary>
        private Dictionary<string, LanguageFontMapping> fontMappingCache;

        /// <summary>
        /// 重建字体查找缓存
        /// </summary>
        private void RebuildFontCache()
        {
            if (fontMappingCache == null)
                fontMappingCache = new Dictionary<string, LanguageFontMapping>();
            else
                fontMappingCache.Clear();

            foreach (var mapping in fontMappings)
            {
                if (!string.IsNullOrEmpty(mapping.languageCode) && !fontMappingCache.ContainsKey(mapping.languageCode))
                {
                    fontMappingCache[mapping.languageCode] = mapping;
                }
                else if (!string.IsNullOrEmpty(mapping.languageCode))
                {
                    Debug.LogWarning($"[LocalizationConfig] 发现重复的语言代码 '{mapping.languageCode}'，仅保留第一个映射。");
                }
            }
        }

        #endregion

        #region Unity 生命周期

        private void OnEnable()
        {
            RebuildFontCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 编辑器内 Inspector 修改时自动重建缓存
            RebuildFontCache();
        }
#endif

        #endregion

        #region 公开属性

        /// <summary>默认语言名称</summary>
        public string DefaultLanguage => defaultLanguage;

        /// <summary>字体映射列表（只读）</summary>
        public IReadOnlyList<LanguageFontMapping> FontMappings => fontMappings;

        /// <summary>CSV 分组列表（只读）</summary>
        public IReadOnlyList<CsvGroupEntry> CsvGroups => csvGroups;

        #endregion

        #region CSV 分组查询

        /// <summary>
        /// 根据 groupId 获取对应的 CSV 分组条目
        /// </summary>
        /// <param name="groupId">组标识，如 "base"、"story_act1"</param>
        /// <returns>找到的条目，未找到返回 null</returns>
        public CsvGroupEntry GetCsvGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return null;

            return csvGroups.Find(g => g.groupId == groupId);
        }

        /// <summary>
        /// 检查指定 groupId 是否在配置中定义
        /// </summary>
        public bool HasCsvGroup(string groupId)
        {
            return GetCsvGroup(groupId) != null;
        }

        #endregion

        #region 字体查询（带缓存）

        /// <summary>
        /// 获取指定语言对应的 UGUI 字体（缓存加速，O(1) 查找）
        /// </summary>
        /// <param name="languageName">语言名称，如 "ChineseSimplified"</param>
        public Font GetFont(string languageName)
        {
            if (string.IsNullOrEmpty(languageName))
                return null;

            if (fontMappingCache == null)
                RebuildFontCache();

            return fontMappingCache.TryGetValue(languageName, out var mapping) ? mapping.font : null;
        }

        /// <summary>
        /// 获取指定语言对应的 TextMeshPro 字体资产（缓存加速，O(1) 查找）
        /// </summary>
        /// <param name="languageName">语言名称，如 "ChineseSimplified"</param>
        public TMPro.TMP_FontAsset GetTMPFont(string languageName)
        {
            if (string.IsNullOrEmpty(languageName))
                return null;

            if (fontMappingCache == null)
                RebuildFontCache();

            return fontMappingCache.TryGetValue(languageName, out var mapping) ? mapping.tmpFont : null;
        }

        #endregion

        #region 编辑器辅助

#if UNITY_EDITOR
        /// <summary>
        /// 在 Inspector 中通过 ContextMenu 更新字体映射列表
        /// </summary>
        [ContextMenu("同步字体映射到支持的语言列表")]
        private void SyncFontMappings()
        {
            Debug.Log("[LocalizationConfig] 请手动添加字体映射条目");
        }
#endif

        #endregion
    }

    #region CSV 分组条目

    /// <summary>
    /// CSV 数据文件分组条目
    /// 用于在 LocalizationConfig 中定义多个 CSV 文件，按 groupId 区分
    /// 在 LocalizationComponent 中配置 groupId 即可自动加载对应的 CSV 组
    /// </summary>
    [System.Serializable]
    public class CsvGroupEntry
    {
        /// <summary>组标识，如 "base"、"story_act1"、"dialogue_forest"</summary>
        public string groupId;

        /// <summary>CSV 文件引用（TextAsset）</summary>
        public TextAsset csvFile;
    }

    #endregion
}
