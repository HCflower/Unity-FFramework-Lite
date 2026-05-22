// =============================================================
// 描述：编辑器命令控制台 — Lite 扁平版
//       通过 WinAPI GetAsyncKeyState 全局检测 LeftCtrl+Tab 热键，
//       在 Play 模式下按 LeftCtrl+Tab 切换控制台显示/隐藏。
//       使用 Unity 编辑器风格配色。
// =============================================================
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Reflection;
using FFramework.Core;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System;

namespace FFramework.Editor
{
    [InitializeOnLoad]
    public static class CommandConsoleEditor
    {
        #region WinAPI 全局热键

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_LCONTROL = 0xA2;
        private const int VK_TAB = 0x09;
        private static bool lastCtrlTabState;

        #endregion

        #region 命令元数据

        private struct ConsoleCommandInfo
        {
            public string CommandName { get; set; }
            public string Description { get; set; }
            public string Usage { get; set; }
            public bool IsClassCommand { get; set; }
            public Type ClassType { get; set; }
            public MethodInfo Method { get; set; }

            public string ParameterSignature
            {
                get
                {
                    if (IsClassCommand && ClassType != null)
                        return GetParameterSignatureFromType(ClassType);
                    if (!IsClassCommand && Method != null)
                    {
                        var ps = Method.GetParameters();
                        return ps.Length == 1 && ps[0].ParameterType == typeof(string[])
                            ? "args: string[]"
                            : string.Join(", ", ps.Select(p => $"{p.Name}: {p.ParameterType.Name}"));
                    }
                    return "";
                }
            }

            public string FullUsage
            {
                get
                {
                    string sig = ParameterSignature;
                    return !string.IsNullOrEmpty(sig)
                        ? $"{CommandName} <{sig.Replace(", ", "> <")}>"
                        : CommandName;
                }
            }
        }

        private static string GetParameterSignatureFromType(Type classType)
        {
            var props = classType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.CanWrite && p.CanRead).ToList();
            return props.Count == 0 ? "" : string.Join(", ", props.Select(p => $"{p.Name}: {p.PropertyType.Name}"));
        }

        private struct CommandParamInfo { public string Name; public Type PropertyType; public PropertyInfo Property; }

        private static List<CommandParamInfo> GetCommandParameters(Type classType)
        {
            return classType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.CanWrite && p.CanRead)
                .Select(p => new CommandParamInfo { Name = p.Name, PropertyType = p.PropertyType, Property = p })
                .ToList();
        }

        private enum OutputType { Info, Success, Error, Command, Help }

        #endregion

        #region 状态字段

        private static Dictionary<string, ConsoleCommandInfo> commandCache;
        private static bool cacheInitialized;
        private static string inputText = "";
        private static int historyIndex = -1;
        private static List<string> inputHistory = new List<string>();
        private const int MAX_HISTORY = 50;
        private static List<OutputLineData> outputLines = new List<OutputLineData>();

        private struct OutputLineData { public string Text; public OutputType Type; }

        private static bool consoleVisible;
        private static EditorWindow gameView;
        private static Type gameViewType;
        private static Vector2 lastGameViewSize;

        // UI 元素
        private static VisualElement overlay;
        private static ScrollView outputScrollView;
        private static VisualElement outputContainer;
        private static TextField inputTextField;
        private static Button execButton;
        private static VisualElement suggestionDropdown;
        private static List<string> suggestionItems = new List<string>();
        private static int selectedSuggestionIndex = -1;

        // 布局常量
        private const float PADDING = 2f;
        private const float LINE_HEIGHT = 20f;
        private const float OVERLAY_HEIGHT_RATIO = 0.5f;

        #endregion

        #region Unity 风格色值

        private static Color BgColor => EditorGUIUtility.isProSkin
            ? new Color(0.15f, 0.15f, 0.15f, 0.92f)
            : new Color(0.78f, 0.78f, 0.78f, 0.92f);

        private static Color TextColor => EditorGUIUtility.isProSkin
            ? new Color(0.85f, 0.85f, 0.85f)
            : new Color(0.12f, 0.12f, 0.12f);

        private static Color InputBgColor => new Color(0.208f, 0.208f, 0.208f); // #353535

        private static Color AccentColor => EditorGUIUtility.isProSkin
            ? new Color(0.35f, 0.35f, 0.35f)
            : new Color(0.55f, 0.55f, 0.55f);

        private static Color ErrorColor => new Color(1.0f, 0.35f, 0.35f);
        private static Color SuccessColor => new Color(0.30f, 0.90f, 0.50f);
        private static Color CommandColor => EditorGUIUtility.isProSkin
            ? new Color(0.60f, 0.60f, 0.60f)
            : new Color(0.35f, 0.35f, 0.35f);
        private static Color HelpColor => new Color(1.0f, 0.65f, 0.0f);

        #endregion

        #region 初始化

        static CommandConsoleEditor()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            RefreshCommandCache();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                lastCtrlTabState = false;
            else if (state == PlayModeStateChange.ExitingPlayMode && consoleVisible)
                HideConsole();
        }

        private static void OnAfterAssemblyReload() => RefreshCommandCache();

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying) return;

            bool isCtrlTabDown = (GetAsyncKeyState(VK_TAB) & 0x8000) != 0
                               && (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0;
            if (isCtrlTabDown && !lastCtrlTabState)
            {
                if (consoleVisible) HideConsole(); else ShowConsole();
            }
            lastCtrlTabState = isCtrlTabDown;

            if (consoleVisible && overlay != null && gameView != null)
            {
                Vector2 currentSize = gameView.position.size;
                if (currentSize != lastGameViewSize)
                {
                    lastGameViewSize = currentSize;
                    UpdateOverlayPosition();
                }
            }
        }

        #endregion

        #region 显示/隐藏

        public static void ShowConsole()
        {
            if (consoleVisible) return;
            if (!EditorApplication.isPlaying) { Debug.LogWarning("命令控制台仅在 Play 模式下可用"); return; }
            if (!EnsureGameView()) { Debug.LogWarning("[CommandConsole] 找不到 Game 视图"); return; }

            try
            {
                overlay = CreateConsoleUI();
                Vector2 gameViewSize = gameView.position.size;
                float consoleHeight = Mathf.Max(gameViewSize.y * OVERLAY_HEIGHT_RATIO, 180f);

                overlay.style.position = Position.Absolute;
                overlay.style.left = 0;
                overlay.style.top = 20; // 避开 Game 窗口顶部按钮栏
                overlay.style.width = gameViewSize.x;
                overlay.style.height = consoleHeight;
                lastGameViewSize = gameViewSize;

                gameView.rootVisualElement.Add(overlay);
                consoleVisible = true;
                inputTextField?.Focus();

                if (outputLines.Count == 0)
                {
                    AddOutput("命令控制台已加载  |  <color=#FFA600><b>↑↓</b></color>切换命令  <color=#FFA600><b>Tab</b></color>选择  <color=#FFA600><b>help</b></color>查看", OutputType.Info);
                }
                gameView.Repaint();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CommandConsole] 创建 UI 失败: {e.Message}");
                consoleVisible = false;
                overlay = null;
            }
        }

        public static void HideConsole()
        {
            if (!consoleVisible) return;
            try
            {
                if (overlay != null)
                {
                    if (overlay.panel != null) overlay.RemoveFromHierarchy();
                    outputContainer = null; outputScrollView = null;
                    inputTextField = null; execButton = null;
                    overlay = null;
                }
            }
            catch (Exception e) { Debug.LogWarning($"[CommandConsole] 移除 UI 时出错: {e.Message}"); }
            finally
            {
                consoleVisible = false;
                lastGameViewSize = Vector2.zero;
                if (gameView != null) gameView.Repaint();
            }
        }

        private static bool EnsureGameView()
        {
            if (gameView != null && gameView.rootVisualElement != null) return true;
            try
            {
                if (gameViewType == null)
                    gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null)
                {
                    var views = Resources.FindObjectsOfTypeAll(gameViewType);
                    if (views != null && views.Length > 0)
                    {
                        gameView = views[0] as EditorWindow;
                        return gameView != null && gameView.rootVisualElement != null;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void UpdateOverlayPosition()
        {
            if (overlay == null || gameView == null) return;
            try
            {
                Vector2 size = gameView.position.size;
                float h = Mathf.Max(size.y * OVERLAY_HEIGHT_RATIO, 180f);
                overlay.style.left = 0;
                overlay.style.width = size.x;
                overlay.style.height = h;
            }
            catch { }
        }

        #endregion

        #region UI 构建

        private static VisualElement CreateConsoleUI()
        {
            var root = new VisualElement { name = "CommandConsoleOverlay", pickingMode = PickingMode.Position };
            root.style.backgroundColor = BgColor;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = PADDING;
            root.style.paddingRight = PADDING;
            root.style.paddingBottom = PADDING;
            root.style.paddingTop = PADDING;
            root.style.overflow = Overflow.Hidden;

            // ===== 输出区域 =====
            outputContainer = new VisualElement { name = "outputContainer" };
            outputContainer.style.flexGrow = 1;
            outputContainer.style.paddingLeft = 2;
            outputContainer.style.paddingRight = 0;
            outputContainer.style.paddingTop = 0;
            outputContainer.style.paddingBottom = 2;
            outputContainer.style.minHeight = LINE_HEIGHT;

            outputScrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "outputScrollView",
                style = { flexGrow = 1 }
            };
            outputScrollView.contentContainer.style.flexDirection = FlexDirection.Column;
            outputScrollView.contentContainer.style.flexGrow = 1;
            outputScrollView.contentContainer.Add(outputContainer);
            outputScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            outputScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(outputScrollView);

            // ===== 分隔线 =====
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = AccentColor;
            separator.style.flexShrink = 0;
            separator.style.marginTop = 4;
            separator.style.marginBottom = 4;
            root.Add(separator);

            // ===== 输入区域 =====
            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.height = 20;
            inputRow.style.flexShrink = 0;
            inputRow.style.alignItems = Align.Center;

            inputTextField = new TextField { name = "ConsoleTextField", value = inputText };
            inputTextField.style.flexGrow = 1;
            inputTextField.RegisterCallback<FocusInEvent>(evt =>
            {
                if (inputTextField.value == "输入命令...")
                {
                    inputTextField.SetValueWithoutNotify("");
                    inputTextField.style.color = TextColor;
                }
            });
            inputTextField.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (string.IsNullOrWhiteSpace(inputTextField.value))
                {
                    inputTextField.SetValueWithoutNotify("输入命令...");
                    inputTextField.style.color = new Color(0.5f, 0.5f, 0.5f);
                }
            });
            inputTextField.SetValueWithoutNotify(inputText.Length > 0 ? inputText : "输入命令...");
            if (string.IsNullOrEmpty(inputText)) inputTextField.style.color = new Color(0.5f, 0.5f, 0.5f);
            inputTextField.style.height = 20;
            inputTextField.style.fontSize = 12;
            inputTextField.style.color = TextColor;
            inputTextField.style.paddingLeft = 0;
            inputTextField.style.paddingRight = 1;
            inputTextField.style.marginAll(1);
            inputRow.style.borderWidthAll(0);

            inputTextField.style.unityTextAlign = TextAnchor.MiddleLeft;
            inputTextField.RegisterValueChangedCallback(evt =>
            {
                inputText = evt.newValue;
                UpdateSuggestions();
            });
            inputTextField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (!string.IsNullOrWhiteSpace(inputText)) { ExecuteCommand(inputText); inputText = ""; inputTextField.SetValueWithoutNotify(""); HideSuggestions(); inputTextField.Focus(); }
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.UpArrow)
                {
                    if (suggestionDropdown != null && suggestionDropdown.style.display == DisplayStyle.Flex)
                    {
                        if (selectedSuggestionIndex > 0) { selectedSuggestionIndex--; UpdateSuggestionHighlight(); }
                        evt.StopPropagation();
                    }
                    else if (inputHistory.Count > 0)
                    {
                        if (historyIndex > 0) { historyIndex--; inputText = inputHistory[historyIndex]; inputTextField.SetValueWithoutNotify(inputText); inputTextField.SelectRange(inputText.Length, inputText.Length); }
                        evt.StopPropagation();
                    }
                }
                else if (evt.keyCode == KeyCode.DownArrow)
                {
                    if (suggestionDropdown != null && suggestionDropdown.style.display == DisplayStyle.Flex)
                    {
                        if (selectedSuggestionIndex < suggestionItems.Count - 1) { selectedSuggestionIndex++; UpdateSuggestionHighlight(); }
                        evt.StopPropagation();
                    }
                    else if (inputHistory.Count > 0)
                    {
                        if (historyIndex < inputHistory.Count - 1) { historyIndex++; inputText = inputHistory[historyIndex]; inputTextField.SetValueWithoutNotify(inputText); inputTextField.SelectRange(inputText.Length, inputText.Length); }
                        else { historyIndex = inputHistory.Count; inputText = ""; inputTextField.SetValueWithoutNotify(""); }
                        evt.StopPropagation();
                    }
                }
                else if (evt.keyCode == KeyCode.Tab)
                {
                    if (suggestionDropdown != null && suggestionDropdown.style.display == DisplayStyle.Flex && selectedSuggestionIndex >= 0)
                    {
                        SelectSuggestion(selectedSuggestionIndex);
                        evt.StopPropagation();
                    }
                }
            });
            inputRow.Add(inputTextField);

            execButton = new Button(() =>
            {
                if (!string.IsNullOrWhiteSpace(inputText)) { ExecuteCommand(inputText); inputText = ""; inputTextField.SetValueWithoutNotify(""); HideSuggestions(); inputTextField.Focus(); }
            });
            execButton.text = "执行";
            execButton.style.width = 40;
            execButton.style.height = 20;
            execButton.style.fontSize = 11;
            execButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            execButton.style.color = TextColor;
            execButton.style.backgroundColor = AccentColor;
            execButton.style.borderLeftWidth = 0;
            execButton.style.borderRightWidth = 1;
            execButton.style.borderTopWidth = 1;
            execButton.style.borderBottomWidth = 1;
            execButton.style.borderRightColor = AccentColor;
            execButton.style.borderTopColor = AccentColor;
            execButton.style.borderBottomColor = AccentColor;
            execButton.style.marginLeft = 0;
            execButton.style.paddingLeft = 0;
            execButton.style.paddingRight = 0;
            inputRow.Add(execButton);
            root.Add(inputRow);

            // ===== 联想下拉列表 =====
            suggestionDropdown = new VisualElement();
            suggestionDropdown.style.display = DisplayStyle.None;
            suggestionDropdown.style.position = Position.Absolute;
            suggestionDropdown.style.left = PADDING;
            suggestionDropdown.style.right = PADDING;
            suggestionDropdown.style.bottom = 30;
            suggestionDropdown.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            suggestionDropdown.style.borderWidthAll(1);
            suggestionDropdown.style.borderColorAll(AccentColor);
            suggestionDropdown.style.borderRadiusAll(3);
            suggestionDropdown.style.maxHeight = 120;
            suggestionDropdown.style.overflow = Overflow.Hidden;
            root.Add(suggestionDropdown);

            RebuildOutputLines();
            return root;
        }

        #region 联想建议

        private static void UpdateSuggestions()
        {
            if (suggestionDropdown == null || string.IsNullOrWhiteSpace(inputText))
            {
                HideSuggestions();
                return;
            }
            var matches = commandCache?.Keys
                .Where(k => k.IndexOf(inputText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(k => k)
                .ToList();

            if (matches == null || matches.Count == 0)
            {
                HideSuggestions();
                return;
            }

            suggestionItems = matches;
            selectedSuggestionIndex = -1;
            suggestionDropdown.Clear();

            for (int i = 0; i < matches.Count; i++)
            {
                int idx = i;
                string name = matches[i];
                if (commandCache.TryGetValue(name, out var info))
                {
                    string desc = string.IsNullOrEmpty(info.Description) ? "无描述" : info.Description;
                    string usage = string.IsNullOrEmpty(info.Usage) || info.Usage == name ? "" : $"  |  使用方法：{info.Usage}";
                    name = $"命令名称：{name}  |  描述：{desc}{usage}";
                }
                var item = new Label(name)
                {
                    style =
                    {
                        fontSize = 12, color = TextColor, height = LINE_HEIGHT,
                        paddingLeft = 4, paddingRight = 4,
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };
                item.RegisterCallback<PointerDownEvent>(evt => { SelectSuggestion(idx); evt.StopPropagation(); });
                item.RegisterCallback<PointerEnterEvent>(evt => { selectedSuggestionIndex = idx; UpdateSuggestionHighlight(); });
                suggestionDropdown.Add(item);
            }

            suggestionDropdown.style.display = DisplayStyle.Flex;
        }

        private static void HideSuggestions()
        {
            if (suggestionDropdown != null)
            {
                suggestionDropdown.style.display = DisplayStyle.None;
                suggestionDropdown.Clear();
            }
            suggestionItems.Clear();
            selectedSuggestionIndex = -1;
        }

        private static void UpdateSuggestionHighlight()
        {
            var children = suggestionDropdown.Children().ToList();
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Label label)
                    label.style.backgroundColor = i == selectedSuggestionIndex
                        ? new Color(0.3f, 0.3f, 0.3f)
                        : Color.clear;
            }
        }

        private static void SelectSuggestion(int index)
        {
            if (index >= 0 && index < suggestionItems.Count)
            {
                inputText = suggestionItems[index] + " ";
                inputTextField.SetValueWithoutNotify(inputText);
                inputTextField.Focus();
                inputTextField.SelectRange(inputText.Length, inputText.Length);
                HideSuggestions();
            }
        }

        #endregion

        #endregion

        #region 输出管理

        private static void AddOutput(string text, OutputType type = OutputType.Info)
        {
            outputLines.Add(new OutputLineData { Text = text, Type = type });
            if (outputLines.Count > 200) outputLines.RemoveRange(0, outputLines.Count - 200);
            if (outputContainer != null)
            {
                outputContainer.Add(CreateOutputLabel(text, type));
                EditorApplication.delayCall -= ScrollOutputToBottom;
                EditorApplication.delayCall += ScrollOutputToBottom;
            }
        }

        private static void RebuildOutputLines()
        {
            if (outputContainer == null) return;
            outputContainer.Clear();
            int start = Math.Max(0, outputLines.Count - 80);
            for (int i = start; i < outputLines.Count; i++)
            {
                var line = outputLines[i];
                outputContainer.Add(CreateOutputLabel(line.Text, line.Type));
            }
            EditorApplication.delayCall -= ScrollOutputToBottom;
            EditorApplication.delayCall += ScrollOutputToBottom;
        }

        private static Label CreateOutputLabel(string text, OutputType type)
        {
            Color c = type switch
            {
                OutputType.Error => ErrorColor,
                OutputType.Success => SuccessColor,
                OutputType.Command => CommandColor,
                OutputType.Help => HelpColor,
                _ => TextColor
            };
            return new Label(text)
            {
                enableRichText = true,
                style =
                {
                    fontSize = 12, color = c, height = LINE_HEIGHT, unityTextAlign = TextAnchor.UpperLeft,
                    overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis
                }
            };
        }

        private static void ScrollOutputToBottom()
        {
            EditorApplication.delayCall -= ScrollOutputToBottom;
            if (outputScrollView != null)
                outputScrollView.scrollOffset = new Vector2(0, float.MaxValue);
        }

        #endregion

        #region 命令发现

        private static void RefreshCommandCache()
        {
            commandCache = new Dictionary<string, ConsoleCommandInfo>(StringComparer.OrdinalIgnoreCase);
            cacheInitialized = true;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = assembly.GetName().Name;
                    if (name.StartsWith("System") || name.StartsWith("Mono") ||
                        name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor") ||
                        name.StartsWith("mscorlib") || name.StartsWith("netstandard"))
                        continue;
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            var classAttr = type.GetCustomAttribute<ConsoleCommandAttribute>(false);
                            if (classAttr != null) RegisterClassCommand(type, classAttr);
                            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                            {
                                var methodAttr = method.GetCustomAttribute<ConsoleCommandAttribute>(false);
                                if (methodAttr != null) RegisterMethodCommand(method, methodAttr);
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException) { }
                    catch (Exception ex) { Debug.LogWarning($"[CommandConsole] 扫描 {name} 出错: {ex.Message}"); }
                }
            }
            catch (Exception e) { Debug.LogError($"[CommandConsole] 命令扫描失败: {e.Message}"); }
        }

        private static void RegisterClassCommand(Type type, ConsoleCommandAttribute attr)
        {
            if (!typeof(ICommand).IsAssignableFrom(type)) return;
            if (commandCache.ContainsKey(attr.CommandName)) return;
            commandCache[attr.CommandName] = new ConsoleCommandInfo
            {
                CommandName = attr.CommandName,
                Description = attr.Description,
                Usage = string.IsNullOrEmpty(attr.Usage) ? attr.CommandName : attr.Usage,
                IsClassCommand = true,
                ClassType = type,
                Method = null
            };
        }

        private static void RegisterMethodCommand(MethodInfo method, ConsoleCommandAttribute attr)
        {
            var ps = method.GetParameters();
            if (ps.Length != 1 || ps[0].ParameterType != typeof(string[])) return;
            if (commandCache.ContainsKey(attr.CommandName)) return;
            commandCache[attr.CommandName] = new ConsoleCommandInfo
            {
                CommandName = attr.CommandName,
                Description = attr.Description,
                Usage = string.IsNullOrEmpty(attr.Usage) ? attr.CommandName : attr.Usage,
                IsClassCommand = false,
                ClassType = null,
                Method = method
            };
        }

        #endregion

        #region 命令执行

        private static void ExecuteCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            string trimmed = input.Trim();
            inputHistory.Add(trimmed);
            if (inputHistory.Count > MAX_HISTORY) inputHistory.RemoveAt(0);
            historyIndex = inputHistory.Count;
            AddOutput($"> {trimmed}", OutputType.Command);

            string[] parts = ParseCommandLine(trimmed);
            string commandName = parts[0].ToLowerInvariant();
            string[] args = parts.Length > 1 ? parts.Skip(1).ToArray() : new string[0];

            if (commandName == "help" || commandName == "?") { ShowHelp(); return; }
            if (commandName == "clear" || commandName == "cls") { outputLines.Clear(); if (outputContainer != null) outputContainer.Clear(); return; }

            if (!cacheInitialized) RefreshCommandCache();
            if (commandCache == null || !commandCache.TryGetValue(commandName, out var cmdInfo))
            { AddOutput($"未知命令: '{commandName}'。输入 'help' 查看所有可用命令", OutputType.Error); return; }

            List<string> capturedErrors = new List<string>();
            bool hasError = false;
            Application.LogCallback logHandler = (condition, stackTrace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) { hasError = true; capturedErrors.Add(condition); }
            };

            try
            {
                Application.logMessageReceived += logHandler;
                string result = cmdInfo.IsClassCommand ? ExecuteClassCommand(cmdInfo, args) : ExecuteMethodCommand(cmdInfo, args);
                if (hasError)
                {
                    string errorMsg = $"命令 '{commandName}' 执行失败（内部错误）";
                    foreach (var err in capturedErrors)
                    {
                        int idx = err.IndexOf('\n');
                        errorMsg += $"\n  {(idx > 0 ? err.Substring(0, idx) : err)}";
                    }
                    AddOutput(errorMsg, OutputType.Error);
                }
                else if (!string.IsNullOrEmpty(result)) AddOutput(result, OutputType.Success);
                else AddOutput($"命令 '{commandName}' 执行成功", OutputType.Success);
            }
            catch (Exception e) { AddOutput($"命令执行失败: {e.Message}", OutputType.Error); }
            finally { Application.logMessageReceived -= logHandler; }
        }

        private static string ExecuteClassCommand(ConsoleCommandInfo info, string[] args)
        {
            var instance = Activator.CreateInstance(info.ClassType) as ICommand;
            if (instance == null) return $"无法创建命令实例: {info.ClassType.Name}";

            // 依赖注入：解析 [Inject] 标记的依赖
            InjectHelper.AutoInject(instance);

            var parameters = GetCommandParameters(info.ClassType);
            if (parameters.Count > 0 && args.Length == 0)
            {
                string paramList = string.Join(", ", parameters.Select(p => $"{p.Name}: {GetFriendlyTypeName(p.PropertyType)}"));
                return $"缺少参数！该命令需要以下参数:\n  用法: {info.CommandName} <{string.Join("> <", parameters.Select(p => p.Name))}>\n  参数: {paramList}";
            }
            if (args.Length > parameters.Count) return $"参数过多！期望 {parameters.Count} 个，实际收到 {args.Length} 个。\n  用法: {info.FullUsage}";

            string paramDesc = "";
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i >= args.Length) return $"缺少参数: {parameters[i].Name} ({GetFriendlyTypeName(parameters[i].PropertyType)})";
                if (!TryParseParameter(args[i], parameters[i].PropertyType, out object parsedValue))
                    return $"参数 '{parameters[i].Name}' 解析失败: '{args[i]}' 不是有效的 {GetFriendlyTypeName(parameters[i].PropertyType)}。\n  用法: {info.FullUsage}";
                parameters[i].Property.SetValue(instance, parsedValue);
                paramDesc += (paramDesc.Length > 0 ? ", " : "") + $"{parameters[i].Name}: {parsedValue}";
            }

            var arch = FindGameArchitecture();
            if (arch == null) return "Architecture 尚未初始化";
            arch.SendCommand(instance);

            string msg = info.Description;
            if (!string.IsNullOrEmpty(paramDesc)) msg += $" (参数: {paramDesc})";
            return msg;
        }

        private static string ExecuteMethodCommand(ConsoleCommandInfo info, string[] args)
        {
            info.Method.Invoke(null, new object[] { args });
            return info.Description;
        }

        private static void ShowHelp()
        {
            AddOutput("内置: <color=#FFA600><b>help/?</b></color>  描述：查看帮助  |  <color=#FFA600><b>clear/cls</b></color>  描述：清空", OutputType.Info);
            if (commandCache != null && commandCache.Count > 0)
            {
                foreach (var cmd in commandCache.Values.OrderBy(c => c.CommandName))
                {
                    string desc = string.IsNullOrEmpty(cmd.Description) ? "无描述" : cmd.Description;
                    string usage = string.IsNullOrEmpty(cmd.Usage) || cmd.Usage == cmd.CommandName ? "" : $"  |  使用方法：{cmd.Usage}";
                    AddOutput($"命令名称：{cmd.CommandName}  |  描述：{desc}{usage}", OutputType.Info);
                }
            }
            else
            {
                AddOutput("暂未发现自定义命令", OutputType.Info);
            }
        }

        private static string[] ParseCommandLine(string input)
        {
            var parts = new List<string>();
            bool inQuotes = false;
            string current = "";
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ' ' && !inQuotes) { if (current.Length > 0) { parts.Add(current); current = ""; } }
                else current += c;
            }
            if (current.Length > 0) parts.Add(current);
            return parts.ToArray();
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == typeof(int)) return "整数 (int)";
            if (type == typeof(float)) return "小数 (float)";
            if (type == typeof(double)) return "小数 (double)";
            if (type == typeof(long)) return "长整数 (long)";
            if (type == typeof(bool)) return "布尔值 (bool)";
            if (type == typeof(string)) return "文本 (string)";
            if (type == typeof(uint)) return "无符号整数 (uint)";
            if (type == typeof(short)) return "短整数 (short)";
            if (type == typeof(byte)) return "字节 (byte)";
            return type.Name;
        }

        private static bool TryParseParameter(string value, Type targetType, out object result)
        {
            result = null;
            if (targetType == typeof(string)) { result = value; return true; }
            if (targetType == typeof(int)) { if (int.TryParse(value, out int r)) { result = r; return true; } return false; }
            if (targetType == typeof(float)) { if (float.TryParse(value, out float r)) { result = r; return true; } return false; }
            if (targetType == typeof(double)) { if (double.TryParse(value, out double r)) { result = r; return true; } return false; }
            if (targetType == typeof(long)) { if (long.TryParse(value, out long r)) { result = r; return true; } return false; }
            if (targetType == typeof(bool)) { if (bool.TryParse(value, out bool r)) { result = r; return true; } if (value == "1") { result = true; return true; } if (value == "0") { result = false; return true; } return false; }
            if (targetType == typeof(uint)) { if (uint.TryParse(value, out uint r)) { result = r; return true; } return false; }
            if (targetType == typeof(short)) { if (short.TryParse(value, out short r)) { result = r; return true; } return false; }
            if (targetType == typeof(byte)) { if (byte.TryParse(value, out byte r)) { result = r; return true; } return false; }
            if (targetType.IsEnum) { try { result = Enum.Parse(targetType, value, true); return true; } catch { return false; } }
            try { result = Convert.ChangeType(value, targetType); return true; } catch { return false; }
        }

        private static Architecture FindGameArchitecture()
        {
            var allArchs = Resources.FindObjectsOfTypeAll<Architecture>();
            if (allArchs != null && allArchs.Length > 0)
            {
                foreach (var a in allArchs)
                {
                    if (a == null || string.IsNullOrEmpty(a.gameObject.scene.name)) continue;
                    if ((a.gameObject.hideFlags & HideFlags.HideAndDontSave) != 0) continue;
                    return a;
                }
                return allArchs[0];
            }
            return Architecture.Instance;
        }

        #endregion

        #region 菜单项

        [MenuItem("Tools/命令控制台 (嵌入 Game 视图)")]
        private static void ToggleConsoleMenuItem()
        {
            if (consoleVisible) HideConsole(); else ShowConsole();
        }

        [MenuItem("Tools/命令控制台 (嵌入 Game 视图)", true)]
        private static bool ValidateConsoleMenuItem() => EditorApplication.isPlaying;

        #endregion
    }
}
