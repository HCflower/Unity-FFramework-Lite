// =============================================================
// 描述：本地化管理器的自定义 Inspector - UI Toolkit 风格
// 作者：HCFlower
// 创建时间：2026-05-18
// 版本：2.1.0
// 修改：
//   v2.1.0 - 优化 Inspector 样式，对齐编辑器风格
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
        private List<string> languageOptions = new List<string>();

        // 配色常量
        private static readonly Color BoxBgColor = new(0.208f, 0.208f, 0.208f); // #353535

        #endregion

        #region UI Toolkit

        public override VisualElement CreateInspectorGUI()
        {
            manager = target as LocalizationManager;
            var root = new VisualElement
            {
                style =
                {
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 2,
                    paddingBottom = 2,
                }
            };

            // 设置 InspectorElement（父级容器）的左右边距为 6px
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var parent = root.parent;
                if (parent != null)
                {
                    parent.style.paddingLeft = 6;
                    parent.style.paddingRight = 6;
                }
            });

            // ==================== 标题 ====================
            var header = new Label("本地化管理器")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    marginBottom = 2,
                    marginTop = 2,
                }
            };
            root.Add(header);

            // ==================== 语言切换（包裹区域） ====================
            root.Add(CreateBox("语言切换", BuildLanguageContent));

            // ==================== 本地化配置（包裹区域，上边距 0） ====================
            root.Add(CreateBox(null, BuildConfigContent));

            // 绑定 serializedObject
            root.Bind(serializedObject);

            // 初始刷新
            RefreshLanguageOptions();

            return root;
        }

        #endregion

        #region 创建圆角描边盒子

        /// <summary>创建一个带黑色描边和圆角的盒子容器</summary>
        private static VisualElement CreateBox(string title, System.Action<VisualElement> buildContent)
        {
            var box = new VisualElement
            {
                style =
                {
                    backgroundColor = BoxBgColor,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 2,
                    marginBottom = 0,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = Color.black,
                    borderBottomColor = Color.black,
                    borderLeftColor = Color.black,
                    borderRightColor = Color.black,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    overflow = Overflow.Hidden,
                }
            };

            if (!string.IsNullOrEmpty(title))
            {
                var header = new Label(title)
                {
                    style =
                    {
                        fontSize = 12,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = Color.white,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        marginLeft = 0,
                        marginRight = 0,
                        marginTop = 0,
                        marginBottom = 4,
                        paddingLeft = 0,
                    }
                };
                box.Add(header);
            }

            buildContent?.Invoke(box);
            return box;
        }

        #endregion

        #region UI 构建

        private void BuildLanguageContent(VisualElement container)
        {
            // ===== 第一行：下拉框 + 刷新按钮 =====
            var topRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 2,
                }
            };
            container.Add(topRow);

            // 下拉框
            languagePopup = new PopupField<string>("语言", languageOptions, 0);
            languagePopup.SetEnabled(false);
            languagePopup.style.flexGrow = 1;
            languagePopup.style.marginRight = 20;
            languagePopup.RegisterValueChangedCallback(evt =>
            {
                if (manager != null && Application.isPlaying && !string.IsNullOrEmpty(evt.newValue))
                {
                    manager.SetLanguage(evt.newValue);
                    UpdateStatus();
                }
            });
            topRow.Add(languagePopup);

            // 刷新按钮
            var refreshBtn = new Button(RefreshLanguageOptions)
            {
                text = "",
                style =
                {
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    width = 20,
                    height = 20,
                    left = -20,
                    flexShrink = 0,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                }
            };
            var refreshIcon = new Image
            {
                image = EditorGUIUtility.IconContent("d_RotateTool").image,
                style =
                {
                    width = 14,
                    height = 14,
                }
            };
            refreshBtn.Add(refreshIcon);
            topRow.Add(refreshBtn);

            // ===== 第二行：状态信息 =====
            var statusRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 2,
                    marginLeft = 2,
                }
            };
            container.Add(statusRow);

            statusLabel = new Label("(运行后显示详细状态)")
            {
                style =
                {
                    fontSize = 11,
                    color = Color.white,
                    whiteSpace = WhiteSpace.Normal,
                    flexGrow = 1,
                }
            };
            statusRow.Add(statusLabel);
        }

        private void BuildConfigContent(VisualElement container)
        {
            var configsField = new PropertyField(serializedObject.FindProperty("configs"), "配置资产列表")
            {
                tooltip = "支持多个 LocalizationConfig 资产，自动合并所有 CSV 组和字体映射",
                style =
                {
                    flexGrow = 1,
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                }
            };

            // 挂载后设置内部样式
            configsField.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                // 折叠标题上边距置 0
                var foldout = configsField.Q<Foldout>();
                if (foldout != null)
                    foldout.style.marginTop = 0;

                // 列表区域左边距
                var listView = configsField.Q<ListView>();
                if (listView != null)
                    listView.style.marginLeft = 16;
            });

            configsField.RegisterValueChangeCallback(evt =>
            {
                serializedObject.ApplyModifiedProperties();
                RefreshLanguageOptions();
            });
            configsField.Bind(serializedObject);
            container.Add(configsField);
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
            if (statusLabel == null)
                return;

            if (manager == null)
            {
                statusLabel.text = "(管理器不可用)";
                return;
            }

            if (Application.isPlaying && manager.DataSet != null)
            {
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
                statusLabel.text = "(运行后显示详细状态)";
            }
        }

        #endregion
    }
}
