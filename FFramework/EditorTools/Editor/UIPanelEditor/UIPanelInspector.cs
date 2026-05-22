// =============================================================
// 描述：UI检查器面板 - Lite版（仅保留UI事件检查器）
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：2.0.0
// =============================================================
using System.Collections.Generic;
using UnityEngine.EventSystems;
using FFramework.Utility;
using UnityEngine.Events;
using System.Reflection;
using UnityEngine.UI;
using UnityEditor;
using System.Linq;
using UnityEngine;
using System;
using TMPro;

namespace FFramework.Editor
{
    [CustomEditor(typeof(UIPanel), true)]
    public class UIPanelInspector : UnityEditor.Editor
    {
        #region 私有字段
        private UIPanel panel;
        private bool showSummary = false;
        #endregion

        #region Unity 内部方法

        // UIToolkit 入口
        public override UnityEngine.UIElements.VisualElement CreateInspectorGUI()
        {
            panel = (UIPanel)target;
            var root = new UnityEngine.UIElements.VisualElement();
            root.style.flexDirection = UnityEngine.UIElements.FlexDirection.Column;
            root.style.paddingLeft = 2;
            root.style.paddingRight = 2;
            root.style.paddingTop = 2;
            root.style.paddingBottom = 2;

            if (panel == null)
            {
                root.Add(new UnityEngine.UIElements.HelpBox("面板为空。", UnityEngine.UIElements.HelpBoxMessageType.Error));
                return root;
            }

            // 默认序列化属性显示 + 脚本行内置锚点按钮
            var defaultInspector = new UnityEngine.UIElements.IMGUIContainer(() =>
            {
                serializedObject.Update();
                SerializedProperty iterator = serializedObject.GetIterator();
                bool enterChildren = true;

                // 第一项 m_Script — 压灰显示 + 右侧操作按钮
                if (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    SerializedProperty sp = iterator.Copy();

                    EditorGUILayout.BeginHorizontal();

                    // 脚本字段灰显只读
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.PropertyField(sp, true);
                    EditorGUI.EndDisabledGroup();

                    // 打开脚本
                    if (GUILayout.Button(
                        EditorGUIUtility.IconContent("d_cs Script Icon"),
                        GUILayout.Width(22),
                        GUILayout.Height(18)))
                    {
                        if (panel != null)
                        {
                            var script = MonoScript.FromMonoBehaviour(panel);
                            if (script != null) AssetDatabase.OpenAsset(script);
                        }
                    }

                    // 设置对象名称为类名
                    if (GUILayout.Button(
                        EditorGUIUtility.IconContent("editicon.sml"),
                        GUILayout.Width(22),
                        GUILayout.Height(18)))
                    {
                        if (panel != null)
                        {
                            Undo.RecordObject(panel.gameObject, "设置面板名");
                            panel.gameObject.name = panel.GetType().Name;
                            EditorUtility.SetDirty(panel.gameObject);
                        }
                    }

                    // 锚点全覆盖
                    if (GUILayout.Button(
                        EditorGUIUtility.IconContent("RectTransformBlueprint"),
                        GUILayout.Width(22),
                        GUILayout.Height(18)))
                    {
                        if (panel != null && panel.transform is RectTransform rect)
                        {
                            Undo.RecordObject(rect, "RectTransform锚点全覆盖");
                            rect.anchorMin = Vector2.zero;
                            rect.anchorMax = Vector2.one;
                            rect.offsetMin = Vector2.zero;
                            rect.offsetMax = Vector2.zero;
                            EditorUtility.SetDirty(rect);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }

                // 其余字段正常可编辑
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    SerializedProperty sp = iterator.Copy();
                    EditorGUILayout.PropertyField(sp, true);
                    GUILayout.Space(2);
                }

                serializedObject.ApplyModifiedProperties();
            });
            defaultInspector.style.marginLeft = 0;
            defaultInspector.style.marginRight = 0;
            root.Add(defaultInspector);

            // 底部：UI 事件检查器（UIToolkit 版本）
            var summaryRoot = BuildSummarySectionUI();
            summaryRoot.style.marginLeft = -13;
            summaryRoot.style.marginRight = -6;
            root.Add(summaryRoot);

            return root;
        }

        #endregion

        #region UIToolkit Summary 区域构建

        /// <summary>
        /// 构建 Summary 区域（UIToolkit 版本）
        /// </summary>
        private UnityEngine.UIElements.VisualElement BuildSummarySectionUI()
        {
            var container = new UnityEngine.UIElements.VisualElement();
            container.style.marginTop = 2;
            container.style.marginLeft = -13;
            container.style.marginRight = -6;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new Color(0, 0, 0, 0.2f);
            container.style.borderBottomColor = new Color(0, 0, 0, 0.2f);
            container.style.borderLeftColor = new Color(0, 0, 0, 0.2f);
            container.style.borderRightColor = new Color(0, 0, 0, 0.2f);

            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;

            var contentRoot = new UnityEngine.UIElements.VisualElement();
            contentRoot.style.display = showSummary ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;

            var titleButton = new UnityEngine.UIElements.Button();
            titleButton.style.height = 22;
            titleButton.text = (showSummary ? "<color=yellow>▼ </color>" : "► ") + "UI事件检查器";
            titleButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleButton.style.marginLeft = 0;
            titleButton.style.marginRight = 0;
            titleButton.style.marginTop = 0;
            titleButton.style.marginBottom = 0;
            titleButton.style.borderTopWidth = 0;
            titleButton.style.borderLeftWidth = 0;
            titleButton.style.borderRightWidth = 0;

            titleButton.style.borderBottomWidth = showSummary ? 1 : 0;
            titleButton.style.borderBottomColor = new Color(0, 0, 0, 0.2f);

            titleButton.clicked += () =>
            {
                showSummary = !showSummary;
                titleButton.text = (showSummary ? "<color=yellow>▼ </color>" : "► ") + "UI事件检查器";
                titleButton.style.borderBottomWidth = showSummary ? 1 : 0;
                contentRoot.style.display = showSummary ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
                contentRoot.style.paddingLeft = 0;
                contentRoot.style.paddingRight = 0;
                contentRoot.style.paddingTop = 0;
                contentRoot.style.paddingBottom = 0;

                if (showSummary)
                {
                    titleButton.style.borderBottomLeftRadius = 0;
                    titleButton.style.borderBottomRightRadius = 0;
                    contentRoot.Clear();
                    BuildComponentsListUI(contentRoot);
                }
                else
                {
                    titleButton.style.borderBottomLeftRadius = 4;
                    titleButton.style.borderBottomRightRadius = 4;
                }

                contentRoot.style.marginLeft = 0;
                contentRoot.style.marginRight = 0;
                contentRoot.style.marginTop = 0;
                contentRoot.style.marginBottom = 0;
            };

            container.Add(titleButton);
            container.Add(contentRoot);

            if (showSummary)
            {
                BuildComponentsListUI(contentRoot);
            }

            return container;
        }

        /// <summary>
        /// 组件列表（UIToolkit）
        /// </summary>
        private void BuildComponentsListUI(UnityEngine.UIElements.VisualElement parent)
        {
            var allUIComponents = GetAllUIComponentsWithEvents();

            if (allUIComponents.Count > 0)
            {
                // 用box包裹整个区域
                var box = new UnityEngine.UIElements.VisualElement();
                box.style.marginTop = 0;
                box.style.marginLeft = 0;
                box.style.marginRight = 0;
                box.style.marginBottom = 0;
                box.style.paddingLeft = 0;
                box.style.paddingRight = 0;
                box.style.borderTopLeftRadius = 0;
                box.style.borderTopRightRadius = 0;
                box.style.borderBottomLeftRadius = 4;
                box.style.borderBottomRightRadius = 4;
                box.style.borderTopWidth = 1;
                box.style.borderBottomWidth = 1;
                box.style.borderLeftWidth = 1;
                box.style.borderRightWidth = 1;
                box.style.borderTopColor = new Color(0, 0, 0, 0.5f);
                box.style.borderBottomColor = new Color(0, 0, 0, 0.5f);
                box.style.borderLeftColor = new Color(0, 0, 0, 0.5f);
                box.style.borderRightColor = new Color(0, 0, 0, 0.5f);
                box.style.backgroundColor = new Color(1, 1, 1, 0.04f);

                var title = new UnityEngine.UIElements.Label($"已绑定事件的UI组件 ({allUIComponents.Count})");
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.marginTop = 2;
                title.style.marginLeft = 3;
                title.style.marginRight = 0;
                title.style.marginBottom = 2;
                box.Add(title);

                foreach (var uiComponent in allUIComponents)
                {
                    DrawUIComponentItemCompactUI(box, uiComponent);
                }

                parent.Add(box);
            }
            else
            {
                var HelpBox = new UnityEngine.UIElements.HelpBox("无已绑定事件的UI组件", UnityEngine.UIElements.HelpBoxMessageType.Info);
                HelpBox.style.marginTop = 0;
                HelpBox.style.marginLeft = 0;
                HelpBox.style.marginRight = 0;
                HelpBox.style.marginBottom = 0;
                HelpBox.style.paddingLeft = 0;
                HelpBox.style.paddingRight = 0;
                HelpBox.style.borderTopLeftRadius = 0;
                HelpBox.style.borderTopRightRadius = 0;
                HelpBox.style.flexGrow = 1;
                parent.Add(HelpBox);
            }
        }

        /// <summary>
        /// UIToolkit 版紧凑组件项
        /// </summary>
        private void DrawUIComponentItemCompactUI(UnityEngine.UIElements.VisualElement parent, UIComponentInfo uiComponent)
        {
            var item = new UnityEngine.UIElements.VisualElement();
            item.style.flexDirection = UnityEngine.UIElements.FlexDirection.Column;
            item.style.marginTop = 0;
            item.style.marginBottom = 2;
            item.style.marginLeft = 2;
            item.style.marginRight = 2;

            item.style.paddingLeft = 0;
            item.style.paddingRight = 0;
            item.style.paddingBottom = 2;
            item.style.borderTopWidth = 1;
            item.style.borderBottomWidth = 1;
            item.style.borderLeftWidth = 1;
            item.style.borderRightWidth = 1;

            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            item.style.borderTopColor = new Color(0, 0, 0, 0.2f);
            item.style.borderBottomColor = new Color(0, 0, 0, 0.2f);
            item.style.borderLeftColor = new Color(0, 0, 0, 0.2f);
            item.style.borderRightColor = new Color(0, 0, 0, 0.2f);

            var row = new UnityEngine.UIElements.VisualElement();
            row.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
            row.style.alignItems = UnityEngine.UIElements.Align.Center;
            row.style.justifyContent = UnityEngine.UIElements.Justify.Center;
            item.Add(row);

            var typeLabel = new UnityEngine.UIElements.Label($"[{uiComponent.ComponentType}]");
            typeLabel.style.color = uiComponent.TypeColor;
            typeLabel.style.minWidth = 90;
            row.Add(typeLabel);

            string displayName = uiComponent.Component.gameObject.name;
            if (displayName.Length > 20)
                displayName = displayName.Substring(0, 17) + "...";

            var nameLabel = new UnityEngine.UIElements.Label(displayName);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.minWidth = 140;
            row.Add(nameLabel);

            var countLabel = new UnityEngine.UIElements.Label($"事件:{uiComponent.ListenerCount}");
            countLabel.style.minWidth = 50;
            row.Add(countLabel);

            var flex = new UnityEngine.UIElements.VisualElement();
            flex.style.flexGrow = 1;
            row.Add(flex);

            var selectBtn = CreateIconButton("d_scenevis_visible_hover", "选中对象", () =>
            {
                Selection.activeObject = uiComponent.Component.gameObject;
                EditorGUIUtility.PingObject(uiComponent.Component.gameObject);
            }, 22, 22);
            selectBtn.style.marginLeft = 1;
            selectBtn.style.marginRight = 1;
            selectBtn.style.marginTop = 1;
            row.Add(selectBtn);

            var detailBtn = CreateIconButton("d_UnityEditor.InspectorWindow", "查看详情", () =>
            {
                ShowComponentDetail(uiComponent);
            }, 22, 22);
            detailBtn.style.marginLeft = 1;
            detailBtn.style.marginRight = 1;
            detailBtn.style.marginTop = 1;
            row.Add(detailBtn);

            if (ShouldShowPath(uiComponent.Component.gameObject))
            {
                var pathRow = new UnityEngine.UIElements.VisualElement();
                pathRow.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
                pathRow.style.marginTop = 2;

                var spacer = new UnityEngine.UIElements.VisualElement();
                spacer.style.width = 10;
                pathRow.Add(spacer);

                string shortPath = GetShortPath(uiComponent.Component.gameObject);
                var pathLabel = new UnityEngine.UIElements.Label($"路径: {shortPath}");
                pathRow.Add(pathLabel);

                item.Add(pathRow);
            }

            parent.Add(item);
        }

        /// <summary>
        /// 在弹窗中显示组件详细的事件绑定信息
        /// </summary>
        private void ShowComponentDetail(UIComponentInfo uiComponent)
        {
            if (uiComponent == null || uiComponent.Component == null) return;

            string title = $"组件详情: {uiComponent.Component.name} ({uiComponent.ComponentType})";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"对象路径: {GetGameObjectPath(uiComponent.Component.gameObject)}");
            sb.AppendLine($"监听器总数: {uiComponent.ListenerCount}");
            sb.AppendLine();
            sb.AppendLine("--- 事件详情 ---");

            void AppendEventInfo(string eventName, UnityEventBase unityEvent)
            {
                if (unityEvent == null) return;
                int persistent = unityEvent.GetPersistentEventCount();
                int runtime = GetRuntimeListenerCount(unityEvent);
                if (persistent + runtime == 0) return;

                sb.AppendLine($"[{eventName}] (持久:{persistent}, 运行时:{runtime}):");
                for (int i = 0; i < persistent; i++)
                {
                    string target = unityEvent.GetPersistentTarget(i) != null ? unityEvent.GetPersistentTarget(i).name : "Null";
                    string method = unityEvent.GetPersistentMethodName(i);
                    sb.AppendLine($"  • [P] {target}.{method}");
                }
                if (runtime > 0) sb.AppendLine($"  • [R] 存在 {runtime} 个运行时监听器");
            }

            var comp = uiComponent.Component;
            if (comp is Button b) AppendEventInfo("onClick", b.onClick);
            else if (comp is Toggle t) AppendEventInfo("onValueChanged", t.onValueChanged);
            else if (comp is Slider s) AppendEventInfo("onValueChanged", s.onValueChanged);
            else if (comp is InputField i) { AppendEventInfo("onValueChanged", i.onValueChanged); AppendEventInfo("onEndEdit", i.onEndEdit); }
            else if (comp is TMP_InputField ti) { AppendEventInfo("onValueChanged", ti.onValueChanged); AppendEventInfo("onEndEdit", ti.onEndEdit); }
            else if (comp is Dropdown d) AppendEventInfo("onValueChanged", d.onValueChanged);
            else if (comp is TMP_Dropdown td) AppendEventInfo("onValueChanged", td.onValueChanged);
            else if (comp is ScrollRect sr) AppendEventInfo("onValueChanged", sr.onValueChanged);
            else if (comp is EventTrigger et)
            {
                foreach (var entry in et.triggers)
                    sb.AppendLine($"  • [Trigger] {entry.eventID} ({entry.callback.GetPersistentEventCount()}个持久监听)");
            }

            EditorUtility.DisplayDialog(title, sb.ToString(), "关闭");
        }

        #endregion

        #region 辅助方法 - 组件公用逻辑

        /// <summary>
        /// 判断是否需要显示路径
        /// </summary>
        private bool ShouldShowPath(GameObject obj)
        {
            return obj.transform.parent != panel.transform;
        }

        /// <summary>
        /// 获取简短的路径
        /// </summary>
        private string GetShortPath(GameObject obj)
        {
            string fullPath = GetGameObjectPath(obj);

            string[] pathParts = fullPath.Split('/');
            if (pathParts.Length > 2)
            {
                return ".../" + pathParts[pathParts.Length - 2] + "/" + pathParts[pathParts.Length - 1];
            }

            return fullPath;
        }

        /// <summary>
        /// 获取GameObject的层级路径
        /// </summary>
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null && parent != panel.transform)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        #endregion

        #region UI 辅助方法

        /// <summary>
        /// 创建带图标的 UIToolkit 按钮
        /// </summary>
        private UnityEngine.UIElements.Button CreateIconButton(
            string iconName,
            string tooltip,
            Action onClick,
            float width,
            float height,
            ScaleMode scaleMode = ScaleMode.ScaleToFit,
            Color? iconColor = null)
        {
            var button = new UnityEngine.UIElements.Button(() =>
            {
                onClick?.Invoke();
            });
            button.tooltip = tooltip;
            button.style.width = width;
            button.style.height = height;

            button.style.paddingLeft = 2;
            button.style.paddingRight = 2;
            button.style.paddingTop = 1;
            button.style.paddingBottom = 1;

            button.style.marginTop = 0;
            button.style.marginBottom = 0;

            button.style.justifyContent = UnityEngine.UIElements.Justify.Center;
            button.style.alignItems = UnityEngine.UIElements.Align.Center;

            var content = EditorGUIUtility.IconContent(iconName);
            if (content != null && content.image != null)
            {
                var image = new UnityEngine.UIElements.Image
                {
                    image = content.image,
                    scaleMode = scaleMode
                };
                image.style.width = width - 6;
                image.style.height = height - 6;
                image.style.alignSelf = UnityEngine.UIElements.Align.Center;
                image.style.justifyContent = UnityEngine.UIElements.Justify.Center;
                if (iconColor.HasValue)
                    image.tintColor = iconColor.Value;
                button.Add(image);
            }

            return button;
        }

        #endregion

        #region 数据结构
        /// <summary>
        /// UI组件信息数据结构
        /// </summary>
        private class UIComponentInfo
        {
            public Component Component;
            public string ComponentType;
            public int ListenerCount;
            public Color TypeColor;
        }
        #endregion

        #region 数据收集方法
        /// <summary>
        /// 获取所有有事件绑定的UI组件
        /// </summary>
        private List<UIComponentInfo> GetAllUIComponentsWithEvents()
        {
            var result = new List<UIComponentInfo>();

            // Buttons
            AddComponentsWithEvents<Button>(result, "Button", Color.cyan, GetListenerCount_Button);
            // Toggles
            AddComponentsWithEvents<Toggle>(result, "Toggle", Color.green, GetListenerCount_Toggle);
            // Sliders
            AddComponentsWithEvents<Slider>(result, "Slider", new Color(0.8f, 0.6f, 0.2f), GetListenerCount_Slider);
            // InputFields
            AddComponentsWithEvents<InputField>(result, "InputField", Color.magenta, GetListenerCount_InputField);
            // Dropdowns
            AddComponentsWithEvents<Dropdown>(result, "Dropdown", Color.yellow, GetListenerCount_Dropdown);
            // ScrollRects
            AddComponentsWithEvents<ScrollRect>(result, "ScrollRect", Color.gray, GetListenerCount_ScrollRect);
            // EventTriggers
            AddEventTriggersWithEvents(result);

            return result;
        }

        /// <summary>
        /// 添加有事件的组件到结果列表
        /// </summary>
        private void AddComponentsWithEvents<T>(List<UIComponentInfo> result, string typeName, Color typeColor,
            Func<T, int> getListenerCount) where T : Component
        {
            var components = FetchComponents<T>();
            foreach (var component in components)
            {
                int count = getListenerCount(component);
                if (count > 0)
                {
                    result.Add(new UIComponentInfo
                    {
                        Component = component,
                        ComponentType = typeName,
                        ListenerCount = count,
                        TypeColor = typeColor
                    });
                }
            }
        }

        /// <summary>
        /// 添加有事件的EventTrigger组件
        /// </summary>
        private void AddEventTriggersWithEvents(List<UIComponentInfo> result)
        {
            var eventTriggers = FetchComponents<EventTrigger>();
            foreach (var trigger in eventTriggers)
            {
                int count = (trigger.triggers != null && trigger.triggers.Count > 0) ? trigger.triggers.Count : 0;
                if (count > 0)
                {
                    result.Add(new UIComponentInfo
                    {
                        Component = trigger,
                        ComponentType = "EventTrigger",
                        ListenerCount = count,
                        TypeColor = Color.red
                    });
                }
            }
        }

        /// <summary>
        /// 获取指定类型的组件列表
        /// </summary>
        private List<T> FetchComponents<T>() where T : Component
        {
            if (panel == null) return new List<T>();
            return panel.GetComponentsInChildren<T>(true).ToList();
        }
        #endregion

        #region 监听器计数方法
        private int GetListenerCount_Button(Button b)
        {
            if (b == null) return 0;
            return b.onClick.GetPersistentEventCount() + GetRuntimeListenerCount(b.onClick);
        }

        private int GetListenerCount_Toggle(Toggle t)
        {
            if (t == null) return 0;
            return t.onValueChanged.GetPersistentEventCount() + GetRuntimeListenerCount(t.onValueChanged);
        }

        private int GetListenerCount_Slider(Slider s)
        {
            if (s == null) return 0;
            return s.onValueChanged.GetPersistentEventCount() + GetRuntimeListenerCount(s.onValueChanged);
        }

        private int GetListenerCount_InputField(InputField i)
        {
            if (i == null) return 0;
            return i.onValueChanged.GetPersistentEventCount() + GetRuntimeListenerCount(i.onValueChanged) +
                   i.onEndEdit.GetPersistentEventCount() + GetRuntimeListenerCount(i.onEndEdit);
        }

        private int GetListenerCount_Dropdown(Dropdown d)
        {
            if (d == null) return 0;
            return d.onValueChanged.GetPersistentEventCount() + GetRuntimeListenerCount(d.onValueChanged);
        }

        private int GetListenerCount_ScrollRect(ScrollRect sr)
        {
            if (sr == null) return 0;
            return sr.onValueChanged.GetPersistentEventCount() + GetRuntimeListenerCount(sr.onValueChanged);
        }

        /// <summary>
        /// 通过反射获取运行时监听器数量
        /// </summary>
        private int GetRuntimeListenerCount(UnityEventBase unityEvent)
        {
            if (unityEvent == null) return 0;

            try
            {
                var field = typeof(UnityEventBase).GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var callsObject = field.GetValue(unityEvent);
                    if (callsObject != null)
                    {
                        var runtimeCallsField = callsObject.GetType().GetField("m_RuntimeCalls", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (runtimeCallsField != null)
                        {
                            var runtimeCalls = runtimeCallsField.GetValue(callsObject) as System.Collections.IList;
                            return runtimeCalls?.Count ?? 0;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"获取运行时监听器数量失败: {e.Message}");
            }

            return 0;
        }
        #endregion
    }
}
