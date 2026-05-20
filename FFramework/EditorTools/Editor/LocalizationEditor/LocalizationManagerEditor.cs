// =============================================================
// 描述：本地化管理器的自定义 Inspector - UI Toolkit 风格
// 作者：HCFlower
// 创建时间：2026-05-18
// 版本：2.0.0
// 修改：
//   v2.0.0 - 从 IMGUI (EditorGUILayout) 迁移至 UI Toolkit (VisualElement)
// =============================================================
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;

namespace FFramework.Utility
{
    /// <summary>
    /// LocalizationManager 的自定义 Inspector
    /// 提供语言切换下拉菜单，切换后自动触发所有组件刷新
    /// </summary>
    [CustomEditor(typeof(LocalizationManager))]
    [CanEditMultipleObjects]
    public class LocalizationManagerEditor : UnityEditor.Editor
    {
        #region 字段

        private LocalizationManager manager;
        private PopupField<string> languagePopup;
        private Label statusLabel;
        private Label currentLangLabel;
        private List<string> languageOptions = new List<string>();

        #endregion

        #region UI Toolkit

        public override VisualElement CreateInspectorGUI()
        {
            manager = target as LocalizationManager;
            var root = new VisualElement();

            // ==================== 标题 ====================
            var header = new Label("本地化管理器");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.style.fontSize = 16;
            header.style.marginBottom = 2;
            header.style.marginTop = 2;
            root.Add(header);

            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            separator.style.marginBottom = 4;
            separator.style.marginTop = 4;
            root.Add(separator);

            // ==================== 配置资产 ====================
            BuildConfigReference(root);

            // ==================== 语言切换 ====================
            BuildLanguageSelector(root);

            // ==================== 状态信息 ====================
            BuildStatusInfo(root);

            // 绑定 serializedObject
            root.Bind(serializedObject);

            return root;
        }

        #endregion

        #region UI 构建

        private void BuildConfigReference(VisualElement root)
        {
            var configsField = new PropertyField(serializedObject.FindProperty("configs"), "配置资产列表");
            configsField.tooltip = "支持多个 LocalizationConfig 资产，自动合并所有 CSV 组和字体映射";
            configsField.RegisterValueChangeCallback(evt =>
            {
                serializedObject.ApplyModifiedProperties();
                RefreshLanguageOptions();
            });
            root.Add(configsField);
        }

        private void BuildLanguageSelector(VisualElement root)
        {
            // ===== 标题行（标题 + 当前语言提示 + 刷新按钮） =====
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.marginTop = 6;
            headerRow.style.marginBottom = 2;
            headerRow.style.marginLeft = 4;
            headerRow.style.marginRight = 1;
            root.Add(headerRow);

            var langHeader = new Label("语言切换");
            langHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            langHeader.style.unityTextAlign = TextAnchor.MiddleLeft;
            headerRow.Add(langHeader);

            var rightGroup = new VisualElement();
            rightGroup.style.flexDirection = FlexDirection.Row;
            rightGroup.style.alignItems = Align.Center;
            headerRow.Add(rightGroup);

            currentLangLabel = new Label("当前语言: --");
            currentLangLabel.style.fontSize = 11;
            currentLangLabel.style.marginRight = 6;
            rightGroup.Add(currentLangLabel);

            var refreshBtn = new Button(RefreshLanguageOptions);
            refreshBtn.text = "";
            refreshBtn.style.justifyContent = Justify.Center;
            refreshBtn.style.alignItems = Align.Center;
            var refreshIcon = new Image();
            refreshIcon.image = EditorGUIUtility.IconContent("d_RotateTool").image;
            refreshIcon.style.width = 15;
            refreshIcon.style.height = 15;
            refreshBtn.Add(refreshIcon);
            refreshBtn.style.width = 20;
            refreshBtn.style.height = 20;
            refreshBtn.style.marginRight = 0;
            rightGroup.Add(refreshBtn);

            // ===== 下拉框行（右对齐） =====
            var popupRow = new VisualElement();
            popupRow.style.flexDirection = FlexDirection.RowReverse;
            root.Add(popupRow);

            languagePopup = new PopupField<string>("语言", languageOptions, 0);
            languagePopup.SetEnabled(false);
            languagePopup.style.flexGrow = 1;
            languagePopup.RegisterValueChangedCallback(evt =>
            {
                if (manager != null && Application.isPlaying && !string.IsNullOrEmpty(evt.newValue))
                {
                    manager.SetLanguage(evt.newValue);
                    UpdateStatus();
                }
            });
            popupRow.Add(languagePopup);

            // 初始刷新
            RefreshLanguageOptions();
        }

        private void BuildStatusInfo(VisualElement root)
        {
            var statusHeader = new Label("状态信息");
            statusHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusHeader.style.unityTextAlign = TextAnchor.MiddleLeft;
            statusHeader.style.marginTop = 6;
            statusHeader.style.marginLeft = 4;
            statusHeader.style.marginBottom = 2;
            root.Add(statusHeader);

            statusLabel = new Label("(运行后显示详细状态)");
            statusLabel.style.fontSize = 11;
            statusLabel.style.marginLeft = 4;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(statusLabel);
        }

        #endregion

        #region 逻辑方法

        private void RefreshLanguageOptions()
        {
            if (manager == null)
            {
                manager = target as LocalizationManager;
                if (manager == null) return;
            }

            languageOptions.Clear();
            if (manager.DataSet != null)
            {
                languageOptions.AddRange(manager.GetSupportedLanguages());
            }

            if (languagePopup != null)
            {
                languagePopup.choices = languageOptions;

                bool runtime = Application.isPlaying;
                languagePopup.SetEnabled(runtime && languageOptions.Count > 0);

                if (runtime && languageOptions.Count > 0)
                {
                    string current = manager.CurrentLanguage;
                    int idx = languageOptions.IndexOf(current);
                    if (idx >= 0)
                        languagePopup.index = idx;
                }
            }

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (statusLabel == null || currentLangLabel == null)
                return;

            if (manager == null)
            {
                statusLabel.text = "(管理器不可用)";
                return;
            }

            if (Application.isPlaying && manager.DataSet != null)
            {
                string current = manager.CurrentLanguage ?? "无";
                currentLangLabel.text = $"当前语言: {current}";

                var languages = manager.GetSupportedLanguages();
                var keys = manager.GetAllKeys();
                int fontCount = 0;
                if (manager.Configs != null)
                {
                    foreach (var cfg in manager.Configs)
                        if (cfg != null)
                            fontCount += cfg.FontMappings.Count;
                }
                string fontStatus = fontCount > 0 ? $"{fontCount} 条映射" : "未配置";

                statusLabel.text = $"支持语言: {string.Join(", ", languages)}\n" +
                                   $"条目总数: {keys.Count}\n" +
                                   $"字体配置: {fontStatus}";
            }
            else
            {
                currentLangLabel.text = "当前语言: -- (运行后显示)";
                statusLabel.text = "(运行后显示详细状态)";
            }
        }

        #endregion
    }
}
