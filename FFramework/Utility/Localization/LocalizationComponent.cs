// =============================================================
// 描述：本地化 UI 组件 - 拖拽到 UI 对象上,自动更新文本和字体
// 作者：HCFlower
// 创建时间：2026-05-18
// 版本：1.1.0
// 修改：
//   v1.1.0 - 自动加载失败时降级写入占位符
//             UpdateText 简化 formatArgs 转换
//             新增延迟绑定机制（支持 Manager 异步初始化场景）
//             精简 Debug.Log 日志（正常流程静默）
//             修复 OnEnable/OnDisable 配对逻辑
// =============================================================
using UnityEngine.UI;
using UnityEngine;
using System;
using TMPro;

namespace FFramework.Utility
{
    /// <summary>
    /// 本地化 UI 组件
    /// 拖拽到任意 GameObject 上,配置本地化 Key 即可自动监听语言变化并更新文本和字体
    /// 支持 UGUI Text 和 TextMeshProUGUI 两种组件
    /// </summary>
    [AddComponentMenu("FFramework/UI/Localization Component")]
    public class LocalizationComponent : MonoBehaviour
    {
        #region Inspector 配置

        [SerializeField, Tooltip("本地化 Key,对应 CSV 中的 key 列")]
        private string localizationKey;

        [SerializeField, Tooltip("CSV 组 ID(可选),当 Key 在当前数据中不存在时，自动加载此组\n例如:\"chapter_1\"、\"dialogue_forest\"")]
        private string groupId;

        [SerializeField, Tooltip("启用后会对翻译文本应用 string.Format 格式化")]
        private bool enableFormatting = false;

        [SerializeField, Tooltip("格式化参数数组,对应文本中的 {0}、{1} 等占位符")]
        private string[] formatArgs;

        /// <summary>获取或设置格式化参数数组（设置后自动启用格式化并刷新文本）</summary>
        public string[] FormatArgs
        {
            get => formatArgs;
            set
            {
                formatArgs = value;
                enableFormatting = true;
                Refresh();
            }
        }

        /// <summary>
        /// 设置指定索引位置的格式化参数并立即刷新文本
        /// </summary>
        /// <param name="index">占位符索引，对应 {0}、{1} 等</param>
        /// <param name="value">要设置的值</param>
        public void SetFormatArg(int index, string value)
        {
            if (formatArgs == null)
                formatArgs = new string[index + 1];
            else if (index >= formatArgs.Length)
                Array.Resize(ref formatArgs, index + 1);

            formatArgs[index] = value;
            enableFormatting = true;
            Refresh();
        }

        /// <summary>
        /// 批量设置格式化参数并立即刷新文本
        /// </summary>
        /// <param name="args">格式化参数，对应文本中的 {0}、{1} 等占位符</param>
        public void SetFormatArgs(params string[] args)
        {
            formatArgs = args;
            enableFormatting = true;
            Refresh();
        }

        [SerializeField, Tooltip("UGUI Text 组件(未指定则自动查找)")]
        private Text textComponent;

        [SerializeField, Tooltip("TextMeshPro 组件(未指定则自动查找)")]
        private TextMeshProUGUI tmpComponent;

        #endregion

        #region 运行时属性

        /// <summary>本地化 Key</summary>
        public string LocalizationKey
        {
            get => localizationKey;
            set
            {
                if (localizationKey != value)
                {
                    localizationKey = value;
                    Refresh();
                }
            }
        }

        /// <summary>是否已绑定到管理器事件</summary>
        public bool IsBound { get; private set; }

        /// <summary>是否已注册延迟绑定（防止重复注册）</summary>
        private bool hasPendingBind = false;

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            AutoFindComponents();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void Start()
        {
            if (!IsBound)
            {
                // OnEnable 时未完成绑定 → 尝试延迟绑定
                TryDelayedBind();
            }
            else
            {
                // 已绑定，刷新一次确保显示最新文本
                Refresh();
            }
        }

        private void OnDisable()
        {
            Unbind();
            UnregisterDelayedBind();
        }

        private void OnDestroy()
        {
            Unbind();
            UnregisterDelayedBind();
        }

        #endregion

        #region 组件查找

        private void AutoFindComponents()
        {
            if (textComponent == null)
                textComponent = GetComponent<Text>();

            if (tmpComponent == null)
                tmpComponent = GetComponent<TextMeshProUGUI>();
        }

        #endregion

        #region 绑定 / 解绑

        /// <summary>
        /// 绑定到本地化管理器的语言变更事件
        /// </summary>
        public void Bind()
        {
            if (IsBound)
                return;

            var manager = LocalizationManager.Instance;
            if (manager == null)
            {
                // Manager 尚未初始化，注册延迟绑定
                TryDelayedBind();
                return;
            }

            // 同时绑定语言变更和数据变更事件
            manager.OnLanguageChanged += OnLanguageChanged;
            manager.OnDataChanged += OnDataChanged;
            IsBound = true;

            // 绑定后立即刷新
            Refresh();
        }

        /// <summary>
        /// 从本地化管理器的事件解绑
        /// </summary>
        public void Unbind()
        {
            if (!IsBound)
                return;

            var manager = LocalizationManager.Instance;
            if (manager != null)
            {
                manager.OnLanguageChanged -= OnLanguageChanged;
                manager.OnDataChanged -= OnDataChanged;
            }

            IsBound = false;
        }

        /// <summary>
        /// 重新绑定(修改 Key 后调用)
        /// </summary>
        public void Rebind()
        {
            Unbind();
            Bind();
        }

        #endregion

        #region 延迟绑定（支持 Manager 异步初始化/复用场景）

        /// <summary>
        /// 尝试注册延迟绑定：监听 Manager 的 OnInitialized 事件
        /// </summary>
        private void TryDelayedBind()
        {
            if (hasPendingBind)
                return;

            var manager = LocalizationManager.Instance;
            if (manager == null)
                return;

            if (!manager.IsInitialized)
            {
                manager.OnInitialized += OnManagerInitialized;
                hasPendingBind = true;
            }
            else
            {
                // Manager 已初始化但 Bind 失败（如当时 dataSet 为空）
                Bind();
            }
        }

        /// <summary>
        /// 取消延迟绑定注册
        /// </summary>
        private void UnregisterDelayedBind()
        {
            if (!hasPendingBind)
                return;

            var manager = LocalizationManager.Instance;
            if (manager != null)
            {
                manager.OnInitialized -= OnManagerInitialized;
            }
            hasPendingBind = false;
        }

        /// <summary>
        /// Manager 初始化完成回调：执行绑定并刷新
        /// </summary>
        private void OnManagerInitialized()
        {
            UnregisterDelayedBind();
            Bind();
        }

        #endregion

        #region 事件回调

        /// <summary>
        /// 语言变更回调(由 LocalizationManager.OnLanguageChanged 触发)
        /// </summary>
        private void OnLanguageChanged(string languageCode)
        {
            Refresh();
        }

        /// <summary>
        /// 数据变更回调(由 LocalizationManager.OnDataChanged 触发)
        /// </summary>
        private void OnDataChanged(string groupId, DataChangeType changeType)
        {
            // 数据变更后仅当组件配置了该 groupId 时才刷新
            if (!string.IsNullOrEmpty(this.groupId) && this.groupId == groupId)
            {
                Refresh();
            }
        }

        #endregion

        #region 刷新

        /// <summary>
        /// 强制刷新文本和字体
        /// 如果 Key 不存在且配置了 groupId，会自动加载对应的 CSV 组
        /// </summary>
        [ContextMenu("强制刷新")]
        public void Refresh()
        {
            if (string.IsNullOrEmpty(localizationKey))
                return;

            var manager = LocalizationManager.Instance;
            if (manager == null)
                return;

            // === 自动加载逻辑 ===
            bool keyMissing = manager.DataSet == null || !manager.HasKey(localizationKey);
            if (keyMissing && !string.IsNullOrEmpty(groupId))
            {
                if (!manager.IsGroupLoaded(groupId))
                {
                    manager.LoadCsvGroup(groupId);
                    // LoadCsvGroup 会触发 OnDataChanged → 回到 Refresh
                    // 但加载可能失败，fallthrough 显示占位符
                }
            }

            if (manager.DataSet == null || manager.CurrentData == null)
                return;

            // === 组范围限制 ===
            // 如果配置了 groupId，只显示该组内的 Key
            if (!string.IsNullOrEmpty(groupId))
            {
                if (!manager.HasKeyInGroup(localizationKey, groupId))
                {
                    // Key 不属于配置的组，显示占位符
                    string fallback = $"[{localizationKey}]";
                    if (textComponent != null) textComponent.text = fallback;
                    if (tmpComponent != null) tmpComponent.text = fallback;
                    return;
                }
            }

            // 即使 keyMissing 为 true 且加载失败，也 fallthrough 显示占位符
            UpdateText(manager);
            UpdateFont(manager);
        }

        /// <summary>
        /// 更新文本组件的显示内容
        /// </summary>
        private void UpdateText(LocalizationManager manager)
        {
            string text;

            if (enableFormatting && formatArgs != null && formatArgs.Length > 0)
            {
                text = manager.GetText(localizationKey, Array.ConvertAll(formatArgs, s => (object)s));
            }
            else
            {
                text = manager.GetText(localizationKey);
            }

            if (textComponent != null)
                textComponent.text = text;

            if (tmpComponent != null)
                tmpComponent.text = text;
        }

        /// <summary>
        /// 更新文本组件的字体(随语言切换)
        /// </summary>
        private void UpdateFont(LocalizationManager manager)
        {
            string currentLanguage = manager.CurrentLanguage;

            // 更新 UGUI Text 字体
            if (textComponent != null)
            {
                Font font = manager.GetFont(currentLanguage);
                if (font != null)
                {
                    textComponent.font = font;
                }
                else
                {
                    Debug.LogWarning($"[LocalizationComponent] 语言 '{currentLanguage}' 未配置 UGUI 字体," +
                        $"请在 LocalizationConfig 资产的 FontMappings 中设置。");
                }
            }

            // 更新 TextMeshPro 字体
            if (tmpComponent != null)
            {
                TMP_FontAsset tmpFont = manager.GetTMPFont(currentLanguage);
                if (tmpFont != null)
                {
                    tmpComponent.font = tmpFont;
                }
                else
                {
                    Debug.LogWarning($"[LocalizationComponent] 语言 '{currentLanguage}' 未配置 TMP 字体," +
                        $"请在 LocalizationConfig 资产的 FontMappings 中为 '{currentLanguage}' 设置 TMP 字体," +
                        $"否则中文等字符可能无法正确显示(将显示为 □ 方框)。");
                }
            }
        }

        #endregion
    }
}
