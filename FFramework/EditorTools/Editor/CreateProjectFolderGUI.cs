// =============================================================
// 描述：用于在Unity编辑器中创建和管理项目文件夹结构的工具，支持自定义文件夹结构和预设模板。
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：2.0.0
// 更新记录：Lite版 - 独立工具面板
// =============================================================
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace FFramework.Editor
{
    public class CreateProjectFolderGUI : EditorWindow
    {
        #region 面板入口

        [MenuItem("FFramework/创建文件夹工具")]
        public static void ShowWindow()
        {
            var window = GetWindow<CreateProjectFolderGUI>("创建文件夹工具");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexGrow = 1;
            root.Add(CreateFoldersView());
        }

        #endregion

        #region 数据结构与静态字段

        [System.Serializable]
        public class FolderItem
        {
            public string name;
            public bool enabled = true;
            public List<FolderItem> children = new List<FolderItem>();
            public bool foldout = true;
            public FolderItem(string name) { this.name = name; }
        }

        public class FolderPreset { public string Name; public List<string> Paths = new List<string>(); }

        private static List<FolderItem> folderItems = new List<FolderItem>();
        private static string gameRootName = "Game";
        private static string singlePathInput = "";
        private static string selectedPresetKey = "默认游戏项目";
        private static Dictionary<string, FolderPreset> presets = new Dictionary<string, FolderPreset>();

        #endregion

        /// <summary>
        /// 构建文件夹管理视图
        /// </summary>
        public static VisualElement CreateFoldersView()
        {
            if (presets.Count == 0) InitializePresets();
            if (folderItems.Count == 0) LoadPreset(selectedPresetKey);

            var root = new VisualElement();
            root.style.flexGrow = 1;

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = scrollView.style.paddingRight = 5;
            root.Add(scrollView);

            // --- 根目录设置 ---
            var rootGroup = CreateGroup("根目录设置");
            var rootField = new TextField("根文件夹名称:") { value = gameRootName };
            rootField.labelElement.style.minWidth = 80;
            rootField.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootField.RegisterValueChangedCallback(evt => gameRootName = evt.newValue);
            rootGroup.Add(rootField);
            var rootHelp = new HelpBox($"所有文件夹将在 Assets/{gameRootName}/ 下创建", HelpBoxMessageType.Info);
            rootGroup.Add(rootHelp);
            scrollView.Add(rootGroup);

            // --- 预设与追加 ---
            var presetGroup = CreateGroup("预设选择与追加");

            // 创建水平排列的预设行
            var presetRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var presetPopup = new PopupField<string>("选择预设", presets.Keys.ToList(), selectedPresetKey);
            presetPopup.labelElement.style.minWidth = 60;
            presetPopup.style.width = 180;
            presetPopup.style.marginRight = 1;
            presetPopup.style.flexGrow = 1;
            presetRow.Add(presetPopup);

            // 应用预设按钮 (图标化)
            var applyBtn = new Button(() =>
            {
                LoadPreset(presetPopup.value); RefreshFolderList(scrollView);
            });
            applyBtn.tooltip = "应用选中预设";
            applyBtn.style.marginLeft = 0;
            applyBtn.style.marginRight = 0;
            ApplyIconButtonStyle(applyBtn, "d_Refresh");
            presetRow.Add(applyBtn);

            // 清空结构按钮 (图标化)
            var clearBtn = new Button(() =>
            {
                folderItems.Clear(); RefreshFolderList(scrollView);
            });
            clearBtn.tooltip = "清空当前列表";
            clearBtn.style.marginLeft = 0;
            clearBtn.style.marginRight = 0;
            ApplyIconButtonStyle(clearBtn, "TreeEditor.Trash");
            presetRow.Add(clearBtn);

            presetGroup.Add(presetRow);

            // 路径追加行保持不变
            var pathRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            var pathInput = new TextField("追加路径:") { value = singlePathInput, style = { flexGrow = 1 } };
            pathInput.labelElement.style.minWidth = 60;
            pathInput.style.marginRight = 0;

            // 路径输入提示
            var addBtn = new Button(() =>
            {
                if (!string.IsNullOrWhiteSpace(pathInput.value))
                {
                    AddPathToTree(folderItems, pathInput.value.Trim());
                    pathInput.value = "";
                    RefreshFolderList(scrollView);
                }
            });
            addBtn.tooltip = "追加路径";
            addBtn.style.marginLeft = 0;
            addBtn.style.marginRight = 0;
            // 使用 Unity 内置图标
            ApplyIconButtonStyle(addBtn, "d_winbtn_mac_max_h");

            pathRow.Add(pathInput);
            pathRow.Add(addBtn);

            presetGroup.Add(pathRow);
            scrollView.Add(presetGroup);

            // --- 文件夹结构预览 (整体包裹) ---
            var structureGroup = CreateGroup("文件夹结构预览");

            var structureFoldout = new Foldout
            {
                text = "勾选需要创建的文件夹 (点击展开/收起)",
                value = false // 默认折叠
            };
            structureFoldout.name = "FolderStructureFoldout";

            // 样式优化：移除 Foldout 默认的大边距，融入 Group 容器
            structureFoldout.style.marginTop = 0;
            structureFoldout.style.marginBottom = 0;

            // 调整 Foldout 内部容器的间距
            structureFoldout.contentContainer.style.marginTop = 5;
            structureFoldout.contentContainer.style.marginLeft = 5;
            structureFoldout.contentContainer.name = "FolderStructureGroup";

            BuildFolderTreeUI(structureFoldout.contentContainer);

            structureGroup.Add(structureFoldout);
            scrollView.Add(structureGroup);

            // --- 底部操作 ---
            var createBtn = new Button(() => CreateAllFoldersLogic()) { text = "开始创建项目结构" };
            createBtn.style.height = 28;
            createBtn.style.marginLeft = 0;
            createBtn.style.marginTop = 2;
            createBtn.style.marginRight = 0;
            createBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            scrollView.Add(createBtn);

            return root;
        }

        /// <summary>
        /// 统一样式的图标按钮助手方法
        /// </summary>
        private static void ApplyIconButtonStyle(Button button, string iconName)
        {
            button.text = string.Empty;
            button.style.width = 24;
            button.style.height = 20;
            button.style.marginLeft = 2;
            button.style.paddingLeft = 2;
            button.style.paddingRight = 2;

            var texture = EditorGUIUtility.IconContent(iconName).image as Texture2D;
            if (texture != null)
            {
                var icon = new VisualElement();
                icon.style.width = 16;
                icon.style.height = 16;
                icon.style.backgroundImage = new StyleBackground(texture);
                icon.style.alignSelf = Align.Center;
                button.Add(icon);
            }
        }

        private static void RefreshFolderList(ScrollView scroll)
        {
            var foldout = scroll.Q<Foldout>("FolderStructureFoldout");
            if (foldout != null)
            {
                var group = foldout.contentContainer;
                group.Clear();
                BuildFolderTreeUI(group);
            }
        }

        private static void BuildFolderTreeUI(VisualElement parent)
        {
            foreach (var item in folderItems)
            {
                DrawFolderItemRecursive(parent, item, 0);
            }
        }

        private static void DrawFolderItemRecursive(VisualElement container, FolderItem item, int depth)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = depth * 15, alignItems = Align.Center, height = 20 } };

            var toggle = new Toggle { value = item.enabled, style = { marginRight = 2 } };
            toggle.RegisterValueChangedCallback(evt => item.enabled = evt.newValue);
            row.Add(toggle);

            var icon = new VisualElement
            {
                style = { width = 16, height = 16, marginRight = 4, backgroundImage = new StyleBackground(EditorGUIUtility.IconContent("Folder Icon").image as Texture2D) }
            };
            row.Add(icon);

            var label = new Label(item.name) { style = { flexGrow = 1, fontSize = 12 } };
            row.Add(label);

            container.Add(row);
            foreach (var child in item.children) DrawFolderItemRecursive(container, child, depth + 1);
        }

        private static VisualElement CreateGroup(string title)
        {
            var group = new VisualElement();
            group.style.marginTop = 4;
            group.style.paddingTop = group.style.paddingBottom = 8;
            group.style.paddingLeft = group.style.paddingRight = 10;

            // 四周描边
            group.style.borderTopWidth = 1;
            group.style.borderBottomWidth = 1;
            group.style.borderLeftWidth = 1;
            group.style.borderRightWidth = 1;

            var borderColor = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.7f, 0.7f, 0.7f);
            group.style.borderTopColor = borderColor;
            group.style.borderBottomColor = borderColor;
            group.style.borderLeftColor = borderColor;
            group.style.borderRightColor = borderColor;

            group.style.borderTopLeftRadius = group.style.borderTopRightRadius = group.style.borderBottomLeftRadius = group.style.borderBottomRightRadius = 4;
            group.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.92f, 0.92f, 0.92f);

            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 11;
            label.style.marginBottom = 5;
            label.style.color = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);
            group.Add(label);
            return group;
        }

        #region 逻辑方法
        private static void CreateAllFoldersLogic()
        {
            string rootPath = $"Assets/{gameRootName}";
            if (!AssetDatabase.IsValidFolder(rootPath)) AssetDatabase.CreateFolder("Assets", gameRootName);

            int count = 0;
            void CreateRecursive(string parentPath, FolderItem item)
            {
                if (!item.enabled) return;
                string currentPath = $"{parentPath}/{item.name}";
                if (!AssetDatabase.IsValidFolder(currentPath))
                {
                    AssetDatabase.CreateFolder(parentPath, item.name);
                    count++;
                }
                foreach (var child in item.children) CreateRecursive(currentPath, child);
            }

            foreach (var item in folderItems) CreateRecursive(rootPath, item);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", $"已成功创建 {count} 个文件夹", "确定");
        }

        private static void LoadPreset(string key)
        {
            if (presets.ContainsKey(key))
            {
                folderItems.Clear();
                foreach (var path in presets[key].Paths) AddPathToTree(folderItems, path);
                selectedPresetKey = key;
            }
        }

        private static void AddPathToTree(List<FolderItem> roots, string path)
        {
            var parts = path.Split('/').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            List<FolderItem> current = roots;
            foreach (var part in parts)
            {
                var existing = current.FirstOrDefault(n => n.name == part);
                if (existing == null)
                {
                    existing = new FolderItem(part);
                    current.Add(existing);
                }
                current = existing.children;
            }
        }

        private static void InitializePresets()
        {
            presets["默认游戏项目"] = new FolderPreset
            {
                Paths = new List<string>
                { 
                    // 脚本
                    "Scripts/Managers",
                    "Scripts/Models",
                    "Scripts/ViewControllers",
                    "Scripts/Commands",
                    "Scripts/ViewControllers/UI",
                    "Scripts/Utility",
                    "Scripts/Shaders",
                    "Scripts/Utility/Editor",
                    // 资源
                    "GameRes/Animations",
                    "GameRes/Prefabs",
                    "GameRes/Fonts",
                    "GameRes/Materials",
                    "GameRes/Mesh",
                    "GameRes/Audios",
                    "GameRes/Images",
                    "GameRes/Resources",
                    "GameRes/Resources/UI",
                    "GameRes/Scenes",
                    "GameRes/Sprites",
                    // Other
                    "Setting",
                    "Test"
                }
            };
            presets["2D项目"] = new FolderPreset
            {
                Paths = new List<string>
                {
                    // 脚本
                    "Scripts/Managers",
                    "Scripts/Models",
                    "Scripts/ViewControllers",
                    "Scripts/Commands",
                    "Scripts/ViewControllers/UI",
                    "Scripts/Utility",
                    "Scripts/Shaders",
                    "Scripts/Utility/Editor",
                    // 资源
                    "GameRes/Sprites",
                    "GameRes/Animations",
                    "GameRes/Prefabs",
                    "GameRes/Fonts",
                    "GameRes/Materials",
                    "GameRes/Audios",
                    "GameRes/Images",
                    "GameRes/Resources/UI",
                    "GameRes/Scenes",
                    // Other
                    "Setting",
                    "Test"
                }
            };
            presets["热更新"] = new FolderPreset
            {
                Paths = new List<string>
                {
                    // 脚本
                    "Scripts/HotUpdate",
                    "Scripts/Utility/Editor",
                    "Scripts/HotUpdate/Managers",
                    "Scripts/HotUpdate/Models",
                    "Scripts/Shaders",
                    "Scripts/HotUpdate/ViewControllers",
                    "Scripts/HotUpdate/ViewControllers/UI",
                    "Scripts/HotUpdate/Commands",
                    "Scripts/HotUpdate/Common",
                    // 资源
                    "GameRes/Animations",
                    "GameRes/Prefabs",
                    "GameRes/Fonts",
                    "GameRes/Materials",
                    "GameRes/Mesh",
                    "GameRes/Audios",
                    "GameRes/Images",
                    "GameRes/Resources",
                    "GameRes/Resources/UI",
                    "GameRes/Scenes",
                    "GameRes/Sprites",
                    // Other
                    "Setting",
                    "Test"
                }
            };
        }
        #endregion
    }
}
