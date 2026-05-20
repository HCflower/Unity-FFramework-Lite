// =============================================================
// 描述：本地化组件的自定义 Inspector - UI Toolkit 风格
// 作者：HCFlower
// 创建时间：2026-05-18
// 版本：2.0.0
// 修改：
//   v2.0.0 - 从 IMGUI (EditorGUILayout) 迁移至 UI Toolkit (VisualElement)
//   v1.1.0 - LocalizationManager 改用 SingletonMono
// =============================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using FFramework.Editor;
using UnityEditor;
using UnityEngine;

namespace FFramework.Utility
{
    [CustomEditor(typeof(LocalizationComponent))]
    [CanEditMultipleObjects]
    public class LocalizationComponentEditor : UnityEditor.Editor
    {
        #region 字段

        private LocalizationComponent component;
        private VisualElement dropdownContainer;
        private ListView keyListView;
        private TextField searchField;
        private List<string> filteredKeys = new List<string>();
        private bool showKeyDropdown = false;
        private Label previewTextLabel;
        private Label languageLabel;
        private VisualElement previewSection;

        // 组 ID 下拉列表
        private PopupField<string> groupPopup;
        private List<string> groupOptions = new List<string>();

        #endregion

        #region UI Toolkit

        public override VisualElement CreateInspectorGUI()
        {
            component = target as LocalizationComponent;
            var root = new VisualElement();
            root.style.marginLeft = -10;

            // ==================== 本地化配置 ====================
            BuildKeySection(root);

            // ==================== CSV 组配置 ====================
            BuildGroupSection(root);

            // ==================== 格式化参数 ====================
            BuildFormattingSection(root);

            // ==================== 组件引用 ====================
            BuildComponentReferences(root);

            // ==================== 预览 ====================
            BuildPreviewSection(root);

            // 绑定 serializedObject
            root.Bind(serializedObject);

            // 刷新 Key 列表（运行时自动加载）
            if (Application.isPlaying)
                RefreshKeyList();

            return root;
        }

        /// <summary>
        /// 创建分区容器（圆角背景 + 内边距 + 底部间距）
        /// </summary>
        private VisualElement CreateSectionContainer()
        {
            var container = new VisualElement();
            container.style.marginBottom = 4;
            container.style.paddingTop = 5;
            container.style.paddingBottom = 5;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            // #404040
            container.style.backgroundColor = new Color(0.251f, 0.251f, 0.251f);
            container.style.borderWidthAll(1);
            container.style.borderColorAll(Color.black);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            return container;
        }

        #endregion

        #region UI 构建

        private void BuildKeySection(VisualElement root)
        {
            var container = CreateSectionContainer();

            // 标题行：标题左对齐 + 操作按钮右对齐
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 1;

            var header = new Label("本地化配置");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(header);

            // 右侧按钮组（使用 Unity 内置图标）
            var btnGroup = new VisualElement();
            btnGroup.style.flexDirection = FlexDirection.Row;

            var toggleBtn = new Button(() =>
            {
                showKeyDropdown = !showKeyDropdown;
                if (showKeyDropdown)
                {
                    RefreshKeyList();
                    dropdownContainer.style.display = DisplayStyle.Flex;
                }
                else
                {
                    dropdownContainer.style.display = DisplayStyle.None;
                }
            });
            toggleBtn.tooltip = "展开 Key 列表";
            toggleBtn.style.backgroundImage = EditorGUIUtility.IconContent("d_ListView").image as Texture2D;
            toggleBtn.style.width = 20;
            toggleBtn.style.height = 20;
            toggleBtn.style.backgroundSize = new BackgroundSize(new Length(80, LengthUnit.Percent), new Length(80, LengthUnit.Percent));
            btnGroup.Add(toggleBtn);

            var refreshKeysBtn = new Button(RefreshKeyList);
            refreshKeysBtn.tooltip = "刷新 Key 列表";
            refreshKeysBtn.style.backgroundImage = EditorGUIUtility.IconContent("d_Refresh").image as Texture2D;
            refreshKeysBtn.style.width = 20;
            refreshKeysBtn.style.height = 20;
            refreshKeysBtn.style.marginLeft = 2;
            refreshKeysBtn.style.backgroundSize = new BackgroundSize(new Length(80, LengthUnit.Percent), new Length(80, LengthUnit.Percent));
            btnGroup.Add(refreshKeysBtn);

            var forceRefreshBtn = new Button(ForceRefreshComponent);
            forceRefreshBtn.tooltip = "强制刷新组件";
            forceRefreshBtn.style.backgroundImage = EditorGUIUtility.IconContent("d_RotateTool").image as Texture2D;
            forceRefreshBtn.style.width = 20;
            forceRefreshBtn.style.height = 20;
            forceRefreshBtn.style.marginLeft = 2;
            forceRefreshBtn.style.backgroundSize = new BackgroundSize(new Length(70, LengthUnit.Percent), new Length(70, LengthUnit.Percent));
            btnGroup.Add(forceRefreshBtn);

            headerRow.Add(btnGroup);
            container.Add(headerRow);

            // Key 输入行
            var keyRow = new VisualElement();
            keyRow.style.flexDirection = FlexDirection.Row;

            var keyField = new PropertyField(serializedObject.FindProperty("localizationKey"), "");
            keyField.style.flexGrow = 1;
            keyField.RegisterValueChangeCallback(evt => UpdatePreview());
            keyRow.Add(keyField);

            container.Add(keyRow);

            // 下拉列表容器
            dropdownContainer = new VisualElement();
            dropdownContainer.style.display = DisplayStyle.None;
            dropdownContainer.style.marginTop = 2;
            dropdownContainer.style.marginBottom = 2;
            dropdownContainer.style.borderWidthAll(1.5f);
            dropdownContainer.style.borderColorAll(new Color(0.145f, 0.145f, 0.145f));
            dropdownContainer.style.borderRadiusAll(4);

            // 搜索框
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.alignItems = Align.Center;

            var searchLabel = new Label("搜索:");
            searchLabel.style.width = 40;
            searchLabel.style.marginLeft = 5;
            searchRow.Add(searchLabel);

            searchField = new TextField();
            searchField.style.flexGrow = 1;
            searchField.style.flexShrink = 1;
            searchField.style.height = 18;
            searchField.style.marginRight = 0;
            searchField.RegisterValueChangedCallback(evt => RefreshKeyList());
            searchRow.Add(searchField);

            var clearBtn = new Button(() =>
            {
                searchField.value = string.Empty;
                searchField.Focus();
            });
            clearBtn.text = "X";
            clearBtn.style.width = 20;
            clearBtn.style.height = 18;
            clearBtn.style.marginLeft = 2;
            clearBtn.style.marginRight = 1;
            clearBtn.style.flexShrink = 0;
            searchRow.Add(clearBtn);

            dropdownContainer.Add(searchRow);

            // Key 列表
            keyListView = new ListView(filteredKeys, 20, () =>
            {
                var label = new Label();
                label.style.paddingLeft = 4;
                label.style.paddingTop = 2;
                label.style.paddingBottom = 2;
                return label;
            },
            (elem, index) =>
            {
                var label = elem as Label;
                if (index < 0 || index >= filteredKeys.Count) return;
                label.text = filteredKeys[index];
            });
            keyListView.style.maxHeight = 250;
            keyListView.selectionType = SelectionType.Single;
            keyListView.selectionChanged += (selected) =>
            {
                foreach (var item in selected)
                {
                    string key = item as string;
                    if (!string.IsNullOrEmpty(key))
                    {
                        serializedObject.FindProperty("localizationKey").stringValue = key;
                        serializedObject.ApplyModifiedProperties();
                        showKeyDropdown = false;
                        dropdownContainer.style.display = DisplayStyle.None;
                        UpdatePreview();
                        // 运行时自动刷新组件，无需手动点击强制刷新
                        if (Application.isPlaying && component != null)
                            component.Refresh();
                    }
                }
            };

            dropdownContainer.Add(keyListView);
            container.Add(dropdownContainer);
            root.Add(container);
        }

        private void BuildGroupSection(VisualElement root)
        {
            var container = CreateSectionContainer();

            var header = new Label("CSV 组配置（可选）");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 2;
            container.Add(header);

            // 从 Manager 或 Config 中获取可用组列表
            RefreshGroupOptions();

            // 获取当前已选中的组 ID
            string currentGroup = serializedObject.FindProperty("groupId")?.stringValue ?? "";
            int defaultIdx = groupOptions.IndexOf(currentGroup);
            if (defaultIdx < 0) defaultIdx = 0;

            groupPopup = new PopupField<string>("Group Id", groupOptions, defaultIdx);
            groupPopup.tooltip = "当 Key 在当前数据中不存在时，自动加载此 CSV 组\n需与 LocalizationConfig 中定义的 groupId 一致";
            groupPopup.style.marginLeft = 15;

            // 如果当前 groupId 为空但有可用选项，自动写入第一个选项值
            if (string.IsNullOrEmpty(currentGroup) && groupOptions.Count > 0 && !groupOptions[0].StartsWith("(无"))
            {
                var prop = serializedObject.FindProperty("groupId");
                if (prop != null)
                {
                    prop.stringValue = groupOptions[0];
                    serializedObject.ApplyModifiedProperties();
                }
            }
            // 如果当前值不在下拉选项中，显示已输入的值
            else if (!groupOptions.Contains(currentGroup) && !string.IsNullOrEmpty(currentGroup))
            {
                var listWithCustom = new List<string>(groupOptions);
                listWithCustom.Insert(0, currentGroup + " (自定义)");
                groupPopup.choices = listWithCustom;
                groupPopup.index = 0;
            }

            groupPopup.RegisterValueChangedCallback(evt =>
            {
                string selected = evt.newValue;
                if (selected.EndsWith(" (自定义)"))
                    selected = selected.Replace(" (自定义)", "");

                var prop = serializedObject.FindProperty("groupId");
                if (prop != null)
                {
                    prop.stringValue = selected;
                    serializedObject.ApplyModifiedProperties();
                }

                // 运行时自动加载组数据并刷新组件
                if (Application.isPlaying && component != null)
                {
                    var mgr = LocalizationManager.Instance;
                    if (mgr != null && !mgr.IsGroupLoaded(selected))
                        mgr.LoadCsvGroup(selected);
                    component.Refresh();
                    UpdatePreview();
                }
            });

            container.Add(groupPopup);
            root.Add(container);
        }

        /// <summary>
        /// 刷新组下拉列表的选项
        /// </summary>
        private void RefreshGroupOptions()
        {
            groupOptions.Clear();

            // 优先从运行时的 Manager 获取（遍历所有 Config）
            var manager = LocalizationManager.Instance;
            if (manager != null && manager.Configs != null)
            {
                foreach (var cfg in manager.Configs)
                {
                    if (cfg == null) continue;
                    foreach (var g in cfg.CsvGroups)
                        if (!string.IsNullOrEmpty(g.groupId))
                            groupOptions.Add(g.groupId);
                }
                if (groupOptions.Count > 0) return;
            }

            // 运行时 Manager 不可用时，从 Assets 搜索所有 Config
            var guids = AssetDatabase.FindAssets("t:LocalizationConfig");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(path);
                if (config == null) continue;
                foreach (var g in config.CsvGroups)
                    if (!string.IsNullOrEmpty(g.groupId))
                        groupOptions.Add(g.groupId);
            }

            // 添加入口选项
            if (groupOptions.Count == 0)
                groupOptions.Add("(无可用组)");
        }

        private void BuildFormattingSection(VisualElement root)
        {
            var container = CreateSectionContainer();

            // 标题行：标题左对齐 + checkbox 右对齐
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.marginBottom = 2;

            var header = new Label("格式化参数(可选)");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(header);

            var enableField = new PropertyField(serializedObject.FindProperty("enableFormatting"), "");
            enableField.style.flexDirection = FlexDirection.RowReverse;
            enableField.style.flexGrow = 0;
            enableField.style.width = 20;
            enableField.style.marginRight = 2;
            headerRow.Add(enableField);

            container.Add(headerRow);

            var formatArgsField = new PropertyField(serializedObject.FindProperty("formatArgs"), "参数列表");
            var enableProp = serializedObject.FindProperty("enableFormatting");
            formatArgsField.SetEnabled(enableProp.boolValue);
            formatArgsField.style.marginLeft = 15;
            container.Add(formatArgsField);

            container.TrackPropertyValue(enableProp, prop =>
            {
                formatArgsField.SetEnabled(prop.boolValue);
            });

            root.Add(container);
        }

        private void BuildComponentReferences(VisualElement root)
        {
            var container = CreateSectionContainer();

            var header = new Label("组件引用（自动查找，可手动覆盖）");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 2;
            container.Add(header);

            container.Add(new PropertyField(serializedObject.FindProperty("textComponent"), "Text (UGUI)"));
            container.Add(new PropertyField(serializedObject.FindProperty("tmpComponent"), "TextMeshPro"));

            root.Add(container);
        }

        private void BuildPreviewSection(VisualElement root)
        {
            previewSection = CreateSectionContainer();

            var previewHeader = new Label("预览");
            previewHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewSection.Add(previewHeader);

            languageLabel = new Label("当前语言: 未加载");
            languageLabel.style.fontSize = 11;
            languageLabel.style.marginTop = 2;
            languageLabel.style.color = new Color(1f, 0.65f, 0f);
            previewSection.Add(languageLabel);

            previewTextLabel = new Label("(请输入本地化 Key)");
            previewTextLabel.style.whiteSpace = WhiteSpace.Normal;
            previewTextLabel.style.marginTop = 2;
            previewTextLabel.style.color = new Color(1f, 0.65f, 0f);
            previewSection.Add(previewTextLabel);

            root.Add(previewSection);
        }

        #endregion

        #region 逻辑方法

        private void RefreshKeyList()
        {
            var manager = LocalizationManager.Instance;
            if (manager != null && manager.DataSet != null)
            {
                var allKeys = manager.GetAllKeys();
                string search = searchField?.value ?? string.Empty;
                if (string.IsNullOrEmpty(search))
                {
                    filteredKeys = new List<string>(allKeys);
                }
                else
                {
                    filteredKeys = allKeys.Where(k =>
                        k.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }
            }
            else
            {
                filteredKeys.Clear();
            }

            if (keyListView != null)
            {
                keyListView.itemsSource = filteredKeys;
                keyListView.Rebuild();
            }
        }

        private void ForceRefreshComponent()
        {
            if (component != null && Application.isPlaying)
            {
                component.Refresh();
            }
            else
            {
                Debug.Log("[LocalizationComponentEditor] 刷新仅运行时可用");
            }
        }

        private void UpdatePreview()
        {
            if (previewTextLabel == null || languageLabel == null)
                return;

            string key = serializedObject.FindProperty("localizationKey")?.stringValue;
            if (string.IsNullOrEmpty(key))
            {
                previewTextLabel.text = "(请输入本地化 Key)";
                languageLabel.text = "当前语言: 未加载";
                return;
            }

            var manager = LocalizationManager.Instance;
            if (manager == null || manager.DataSet == null)
            {
                previewTextLabel.text = $"[{key}] (管理器未初始化)";
                languageLabel.text = "当前语言: 未加载";
                return;
            }

            languageLabel.text = $"当前语言: {manager.CurrentLanguage}";

            if (!manager.HasKey(key))
            {
                previewTextLabel.text = $"[{key}] (Key 不存在)";
                return;
            }

            string text = manager.GetText(key);

            var enableFormattingProp = serializedObject.FindProperty("enableFormatting");
            var formatArgsProp = serializedObject.FindProperty("formatArgs");
            if (enableFormattingProp != null && enableFormattingProp.boolValue &&
                formatArgsProp != null && formatArgsProp.arraySize > 0)
            {
                object[] args = new object[formatArgsProp.arraySize];
                for (int i = 0; i < formatArgsProp.arraySize; i++)
                {
                    args[i] = formatArgsProp.GetArrayElementAtIndex(i).stringValue;
                }
                try
                {
                    text = string.Format(text, args);
                }
                catch { }
            }

            previewTextLabel.text = text;
        }

        #endregion
    }
}
