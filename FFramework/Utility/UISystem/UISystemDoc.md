# FFramework UISystem 使用文档

> **版本**: 2.0.0 | **最后更新**: 2026-05-03
>
> 一个简洁高效的 Unity UI 管理框架，提供面板生命周期管理、事件绑定与自动清理、组件查找、多层级控制等功能。

---

## 目录

- [一、概述](#一概���)
- [二、快速开始](#二快速开始)
- [三、系统架构](#三系统架构)
- [四、UIPanel 面板基类](#四uipanel-面板基类)
- [五、UI 层级系统](#五ui-层级系统)
- [六、事件绑定系统](#六事件绑定系统)
- [七、组件查找与设置](#七组件查找与设置)
- [八、面板管理（UISystem）](#八面板管理uisystem)
- [九、资源加载](#九资源加载)
- [十、编辑器工具](#十编辑器工具)
- [十一、调试工具](#十一调试工具)
- [十二、最佳实践](#十二最佳实践)
- [十三、常见问题](#十三常见问题)
- [附录：API 速查表](#附录api-速查表)
- [附录：文件结构](#附录文件结构)

---

## 一、概述

### 1.1 系统架构

整个 UISystem 由三个核心模块组成，职责清晰、分层明确：

| 模块                                                                           | 角色                   | 核心职责                                                             |
| ------------------------------------------------------------------------------ | ---------------------- | -------------------------------------------------------------------- |
| [`UISystem`](Assets/FFramework/Utility/UISystem/UISystem.cs)                   | **系统管理器**（单例） | 面板打开/关闭/缓存、UI 层级管理、子物体组件查找、资源加载调度        |
| [`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs)                     | **面板基类**           | 面板生命周期（Show/Hide/Lock）、事件追踪与自动清理、CanvasGroup 控制 |
| [`UIEventExtensions`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs) | **静态扩展类**         | 为 `UIPanel` 提供事件绑定、组件获取、属性设置的链式调用方法          |

**模块间的关系**：

- [`UISystem`](Assets/FFramework/Utility/UISystem/UISystem.cs) **管理** [`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs) — 负责面板的创建、缓存、层级分配和销毁
- [`UIEventExtensions`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs) **扩展** [`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs) — 通过 C# 扩展方法为面板添加事件绑定和组件查找能力
- [`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs) **继承** [`ArchitectureViewController`](Assets/FFramework/Core/Architecture/ArchitectureViewController.cs) — 获得 Model 获取、事件发送/注册等架构能力
- [`UISystem`](Assets/FFramework/Utility/UISystem/UISystem.cs) **继承** [`SingletonMono<UISystem>`](Assets/FFramework/Core/SingletonMono.cs) — 以单例模式全局访问

### 1.2 特性一览

- **一行代码** 打开/关闭面板
- **自动事件清理** — 面板销毁时自动注销事件，防止内存泄漏
- **6 个 UI 层级** — Background → PostProcessing → Content → Popup → Guide → Debug
- **层级自动锁定** — 弹窗自动锁定下层交互
- **便捷组件查找** — 按名称快速获取 UI 组件，支持路径查找
- **Inspector 拖拽创建字段** — 编辑器下拖拽 GameObject 自动生成序列化字段
- **智能缓存** — 可选面板缓存，避免重复实例化
- **自定义资源加载** — 支持 `IUIResLoader` 接口扩展（如 Addressables）

---

## 二、快速开始

### 2.1 环境准备

`UISystem` 采用单例模式，**无需手动搭建 UI 根节点**。首次访问 [`UISystem.Instance`](Assets/FFramework/Utility/UISystem/UISystem.cs:21) 时自动执行：

1. 创建 `Canvas`（ScreenSpaceOverlay）+ `CanvasScaler`（参考分辨率 1920×1080）+ `GraphicRaycaster`
2. 创建 6 个 UI 层级节点
3. 创建 `EventSystem` + `StandaloneInputModule`

```csharp
// 无需额外操作，直接使用 Instance 即可触发自动初始化
UISystem.Instance.OpenPanel<MainPanel>();
```

> 如需手动触发初始化，可调用 `UISystem.Instance` 的任意属性或方法。

### 2.2 创建第一个面板

```csharp
using FFramework.Utility;
using UnityEngine.UI;

public class MainPanel : UIPanel
{
    // 必须重写：面板初始化（只调用一次）
    protected override void OnInitialize()
    {
        // 绑定按钮事件（自动追踪清理）
        this.BindButton("PlayBtn", OnPlay);
        this.BindButton("SettingsBtn", () => UISystem.Instance.OpenPanel<SettingsPanel>());
    }

    private void OnPlay()
    {
        UISystem.Instance.OpenPanel<GamePanel>();
    }
}
```

### 2.3 打开/关闭面板

```csharp
// 打开面板（默认 Content 层，启用缓存）
UISystem.Instance.OpenPanel<MainPanel>();

// 指定层级打开
UISystem.Instance.OpenPanel<MessageDialog>(UILayer.PopupLayer);

// 关闭面板
UISystem.Instance.ClosePanel<MainPanel>();

// 关闭当前顶层面板
UISystem.Instance.CloseCurrentPanel();
```

> **预制体约定**：面板预制体需放置在 `Resources/UI/` 目录下，名称与类名一致（如 `MainPanel.prefab`）。

---

## 三、系统架构

### 3.1 继承关系

**UISystem 继承链**（单例管理器）：

| 层级   | 类型                                                                 | 职责                                                |
| ------ | -------------------------------------------------------------------- | --------------------------------------------------- |
| 基类   | [`SingletonMono<UISystem>`](Assets/FFramework/Core/SingletonMono.cs) | 泛型单例基类，提供 `Instance` 全局访问              |
| 派生类 | [`UISystem`](Assets/FFramework/Utility/UISystem/UISystem.cs)         | UI 系统管理器，提供面板管理、组件查找、资源加载调度 |

**UISystem 核心成员**：

| 成员                                 | 说明           |
| ------------------------------------ | -------------- |
| `OpenPanel<T>()` / `ClosePanel<T>()` | 面板打开与关闭 |
| `GetChildComponent<T>()`             | 子物体组件查找 |
| `IUIResLoader`                       | 资源加载器接口 |
| `cachedPanels`                       | 面板缓存字典   |
| `activeStack`                        | 活跃面板栈     |
| `layerPanels`                        | 各层级面板列表 |

---

**UIPanel 继承链**（面板基类）：

| 层级     | 类型                                                                                              | 职责                                                |
| -------- | ------------------------------------------------------------------------------------------------- | --------------------------------------------------- |
| 根基类   | `MonoBehaviour`                                                                                   | Unity 组件基类                                      |
| 一级派生 | [`ArchitectureViewController`](Assets/FFramework/Core/Architecture/ArchitectureViewController.cs) | ViewController 基类，提供 Model 获取、事件发送/注册 |
| 二级派生 | [`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs)                                        | 面板基类，提供生命周期、显示控制、事件追踪          |
| 业务面板 | `你的具体面板类`                                                                                  | 继承 UIPanel 实现具体业务逻辑                       |

**ArchitectureViewController 提供的能力**：

| 方法                                     | 说明                     |
| ---------------------------------------- | ------------------------ |
| `GetModel<T>()`                          | 获取数据模型             |
| `SendEvent()` / `SendEvent<T>()`         | 发送事件（支持泛型参数） |
| `RegisterEvent()` / `RegisterEvent<T>()` | 注册事件监听             |
| `OnDestroy()`                            | 自动注销所有事件监听     |

**UIPanel 提供的能力**：

| 方法                                   | 说明                       |
| -------------------------------------- | -------------------------- |
| `Show()` / `Hide()` / `Close()`        | 显示控制                   |
| `OnLock()` / `OnUnLock()`              | 锁定控制                   |
| `OnInitialize()` (abstract)            | ★ **必须重写**，只调用一次 |
| `OnShow()` / `OnHide()`                | 显示/隐藏回调              |
| `OnPanelEnable()` / `OnPanelDisable()` | 启用/禁用回调              |
| `OnPanelDestroy()`                     | 销毁回调                   |
| `OnAwake()` / `OnStart()`              | Unity 生命周期回调         |
| `AddEventCleanup()`                    | 事件追踪与自动清理         |

### 3.2 ArchitectureViewController 提供的能力

由于 [`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs) 继承自 [`ArchitectureViewController`](Assets/FFramework/Core/Architecture/ArchitectureViewController.cs)，面板内可直接使用以下方法：

```csharp
public class MyPanel : UIPanel
{
    protected override void OnInitialize()
    {
        // 获取 Model
        var userModel = GetModel<UserModel>();

        // 发送事件
        SendEvent("OnLoginSuccess");
        SendEvent<int>("OnScoreChanged", 100);

        // 注册事件监听
        RegisterEvent("OnRefresh", OnRefresh);
    }

    private void OnRefresh()
    {
        Debug.Log("面板收到刷新事件");
    }
}
```

---

## 四、UIPanel 面板基类

### 4.1 生命周期

面板生命周期按以下阶段顺序执行：

| 阶段             | 触发时机                                                       | 调用方法       | 说明                                   |
| ---------------- | -------------------------------------------------------------- | -------------- | -------------------------------------- |
| **① 初始化阶段** | `Awake()` → `OnAwake()`                                        | 虚方法，可重写 | Unity 原生 Awake 回调                  |
|                  | `Start()` → `OnStart()`                                        | 虚方法，可重写 | Unity 原生 Start 回调                  |
|                  | 首次 `OnEnable()` → `base.Initialize()` → **`OnInitialize()`** | ★ **必须重写** | 只调用一次，适合绑定事件、获取组件引用 |
|                  | 非首次 `OnEnable()` → `OnPanelEnable()`                        | 虚方法，可重写 | 每次启用时回调                         |
| **② 显示阶段**   | `Show()` → `OnShow()`                                          | 虚方法，可重写 | 面板显示时回调                         |
| **③ 隐藏阶段**   | `Hide()` → `OnHide()`                                          | 虚方法，可重写 | 面板隐藏时回调                         |
| **④ 锁定阶段**   | `OnLock()` → `OnLockPanel()`                                   | 虚方法，可重写 | 面板被锁定时回调                       |
|                  | `OnUnLock()` → `OnUnlockPanel()`                               | 虚方法，可重写 | 面板解锁时回调                         |
| **⑤ 禁用阶段**   | `OnDisable()` → `OnPanelDisable()`                             | 虚方法，可重写 | 面板禁用时回调                         |
| **⑥ 销毁阶段**   | `OnDestroy()` → `OnPanelDestroy()` + `CleanupAll()`            | 自动执行       | 自动清理所有追踪的事件                 |

**关键说明**：

- **`OnInitialize()`** 是唯一必须重写的方法，仅在面板**首次启用时调用一次**
- 所有 `OnXxx()` 虚方法均为可选重写，默认空实现
- `CleanupAll()` 在销毁阶段自动调用，先清理追踪事件列表，再清理所有 UI 组件上绑定的事件

### 4.2 生命周期方法速查

| 方法                                                                    | 时机               | 是否必须重写 |
| ----------------------------------------------------------------------- | ------------------ | ------------ |
| [`OnInitialize()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:86)    | 首次启用时调用一次 | ✅ **是**    |
| [`OnAwake()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:89)         | Awake 时           | 否           |
| [`OnStart()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:92)         | Start 时           | 否           |
| [`OnPanelEnable()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:95)   | 每次启用时         | 否           |
| [`OnPanelDisable()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:98)  | 禁用时             | 否           |
| [`OnPanelDestroy()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:101) | 销毁时             | 否           |
| [`OnShow()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:104)         | 调用 `Show()` 时   | 否           |
| [`OnHide()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:107)         | 调用 `Hide()` 时   | 否           |
| [`OnLockPanel()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:110)    | 锁定时             | 否           |
| [`OnUnlockPanel()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:113)  | 解锁时             | 否           |

### 4.3 面板控制方法

```csharp
// 显示/隐藏
panel.Show();           // 显示面板（激活 GameObject，恢复射线检测）
panel.Hide();           // 隐藏面板（禁用 GameObject，关闭射线检测）
panel.Close();          // Hide() 的别名

// 锁定/解锁
panel.OnLock();         // 锁定面板（禁用交互，blocksRaycasts = false）
panel.OnUnLock();       // 解锁面板（恢复交互，如正在显示则恢复射线检测）

// 状态查询
bool isInit    = panel.IsInitialized;  // 是否已初始化
bool isShowing = panel.IsShowing;      // 是否正在显示
bool isLocked  = panel.IsLocked;       // 是否被锁定
UILayer layer  = panel.Layer;          // 所属层级
int eventCount = panel.EventCount;     // 追踪的事件数量
```

### 4.4 面板属性设置

```csharp
panel.SetAlpha(0.5f);              // 设置透明度 0~1
panel.SetInteractable(false);      // 设置可交互性
panel.SetBlocksRaycasts(true);     // 设置是否阻挡射线
```

---

## 五、UI 层级系统

### 5.1 层级枚举

```csharp
public enum UILayer
{
    BackgroundLayer,      // 背景层       - 静态背景/底图
    PostProcessingLayer,  // 后期处理层   - UI 特效
    ContentLayer,         // 内容层       - 主要功能界面（默认）
    PopupLayer,           // 弹窗层       - 消息对话框
    GuideLayer,           // 引导层       - 新手引导
    DebugLayer            // 调试层       - 开发调试工具
}
```

### 5.2 层级锁定机制

**打开面板流程**：

| 步骤   | 条件                                                             | 操作                   |
| ------ | ---------------------------------------------------------------- | ---------------------- |
| ① 检查 | 该层级已有活跃面板？且 `ShouldLockPreviousPanel()` 返回 `true`？ | → 锁定同层级上一个面板 |
|        | 否则                                                             | → 不锁定               |
| ② 显示 | —                                                                | 显示新面板，加入活跃栈 |

**关闭面板流程**：

| 步骤   | 条件                 | 操作                   |
| ------ | -------------------- | ---------------------- |
| ① 检查 | 该层级还有其他面板？ | → 解锁同层级最顶层面板 |
|        | 否则                 | → 无需操作             |

**关键规则**（来自 [`ShouldLockPreviousPanel()`](Assets/FFramework/Utility/UISystem/UISystem.cs:211)）：

- **`BackgroundLayer`** 和 **`PostProcessingLayer`** → 不会锁定下层面板（用于背景和特效）
- **`ContentLayer`**、**`PopupLayer`**、**`GuideLayer`**、**`DebugLayer`** → 打开新面板时会自动锁定同层级的上一个面板
- 关闭面板时自动解锁同层级的最新面板
- 锁定/解锁作用于 **同层级内**，不影响其他层级

### 5.3 使用示例

```csharp
// Content 层打开主菜单，自动锁定同层之前的面板
UISystem.Instance.OpenPanel<MainMenuPanel>(UILayer.ContentLayer);

// Popup 层打开弹窗，Content 层不受影响
UISystem.Instance.OpenPanel<MessageDialog>(UILayer.PopupLayer);

// 关闭弹窗后，自动解锁同层（Popup）其余面板
UISystem.Instance.ClosePanel<MessageDialog>();
```

---

## 六、事件绑定系统

所有事件绑定方法通过 [`UIEventExtensions`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs) 静态扩展类提供，建议在 [`OnInitialize()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:86) 中完成绑定。

### 6.1 基于名称绑定（推荐）

通过子物体名称查找组件并绑定事件，支持 **自动追踪清理**（默认开启）：

```csharp
protected override void OnInitialize()
{
    // ──── 基础 UI 组件 ────
    this.BindButton("StartBtn", OnStart);                              // Button 点击
    this.BindToggle("SoundToggle", OnSoundToggle);                     // Toggle 值变化
    this.BindSlider("VolumeSlider", OnVolumeChange);                   // Slider 值变化
    this.BindInputField("NameInput", OnNameChanged);                   // InputField 文本变化
    this.BindDropdown("QualityDropdown", OnQualityChanged);            // Dropdown 选项变化

    // ──── TextMeshPro 组件 ────
    this.BindTMPInputField("TMPInput", OnTMPInputChanged);            // TMP_InputField
    this.BindTMPDropdown("TMPDropdown", OnTMPDropdownChanged);        // TMP_Dropdown
}
```

**自动追踪说明**：`autoTrack = true`（默认）时，框架会记录清理委托到面板的 `eventCleanupActions` 列表，面板销毁时自动调用 [`ClearTrackedEvents()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:227) 统一清理。

### 6.2 直接组件绑定

当已持有组件引用时，可直接绑定：

```csharp
protected override void OnInitialize()
{
    Button btn = this.GetButton("DirectBtn");
    btn.BindClick(OnDirectClick, this);        // 传入 panel 以支持事件追踪

    Toggle toggle = this.GetToggle("EffectToggle");
    toggle.BindValueChanged(OnEffectChanged, this);
}
```

### 6.3 批量绑定

```csharp
protected override void OnInitialize()
{
    this.BindButtons(new Dictionary<string, UnityAction>
    {
        ["Btn1"] = OnBtn1,
        ["Btn2"] = OnBtn2,
        ["Btn3"] = OnBtn3
    });
}
```

### 6.4 手动事件管理

```csharp
// 关闭自动追踪
this.BindButton("Btn", OnClick, autoTrack: false);

// 手动添加清理动作
this.AddEventCleanup(() => SomeAction(), "ComponentName");

// 移除指定的清理动作
this.RemoveEventCleanup(cleanupAction);

// 清理追踪的事件（仅清理追踪列表中的）
this.ClearTrackedEvents();

// 强制清理当前面板下所有 UI 组件的事件（暴力清理）
this.ClearAllEvents();

// UnbindAllEvents 是 ClearAllEvents 的别名
this.UnbindAllEvents();
```

> **双重保险**：面板 [`OnDestroy()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:75) 时，`CleanupAll()` 会先调用 `ClearTrackedEvents()` 再调用 `UnbindAllEvents()`。

---

## 七、组件查找与设置

### 7.1 组件快捷获取

```csharp
// ──── 基础 UI 组件 ────
Button         btn      = this.GetButton("ButtonName");
Toggle         toggle   = this.GetToggle("ToggleName");
Slider         slider   = this.GetSlider("SliderName");
InputField     input    = this.GetInputField("InputName");
Dropdown       dropdown = this.GetDropdown("DropdownName");
Image          image    = this.GetImage("ImageName");
Text           text     = this.GetText("TextName");

// ──── TextMeshPro 组件 ────
TextMeshProUGUI  tmpText   = this.GetTMPText("TMPTextName");
TMP_InputField   tmpInput  = this.GetTMPInputField("TMPInputName");
TMP_Dropdown     tmpDropdown = this.GetTMPDropdown("TMPDropdownName");
```

### 7.2 通用组件查找

所有快捷方法都是通用方法 [`GetComponent<T>()`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:189) 的封装：

```csharp
// 通用查找（默认递归）
Button btn = this.GetComponent<Button>("MyButton");

// 路径查找（支持斜杠路径）
Button btn = this.GetComponent<Button>("Panel/Header/MyButton");

// 非递归查找
Button btn = this.GetComponent<Button>("MyButton", recursive: false);
```

### 7.3 组件属性设置

```csharp
// ──── 文本设置 ────
this.SetText("ScoreText", "Score: 1000");
this.SetTMPText("TMPText", "Hello World");

// ──── 组件状态 ────
this.SetButtonInteractable("StartBtn", false);
this.SetToggleValue("SoundToggle", true, sendCallback: false);           // 不触发回调
this.SetSliderValue("VolumeSlider", 0.8f, sendCallback: true);          // 触发回调

// ──── 图片设置 ────
this.SetImageSprite("Icon", newSprite);
this.SetImageColor("Background", Color.red);

// ──── 通用属性设置 ────
this.SetProperty<Button>("MyBtn", btn => btn.interactable = false);
this.SetProperty<Image>("MyImage", img => img.color = Color.white);
```

---

## 八、面板管理（UISystem）

### 8.1 打开面板

```csharp
// 方式一：从 Resources/UI/ 加载（类名匹配预制体名）
UISystem.Instance.OpenPanel<MainPanel>();                                    // 默认 Content 层，启用缓存
UISystem.Instance.OpenPanel<MainPanel>(UILayer.PopupLayer);                  // 指定层级
UISystem.Instance.OpenPanel<MainPanel>(UILayer.ContentLayer, useCache: false); // 禁用缓存

// 方式二：从预制体创建
GameObject prefab = Resources.Load<GameObject>("UI/CustomPanel");
UISystem.Instance.OpenPanel<CustomPanel>(prefab);
UISystem.Instance.OpenPanel<CustomPanel>(prefab, UILayer.PopupLayer);
```

### 8.2 关闭面板

```csharp
// 关闭指定类型的面板
UISystem.Instance.ClosePanel<MainPanel>();

// 关闭当前顶层面板
UISystem.Instance.CloseCurrentPanel();

// 批量清理所有面板
UISystem.Instance.ClearAllPanels(destroyGameObjects: true);
UISystem.Instance.ClearAllPanels(destroyGameObjects: false);  // 只禁用不销毁

// 清理指定层级的所有面板
UISystem.Instance.ClearPanelsInLayer(UILayer.PopupLayer);
```

### 8.3 获取/查询面板

```csharp
// 获取已打开的面板（不存在或未激活时返回 null）
var panel = UISystem.Instance.GetPanel<MainPanel>();

// 获取当前栈顶面板
var topPanel = UISystem.Instance.GetTopPanel<MainPanel>();
UIPanel current = UISystem.Instance.CurrentPanel;

// 检查当前面板是否为指定类型
bool isMain = UISystem.Instance.IsCurrentPanel<MainPanel>();

// 容器状态
int  openCount   = UISystem.Instance.OpenPanelCount;      // 当前打开数量
int  cachedCount = UISystem.Instance.CachedPanelCount;    // 缓存数量
bool hasOpen     = UISystem.Instance.HasOpenPanels;       // 是否有打开的面板
string currName  = UISystem.Instance.CurrentPanelName;     // 当前面板名
string currType  = UISystem.Instance.CurrentPanelTypeName; // 当前面板类型名

// 层级查询
int count  = UISystem.Instance.GetActivePanelCountInLayer(UILayer.PopupLayer);
bool has   = UISystem.Instance.HasActivePanelsInLayer(UILayer.PopupLayer);
```

### 8.4 缓存机制

- 面板首次打开后缓存到 [`cachedPanels`](Assets/FFramework/Utility/UISystem/UISystem.cs:37) 字典
- 再次打开同一面板时，如果 `useCache = true`，直接复用缓存实例
- 缓存面板会跟随层级变化自动移动父节点
- [`ClearAllPanels()`](Assets/FFramework/Utility/UISystem/UISystem.cs:412) 会清空所有缓存

---

## 九、资源加载

### 9.1 默认加载方式

默认使用 [`ResourcesUILoader`](Assets/FFramework/Utility/UISystem/UISystem.cs:923) 从 `Resources/UI/{PanelName}` 加载预制体：

```
Assets/
└── Resources/
    └── UI/
        ├── MainPanel.prefab
        ├── SettingsPanel.prefab
        └── MessageDialog.prefab
```

预制体名称必须与类名一致（如 `MainPanel.prefab` ↔ `MainPanel` 类）。

### 9.2 自定义资源加载器

实现 [`IUIResLoader`](Assets/FFramework/Utility/UISystem/UISystem.cs:910) 接口可接入其他加载方案（如 Addressables）：

```csharp
// 1. 实现接口
public class AddressablesUILoader : IUIResLoader
{
    public GameObject LoadPanelPrefab(string panelName)
    {
        // 使用 Addressables 加载
        var handle = Addressables.LoadAssetAsync<GameObject>($"UI/{panelName}");
        return handle.WaitForCompletion();
    }
}

// 2. 注册到 UISystem
UISystem.Instance.SetUIResLoader(new AddressablesUILoader());
```

```csharp
public interface IUIResLoader
{
    /// <param name="panelName">面板名称（通常是类名）</param>
    GameObject LoadPanelPrefab(string panelName);
}
```

---

## 十、编辑器工具

### 10.1 UIPanelInspector 总览

当选中继承自 [`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs) 的脚本时，自定义 Inspector [`UIPanelInspector`](Assets/FFramework/Utility/UISystem/Editor/UIPanelInspector.cs) 会提供额外的可视化功能。

Inspector 分为两个区域：

1. **字段管理区**（上部） — 序列化字段展示 + 拖拽创建字段
2. **UI 事件检查器**（下部） — 折叠面板，展示所有绑定了事件的 UI 组件

### 10.2 字段管理

#### 顶部工具栏按钮

| 按钮                    | 功能                                          |
| ----------------------- | --------------------------------------------- |
| **锚点全覆盖**       | 将当前面板的 RectTransform 锚点设为全覆盖模式 |
| **设置面板名为类名** | 将 GameObject 名称设为类名                    |
| **定位脚本**         | 在 Project 窗口中定位并高亮脚本文件           |
| **打开脚本**         | 在代码编辑器中打开脚本文件                    |

#### 拖拽创建序列化字段

将 Hierarchy 或 Scene 中的 **GameObject** 拖拽到 Inspector 的字段管理区域：

1. 拖入对象后，自动检测其上的 UI 组件
2. 如果只有一个可用组件 → **直接创建字段**并在脚本中赋值
3. 如果有多个可用组件 → **弹出组件选择窗口**供选择
4. 如果字段名已存在但类型兼容 → **直接赋值**到现有字段
5. 自动添加必要的 `using` 指令（`UnityEngine.UI`、`TMPro` 等）

```csharp
// 拖拽后自动生成的字段示例
public class MyPanel : UIPanel
{
    #region 字段

    [SerializeField] private Button startBtn;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;

    #endregion

    protected override void OnInitialize()
    {
        // 直接使用字段，无需按名称查找
        startBtn.onClick.AddListener(OnStart);
        titleText.text = "Hello";
    }
}
```

#### 删除字段

每个序列化字段右侧显示红色删除按钮（✖），点击后可选择从脚本中移除该字段定义。

### 10.3 UI 事件检查器

Inspector 底部的折叠面板，展开后可查看当前面板下所有 **已绑定事件** 的 UI 组件：

- 按组件类型分色显示（Button=青色、Toggle=绿色、Slider=橙色等）
- 显示组件名称和监听器数量（持久监听器 + 运行时监听器）
- 支持 **选中对象** 和 **查看详情** 按钮
- 对非直接子对象显示相对路径

**组件详情弹窗** 示例：

```
组件详情: PlayBtn (Button)
对象路径: Canvas/ContentLayer/MainPanel/Header/PlayBtn
监听器总数: 2

--- 事件详情 ---
[onClick] (持久:1, 运行时:1):
  • [P] MainPanel.OnPlay
  • [R] 存在 1 个运行时监听器
```

### 10.4 ScriptModifier 脚本修改器

[`ScriptModifier`](Assets/FFramework/Utility/UISystem/Editor/ScriptModifier.cs) 是供 `UIPanelInspector` 内部调用的代码操作工具：

| 方法                      | 功能                                               |
| ------------------------- | -------------------------------------------------- |
| `InsertFieldIntoRegion()` | 插入字段到指定 `#region`，支持插入到区域开头或末尾 |
| `EnsureUsing()`           | 确保代码中存在指定 `using` 指令                    |
| `RemoveFieldFromCode()`   | 从代码中移除指定字段定义                           |

---

## 十一、调试工具

### 11.1 面板调试方法

在面板 Inspector 的右键菜单（ContextMenu）中可调用：

```csharp
[ContextMenu("打印面板状态")]       // 打印：初始化状态、显示状态、锁定状态、事件数量、层级
panel.PrintPanelStatus();

[ContextMenu("强制清理事件")]       // 强制清理所有追踪事件和组件事件
panel.ForceCleanupEvents();
```

### 11.2 UISystem 调试方法

在 `UISystem` Inspector 的右键菜单中可调用：

```csharp
[ContextMenu("初始化UI根节点")]     // 手动重新初始化 UI 根节点
[ContextMenu("检查数据一致性")]     // 检查缓存、栈、层级列表中的数据一致性
[ContextMenu("清理无效数据")]       // 清理空引用
```

### 11.3 彩色日志说明

系统自动输出带颜色的日志，方便区分：

| 颜色    | 操作          | 示例                            |
| ------- | ------------- | ------------------------------- |
| 🟢 绿色 | 显示面板      | `[UIPanel] 显示面板: MainPanel` |
| 🟡 黄色 | 隐藏/关闭面板 | `[UIPanel] 隐藏面板: MainPanel` |
| 🟠 橙色 | 锁定面板      | `[UIPanel] 锁定面板: MainPanel` |
| 🔵 青色 | 解锁面板      | `[UIPanel] 解锁面板: MainPanel` |

---

## 十二、最佳实践

### ✅ 推荐做法

```csharp
public class BestPracticePanel : UIPanel
{
    // 1. 缓存频繁使用的组件为字段
    private Button startBtn;
    private TextMeshProUGUI titleText;

    protected override void OnInitialize()
    {
        // 2. 组件引用只查一次
        startBtn  = this.GetButton("StartBtn");
        titleText = this.GetTMPText("TitleText");

        // 3. 使用自动事件追踪（默认开启）
        startBtn.BindClick(OnStart, this);
        this.BindToggle("SoundToggle", OnSoundToggle);

        // 4. 合理使用层级
        // 主界面 → Content 层
        // 弹窗    → Popup 层
    }

    private void OnStart()
    {
        UISystem.Instance.OpenPanel<MessageDialog>(UILayer.PopupLayer);
    }

    private void OnSoundToggle(bool isOn)
    {
        AudioListener.volume = isOn ? 1f : 0f;
    }
}
```

### ❌ 避免做法

```csharp
// ❌ 1. 不必要地禁用自动追踪
this.BindButton("Btn", OnClick, autoTrack: false);

// ❌ 2. 每帧查找组件
void Update()
{
    this.GetButton("Btn").interactable = canClick;  // 每次都遍历层级查找
}

// ❌ 3. 预制体名与类名不一致
// Resources/UI/MyStartPanel.prefab  ↔  class MainPanel  → 加载失败

// ❌ 4. 在 OnShow 中重复绑定事件（每次 Show 都会添加新监听器）
protected override void OnShow()
{
    this.BindButton("Btn", OnClick);  // 每次显示都新增一个监听
}
```

### 性能优化建议

| 建议                               | 原因                           |
| ---------------------------------- | ------------------------------ |
| 在 `OnInitialize()` 中完成所有绑定 | 只调用一次，避免重复注册       |
| 用字段缓存频繁使用的组件引用       | 避免每次 `GetComponent` 的开销 |
| 启用面板缓存（`useCache: true`）   | 避免频繁 Instantiate/Destroy   |
| 合理使用层级锁定                   | 减少不必要的 Canvas 重建       |
| 避免在 Update 中查找组件           | 组件查找涉及递归遍历           |

---

## 十三、常见问题

**Q: 面板打开失败？**
A: 检查 `Resources/UI/` 路径是否正确，预制体名称是否与类名完全一致。打开 Unity 控制台查看具体错误日志。

**Q: 事件重复触发？**
A: 确保在 [`OnInitialize()`](Assets/FFramework/Utility/UISystem/UIPanel.cs:86)（只调用一次）中绑定事件，而非在 `OnShow()` 中。如果已出现重复，调用 `ForceCleanupEvents()` 清理。

**Q: 找不到组件？**
A: 检查组件的 GameObject 名称拼写和层级结构。支持路径格式如 `"Header/PlayButton"`。确保组件类型与查找方法匹配（如用 `GetButton()` 查 Button）。

**Q: 面板层级混乱？**
A: 理解层级锁定机制：只有同层级内的面板会互相锁定。Content 层面板打开 Popup 层面板时，双方互不影响。

**Q: 编辑器下拖拽无效？**
A: 确保脚本继承自 [`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs)，且拖拽对象包含可支持的 UI 组件（Button、Toggle、Slider、TMP 等）。

**Q: 如何扩展资源加载方式？**
A: 实现 [`IUIResLoader`](Assets/FFramework/Utility/UISystem/UISystem.cs:910) 接口，通过 [`SetUIResLoader()`](Assets/FFramework/Utility/UISystem/UISystem.cs:191) 注册。

---

## 附录：API 速查表

### UISystem 实例方法

| API                                                                                           | 说明                  |
| --------------------------------------------------------------------------------------------- | --------------------- |
| [`OpenPanel<T>(layer, useCache)`](Assets/FFramework/Utility/UISystem/UISystem.cs:322)         | 从 Resources 打开面板 |
| [`OpenPanel<T>(prefab, layer, useCache)`](Assets/FFramework/Utility/UISystem/UISystem.cs:331) | 从预制体打开面板      |
| [`ClosePanel<T>()`](Assets/FFramework/Utility/UISystem/UISystem.cs:359)                       | 关闭指定类型面板      |
| [`CloseCurrentPanel()`](Assets/FFramework/Utility/UISystem/UISystem.cs:344)                   | 关闭当前顶层面板      |
| [`GetPanel<T>()`](Assets/FFramework/Utility/UISystem/UISystem.cs:378)                         | 获取已打开的面板      |
| [`GetTopPanel<T>()`](Assets/FFramework/Utility/UISystem/UISystem.cs:392)                      | 获取栈顶面板          |
| [`IsCurrentPanel<T>()`](Assets/FFramework/Utility/UISystem/UISystem.cs:400)                   | 检查当前面板类型      |
| [`ClearAllPanels(destroy)`](Assets/FFramework/Utility/UISystem/UISystem.cs:412)               | 清理所有面板          |
| [`ClearPanelsInLayer(layer, destroy)`](Assets/FFramework/Utility/UISystem/UISystem.cs:488)    | 清理指定层级面板      |
| [`GetChildComponent<T>(panel, name)`](Assets/FFramework/Utility/UISystem/UISystem.cs:773)     | 获取子物体组件        |
| [`SetUIResLoader(loader)`](Assets/FFramework/Utility/UISystem/UISystem.cs:191)                | 设置自定义资源加载器  |

### UIEventExtensions 扩展方法

| API                                                                                               | 类别     |
| ------------------------------------------------------------------------------------------------- | -------- |
| [`BindButton(name, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:62)          | 事件绑定 |
| [`BindToggle(name, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:73)          | 事件绑定 |
| [`BindSlider(name, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:84)          | 事件绑定 |
| [`BindInputField(name, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:95)      | 事件绑定 |
| [`BindDropdown(name, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:106)       | 事件绑定 |
| [`BindTMPInputField(name, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:117)  | 事件绑定 |
| [`BindTMPDropdown(name, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:128)    | 事件绑定 |
| [`BindClick(button, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:143)        | 直接绑定 |
| [`BindValueChanged(toggle, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:157) | 直接绑定 |
| [`BindValueChanged(slider, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:171) | 直接绑定 |
| [`GetComponent<T>(name)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:189)            | 组件查找 |
| [`SetProperty<T>(name, action)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:213)     | 属性设置 |
| [`SetToggleValue(name, value)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:224)      | 属性设置 |
| [`SetSliderValue(name, value)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:231)      | 属性设置 |
| [`BindButtons(dict)`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:257)                | 批量绑定 |
| [`UnbindAllEvents()`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:269)                | 事件清理 |
| [`ClearAllEvents()`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs:277)                 | 事件清理 |
