// =============================================================
// 描述：本地化管理器 - 加载 CSV、切换语言、文本获取、字体查询
// 作者：HCFlower
// 创建时间：2026-05-18
// 版本：3.1.0
// 修改：
//   v2.0.0 - 改为继承 SingletonMono<T>，支持在 Inspector 中直接拖拽 CSV 和 Config 文件
//   v2.1.0 - 添加 [Button] 编辑器语言切换功能
//   v3.0.0 - 新增多 CSV 分组管理：LoadCsvGroup / UnloadGroup / IsGroupLoaded / GetLoadedGroups
//             ParseCsv 改为 public static，支持从外部加载 CSV
//   v3.1.0 - 移除冗余 loadedGroups 字段，统一委托给 dataSet
//             新增 OnDataChanged 事件（数据变更专用，与语言切换事件分离）
//             修复无 base 组时语言恢复逻辑缺陷
//             优化 StringBuilder 缓存减少 GC
//             精简不必要的 Debug.Log（生产环境静默）
// =============================================================
using System;
using System.Collections.Generic;
using System.Text;
using FFramework.Core;
using UnityEngine;

namespace FFramework.Utility
{
    /// <summary>
    /// 本地化管理器 - MonoBehaviour 单例
    /// 在 Inspector 中拖拽 CSV 文件 和 LocalizationConfig 资产即可使用
    /// 首次通过 Instance 访问时自动创建 GameObject 并初始化
    /// </summary>
    [AddComponentMenu("FFramework/Localization/Localization Manager")]
    public class LocalizationManager : SingletonMono<LocalizationManager>
    {
        #region Inspector 配置

        [Header("本地化配置资产")]
        [SerializeField, Tooltip("拖拽 LocalizationConfig 资产到此处（支持多个配置，自动合并所有 CSV 组和字体映射）")]
        private List<LocalizationConfig> configs = new List<LocalizationConfig>();

        #endregion

        #region 常量

        /// <summary>PlayerPrefs 中保存语言设置的 key</summary>
        private const string PREFS_LANGUAGE_KEY = "FFramework_Localization_Language";

        /// <summary>key 未找到时返回的默认文本</summary>
        private const string DEFAULT_FALLBACK_FORMAT = "[{0}]";

        /// <summary>基础 CSV 加载后自动分配的组 ID</summary>
        private const string BASE_GROUP_ID = "base";

        #endregion

        #region 私有字段

        /// <summary>运行时语言数据集</summary>
        private LocalizationDataSet dataSet;

        /// <summary>当前语言名称</summary>
        private string currentLanguage;

        /// <summary>是否已初始化</summary>
        private bool isInitialized = false;

        #endregion

        #region 公开事件

        /// <summary>
        /// 语言变更事件（参数：新的语言名称）
        /// 仅在 SetLanguage 切换语言时触发
        /// LocalizationComponent 通过此事件自动刷新文本和字体
        /// </summary>
        public event Action<string> OnLanguageChanged;

        /// <summary>
        /// 数据变更事件（参数：变更类型+组ID）
        /// 在 LoadCsv/UnloadGroup 加载或卸载 CSV 分组时触发
        /// LocalizationComponent 通过此事件自动刷新
        /// </summary>
        public event Action<string, DataChangeType> OnDataChanged;

        /// <summary>
        /// 单例初始化完成事件
        /// 用于 LocalizationComponent 在 Manager 延迟初始化时进行延迟绑定
        /// </summary>
        public event Action OnInitialized;

        #endregion

        #region 公开属性

        /// <summary>当前语言名称，如 "ChineseSimplified"</summary>
        public string CurrentLanguage
        {
            get => currentLanguage;
            private set => currentLanguage = value;
        }

        /// <summary>完整语言数据集（只读）</summary>
        public LocalizationDataSet DataSet => dataSet;

        /// <summary>当前语言数据（只读）</summary>
        public LanguageData CurrentData
        {
            get
            {
                if (dataSet != null && !string.IsNullOrEmpty(currentLanguage))
                {
                    dataSet.Languages.TryGetValue(currentLanguage, out var data);
                    return data;
                }
                return null;
            }
        }

        /// <summary>所有本地化配置列表（只读）</summary>
        public IReadOnlyList<LocalizationConfig> Configs => configs;

        /// <summary>主配置（第一个），向后兼容</summary>
        public LocalizationConfig Config => configs != null && configs.Count > 0 ? configs[0] : null;

        /// <summary>是否已完成初始化</summary>
        public bool IsInitialized => isInitialized;

        #endregion

        #region 初始化

        /// <summary>
        /// 单例初始化 - 由 SingletonMono 在首次访问 Instance 时自动调用
        /// 子类可在此处执行初始化逻辑
        /// </summary>
        protected override void InitializeSingleton()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化本地化系统
        /// 1. 从 LocalizationConfig 的 CSV Groups 中加载 base 组（如果存在）
        ///    如果没有 base 组，自动加载配置中的第一个 CSV 组作为初始数据
        /// 2. 恢复上次保存的语言
        /// </summary>
        private void LoadAllConfigs()
        {
            if (configs == null || configs.Count == 0)
            {
                Debug.LogError("[LocalizationManager] 未配置任何 LocalizationConfig！请在 Inspector 中拖拽 LocalizationConfig 资产到 'Configs' 列表。");
                return;
            }

            dataSet = new LocalizationDataSet();

            // 遍历所有配置，合并每个配置中的 CSV 组
            foreach (var cfg in configs)
            {
                if (cfg == null) continue;

                // 尝试加载每个配置中所有 CSV 组
                bool hasGroupLoaded = false;
                foreach (var group in cfg.CsvGroups)
                {
                    if (group == null || group.csvFile == null) continue;
                    var parsed = ParseCsv(group.csvFile.text);
                    if (parsed != null && parsed.LanguageCodes.Count > 0)
                    {
                        dataSet.MergeGroup(parsed, group.groupId);
                        hasGroupLoaded = true;
                    }
                }

                if (!hasGroupLoaded)
                {
                    Debug.Log($"[LocalizationManager] 配置 '{cfg.name}' 没有可用的 CSV 组");
                }
            }
        }

        public void Initialize()
        {
            if (isInitialized)
                return;

            if (configs == null || configs.Count == 0 || configs.TrueForAll(c => c == null))
            {
                Debug.LogError("[LocalizationManager] 未配置任何 LocalizationConfig！请在 Inspector 中拖拽 LocalizationConfig 资产到 'Configs' 列表。");
                return;
            }

            // 创建空数据集并加载所有配置
            LoadAllConfigs();

            // 恢复语言偏好
            string savedLanguage = PlayerPrefs.GetString(PREFS_LANGUAGE_KEY, string.Empty);
            string defaultLanguage = Config != null ? Config.DefaultLanguage : "ChineseSimplified";

            if (dataSet.LanguageCodes.Count > 0)
            {
                // 有已加载的数据，从其中选择语言
                if (!string.IsNullOrEmpty(savedLanguage) && dataSet.HasLanguage(savedLanguage))
                {
                    currentLanguage = savedLanguage;
                }
                else if (dataSet.HasLanguage(defaultLanguage))
                {
                    currentLanguage = defaultLanguage;
                }
                else
                {
                    currentLanguage = dataSet.LanguageCodes[0];
                }
            }
            else
            {
                // 没有任何数据时，将 defaultLanguage 标记为"待定"语言
                // 后续首次 LoadCsvGroup 时会自动设置
                currentLanguage = defaultLanguage;
            }

            isInitialized = true;
            Debug.Log($"[LocalizationManager] 初始化完成，当前语言: {currentLanguage}");

            // 5. 触发语言变更事件（仅在数据就绪时触发）
            if (dataSet != null && dataSet.LanguageCodes.Count > 0)
            {
                OnLanguageChanged?.Invoke(currentLanguage);
            }

            // 6. 触发初始化完成事件（供 LocalizationComponent 延迟绑定使用）
            OnInitialized?.Invoke();
        }

        #endregion

        #region 语言切换

        /// <summary>
        /// 切换当前语言
        /// </summary>
        /// <param name="languageName">语言名称，如 "ChineseSimplified"、"English"</param>
        public void SetLanguage(string languageName)
        {
            if (string.IsNullOrEmpty(languageName))
            {
                Debug.LogError("[LocalizationManager] 语言名称不能为空");
                return;
            }

            if (dataSet == null)
            {
                Debug.LogError("[LocalizationManager] 数据未加载，请先调用 Initialize()");
                return;
            }

            if (!dataSet.HasLanguage(languageName))
            {
                Debug.LogError($"[LocalizationManager] 不支持的语言: {languageName}");
                return;
            }

            if (currentLanguage == languageName)
                return;

            // 更新当前语言
            currentLanguage = languageName;

            // 持久化保存
            PlayerPrefs.SetString(PREFS_LANGUAGE_KEY, languageName);
            PlayerPrefs.Save();

            Debug.Log($"[LocalizationManager] 切换语言至: {languageName}");

            // 触发事件，通知所有 LocalizationComponent
            OnLanguageChanged?.Invoke(languageName);
        }

        #endregion

        #region 文本查询

        /// <summary>
        /// 获取当前语言的翻译文本
        /// </summary>
        /// <param name="key">本地化 Key</param>
        /// <returns>翻译文本，key 不存在时返回 "[key]" 格式的占位符</returns>
        public string GetText(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (CurrentData == null)
                return string.Format(DEFAULT_FALLBACK_FORMAT, key);

            string text = CurrentData.GetText(key);
            return text ?? string.Format(DEFAULT_FALLBACK_FORMAT, key);
        }

        /// <summary>
        /// 获取当前语言的翻译文本并应用格式化参数
        /// </summary>
        /// <param name="key">本地化 Key</param>
        /// <param name="args">格式化参数，对应文本中的 {0}、{1} 等占位符</param>
        /// <returns>格式化后的翻译文本</returns>
        public string GetText(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (CurrentData == null)
                return string.Format(DEFAULT_FALLBACK_FORMAT, key);

            string text = CurrentData.GetText(key);
            if (text == null)
                return string.Format(DEFAULT_FALLBACK_FORMAT, key);

            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(text, args);
                }
                catch (FormatException ex)
                {
                    Debug.LogWarning($"[LocalizationManager] 格式化文本失败 key='{key}', text='{text}': {ex.Message}");
                    return text;
                }
            }

            return text;
        }

        /// <summary>
        /// 获取指定语言的翻译文本
        /// </summary>
        /// <param name="languageName">语言名称</param>
        /// <param name="key">本地化 Key</param>
        /// <param name="fallback">未找到时的默认值</param>
        public string GetText(string languageName, string key, string fallback = null)
        {
            if (dataSet == null)
                return fallback ?? string.Format(DEFAULT_FALLBACK_FORMAT, key);

            return dataSet.GetText(languageName, key, fallback ?? string.Format(DEFAULT_FALLBACK_FORMAT, key));
        }

        /// <summary>
        /// 检查当前语言中是否存在指定 key
        /// </summary>
        public bool HasKey(string key)
        {
            return dataSet != null && dataSet.HasKey(key);
        }

        /// <summary>
        /// 获取所有可用的 Key 列表
        /// </summary>
        public List<string> GetAllKeys()
        {
            return dataSet?.Keys ?? new List<string>();
        }

        /// <summary>
        /// 获取支持的语言名称列表
        /// </summary>
        public List<string> GetSupportedLanguages()
        {
            return dataSet?.LanguageCodes ?? new List<string>();
        }

        #endregion

        #region 字体查询

        /// <summary>
        /// 获取当前语言对应的 UGUI 字体
        /// </summary>
        public Font GetCurrentFont()
        {
            return GetFont(currentLanguage);
        }

        /// <summary>
        /// 获取当前语言对应的 TextMeshPro 字体
        /// </summary>
        public TMPro.TMP_FontAsset GetCurrentTMPFont()
        {
            return GetTMPFont(currentLanguage);
        }

        /// <summary>
        /// 获取指定语言对应的 UGUI 字体
        /// </summary>
        /// <param name="languageName">语言名称，如 "ChineseSimplified"</param>
        public Font GetFont(string languageName)
        {
            if (configs == null) return null;
            foreach (var cfg in configs)
            {
                if (cfg == null) continue;
                var font = cfg.GetFont(languageName);
                if (font != null) return font;
            }
            return null;
        }

        /// <summary>
        /// 获取指定语言对应的 TextMeshPro 字体
        /// 按配置列表顺序查找，先找到的配置优先
        /// </summary>
        /// <param name="languageName">语言名称，如 "ChineseSimplified"</param>
        public TMPro.TMP_FontAsset GetTMPFont(string languageName)
        {
            if (configs == null) return null;
            foreach (var cfg in configs)
            {
                if (cfg == null) continue;
                var font = cfg.GetTMPFont(languageName);
                if (font != null) return font;
            }
            return null;
        }

        #endregion

        #region CSV 分组管理（多 CSV 按需加载/卸载）

        /// <summary>
        /// 从 LocalizationConfig 中加载指定组（按 groupId 查找对应的 CSV 并合并到数据集）
        /// </summary>
        /// <param name="groupId">组标识，如 "chapter_1"、"chapter_2"</param>
        public void LoadCsvGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                Debug.LogError("[LocalizationManager] groupId 不能为空");
                return;
            }

            // 遍历所有配置查找 CSV 组
            CsvGroupEntry entry = null;
            foreach (var cfg in configs)
            {
                if (cfg == null) continue;
                entry = cfg.GetCsvGroup(groupId);
                if (entry != null) break;
            }

            if (entry == null)
            {
                Debug.LogError($"[LocalizationManager] Config 中未找到 groupId='{groupId}' 的 CSV 配置");
                return;
            }

            if (entry.csvFile == null)
            {
                Debug.LogError($"[LocalizationManager] 组 '{groupId}' 的 CSV 文件引用为空，请在 LocalizationConfig 中拖拽文件");
                return;
            }

            LoadCsv(entry.csvFile, groupId);
        }

        /// <summary>
        /// 通过 TextAsset 加载 CSV 数据并合并到指定组
        /// </summary>
        /// <param name="csv">CSV 文件引用</param>
        /// <param name="groupId">组标识</param>
        public void LoadCsv(TextAsset csv, string groupId)
        {
            if (csv == null)
            {
                Debug.LogError("[LocalizationManager] CSV 文件为空");
                return;
            }

            LoadCsv(csv.text, groupId);
        }

        /// <summary>
        /// 通过文本内容加载 CSV 数据并合并到指定组
        /// </summary>
        /// <param name="csvText">CSV 文本内容</param>
        /// <param name="groupId">组标识</param>
        public void LoadCsv(string csvText, string groupId)
        {
            if (string.IsNullOrEmpty(csvText))
            {
                Debug.LogError("[LocalizationManager] CSV 文本内容为空");
                return;
            }

            if (string.IsNullOrEmpty(groupId))
            {
                Debug.LogError("[LocalizationManager] groupId 不能为空");
                return;
            }

            if (dataSet == null)
            {
                Debug.LogError("[LocalizationManager] 数据集未初始化，请先调用 Initialize()");
                return;
            }

            // 解析 CSV
            var parsed = ParseCsv(csvText);
            if (parsed == null || parsed.Keys.Count == 0)
            {
                Debug.LogError($"[LocalizationManager] CSV 解析失败或没有有效数据 (组: {groupId})");
                return;
            }

            // 首次加载 CSV 且 dataSet 还没有数据时，同步语言和语言列表
            if (dataSet.LanguageCodes.Count == 0)
            {
                // dataSet.MergeGroup 会自动从 parsed 复制语言列表
            }

            // 合并到主数据集
            dataSet.MergeGroup(parsed, groupId);

            // 如果当前语言尚未在数据集中，自动选择第一个可用语言
            if (dataSet.LanguageCodes.Count > 0 && !dataSet.HasLanguage(currentLanguage))
            {
                string oldLanguage = currentLanguage;
                currentLanguage = dataSet.LanguageCodes[0];
                Debug.Log($"[LocalizationManager] 当前语言 '{oldLanguage}' 不在已加载数据中，自动切换至 '{currentLanguage}'");
            }

            Debug.Log($"[LocalizationManager] 加载 CSV 组 '{groupId}': {parsed.Keys.Count} 条条目");

            // 触发数据变更事件
            OnDataChanged?.Invoke(groupId, DataChangeType.Loaded);
            // 兼容旧版：如果语言已切换或首次加载数据，触发语言变更事件
            OnLanguageChanged?.Invoke(currentLanguage);
        }

        /// <summary>
        /// 卸载指定组的所有本地化数据
        /// </summary>
        /// <param name="groupId">组标识，如 "chapter_1"</param>
        /// <returns>是否成功卸载（false 表示该组未加载）</returns>
        public bool UnloadGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                Debug.LogError("[LocalizationManager] groupId 不能为空");
                return false;
            }

            if (dataSet == null)
                return false;

            // 不允许卸载 base 组（基础 CSV）
            if (groupId == BASE_GROUP_ID)
            {
                Debug.LogWarning($"[LocalizationManager] 不允许卸载基础组 '{BASE_GROUP_ID}'");
                return false;
            }

            if (!dataSet.RemoveGroup(groupId))
            {
                Debug.LogWarning($"[LocalizationManager] 组 '{groupId}' 未加载，无法卸载");
                return false;
            }

            Debug.Log($"[LocalizationManager] 卸载 CSV 组 '{groupId}'");

            // 触发数据变更事件
            OnDataChanged?.Invoke(groupId, DataChangeType.Unloaded);
            // 兼容旧版：维持 OnLanguageChanged 调用
            OnLanguageChanged?.Invoke(currentLanguage);
            return true;
        }

        /// <summary>
        /// 检查指定组是否已加载（委托给 DataSet）
        /// </summary>
        /// <param name="groupId">组标识</param>
        public bool IsGroupLoaded(string groupId)
        {
            return dataSet != null && dataSet.IsGroupLoaded(groupId);
        }

        /// <summary>
        /// 检查指定 key 是否属于指定分组
        /// </summary>
        /// <param name="key">本地化 Key</param>
        /// <param name="groupId">组标识</param>
        /// <returns>key 属于该组返回 true；组未加载或 key 不在组内返回 false</returns>
        public bool HasKeyInGroup(string key, string groupId)
        {
            if (dataSet == null || string.IsNullOrEmpty(groupId))
                return false;
            // 遍历组内所有 key 比对
            foreach (var k in dataSet.GetGroupKeys(groupId))
            {
                if (k == key) return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有已加载的组 ID（委托给 DataSet）
        /// </summary>
        public string[] GetLoadedGroups()
        {
            if (dataSet == null)
                return System.Array.Empty<string>();

            var groups = dataSet.GetLoadedGroups();
            var result = new List<string>(groups);
            return result.ToArray();
        }

        #endregion

        #region CSV 解析

        /// <summary>
        /// 解析 CSV 文本为 LocalizationDataSet
        /// CSV 格式：第一行为标题 key,lang1,lang2,...，后续每行为一条翻译
        /// 支持双引号包裹含逗号的值
        /// </summary>
        public static LocalizationDataSet ParseCsv(string csvText)
        {
            if (string.IsNullOrEmpty(csvText))
                return null;

            var dataSet = new LocalizationDataSet();

            // 按行分割（支持 \r\n 和 \n）
            string[] lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool isHeader = true;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string rawLine = lines[lineIndex].Trim();
                if (string.IsNullOrEmpty(rawLine))
                    continue;

                // 以 # 开头的行是注释，跳过
                if (rawLine.StartsWith("#"))
                    continue;

                // 解析 CSV 行（支持引号）
                List<string> parts = ParseCsvLine(rawLine);
                if (parts.Count < 2)
                    continue;

                if (isHeader)
                {
                    // 第一行：key, lang1, lang2, ...
                    for (int i = 1; i < parts.Count; i++)
                    {
                        string langName = parts[i].Trim();
                        if (string.IsNullOrEmpty(langName))
                            continue;

                        dataSet.LanguageCodes.Add(langName);
                        dataSet.Languages[langName] = new LanguageData
                        {
                            LanguageCode = langName
                        };
                    }

                    isHeader = false;
                    Debug.Log($"[LocalizationManager] CSV 解析: 发现 {dataSet.LanguageCodes.Count} 种语言 - {string.Join(", ", dataSet.LanguageCodes)}");
                }
                else
                {
                    // 数据行
                    string key = parts[0].Trim();
                    if (string.IsNullOrEmpty(key))
                        continue;

                    dataSet.Keys.Add(key);

                    for (int i = 0; i < dataSet.LanguageCodes.Count && i + 1 < parts.Count; i++)
                    {
                        string langName = dataSet.LanguageCodes[i];
                        string value = parts[i + 1].Trim();
                        dataSet.Languages[langName].Strings[key] = value;
                    }
                }
            }

            return dataSet;
        }

        /// <summary>
        /// 缓存 StringBuilder 实例，减少大型 CSV 解析时的 GC 分配
        /// </summary>
        [ThreadStatic]
        private static StringBuilder csvBuilderCache;

        /// <summary>
        /// 获取缓存的 StringBuilder
        /// </summary>
        private static StringBuilder GetCsvStringBuilder()
        {
            if (csvBuilderCache == null)
                csvBuilderCache = new StringBuilder(256);
            else
                csvBuilderCache.Clear();
            return csvBuilderCache;
        }

        /// <summary>
        /// 解析单行 CSV，支持双引号包裹含逗号的值
        /// 例如: key,"Hello, world!",value3 → ["key", "Hello, world!", "value3"]
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            StringBuilder current = GetCsvStringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // 处理转义的双引号 ""
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // 跳过下一个引号
                    }
                    else
                    {
                        // 切换引号状态
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // 逗号分隔符（不在引号内）
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // 添加最后一个字段
            result.Add(current.ToString());

            return result;
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 单例销毁时清理
        /// </summary>
        protected override void OnDestroy()
        {
            OnLanguageChanged = null;
            OnDataChanged = null;
            OnInitialized = null;
            dataSet = null;
            configs = null;
            isInitialized = false;
            base.OnDestroy();
        }

        #endregion
    }
}
