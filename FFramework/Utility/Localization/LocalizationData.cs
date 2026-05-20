// =============================================================
// 描述：本地化数据模型 - 运行时语言数据和字体映射结构
// 作者：HCFlower
// 创建时间：2026-05-18
// 版本：1.0.0
// =============================================================
using System.Collections.Generic;
using UnityEngine;

namespace FFramework.Utility
{
    #region 运行时语言数据

    /// <summary>
    /// 运行时单语言数据
    /// </summary>
    public class LanguageData
    {
        /// <summary>语言代码，如 "zh-CN"</summary>
        public string LanguageCode { get; set; }

        /// <summary>key → 翻译文本 的字典</summary>
        public Dictionary<string, string> Strings { get; set; }

        public LanguageData()
        {
            Strings = new Dictionary<string, string>();
        }

        /// <summary>
        /// 获取翻译文本
        /// </summary>
        /// <param name="key">本地化 Key</param>
        /// <param name="fallback">key 不存在时返回的默认值</param>
        public string GetText(string key, string fallback = null)
        {
            return Strings.TryGetValue(key, out string value) ? value : fallback;
        }

        /// <summary>
        /// 获取翻译文本并格式化
        /// </summary>
        public string GetTextFormat(string key, params object[] args)
        {
            if (Strings.TryGetValue(key, out string value))
            {
                return string.Format(value, args);
            }
            return null;
        }
    }

    /// <summary>
    /// 运行时语言数据集（由 CSV 解析后构建）
    /// 支持按组（Group）动态加载/卸载，适用于多 CSV 按章节管理的场景
    /// </summary>
    public class LocalizationDataSet
    {
        /// <summary>所有已加载的语言数据（语言代码 → LanguageData）</summary>
        public Dictionary<string, LanguageData> Languages { get; private set; }

        /// <summary>语言代码列表（保持 CSV 标题行顺序）</summary>
        public List<string> LanguageCodes { get; private set; }

        /// <summary>所有可用的 Key 列表（保持添加顺序）</summary>
        public List<string> Keys { get; private set; }

        /// <summary>Key 快速查询表（O(1) 查找）</summary>
        private HashSet<string> keyLookup;

        /// <summary>分组索引：groupId → 该组包含的所有 key</summary>
        private Dictionary<string, HashSet<string>> groupKeys;

        public LocalizationDataSet()
        {
            Languages = new Dictionary<string, LanguageData>();
            LanguageCodes = new List<string>();
            Keys = new List<string>();
            keyLookup = new HashSet<string>();
            groupKeys = new Dictionary<string, HashSet<string>>();
        }

        /// <summary>
        /// 获取指定语言的文本
        /// </summary>
        public string GetText(string languageCode, string key, string fallback = null)
        {
            if (Languages.TryGetValue(languageCode, out var langData))
            {
                return langData.GetText(key, fallback);
            }
            return fallback;
        }

        /// <summary>
        /// 检查指定语言中是否有该 key（O(1) 查询）
        /// </summary>
        public bool HasKey(string key)
        {
            return keyLookup.Contains(key);
        }

        /// <summary>
        /// 检查指定语言是否被支持
        /// </summary>
        public bool HasLanguage(string languageCode)
        {
            return Languages.ContainsKey(languageCode);
        }

        #region 分组管理（多 CSV 按需加载/卸载）

        /// <summary>
        /// 将另一个数据集合并到当前数据集中，并按 groupId 记录以便后续卸载
        /// </summary>
        /// <param name="other">要合并的数据集（必须与当前数据集有相同的语言列）</param>
        /// <param name="groupId">组标识，如 "chapter_1"</param>
        public void MergeGroup(LocalizationDataSet other, string groupId)
        {
            if (other == null)
                return;

            // 如果该组已存在，先卸载旧数据
            if (groupKeys.ContainsKey(groupId))
            {
                RemoveGroup(groupId);
            }

            // 如果主数据集还没有语言，先从 other 复制语言列表
            if (LanguageCodes.Count == 0)
            {
                foreach (var langCode in other.LanguageCodes)
                {
                    LanguageCodes.Add(langCode);
                    Languages[langCode] = new LanguageData
                    {
                        LanguageCode = langCode
                    };
                }
            }
            else
            {
                // 验证语言列兼容性：other 必须包含与主数据集完全一致的语言列（顺序可以不同）
                foreach (var langCode in LanguageCodes)
                {
                    if (!other.Languages.ContainsKey(langCode))
                    {
                        Debug.LogError($"[LocalizationDataSet] 组 '{groupId}' 缺少语言列 '{langCode}'，无法合并！" +
                            $"请确保所有 CSV 文件包含相同的语言列。");
                        return;
                    }
                }
            }

            var keySet = new HashSet<string>();

            foreach (var key in other.Keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                keySet.Add(key);
                Keys.Add(key);
                keyLookup.Add(key);

                // 将每个语言的翻译合并到主数据集
                foreach (var langCode in LanguageCodes)
                {
                    if (other.Languages.TryGetValue(langCode, out var otherLangData))
                    {
                        if (otherLangData.Strings.TryGetValue(key, out var value))
                        {
                            Languages[langCode].Strings[key] = value;
                        }
                    }
                }
            }

            groupKeys[groupId] = keySet;
        }

        /// <summary>
        /// 卸载指定组的所有本地化数据
        /// </summary>
        /// <param name="groupId">组标识</param>
        /// <returns>是否成功卸载（false 表示该组不存在）</returns>
        public bool RemoveGroup(string groupId)
        {
            if (!groupKeys.TryGetValue(groupId, out var keys))
                return false;

            foreach (var key in keys)
            {
                Keys.Remove(key);
                keyLookup.Remove(key);

                // 从每种语言的字典中移除
                foreach (var langData in Languages.Values)
                {
                    langData.Strings.Remove(key);
                }
            }

            groupKeys.Remove(groupId);
            return true;
        }

        /// <summary>
        /// 获取指定组包含的所有 key（只读）
        /// </summary>
        public IReadOnlyCollection<string> GetGroupKeys(string groupId)
        {
            if (groupKeys.TryGetValue(groupId, out var keys))
                return keys;
            return System.Array.Empty<string>();
        }

        /// <summary>
        /// 检查指定组是否已加载
        /// </summary>
        public bool IsGroupLoaded(string groupId)
        {
            return groupKeys.ContainsKey(groupId);
        }

        /// <summary>
        /// 获取所有已加载的组 ID
        /// </summary>
        public IEnumerable<string> GetLoadedGroups()
        {
            return groupKeys.Keys;
        }

        #endregion
    }

    #region 数据变更类型

    /// <summary>
    /// 数据变更类型（用于 LocalizationManager.OnDataChanged 事件）
    /// </summary>
    public enum DataChangeType
    {
        /// <summary>加载 CSV 分组</summary>
        Loaded,
        /// <summary>卸载 CSV 分组</summary>
        Unloaded,
    }

    #endregion

    #region 字体映射

    /// <summary>
    /// 语言 → 字体映射条目（用于 Inspector 配置）
    /// </summary>
    [System.Serializable]
    public class LanguageFontMapping
    {
        /// <summary>语言代码，如 "zh-CN"</summary>
        public string languageCode;

        /// <summary>UGUI 字体（Text 组件使用）</summary>
        public Font font;

        /// <summary>TextMeshPro 字体资产</summary>
        public TMPro.TMP_FontAsset tmpFont;
    }

    #endregion

    #endregion
}
