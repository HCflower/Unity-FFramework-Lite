// =============================================================
// 描述：本地化数据编辑器 — 独立面板（EditorWindow）
// 作者：HCFlower
// 创建时间：2026-05-19
// 版本：2.0.0
// =============================================================
using System.Collections.Generic;
using UnityEngine.UIElements;
using FFramework.Utility;
using UnityEditor;
using UnityEngine;
using System.Text;
using System.IO;
using System;

namespace FFramework.Editor
{
    public class LocalizationDataEditorView : EditorWindow
    {
        #region 入口

        [MenuItem("FFramework/本地化数据编辑器")]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalizationDataEditorView>("本地化数据编辑器");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        #endregion

        #region 数据模型

        private class RowData
        {
            public string Key;
            public Dictionary<string, string> Translations;
        }

        #endregion

        #region 状态字段

        private LocalizationConfig config;
        private List<LocalizationConfig> allConfigs = new List<LocalizationConfig>();
        private List<string> configNames = new List<string>();
        private List<string> groupIds = new List<string>();

        private List<RowData> rows = new List<RowData>();
        private List<string> langs = new List<string>();
        private string curGroupId;
        private string csvFilePath;
        private bool dirty = false;

        private HashSet<string> deletedKeys = new HashSet<string>();
        private HashSet<string> addedKeys = new HashSet<string>();
        private HashSet<string> keyLookup = new HashSet<string>(); // O(1) Key 查重

        private int selRowIdx = -1;
        private int selColIdx = -1;
        private string selLang = null;

        private List<int> filtered = new List<int>();

        #endregion

        #region UI 元素

        private VisualElement root;
        private PopupField<string> configPopup;
        private PopupField<string> groupPopup;
        private ScrollView tableScroll;
        private VisualElement tableBody;
        private TextField searchBox;

        private VisualElement editorPanel;
        private Label editorLabel;
        private TextField editorField;
        private Label editorStatusLabel;

        #endregion

        #region GUI 入口

        public void CreateGUI()
        {
            root = rootVisualElement;
            root.style.paddingAll(3);

            FindConfig();
            BuildToolbar();
            BuildTable();
            BuildEditorPanel();

            RefreshGroups();
            if (groupIds.Count > 0)
                LoadGroup(groupIds[0]);
        }

        #endregion

        #region 查找 Config

        private void FindConfig()
        {
            allConfigs.Clear();
            configNames.Clear();

            // 搜集项目中所有 LocalizationConfig 资产
            var guids = AssetDatabase.FindAssets("t:LocalizationConfig");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(path);
                if (cfg != null)
                {
                    allConfigs.Add(cfg);
                    configNames.Add(cfg.name + "  (" + System.IO.Path.GetFileNameWithoutExtension(path) + ")");
                }
            }

            // 默认选中第一个
            if (allConfigs.Count > 0)
                config = allConfigs[0];
            else
                config = null;
        }

        private void SelectConfig(int index)
        {
            if (index < 0 || index >= allConfigs.Count) return;
            config = allConfigs[index];
            RefreshConfigs();
            RefreshGroups();
            if (groupIds.Count > 0)
                LoadGroup(groupIds[0]);
        }

        #endregion

        #region UI 构建

        private Texture2D GetIcon(string name)
        {
            return EditorGUIUtility.IconContent(name).image as Texture2D;
        }

        private void SetIcon(Button btn, string iconName)
        {
            btn.style.justifyContent = Justify.Center;
            btn.style.alignItems = Align.Center;
            var img = new Image();
            img.image = GetIcon(iconName);
            img.style.width = new Length(80, LengthUnit.Percent);
            img.style.height = new Length(80, LengthUnit.Percent);
            img.style.alignSelf = Align.Center;
            btn.Add(img);
        }

        private void BuildToolbar()
        {
            float btnH = 22;
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.minHeight = btnH + 4;
            bar.style.marginBottom = 3;

            // 刷新 Config
            var refreshBtn = new Button(() => { FindConfig(); RefreshConfigs(); });
            refreshBtn.tooltip = "刷新 Config";
            refreshBtn.style.width = 28;
            refreshBtn.style.marginLeft = 0;
            refreshBtn.style.marginRight = 0;
            refreshBtn.style.height = btnH;
            refreshBtn.style.paddingAll(0);
            SetIcon(refreshBtn, "d_Refresh");
            bar.Add(refreshBtn);

            // Config 选择（多 Config 时切换）
            configPopup = new PopupField<string>("", configNames, 0);
            configPopup.style.minWidth = 60;
            configPopup.style.maxWidth = 150;
            configPopup.style.marginLeft = 0;
            configPopup.style.marginRight = 0;
            configPopup.style.flexGrow = 0;
            configPopup.style.flexShrink = 0;
            configPopup.style.height = btnH;
            configPopup.style.fontSize = 10;
            configPopup.tooltip = "选择 LocalizationConfig 资产";
            configPopup.SetEnabled(configNames.Count > 1);
            configPopup.RegisterValueChangedCallback(e =>
            {
                int idx = configNames.IndexOf(e.newValue);
                if (idx >= 0) SelectConfig(idx);
            });
            bar.Add(configPopup);

            // 组选择
            groupPopup = new PopupField<string>(groupIds, 0);
            groupPopup.style.minWidth = 80;
            groupPopup.style.flexGrow = 0;
            groupPopup.style.flexShrink = 0;
            groupPopup.style.marginLeft = 0;
            groupPopup.style.marginRight = 0;
            groupPopup.style.height = btnH;
            groupPopup.style.unityFontStyleAndWeight = FontStyle.Bold;
            groupPopup.RegisterValueChangedCallback(e =>
            {
                if (dirty && EditorUtility.DisplayDialog("未保存", $"组「{curGroupId}」有未保存更改，是否保存？", "保存", "丢弃"))
                    SaveCsv();
                LoadGroup(e.newValue);
            });
            bar.Add(groupPopup);

            // 搜索
            searchBox = new TextField();
            searchBox.style.minWidth = 100;
            searchBox.style.flexGrow = 1;
            searchBox.style.height = btnH;
            searchBox.style.marginLeft = 4;
            searchBox.tooltip = "搜索 Key";
            searchBox.RegisterValueChangedCallback(_ => RefreshTable());
            bar.Add(searchBox);

            var clearBtn = new Button(() => { searchBox.value = ""; searchBox.Focus(); });
            clearBtn.tooltip = "清除搜索";
            clearBtn.style.width = 24;
            clearBtn.style.marginLeft = 0;
            clearBtn.style.marginRight = 0;
            clearBtn.style.height = btnH;
            clearBtn.style.paddingAll(0);
            SetIcon(clearBtn, "d_winbtn_mac_close_h@2x");
            bar.Add(clearBtn);

            // 新建 CSV
            var newBtn = new Button(CreateNewCsv);
            newBtn.text = "新建 CSV";
            newBtn.tooltip = "创建新的 CSV 文件并刷新";
            newBtn.style.height = btnH;
            newBtn.style.marginLeft = 0;
            newBtn.style.marginRight = 0;
            newBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            newBtn.style.backgroundColor = new Color(0.2f, 0.4f, 0.25f);
            bar.Add(newBtn);

            // 新增
            var addBtn = new Button(AddRow);
            addBtn.text = "新增行";
            addBtn.tooltip = "添加新 Key";
            addBtn.style.height = btnH;
            addBtn.style.marginLeft = 0;
            addBtn.style.marginRight = 0;
            addBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            addBtn.style.backgroundColor = new Color(0.22f, 0.45f, 0.22f);
            bar.Add(addBtn);

            // 删除
            var delBtn = new Button(DeleteSelected);
            delBtn.text = "删除";
            delBtn.tooltip = "删除选中行";
            delBtn.style.height = btnH;
            delBtn.style.marginLeft = 0;
            delBtn.style.marginRight = 0;
            delBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            delBtn.style.backgroundColor = new Color(0.5f, 0.2f, 0.2f);
            bar.Add(delBtn);

            // 保存
            var saveBtn = new Button(SaveCsv);
            saveBtn.text = "保存";
            saveBtn.tooltip = "保存到 CSV 文件";
            saveBtn.style.height = btnH;
            saveBtn.style.marginLeft = 0;
            saveBtn.style.marginRight = 0;
            saveBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            saveBtn.style.backgroundColor = new Color(0.2f, 0.3f, 0.55f);
            bar.Add(saveBtn);

            root.Add(bar);
        }

        private void BuildTable()
        {
            tableScroll = new ScrollView();
            tableScroll.style.flexGrow = 1;

            tableBody = new VisualElement();
            tableScroll.Add(tableBody);
            root.Add(tableScroll);
        }

        private void BuildEditorPanel()
        {
            editorPanel = new VisualElement();
            editorPanel.style.flexDirection = FlexDirection.Column;
            // #404040
            editorPanel.style.backgroundColor = new Color(0.251f, 0.251f, 0.251f);
            editorPanel.style.borderWidthAll(1);
            editorPanel.style.borderColorAll(new Color(0.35f, 0.35f, 0.35f));
            editorPanel.style.borderRadiusAll(3);
            editorPanel.style.paddingAll(5);
            editorPanel.style.marginTop = 4;
            editorPanel.style.minHeight = 80;
            editorPanel.style.display = DisplayStyle.None;

            // 标题行：编辑上下文左对齐 + 状态信息右对齐
            var editorHeader = new VisualElement();
            editorHeader.style.flexDirection = FlexDirection.Row;
            editorHeader.style.justifyContent = Justify.SpaceBetween;
            editorHeader.style.alignItems = Align.Center;
            editorHeader.style.marginBottom = 4;

            editorLabel = new Label("点击单元格开始编辑");
            editorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            editorLabel.style.fontSize = 12;
            editorLabel.style.color = new Color(1f, 0.7f, 0.2f);
            editorHeader.Add(editorLabel);

            editorStatusLabel = new Label("就绪");
            editorStatusLabel.style.fontSize = 11;
            editorStatusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            editorStatusLabel.style.marginLeft = 10;
            editorStatusLabel.style.flexShrink = 1;
            editorStatusLabel.style.overflow = Overflow.Hidden;
            editorStatusLabel.style.textOverflow = TextOverflow.Ellipsis;
            editorStatusLabel.style.whiteSpace = WhiteSpace.NoWrap;
            editorHeader.Add(editorStatusLabel);
            editorPanel.Add(editorHeader);

            var sep = new VisualElement();
            sep.style.height = 1;
            sep.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            sep.style.marginBottom = 4;
            editorPanel.Add(sep);

            editorField = new TextField();
            editorField.style.flexGrow = 1;
            editorField.style.minHeight = 60;
            editorField.style.maxHeight = 140;
            editorField.style.whiteSpace = WhiteSpace.Normal;
            editorField.multiline = true;
            editorField.isDelayed = true;

            // UI Toolkit 中 TextField 的文本对齐需要设置到内部 input 元素上
            var textInput = editorField.Q("unity-text-input");
            if (textInput != null)
                textInput.style.unityTextAlign = TextAnchor.UpperLeft;
            editorPanel.Add(editorField);

            editorField.RegisterValueChangedCallback(e =>
            {
                if (selRowIdx < 0 || selRowIdx >= filtered.Count) return;
                var row = rows[filtered[selRowIdx]];
                if (selColIdx == 0)
                {
                    string newKey = e.newValue?.Trim() ?? "";
                    if (newKey != row.Key && !string.IsNullOrEmpty(newKey))
                    {
                        if (keyLookup.Contains(newKey))
                        {
                            EditorUtility.DisplayDialog("重复 Key", $"Key「{newKey}」已存在。", "确定");
                            editorField.value = row.Key;
                            return;
                        }
                        row.Key = newKey;
                        MarkDirty();
                        RefreshTable();
                    }
                }
                else if (selLang != null)
                {
                    row.Translations[selLang] = e.newValue;
                    MarkDirty();
                    RefreshTable();
                }
            });
            editorPanel.Add(editorField);

            root.Add(editorPanel);
        }

        #endregion

        #region 数据加载

        private void RefreshConfigs()
        {
            if (configPopup == null) return;
            configPopup.choices = configNames;
            configPopup.SetEnabled(configNames.Count > 1);
            if (configNames.Count > 0)
                configPopup.index = 0;
        }

        private void RefreshGroups()
        {
            groupIds.Clear();
            if (config != null)
            {
                foreach (var g in config.CsvGroups)
                    if (!string.IsNullOrEmpty(g.groupId) && g.csvFile != null)
                        groupIds.Add(g.groupId);
            }
            if (groupPopup != null)
            {
                groupPopup.choices = groupIds;
                groupPopup.SetEnabled(groupIds.Count > 0);
                if (groupIds.Count > 0)
                    groupPopup.index = 0;
            }
        }

        private void LoadGroup(string gid)
        {
            if (config == null || string.IsNullOrEmpty(gid)) return;
            var entry = config.GetCsvGroup(gid);
            if (entry?.csvFile == null) { SetStatus($"无法加载「{gid}」", MessageType.Warning); return; }

            curGroupId = gid;
            dirty = false;
            deletedKeys.Clear();
            addedKeys.Clear();
            keyLookup.Clear();
            selRowIdx = -1; selColIdx = -1; selLang = null;
            editorPanel.style.display = DisplayStyle.None;

            var parsed = ParseCsv(entry.csvFile.text, out var l);
            rows = parsed; langs = l;
            // 构建 Key 查询表
            foreach (var r in rows) keyLookup.Add(r.Key);

            var assetPath = AssetDatabase.GetAssetPath(entry.csvFile);
            csvFilePath = Path.GetFullPath(assetPath);

            RefreshTable();
            SetStatus($"已加载「{gid}」: {rows.Count} 条, {langs.Count} 种语言 [{string.Join(", ", langs)}]", MessageType.Info);
        }

        private List<RowData> ParseCsv(string text, out List<string> languages)
        {
            var result = new List<RowData>();
            languages = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool header = true;
            foreach (var line in lines)
            {
                var raw = line.Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#")) continue;
                var parts = SplitCsvLine(raw);
                if (parts.Count < 2) continue;

                if (header)
                {
                    for (int i = 1; i < parts.Count; i++)
                    {
                        var l = parts[i].Trim();
                        if (!string.IsNullOrEmpty(l)) languages.Add(l);
                    }
                    header = false;
                }
                else
                {
                    var key = parts[0].Trim();
                    if (string.IsNullOrEmpty(key)) continue;
                    var row = new RowData { Key = key, Translations = new Dictionary<string, string>() };
                    for (int i = 0; i < languages.Count && i + 1 < parts.Count; i++)
                        row.Translations[languages[i]] = parts[i + 1].Trim();
                    result.Add(row);
                }
            }
            return result;
        }

        private List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (q && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else q = !q;
                }
                else if (c == ',' && !q) { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            result.Add(sb.ToString());
            return result;
        }

        #endregion

        #region 表格渲染

        private VisualElement MakeTableCell(string text, float w, bool flex, Action onClick, bool isHeader = false)
        {
            var cell = new VisualElement();
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            cell.style.borderBottomWidth = 1;
            cell.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            cell.style.minHeight = 22;
            cell.style.overflow = Overflow.Hidden;

            if (w > 0)
            {
                cell.style.minWidth = w;
                cell.style.maxWidth = w;
                cell.style.flexShrink = 0;
                cell.style.flexGrow = 0;
            }
            if (flex)
            {
                cell.style.flexGrow = 1;
                cell.style.flexBasis = 0;
                cell.style.minWidth = 60;
            }
            if (!isHeader)
            {
                cell.RegisterCallback<ClickEvent>(e => onClick?.Invoke());
                cell.RegisterCallback<MouseEnterEvent>(e => cell.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f));
                cell.RegisterCallback<MouseLeaveEvent>(e => cell.style.backgroundColor = Color.clear);
            }

            var lb = new Label(text);
            lb.style.unityTextAlign = TextAnchor.MiddleLeft;
            if (isHeader) lb.style.unityTextAlign = TextAnchor.MiddleCenter;
            lb.style.paddingLeft = 5;
            lb.style.paddingRight = 5;
            lb.style.overflow = Overflow.Hidden;
            lb.style.textOverflow = TextOverflow.Ellipsis;
            lb.style.whiteSpace = WhiteSpace.NoWrap;
            lb.style.fontSize = isHeader ? 11 : 12;
            lb.style.flexShrink = 1;
            lb.style.flexGrow = 1;
            if (isHeader) lb.style.unityFontStyleAndWeight = FontStyle.Bold;
            cell.Add(lb);

            return cell;
        }

        private void RefreshTable()
        {
            tableBody.Clear();

            filtered.Clear();
            var search = searchBox?.value?.Trim() ?? "";
            for (int i = 0; i < rows.Count; i++)
                if (string.IsNullOrEmpty(search) || rows[i].Key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    filtered.Add(i);

            var tableOuter = new VisualElement();
            tableOuter.style.borderWidthAll(1);
            tableOuter.style.borderColorAll(new Color(0.3f, 0.3f, 0.3f));
            tableOuter.style.borderRadiusAll(2);

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            head.style.minHeight = 24;

            head.Add(MakeTableCell("#", 32, false, null, true));
            head.Add(MakeTableCell("Key", 140, false, null, true));

            foreach (var l in langs)
                head.Add(MakeTableCell(l, 0, true, null, true));

            tableOuter.Add(head);

            if (filtered.Count == 0)
            {
                var empty = new Label(rows.Count == 0 ? "暂无数据，点击「新增行」添加" : $"未找到匹配「{search}」的 Key");
                empty.style.paddingAll(8);
                empty.style.color = new Color(0.5f, 0.5f, 0.5f);
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.borderBottomWidth = 1;
                empty.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
                tableOuter.Add(empty);
                tableBody.Add(tableOuter);
                return;
            }

            for (int di = 0; di < filtered.Count; di++)
            {
                int oi = filtered[di];
                var row = rows[oi];
                bool isNew = addedKeys.Contains(row.Key);
                bool isDel = deletedKeys.Contains(row.Key);

                var rowEl = new VisualElement();
                rowEl.style.flexDirection = FlexDirection.Row;
                rowEl.style.minHeight = 22;

                if (isDel) rowEl.style.backgroundColor = new Color(0.4f, 0.12f, 0.12f, 0.3f);
                else if (isNew) rowEl.style.backgroundColor = new Color(0.12f, 0.4f, 0.12f, 0.3f);
                if (selRowIdx == di) rowEl.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);

                int cdi = di;

                rowEl.Add(MakeTableCell((di + 1).ToString(), 32, false, () =>
                {
                    selRowIdx = cdi; selColIdx = -1; selLang = null; editorPanel.style.display = DisplayStyle.None; RefreshTable();
                }));

                rowEl.Add(MakeTableCell(row.Key, 140, false, () => SelectCell(cdi, 0, null, row.Key)));

                for (int ci = 0; ci < langs.Count; ci++)
                {
                    var lang = langs[ci];
                    row.Translations.TryGetValue(lang, out var val);
                    int cCaptured = ci + 1;
                    string lCaptured = lang;
                    rowEl.Add(MakeTableCell(val ?? "", 0, true, () => SelectCell(cdi, cCaptured, lCaptured, val ?? "")));
                }

                tableOuter.Add(rowEl);
            }

            tableBody.Add(tableOuter);

            if (selRowIdx >= filtered.Count) { selRowIdx = -1; selColIdx = -1; selLang = null; editorPanel.style.display = DisplayStyle.None; }

            SetStatus($"组「{curGroupId}」: 显示 {filtered.Count}/{rows.Count} 条, {langs.Count} 种语言" + (dirty ? " !" : ""),
                dirty ? MessageType.Warning : MessageType.Info);
        }

        private void SelectCell(int displayIdx, int colIdx, string lang, string value)
        {
            selRowIdx = displayIdx;
            selColIdx = colIdx;
            selLang = lang;

            var row = rows[filtered[displayIdx]];
            string prefix = colIdx == 0 ? "Key" : lang;
            editorLabel.text = $"{prefix}  ←  {row.Key}";
            editorField.value = value;
            editorField.multiline = value != null && value.Length > 40;
            editorPanel.style.display = DisplayStyle.Flex;
            editorField.Focus();
            RefreshTable();
        }

        #endregion

        #region 编辑操作

        private void AddRow()
        {
            string baseName = "new_key";
            string key = baseName;
            for (int c = 1; keyLookup.Contains(key); c++) key = $"{baseName}_{c}";

            var nr = new RowData { Key = key, Translations = new Dictionary<string, string>() };
            foreach (var l in langs) nr.Translations[l] = "";

            rows.Add(nr);
            addedKeys.Add(key);
            keyLookup.Add(key);
            selRowIdx = filtered.Count;
            MarkDirty();
            RefreshTable();
            tableScroll.scrollOffset = new Vector2(0, tableScroll.contentContainer.layout.height);
        }

        private void DeleteSelected()
        {
            if (selRowIdx < 0 || selRowIdx >= filtered.Count)
            {
                EditorUtility.DisplayDialog("提示", "请先在表格中点击任意单元格选中一行。", "确定");
                return;
            }
            int oi = filtered[selRowIdx];
            var key = rows[oi].Key;
            if (!EditorUtility.DisplayDialog("删除", $"确定删除 Key「{key}」及其全部翻译？", "删除", "取消"))
                return;

            if (addedKeys.Contains(key)) { addedKeys.Remove(key); keyLookup.Remove(key); }
            else { deletedKeys.Add(key); keyLookup.Remove(key); }
            rows.RemoveAt(oi);
            selRowIdx = -1; selColIdx = -1; selLang = null;
            editorPanel.style.display = DisplayStyle.None;
            MarkDirty();
            RefreshTable();
        }

        private void MarkDirty()
        {
            if (!dirty) { dirty = true; SetStatus($"组「{curGroupId}」有未保存更改 !", MessageType.Warning); }
        }

        #endregion

        #region 保存

        private void SaveCsv()
        {
            if (string.IsNullOrEmpty(csvFilePath) || !File.Exists(csvFilePath))
            {
                EditorUtility.DisplayDialog("保存失败", $"找不到 CSV 文件：{csvFilePath}", "确定");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("key");
            foreach (var l in langs) { sb.Append(','); sb.Append(l); }
            sb.AppendLine();

            foreach (var row in rows)
            {
                AppendCsv(sb, row.Key);
                foreach (var l in langs)
                {
                    sb.Append(',');
                    var v = row.Translations.TryGetValue(l, out var x) ? x : "";
                    AppendCsv(sb, v);
                }
                sb.AppendLine();
            }

            var tmp = csvFilePath + ".tmp";
            try
            {
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                File.Delete(csvFilePath);
                File.Move(tmp, csvFilePath);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("保存失败", ex.Message, "确定");
                if (File.Exists(tmp)) File.Delete(tmp);
                return;
            }

            // 将全路径转为相对路径（统一正斜杠）
            string dataPath = Application.dataPath.Replace("\\", "/");
            string relPath = csvFilePath.Replace("\\", "/");
            if (relPath.StartsWith(dataPath))
                relPath = "Assets" + relPath.Substring(dataPath.Length);
            AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);

            dirty = false;
            deletedKeys.Clear();
            addedKeys.Clear();
            SetStatus($"已保存至 {Path.GetFileName(csvFilePath)} — {rows.Count} 条, {langs.Count} 种语言", MessageType.Info);
            RefreshTable();
        }

        /// <summary>
        /// 创建新的 CSV 文件：选择保存路径 → 输入语言列 → 生成文件 → 刷新列表
        /// </summary>
        private void CreateNewCsv()
        {
            // 1. 选择保存目录（从 Assets 文件夹开始选择）
            string savePath = EditorUtility.SaveFilePanel("新建 CSV 文件", Application.dataPath, "new_localization.csv", "csv");
            if (string.IsNullOrEmpty(savePath)) return;

            // 确保目录存在
            string dir = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 2. 选择语言列（基于当前加载的语言，或使用默认值）
            var languages = new List<string>();
            if (langs.Count > 0)
            {
                // 复用当前已加载的语言列
                languages = new List<string>(langs);
            }
            else
            {
                // 默认语言
                languages = new List<string> { "ChineseSimplified", "English", "Japanese" };
            }

            // 3. 生成 CSV 内容
            var sb = new StringBuilder();
            sb.Append("key");
            foreach (var lang in languages) { sb.Append(','); sb.Append(lang); }
            sb.AppendLine();

            // 写入一条示例数据
            sb.Append("example_key");
            foreach (var lang in languages) { sb.Append(",example_" + lang); }
            sb.AppendLine();

            try
            {
                File.WriteAllText(savePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("创建失败", ex.Message, "确定");
                return;
            }

            // 4. 刷新 AssetDatabase（统一正斜杠）
            string dataPath2 = Application.dataPath.Replace("\\", "/");
            string relPath2 = savePath.Replace("\\", "/");
            if (relPath2.StartsWith(dataPath2))
                relPath2 = "Assets" + relPath2.Substring(dataPath2.Length);
            AssetDatabase.ImportAsset(relPath2, ImportAssetOptions.ForceUpdate);

            // 5. 重新查找 Config 并刷新组列表
            FindConfig();
            RefreshConfigs();
            RefreshGroups();

            // 6. 自动选中新文件（用文件名作为 groupId 尝试）
            string fileName = Path.GetFileNameWithoutExtension(savePath);
            if (groupIds.Contains(fileName))
                LoadGroup(fileName);
            else if (groupIds.Count > 0)
                LoadGroup(groupIds[0]);

            SetStatus($"已创建 CSV 文件: {Path.GetFileName(savePath)}", MessageType.Info);
            Debug.Log($"[LocalizationDataEditor] 新建 CSV: {savePath}");
        }

        private void AppendCsv(StringBuilder sb, string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            if (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            { sb.Append('"'); sb.Append(v.Replace("\"", "\"\"")); sb.Append('"'); }
            else sb.Append(v);
        }

        #endregion

        #region 状态

        private void SetStatus(string msg, MessageType type = MessageType.Info)
        {
            if (editorStatusLabel == null) return;
            editorStatusLabel.text = msg;
            editorStatusLabel.style.color = type == MessageType.Warning ? new Color(1f, 0.7f, 0f)
                : type == MessageType.Error ? new Color(1f, 0.3f, 0.3f)
                : new Color(0.6f, 0.6f, 0.6f);
        }

        #endregion
    }
}
