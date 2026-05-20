# 本地化工具 (Localization Tool)

## 概述

`FFramework` 本地化工具支持多语言文本和字体切换，使用 **CSV 文件** 管理所有语言数据，并提供独立的 `LocalizationComponent` 组件拖拽使用。

**v3.0.0 新增**：支持**多 CSV 按组管理**，可在 `LocalizationConfig` 中配置多个 CSV 文件，运行时按需加载/卸载（适用于按章节划分故事文本等场景）。

**v3.1.0 优化**：

- **性能提升**：Key 查找 O(1)、字体查找带缓存、CSV 解析 GC 减少
- **Bug 修复**：自动加载失败降级显示、语言列兼容性校验、无 base 组自动 fallback
- **事件分离**：新增 `OnDataChanged` 事件（数据加载/卸载专用），`OnLanguageChanged` 仅语言切换时触发
- **健壮性**：新增 `OnInitialized` 事件，支持 Manager 延迟初始化场景
- **日志精简**：正常流程日志静默，仅保留错误和警告

## 文件结构

```
Assets/FFramework/Utility/Localization/
├── LocalizationData.cs                   — 运行时语言数据模型（支持分组管理，O(1) Key 查询）
├── LocalizationConfig.cs                 — ScriptableObject 配置资产（含 CSV 分组配置，字体缓存）
├── LocalizationManager.cs                — 核心管理器（单例，支持动态加载/卸载 CSV 组）
├── LocalizationComponent.cs              — 可拖拽的 UI 本地化组件（延迟绑定，双重事件监听）
├── LocalizationDoc.md                    — 本文档
└── Resources/
    ├── LocalizationConfig.asset           — 本地化配置资产
    └── Localization/
        ├── localization.csv              — 基础语言数据文件（推荐 groupId="base"）
        ├── chapter_1.csv                 — 第一章对话
        └── chapter_2.csv                 — 第二章对话
```

---

## 快速开始

### 1. 创建 LocalizationConfig 配置资产

在 Project 窗口中右键 → **Create → FFramework → Localization → LocalizationConfig**

命名为 `LocalizationConfig` 并放置到 `Resources/` 目录下。

### 2. 配置 LocalizationConfig

在 Inspector 中配置：

| 字段               | 说明                                                    |
| ------------------ | ------------------------------------------------------- |
| `Default Language` | 默认语言名称，与 CSV 标题列对应，如 `ChineseSimplified` |
| `CSV Groups`       | CSV 分组列表（详见下方"多 CSV 按组管理"）               |
| `Font Mappings`    | 每种语言对应的 UGUI 字体和 TMP 字体                     |

**字体映射配置示例：**

|   Language Code   |    Font (UGUI)     |      TMP Font      |
| :---------------: | :----------------: | :----------------: |
| ChineseSimplified | NotoSansSC-Regular |   NotoSansSC SDF   |
|      English      |       Arial        | LiberationSans SDF |
|     Japanese      | NotoSansJP-Regular |   NotoSansJP SDF   |

> **提示**：字体映射是可选的。如果未配置字体，切换语言时文本内容会更新，但字体保持不变。

### 3. 编辑 CSV 语言文件

打开 `Assets/FFramework/Utility/Localization/Resources/Localization/localization.csv`

**格式规范：**

```csv
key,ChineseSimplified,English,Japanese
ui_start_game,开始游戏,Start Game,ゲームスタート
ui_level_display,等级 {0},Level {0},レベル {0}
```

- **第一行**：标题行 `key,语言名称1,语言名称2,...`
- **后续行**：`key,翻译1,翻译2,...`
- **注释**：以 `#` 开头的行为注释
- **含逗号的值**：使用双引号包裹，如 `"Hello, world!"`

### 4. 使用 LocalizationComponent 组件

1. 在 Hierarchy 中选择一个 UI 对象（Text 或 TextMeshPro）
2. **Add Component** → 搜索 `Localization Component`
3. 在 Inspector 中配置：
   - **Key**: 输入或从下拉列表选择本地化 Key
   - **Group Id（可选）**：当 Key 不存在时，自动加载此 CSV 组
   - **格式化参数（可选）**：启用后可为 `{0}`、`{1}` 等占位符传参
   - **组件引用**：自动查找 Text/TMP 组件，可手动覆盖

### 5. 运行时切换语言

```csharp
// 切换到英语
LocalizationManager.Instance.SetLanguage("English");

// 切换到简体中文
LocalizationManager.Instance.SetLanguage("ChineseSimplified");

// 切换到日语
LocalizationManager.Instance.SetLanguage("Japanese");
```

切换语言时，所有 `LocalizationComponent` 会自动刷新文本和字体。

---

## 多 CSV 按组管理（v3.0.0 新增 / v3.1.0 增强）

### 适用场景

当游戏有多个章节/关卡，每个章节有自己的对话和剧情文本时，可以将它们拆分为独立的 CSV 文件，按需加载/卸载，避免一次性加载所有数据占用过多内存。

### 配置方式

在 `LocalizationConfig.asset` 的 Inspector 中配置 **CSV Groups** 列表：

```
LocalizationConfig
├── Default Language: ChineseSimplified
├── CSV Groups (Size: 4)
│   ├── Element 0
│   │   ├── Group Id: "base"              ← 基础 UI 文本（启动时自动加载）
│   │   └── Csv File: localization.csv
│   ├── Element 1
│   │   ├── Group Id: "story_act1"        ← 第一章对话/剧情
│   │   └── Csv File: chapter_1.csv
│   ├── Element 2
│   │   ├── Group Id: "dialogue_forest"   ← 森林区域对话
│   │   └── Csv File: forest_dialogue.csv
│   └── Element 3
│       ├── Group Id: "npc_town"          ← 城镇 NPC 对话
│       └── Csv File: npc_town.csv
└── Font Mappings: [...]
```

每个章节 CSV 的格式与基础 CSV 完全一致（相同的语言列），只是 Key 不同：

```csv
key,ChineseSimplified,English
ch1_narrator_intro,在一个遥远的王国...,In a faraway kingdom...
ch1_hero_greeting,你好，勇士！,Hello, warrior!
```

### 自动加载与组范围限制（通过 LocalizationComponent 的 Group Id 字段）

**v3.0.0 核心特性**：`LocalizationComponent` 上新增了 **Group Id** 字段，当 Key 在当前数据中不存在时，会自动加载该组对应的 CSV 文件。

**v3.1.1 组范围限制**：配置了 Group Id 后，组件**只显示该组内的 Key**。如果 Key 属于其他分组（即使已加载），也会显示 `[key]` 占位符。

**组件配置示例：**

```
Localization Component (Script)
├── Key: "ch1_dialogue_001"    ← 本地化 Key（仅当属于 story_act1 组时显示）
├── Group Id: "story_act1"     ← 自动加载 + 显示范围限制
├── Enable Formatting: ☐
└── ...
```

**工作流程（v3.1.0 改进）**：

1. 组件刷新时，检测到 `ch1_dialogue_001` 在当前数据中不存在
2. 检查 `Group Id` 字段，发现配置了 `"story_act1"`
3. 自动调用 `LoadCsvGroup("story_act1")` 加载对应的 CSV 文件
4. 加载完成后触发 **`OnDataChanged`** 事件，组件再次刷新
5. 刷新时验证 `ch1_dialogue_001` 是否属于 `story_act1` 组：
   - 属于 → 正常显示文本
   - 不属于 → 显示 `[ch1_dialogue_001]` 占位符（即使该 Key 存在于其他已加载的组中）
6. **如果加载失败（CSV 不存在/解析错误）**：组件仍会 fallthrough 显示 `[ch1_dialogue_001]` 占位符，不会卡死

> **注意**：如果该组已加载过，不会重复加载。如果未配置 Group Id（为空），则跳过自动加载逻辑且不做范围限制。

### 运行时 API（手动控制）

```csharp
// 从 LocalizationConfig 中加载指定组
LocalizationManager.Instance.LoadCsvGroup("story_act1");

// 直接通过 TextAsset 加载（不依赖 Config）
LocalizationManager.Instance.LoadCsv(chapter1CsvAsset, "story_act1");

// 直接通过文本内容加载（如从网络下载）
string csvContent = "key,ChineseSimplified,English\n...";
LocalizationManager.Instance.LoadCsv(csvContent, "download_chapter");

// 卸载指定组（释放内存）
LocalizationManager.Instance.UnloadGroup("story_act1");

// 检查指定组是否已加载
if (LocalizationManager.Instance.IsGroupLoaded("dialogue_forest"))
{
    Debug.Log("森林对话数据已加载");
}

// 获取所有已加载的组 ID
string[] loadedGroups = LocalizationManager.Instance.GetLoadedGroups();
```

### 典型使用流程

```csharp
// 游戏启动时：基础 CSV 已在 Inspector 中拖拽，自动加载为 "base" 组

// 方式一：通过 LocalizationComponent 的 Group Id 字段自动加载
// 在 Inspector 中配置 Key="ch1_dialogue_001", Group Id="story_act1"
// 当 Key 不存在时，组件会自动加载 "story_act1" 组

// 方式二：在代码中手动预加载（如进入新场景前提前加载）
LocalizationManager.Instance.LoadCsvGroup("dialogue_forest");

// 退出场景时卸载（释放内存）
LocalizationManager.Instance.UnloadGroup("dialogue_forest");

// 退出到主菜单时（卸载所有非 base 组）
string[] groups = LocalizationManager.Instance.GetLoadedGroups();
foreach (var group in groups)
{
    if (group != "base")
        LocalizationManager.Instance.UnloadGroup(group);
}
```

### 注意事项

1. **base 组不可卸载**：基础 CSV 加载后自动标记为 `base` 组，调用 `UnloadGroup("base")` 会被忽略
2. **重复加载自动替换**：如果对同一 groupId 重复调用 `LoadCsvGroup`，旧数据会被自动卸载再重新加载
3. **Group Id 需与 Config 中一致**：`LocalizationComponent` 上填写的 `Group Id` 必须与 `LocalizationConfig` 中定义的 `groupId` 完全一致（区分大小写）
4. **加载/卸载自动刷新 UI**：加载或卸载 CSV 组后，所有 `LocalizationComponent` 会自动刷新显示
5. **自动加载无需代码**：组件在 `Refresh()` 中自动检测 Key 是否存在，若不存在且配置了 `Group Id`，则自动加载对应的 CSV 组

---

## v3.1.0 新增 API

### LocalizationManager 新增事件

```csharp
// [新增] 数据变更事件（加载/卸载 CSV 分组时触发）
// groupId: 变更的组 ID
// changeType: DataChangeType.Loaded 或 DataChangeType.Unloaded
LocalizationManager.Instance.OnDataChanged += (groupId, changeType) => {
    Debug.Log($"组 '{groupId}' 已{(changeType == DataChangeType.Loaded ? "加载" : "卸载")}");
};

// [新增] 初始化完成事件（Manager 初始化完成后触发）
// 用于组件延迟绑定场景
LocalizationManager.Instance.OnInitialized += () => {
    Debug.Log("本地化系统已就绪");
};

// [新增] 检查 Manager 是否已完成初始化
bool ready = LocalizationManager.Instance.IsInitialized;
```

### DataChangeType 枚举

```csharp
public enum DataChangeType
{
    Loaded,   // 加载 CSV 分组
    Unloaded, // 卸载 CSV 分组
}
```

### LocalizationComponent 新增行为

| 行为           | 说明                                                                             |
| -------------- | -------------------------------------------------------------------------------- |
| **延迟绑定**   | 如果 Manager 尚未初始化，组件自动注册 `OnInitialized` 事件，初始化完成后自动绑定 |
| **双重监听**   | 同时监听 `OnLanguageChanged`（语言切换）和 `OnDataChanged`（数据变更）           |
| **智能刷新**   | 仅当 `OnDataChanged` 的组 ID 与组件的 `Group Id` 匹配时，才执行刷新              |
| **组范围限制** | 配置了 Group Id 后，组件只显示该组内的 Key，跨组 Key 显示占位符                  |

---

## API 参考

### LocalizationManager

```csharp
// 单例实例
LocalizationManager.Instance

// 当前语言名称
string currentLang = LocalizationManager.Instance.CurrentLanguage;

// 获取翻译文本
string text = LocalizationManager.Instance.GetText("ui_start_game");

// 获取带格式参数的文本
string levelText = LocalizationManager.Instance.GetText("ui_level_display", 5);
// → "等级 5" (ChineseSimplified) / "Level 5" (English) / "レベル 5" (Japanese)

// 切换语言
LocalizationManager.Instance.SetLanguage("English");

// 检查 Key 是否存在
bool exists = LocalizationManager.Instance.HasKey("ui_start_game");

// 获取所有 Key
List<string> allKeys = LocalizationManager.Instance.GetAllKeys();

// 获取支持的语言列表
List<string> languages = LocalizationManager.Instance.GetSupportedLanguages();

// 获取字体
Font font = LocalizationManager.Instance.GetFont("ChineseSimplified");
TMPro.TMP_FontAsset tmpFont = LocalizationManager.Instance.GetTMPFont("ChineseSimplified");

// 监听语言变化
LocalizationManager.Instance.OnLanguageChanged += (languageName) => {
    Debug.Log($"语言已切换为: {languageName}");
};

// ===== v3.0.0 新增 API =====

// 从 Config 加载 CSV 组
LocalizationManager.Instance.LoadCsvGroup("chapter_1");

// 直接加载 TextAsset 到指定组
LocalizationManager.Instance.LoadCsv(csvTextAsset, "chapter_1");

// 直接加载文本内容到指定组
LocalizationManager.Instance.LoadCsv(csvTextContent, "chapter_1");

// 卸载指定组
LocalizationManager.Instance.UnloadGroup("chapter_1");

// 检查组是否已加载
bool loaded = LocalizationManager.Instance.IsGroupLoaded("chapter_1");

// 获取所有已加载的组
string[] groups = LocalizationManager.Instance.GetLoadedGroups();

// ===== v3.1.1 新增 API =====

// 检查 Key 是否属于指定分组
bool inGroup = LocalizationManager.Instance.HasKeyInGroup("ch1_dialogue_001", "story_act1");
```

### LocalizationComponent（v3.1.0 更新）

| 属性/方法         | 说明                                                           |
| ----------------- | -------------------------------------------------------------- |
| `LocalizationKey` | 获取/设置本地化 Key（设置后自动刷新）                          |
| `IsBound`         | 是否已绑定到管理器事件                                         |
| `Bind()`          | 手动绑定到管理器（同时绑定 OnLanguageChanged + OnDataChanged） |
| `Unbind()`        | 手动解绑                                                       |
| `Rebind()`        | 重新绑定                                                       |
| `Refresh()`       | 强制刷新文本和字体（加载失败时显示 `[key]` 占位符）            |

> **v3.1.0 行为变更**：`Refresh()` 不再因 `LoadCsvGroup` 失败而卡住，始终会 fallthrough 显示占位符。`Bind()` 现在同时绑定语言变更和数据变更两个事件。
>
> **v3.1.1 行为变更**：`groupId` 新增组范围限制功能。配置了 `groupId` 的组件只显示该分组内的 Key，不再显示其他已加载分组中的同名 Key。新增 `LocalizationManager.HasKeyInGroup(key, groupId)` 方法。

---

## CSV 编辑建议

### 使用 Excel / WPS 编辑

1. 打开 Excel/WPS → **数据 → 自文本/CSV**
2. 选择 `localization.csv`
3. 选择分隔符为 **逗号**
4. 编码选择 **UTF-8**
5. 编辑完成后 **另存为 → CSV UTF-8**

### 使用 VS Code 编辑

推荐安装 **Rainbow CSV** 插件，为 CSV 列着色，便于区分不同语言。

### 添加新语言

1. 在 CSV 标题行新增一列，如 `,French`
2. 为每个已有 Key 添加法语翻译
3. 在 `LocalizationConfig` 中添加字体映射
4. 运行时调用 `SetLanguage("French")`

### 添加新 Key

1. 在 CSV 末尾追加一行：`my_new_key,中文翻译,English,日本語`
2. 将 `LocalizationComponent` 的 Key 设置为 `my_new_key`

---

## 完整示例

### 步骤 1: 创建配置

```
Project 窗口
└── Resources/
    ├── LocalizationConfig.asset    ← Create → FFramework → Localization → LocalizationConfig
    └── Localization/
        ├── localization.csv        ← 基础 UI 文本（在 Config 中配置为 base 组）
        ├── chapter_1.csv           ← 第一章对话（在 Config 中配置为 story_act1 组）
        ├── chapter_2.csv           ← 第二章对话
        └── chapter_3.csv           ← 第三章对话
```

### 步骤 2: 配置 LocalizationManager

1. 在场景中创建空 GameObject，命名为 `LocalizationManager`
2. Add Component → `Localization Manager`
3. 将 `LocalizationConfig.asset` 拖拽到 **Config** 字段
4. 确保 Config 的 CSV Groups 中包含 `base` 组（启动时自动加载）

### 步骤 3: 在场景中使用

```csharp
// 在游戏启动时初始化（首次访问 Instance 自动触发）
LocalizationManager.Instance.EnsureInitialized();

// 切换到英语
LocalizationManager.Instance.SetLanguage("English");

// 在代码中手动获取文本
string hpText = LocalizationManager.Instance.GetText("ui_hp_display", 100, 200);
// → "HP: 100/200"

// 进入第一章时加载章节数据
LocalizationManager.Instance.LoadCsvGroup("story_act1");

// 获取章节对话文本
string dialogue = LocalizationManager.Instance.GetText("ch1_narrator_intro");
```

### 步骤 3: 在 UI 上挂载组件

1. 创建 Text → 输入任意内容（会被组件覆盖）
2. Add Component → `Localization Component`
3. Key 输入: `ui_start_game`
4. 运行游戏 → 文本自动显示为当前语言的翻译

---

## 注意事项

1. **CSV 编码必须为 UTF-8**（不含 BOM），否则中文可能显示为乱码
2. **CSV 文件引用**：所有 CSV 文件通过 `LocalizationConfig` 的 `CSV Groups` 列表引用（TextAsset 拖拽），无需放在 Resources 目录下
3. **LocalizationConfig** 建议放在 `Resources/` 目录下，方便管理
4. **Key 不存在时** 会显示 `[key_name]` 格式的占位符，方便调试
5. **注释行**：以 `#` 开头的行会被解析器忽略，可用于分组说明
6. **base 组不可卸载**：基础 CSV 加载后自动标记为 `base` 组，不可卸载
7. **不同章节的 Key 建议使用不同前缀**（如 `ch1_`、`ch2_`），避免意外覆盖
8. **v3.1.0 优化**：`LocalizationConfig` 首组自动加载 — 即使没有名为 `"base"` 的组，Manager 初始化时也会自动加载配置中的第一个 CSV 组
9. **v3.1.0 兼容**：`OnDataChanged` 是新增事件，不影响旧版 `OnLanguageChanged` 的使用，两者同时触发以保持向后兼容
