
轻量、模块化、可扩展的 Unity 游戏开发基础框架，基于 MVC 架构，内置常用游戏开发工具模块。

- 命名空间（核心）：`FFramework.Core`
- 命名空间（工具）：`FFramework.Utility`
- 命名空间（编辑器）：`FFramework.Editor`

---

## 目录

- [快速开始](#快速开始)
- [核心模块](#核心模块-fframeworkcore)
  - [MVC 架构](#mvc-架构)
  - [数据模型（ArchitectureModel）](#数据模型-architecturemodel)
  - [视图控制器（ArchitectureViewController）](#视图控制器-architectureviewcontroller)
  - [依赖注入（[Inject]）](#依赖注入-inject)
  - [数据绑定（BindableProperty）](#数据绑定-bindableproperty)
  - [事件系统（EventSystem）](#事件系统-eventsystem)
  - [生命周期调度（GameMonoBehavior）](#生命周期调度-gamemonobehavior)
  - [单例模式](#单例模式)
  - [数据持久化](#数据持久化)
  - [Command 模式](#command-模式)
- [工具模块](#工具模块-fframeworkutility)
  - [对象池（ObjectPool）](#对象池-objectpool)
  - [有限状态机（FSM）](#有限状态机-fsm)
  - [计时器（Timer）](#计时器-timer)
  - [资源加载（ResLoad）](#资源加载-resload)
  - [数据存储（DataSave）](#数据存储-datasave)
  - [场景加载（SceneLoad）](#场景加载-sceneload)
  - [动画播放（PlayAnima）](#动画播放-playanima)
  - [震动效果（Shake）](#震动效果-shake)
  - [UI 面板（UIPanel）](#ui-面板-uipanel)
  - [UI 事件绑定（UIEventExtensions）](#ui-事件绑定-uieventextensions)
  - [本地化系统（Localization）](#本地化系统-localization)
  - [红点系统（RedDotManager）](#红点系统-reddotmanager)
  - [虚拟摇杆（VirtualRocker）](#虚拟摇杆-virtualrocker)
- [编辑器工具](#编辑器工具-editortools)
- [常见问题](#常见问题-faq)

---

## 快速开始

### 1. 定义 Model

```csharp
using FFramework.Core;

public class GameModel : ArchitectureModel
{
    public BindableProperty<int> Coin  = new(0);
    public BindableProperty<int> Level = new(1);

    public void AddCoin(int amount) => Coin.Value += amount;
}
```

### 2. 定义 ViewController

```csharp
using FFramework.Core;
using UnityEngine;

public class GameViewController : ArchitectureViewController
{
    private GameModel model;

    protected override void OnInitialize()
    {
        model = GetModel<GameModel>();
        model.Coin.Register(OnCoinChanged, gameObject);
        model.Level.Register(OnLevelChanged, gameObject);
    }

    private void OnCoinChanged(int coin) => Debug.Log($"Coin: {coin}");
    private void OnLevelChanged(int level) => Debug.Log($"Level: {level}");
}
```

### 3. 启动架构

```csharp
using FFramework.Core;
using UnityEngine;

public class GameStartup : MonoBehaviour
{
    public GameObject viewPrefab;

    void Start()
    {
        var arch = Architecture.Instance;
        arch.RegisterModel<GameModel>();
        arch.RegisterViewController<GameViewController>(viewPrefab);
        arch.GetModel<GameModel>().AddCoin(10);
    }
}
```

---

## 核心模块（FFramework.Core）

### MVC 架构

架构管理器 [`Architecture`](Assets/FFramework/Core/Architecture/Architecture.cs) 负责 Model / ViewController / Singleton 的注册、解析与生命周期管理。

- 使用 `Architecture.Instance` 获取单例
- 通过 [`[Inject]`](Assets/FFramework/Core/Architecture/InjectAttribute.cs) 属性标记依赖字段，架构自动解析注入
- 统一卸载顺序：Model → ViewController → Singleton

```csharp
// 注册与获取
Architecture.Instance.RegisterModel<MyModel>();
Architecture.Instance.GetModel<MyModel>();

Architecture.Instance.RegisterViewController<MyViewController>(gameObject);
Architecture.Instance.GetViewController<MyViewController>();

// 批量保存 / 加载所有 Model 数据
Architecture.Instance.SaveAllData("slot_save1");
Architecture.Instance.LoadAllData("slot_save1");

// 完全卸载（场景切换时）
Architecture.Instance.UnloadAll();
```

### 数据模型（ArchitectureModel）

[`ArchitectureModel`](Assets/FFramework/Core/Architecture/Model/ArchitectureModel.cs) 是数据层基类，继承自 [`IModel`](Assets/FFramework/Core/Architecture/Model/IModel.cs)，内置 `EventSystem`，可发送和监听事件。

```csharp
public class PlayerModel : ArchitectureModel
{
    public BindableProperty<int> Hp   = new(100);
    public BindableProperty<int> MaxHp = new(100);

    public void TakeDamage(int dmg)
    {
        Hp.Value = Mathf.Max(0, Hp.Value - dmg);
        SendEvent("OnPlayerDamaged", dmg);
    }
}
```

### 视图控制器（ArchitectureViewController）

[`ArchitectureViewController`](Assets/FFramework/Core/Architecture/ViewController/ArchitectureViewController.cs) 是视图层基类（MonoBehaviour），`Awake` 时自动注册到 Architecture 并执行依赖注入。

```csharp
public class HUDViewController : ArchitectureViewController
{
    protected override void OnInitialize()
    {
        var model = GetModel<PlayerModel>();
        model.Hp.Register(OnHpChanged, gameObject);
        RegisterEvent("OnPlayerDamaged", OnDamaged);
    }

    private void OnHpChanged(int hp) { /* 更新血条 */ }
    private void OnDamaged()         { /* 播放受伤特效 */ }
}
```

### 依赖注入（[Inject]）

使用 [`[Inject]`](Assets/FFramework/Core/Architecture/InjectAttribute.cs) 标记字段或属性，`Architecture` 在初始化时自动解析并注入依赖（Model / ViewController / Singleton）。

```csharp
public class BattleModel : ArchitectureModel
{
    [Inject] private EventSystem eventSystem;

    protected override void OnInitialize()
    {
        // eventSystem 已被自动注入，可直接使用
    }
}
```

支持的解析顺序：Model → Singleton（均自动注册）→ EventSystem / Architecture / GameMonoBehavior 等内置单例。

### 数据绑定（BindableProperty）

[`BindableProperty<T>`](Assets/FFramework/Core/BindableProperty.cs) 包装值类型，值变化时自动触发回调。

```csharp
// 基础用法
public BindableProperty<string> PlayerName = new("Default");

// 注册监听（推荐：绑定 GameObject，对象销毁时自动注销）
PlayerName.Register(OnNameChanged, gameObject);

// 手动注册 / 注销
var token = PlayerName.Register(OnNameChanged);
token.UnRegister();

// 注册时立即回调当前值
PlayerName.Register(OnNameChanged, gameObject, callImmediately: true);

// 触发回调
PlayerName.Value = "NewName";  // 值变化时自动调用 OnNameChanged
```

特性：

- 线程安全（双重锁）
- 注册时可选立即回调当前值
- 自动随 GameObject 销毁注销

### 事件系统（EventSystem）

[`EventSystem`](Assets/FFramework/Core/EventSystem/EventSystem.cs) 提供全局事件通信，支持 string 和 struct 两种键类型。

```csharp
// 实例 API
EventSystem.Instance.Register("EVENT_NAME", OnEvent);
EventSystem.Instance.Trigger("EVENT_NAME");
EventSystem.Instance.Trigger("EVENT_NAME", 42);  // 带参数

// 静态 API（更便捷）
EventSystem.S_Register("OnGameStart", OnGameStart);
EventSystem.S_Trigger("OnGameStart");
EventSystem.S_Unregister("OnGameStart", OnGameStart);

// 支持 struct 键
EventSystem.S_Register<MyEventType>(MyEventType.PlayerDie, OnPlayerDie);
EventSystem.S_Trigger(MyEventType.PlayerDie);

// 自动注销（随 GameObject 销毁）
this.RegisterEvent(_eventSystem, "OnLevelUp", OnLevelUp, gameObject);
```

### 生命周期调度（GameMonoBehavior）

内置的 Update 调度器，统一调度 Update / FixedUpdate / LateUpdate，自动清理已销毁对象。

```csharp
public class Enemy : MonoBehaviour
{
    void Start()
    {
        this.RegisterUpdate(OnTick);       // 自动注销（推荐）
        this.RegisterFixedUpdate(OnFixed);
    }

    void OnTick()  { /* 每帧执行 */ }
    void OnFixed() { /* 固定时间步执行 */ }
}

// 手动注册
GameMonoBehavior.Instance.RegisterUpdate(MyUpdate);
GameMonoBehavior.Instance.UnRegisterUpdate(MyUpdate);
```

### 单例模式

[`Singleton<T>`](Assets/FFramework/Core/Singleton.cs)（非 MonoBehaviour）和 [`SingletonMono<T>`](Assets/FFramework/Core/SingletonMono.cs)（MonoBehaviour）提供线程安全的单例实现，自动注册到 Architecture。

```csharp
// 非 Mono 单例
public class ConfigManager : Singleton<ConfigManager>
{
    protected override void OnSingletonInit() { /* 初始化 */ }
}
ConfigManager.Instance.DoSomething();

// Mono 单例（自动创建 GameObject，DontDestroyOnLoad）
public class AudioManager : SingletonMono<AudioManager>
{
    public void Play(string name) { }
}
AudioManager.Instance.Play("bgm");
```

### 数据持久化

[`ArchitectureDataPersistence`](Assets/FFramework/Core/Architecture/ArchitectureDataPersistence.cs) 支持三种模式的成员发现，序列化为 JSON（Newtonsoft）。

| 模式           | 识别方式                                                                                | 适用范围                                |
| -------------- | --------------------------------------------------------------------------------------- | --------------------------------------- |
| ① 自动检测     | `BindableProperty<T>` 类型自动识别                                                      | 所有 public 字段 / 属性                 |
| ② 显式标记     | [`[SaveData]`](Assets/FFramework/Core/Architecture/Model/SaveDataAttribute.cs) 属性标记 | 任意可访问性的字段 / 属性（最高优先级） |
| ③ Unity 序列化 | `[SerializeField]` 自动识别                                                             | 非 `UnityEngine.Object` 引用类型的字段  |

> 三种模式混合扫描时自动去重（按成员名），同一成员仅保存一次。

```csharp
public class PlayerModel : ArchitectureModel
{
    // BindableProperty 自动识别（模式①）
    public BindableProperty<int> Hp = new(100);

    // [SaveData] 显式标记（模式②）
    [SaveData] public string PlayerName;
    [SaveData] public List<string> Inventory;
    [SaveData] public Vector3 SpawnPosition;
    [SaveData] private float hiddenValue;

    // [SerializeField] 自动识别（模式③）
    [SerializeField] private int level;
    [SerializeField] private SerializableClass cfg;
    // [SerializeField] private GameObject go;  // ✗ Unity 对象自动跳过
}

// 单个 Model 保存 / 加载
var model = Architecture.Instance.GetModel<PlayerModel>();
model.SaveData("slot1");
model.LoadData("slot1");

// 批量保存 / 加载所有 Model
Architecture.Instance.SaveAllData("slot1");
Architecture.Instance.LoadAllData("slot1");
```

### Command 模式

将业务操作封装为独立的命令类，实现操作与执行的分离。

```csharp
// 1. 定义命令
public class AddCoinCommand : AbstractCommand
{
    private readonly int amount;
    public AddCoinCommand(int amount) => this.amount = amount;

    protected override void OnExecute()
    {
        var model = GetModel<GameResourceModel>();
        model.Coin.Value += amount;
        SendEvent("CoinChanged", model.Coin.Value);
    }
}

// 2. 发送命令（任何 ICommandSender：ViewController / Model / 另一个 Command）
this.SendCommand(new AddCoinCommand(100));

// 3. 带返回值
int damage = this.SendCommand(new CalculateDamageCommand(50, 1.5f));

// 4. 对象池复用（高频场景）
this.SendCommand<MoveCommand>(cmd => cmd.SetDirection(Vector3.forward));
```

- 接口：[`ICommand`](Assets/FFramework/Core/Architecture/Command/ICommand.cs)、[`ICommand<TResult>`](Assets/FFramework/Core/Architecture/Command/ICommand.cs)、[`ICommandSender`](Assets/FFramework/Core/Architecture/Command/ICommand.cs)
- 基类：[`AbstractCommand`](Assets/FFramework/Core/Architecture/Command/AbstractCommand.cs)（提供 `GetModel<T>()` / `SendEvent()` 方法）
- 对象池：[`CommandPool<T>`](Assets/FFramework/Core/Architecture/Command/CommandPool.cs)
- 扩展方法：[`CommandExtensions`](Assets/FFramework/Core/Architecture/Command/CommandExtensions.cs)

---

## 工具模块（FFramework.Utility）

### 对象池（ObjectPool）

[`ObjectPool`](Assets/FFramework/Utility/ObjectPool/ObjectPool.cs) 支持通过 Resources 路径或 Prefab 获取 / 回收对象，提供链式扩展方法。

```csharp
// 从 Resources 路径获取 / 回收
GameObject bullet = ObjectPool.Instance.GetResourcesObjectFromPool("Bullets/Bullet");
ObjectPool.Instance.ReturnObjectToPool(bullet);

// 从 Prefab 获取（链式调用）
GameObject go = myPrefab.GetPoolObject()
    .SetPosition(new Vector3(1, 0, 0))
    .SetParent(parentTransform);

// 延迟回收
go.ReturnPool(2.0f);   // 2 秒后自动回收

// 预热（预创建对象）
"Bullets/Bullet".PrewarmPool(10);

// 实现 IPoolObject 接口自动回调
public class MyBullet : MonoBehaviour, IPoolObject
{
    public void OnBeforeGetFromPool() { /* 显示、重置状态 */ }
    public void OnAfterReturnToPool() { /* 隐藏、清理 */ }
}
```

### 有限状态机（FSM）

[`FSMStateMachine`](Assets/FFramework/Utility/FSM/FSMStateMachine.cs) 泛型状态机，自动缓存状态实例，支持生命周期回调。

```csharp
// 定义状态（继承 FSMStateBase）
public class PatrolState : FSMStateBase
{
    public override void OnEnter(FSMStateMachine machine) { /* 进入巡逻 */ }
    public override void OnUpdate(FSMStateMachine machine) { /* 巡逻逻辑 */ }
    public override void OnExit(FSMStateMachine machine)  { /* 退出巡逻 */ }
}

public class ChaseState : FSMStateBase
{
    public override void OnEnter(FSMStateMachine machine) { /* 进入追击 */ }
    public override void OnUpdate(FSMStateMachine machine) { /* 追击逻辑 */ }
    public override void OnExit(FSMStateMachine machine) { }
}

// 使用
var fsm = new FSMStateMachine(enemy);
fsm.SetDefault<PatrolState>();
fsm.ChangeState<ChaseState>();

// 查询当前状态
if (fsm.IsCurrentState<PatrolState>()) { /* ... */ }
var state = fsm.GetCurrentState<ChaseState>();

// 每帧驱动
void Update() => fsm.Update();
void FixedUpdate() => fsm.FixedUpdate();
```

> 详细文档见 [`FSM/README.md`](Assets/FFramework/Utility/FSM/README.md)。

### 计时器（Timer）

[`CountdownTimer`](Assets/FFramework/Utility/Timer/Timer.cs) 和 [`StopwatchTimer`](Assets/FFramework/Utility/Timer/Timer.cs) 提供计时功能。

```csharp
var countdown = new CountdownTimer(60f);
countdown.OnTimerStart += () => Debug.Log("开始");
countdown.OnTimerStop  += () => Debug.Log("结束");
countdown.Start();

// 每帧驱动
void Update() => countdown.Tick(Time.deltaTime);

// 秒表
var stopwatch = new StopwatchTimer();
stopwatch.Start();
// ... 一段时间后
float elapsed = stopwatch.Time;
```

### 资源加载（ResLoad）

[`ResLoad`](Assets/FFramework/Utility/ResLoad/ResLoad.cs) 封装 Resources 加载，支持缓存、同步 / 异步、类型安全。

```csharp
// 同步加载
GameObject prefab = ResLoad.Instance.LoadRes<GameObject>("Prefabs/Enemy");

// 异步加载
ResLoad.Instance.LoadResAsync<GameObject>("Prefabs/UI/Panel", (result) =>
{
    Instantiate(result);
});

// 从完整路径加载（Editor 用 AssetDatabase，Runtime 自动提取 Resources 路径）
Sprite sprite = ResLoad.Instance.LoadAssetFromPath<Sprite>("Assets/Art/icon.png");

// 批量异步加载
ResLoad.Instance.LoadMultipleResAsync(
    new[] { "Prefabs/A", "Prefabs/B", "Prefabs/C" },
    (results) => { /* 全部加载完成 */ },
    (progress) => Debug.Log($"进度: {progress:P}")
);
```

### 数据存储（DataSave）

[`DataSave`](Assets/FFramework/Utility/DataSave/DataSave.cs) 提供 Binary 和 JSON 两种序列化方式，文件保存在 `Application.persistentDataPath`。

```csharp
[System.Serializable]
public class SaveData { public int level; public string name; }

var data = new SaveData { level = 5, name = "Player" };

// JSON 存储
DataSave.SaveDataToJson("save.json", data);
var loaded = DataSave.LoadDataFromJson<SaveData>("save.json");

// Binary 存储（支持任何类型）
DataSave.SaveDataToBinary("save.bin", data);
var loadedBin = DataSave.LoadDataFromBinary<SaveData>("save.bin");

// 工具方法
bool exists = DataSave.CheckDataExists("save.json");
DataSave.DeleteJsonData("save.json");
```

### 场景加载（SceneLoad）

[`SceneLoad`](Assets/FFramework/Utility/SceneLoad/SceneLoad.cs) 提供场景加载的同步 / 异步 / 预加载模式。

```csharp
// 异步加载
SceneLoad.Instance.LoadSceneAsync("Battle", LoadSceneMode.Single,
    progress: (p) => UpdateLoadingBar(p),
    complete: () => Debug.Log("加载完成")
);

// 预加载（允许 SceneActivation=false 时，加载资源后等待手动激活）
AsyncOperation op = SceneLoad.Instance.PreloadScene("Battle");
// ... 加载进度到达 0.9 时 ...
SceneLoad.Instance.ActivatePreloadedScene(op);

// 卸载场景
SceneLoad.Instance.UnloadScene("UIScene");
```

### 动画播放（PlayAnima）

[`PlayAnima`](Assets/FFramework/Utility/Anima/PlayAnima.cs) 基于 Animancer 的动画播放，支持动画事件。

```csharp
public class PlayerAnima : MonoBehaviour
{
    private PlayAnima _anima;
    void Awake() => _anima = GetComponent<PlayAnima>();

    void Attack()
    {
        _anima.PlayAnimaClip(new AnimaArgs
        {
            clip = attackClip,
            transitionTime = 0.1f
        });

        // 带事件（在 0.5 归一化时间触发伤害判定）
        _anima.PlayAnimaWithEvent(new AnimaArgs { clip = attackClip },
            0.5f, () => DealDamage());
    }
}
```

### 震动效果（Shake）

[`ShakeBase`](Assets/FFramework/Utility/Shake/ShakeBase.cs) / [`SmoothShake`](Assets/FFramework/Utility/Shake/SmoothShake.cs) 基于 PerlinNoise 的平滑震动，支持位置和旋转。

```csharp
var shake = GetComponent<SmoothShake>();

// 快速震动
shake.StartQuickShake(positionIntensity: 0.5f, rotationIntensity: 5f, duration: 0.3f);

// 使用 ShakePreset（ScriptableObject）
shake.PlayShake(myShakePreset);

// 自定义参数
shake.PlayShake(new Vector3(0.2f, 0.2f, 0), new Vector3(0, 0, 3f), 0.5f);
```

### UI 面板（UIPanel）

[`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs) 继承自 [`ArchitectureViewController`](Assets/FFramework/Core/Architecture/ViewController/ArchitectureViewController.cs)，提供面板的显示 / 隐藏 / 锁定和 CanvasGroup 管理。

```csharp
public class MainMenuPanel : UIPanel
{
    protected override void OnInitialize()
    {
        // 通过 UIEventExtensions 绑定子控件
        BindButton("StartBtn", () => SceneLoad.Instance.LoadSceneAsync("Game"));
        BindToggle("SoundToggle", (on) => AudioManager.Instance.SetSound(on));
    }

    protected override void OnShow() { /* 面板显示时 */ }
    protected override void OnHide() { /* 面板隐藏时 */ }
}

// 使用
mainMenuPanel.Show();
mainMenuPanel.Hide();
mainMenuPanel.OnLock();    // 锁定交互
mainMenuPanel.OnUnLock();  // 解锁交互
```

### UI 事件绑定（UIEventExtensions）

[`UIEventExtensions`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs) 提供按钮、开关、滑动条等组件的便捷绑定，自动随面板销毁清理。

```csharp
// 绑定按钮
panel.BindButton("AttackBtn", OnAttack);
panel.BindButton("SkillBtn", OnUseSkill);

// 绑定开关
panel.BindToggle("SoundToggle", (on) => AudioListener.volume = on ? 1 : 0);

// 绑定滑动条
panel.BindSlider("VolumeSlider", (v) => AudioListener.volume = v);

// 绑定输入框
panel.BindTMPInputField("NameInput", (text) => playerName = text);

// 批量绑定
panel.BindButtons(new Dictionary<string, Action>
{
    { "Btn1", () => Debug.Log("1") },
    { "Btn2", () => Debug.Log("2") },
});

// 主动清理所有已绑定事件
panel.UnbindAllEvents();
```

### 本地化系统（Localization）

[`LocalizationManager`](Assets/FFramework/Utility/Localization/LocalizationManager.cs) 提供多语言文本和字体切换方案，使用 CSV 文件管理语言数据，支持运行时按组加载 / 卸载。

```csharp
// 切换语言
LocalizationManager.Instance.SetLanguage("English");
LocalizationManager.Instance.SetLanguage("ChineseSimplified");

// 获取翻译文本
string text = LocalizationManager.Instance.GetText("ui_start_game");
string levelText = LocalizationManager.Instance.GetText("ui_level_display", 5);

// CSV 分组管理（按需加载 / 卸载）
LocalizationManager.Instance.LoadCsvGroup("story_act1");
LocalizationManager.Instance.UnloadGroup("story_act1");

// 监听语言 / 数据变更
LocalizationManager.Instance.OnLanguageChanged += (lang) => { };
LocalizationManager.Instance.OnDataChanged += (groupId, type) => { };
```

| 组件                                                                                       | 说明                                                         |
| ------------------------------------------------------------------------------------------ | ------------------------------------------------------------ |
| [`LocalizationManager`](Assets/FFramework/Utility/Localization/LocalizationManager.cs)     | 核心管理器（单例，CSV 解析，语言切换）                       |
| [`LocalizationComponent`](Assets/FFramework/Utility/Localization/LocalizationComponent.cs) | UI 组件（可拖拽，配置 Key 后自动监听语言变化刷新文本和字体） |
| [`LocalizationConfig`](Assets/FFramework/Utility/Localization/LocalizationConfig.cs)       | ScriptableObject 配置资产，定义 CSV 分组和字体映射           |
| [`LocalizationData`](Assets/FFramework/Utility/Localization/LocalizationData.cs)           | 运行时数据模型（O(1) Key 查询，分组管理）                    |

### 红点系统（RedDotManager）

[`RedDotManager`](Assets/FFramework/Utility/RedDotSystem/RedDotManager.cs) 基于前缀树（Trie）的红点系统，支持路径式管理和多种计算模式。

```csharp
// 添加红点路径（'/' 分隔）
RedDotManager.Instance.AddPath("Main/Bag");
RedDotManager.Instance.AddPath("Main/Bag/Weapon");
RedDotManager.Instance.AddPath("Main/Bag/Armor");

// 设置叶子节点值
RedDotManager.Instance.SetCount("Main/Bag/Weapon", 1);

// 设置分支计算模式（默认为 Sum）
RedDotManager.Instance.SetMode("Main/Bag", ERedDotMode.Max);

// 注册红点更新回调
RedDotManager.Instance.Register("Main/Bag", (count) =>
{
    redDotBadge.SetActive(count > 0);
});

// 获取当前值
int count = RedDotManager.Instance.GetCount("Main/Bag");
```

### 虚拟摇杆（VirtualRocker）

[`VirtualRocker`](Assets/FFramework/Utility/Other/VirtualRocker.cs) 继承 UIPanel，支持固定位置和区域点击两种模式，带 SmoothDamp 平滑输入。

```csharp
// 获取输入
Vector2 input = rocker.GetInputVector();
float horizontal = rocker.GetHorizontal();
float vertical = rocker.GetVertical();

// 启用 / 禁用
rocker.SetActive(true);
rocker.HideAndReset();

// 切换区域点击模式
rocker.SetAreaClickMode(true);
rocker.SetClickArea(clickAreaRectTransform);

// 监听事件
rocker.OnRockerValueChanged.AddListener((vec) => MovePlayer(vec));
rocker.OnRockerPressed.AddListener(() => Debug.Log("按下"));
rocker.OnRockerReleased.AddListener(() => Debug.Log("释放"));
```

---

## 编辑器工具（EditorTools）

### 工具窗口

| 工具               | 入口                                    | 说明                                                 |
| ------------------ | --------------------------------------- | ---------------------------------------------------- |
| **创建文件夹工具** | `FFramework / 创建文件夹工具`           | 快速创建项目文件夹结构，支持预设模板                 |
| **资源压缩工具**   | `FFramework / 资源压缩工具`             | 图片 / 音频 / 模型资源压缩，支持拖拽批量处理         |
| **命令控制台**     | `Tools / 命令控制台`（或 LeftCtrl+Tab） | 运行时嵌入 Game 视图的命令行调试工具（仅 Play 模式） |

### 自定义 Inspector

| 组件                             | 说明                                                     |
| -------------------------------- | -------------------------------------------------------- |
| **UIPanel Inspector**            | 自动附加到 UIPanel Inspector，列出所有绑定事件的 UI 组件 |
| **LocalizationManager Editor**   | 提供语言切换、状态信息、调试面板                         |
| **LocalizationComponent Editor** | 提供 Key 选择、预览等功能                                |

### Editor 特性

| 特性                                                                                    | 说明                                     |
| --------------------------------------------------------------------------------------- | ---------------------------------------- |
| [`[Button]`](Assets/FFramework/EditorPropertyDrawer/ButtonAttribute.cs)                 | Inspector 中显示可点击按钮，调用指定方法 |
| [`[ShowOnly]`](Assets/FFramework/EditorPropertyDrawer/ShowOnlyAttribute.cs)             | Inspector 中只读显示字段值               |
| [`[TextLabel]`](Assets/FFramework/EditorPropertyDrawer/TextLabelAttribute.cs)           | 为字段添加自定义描述文本                 |
| [`[ConsoleCommand]`](Assets/FFramework/EditorPropertyDrawer/ConsoleCommandAttribute.cs) | 标记命令类 / 方法，注册到命令控制台      |

---

## 常见问题（FAQ）

**Q: ViewController 必须绑定 GameObject 吗？**

A: 是的。`RegisterViewController<T>(GameObject)` 会在 GameObject 上添加组件并初始化。

**Q: 如何跨场景共享 Model？**

A: 将 Architecture 所在对象设为 DontDestroyOnLoad，或在新场景重新注册并迁移数据。

**Q: BindableProperty 性能如何？**

A: 仅在值真正变化时触发回调（内部有相等性判断），适用于 UI 更新等场景。

**Q: 事件名如何管理？**

A: 建议集中定义在一个静态类中，避免魔法字符串。

**Q: 如何做数据持久化？**

A: 三种选择：

- Model 自带 `SaveData()` / `LoadData()` 方法，自动序列化 Model 中所有 `BindableProperty<T>`、[`[SaveData]`](Assets/FFramework/Core/Architecture/Model/SaveDataAttribute.cs) 和 `[SerializeField]` 标记的成员
- 使用 [`SaveDataAttribute`](Assets/FFramework/Core/Architecture/Model/SaveDataAttribute.cs) 精确控制需要持久化的非 BindableProperty 成员
- 使用 [`DataSave`](Assets/FFramework/Utility/DataSave/DataSave.cs) 工具类自行控制序列化格式
