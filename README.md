# Unity-FFramework-Lite

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
  - [单例模式](#单例模式)
  - [数据持久化](#数据持久化)
  - [Command 模式](#command-模式)
  - [自定义 ScriptableObject](#自定义-scriptableobject)
- [工具模块](#工具模块-fframeworkutility)
  - [对象池（ObjectPool）](#对象池-objectpool)
  - [有限状态机（FSM）](#有限状态机-fsm)
  - [资源加载（ResLoad）](#资源加载-resload)
  - [场景加载（SceneLoad）](#场景加载-sceneload)
  - [动画播放（PlayAnima）](#动画播放-playanima)
  - [震动效果（Shake）](#震动效果-shake)
  - [UI 系统（UISystem）](#ui-系统-uisystem)
  - [UI 面板（UIPanel）](#ui-面板-uipanel)
  - [UI 事件绑定（UIEventExtensions）](#ui-事件绑定-uieventextensions)
  - [本地化系统（Localization）](#本地化系统-localization)
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

架构管理器 [`Architecture`](Assets/FFramework/Core/Architecture/Architecture.cs) 继承自 [`SingletonMono<Architecture>`](Assets/FFramework/Core/SingletonMono.cs)（`DefaultExecutionOrder(-100)`），负责 Model / ViewController / Singleton 的注册、解析与生命周期管理。

- 使用 `Architecture.Instance` 获取单例
- 通过 [`[Inject]`](Assets/FFramework/Core/Architecture/InjectAttribute.cs) 属性标记依赖字段/属性，架构自动解析注入
- 依赖注入由 [`InjectHelper`](Assets/FFramework/Core/Architecture/InjectHelper.cs) 静态工具类实现，通过 `Resolver` 委托完成类型解析
- 统一卸载顺序：Model → ViewController → Singleton（基础设施最后销毁）

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

[`ArchitectureModel`](Assets/FFramework/Core/Architecture/Model/ArchitectureModel.cs) 是数据层基类，继承自 [`IModel`](Assets/FFramework/Core/Architecture/Model/IModel.cs)，通过 [`[Inject]`](Assets/FFramework/Core/Architecture/InjectAttribute.cs) 自动注入 [`EventSystem`](Assets/FFramework/Core/EventSystem/EventSystem.cs)，可发送和监听事件。`Dispose()` 时自动注销所有通过 `RegisterEvent` 注册的事件。

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

[`ArchitectureViewController`](Assets/FFramework/Core/Architecture/ViewController/ArchitectureViewController.cs) 是视图层基类（MonoBehaviour），通过 [`[Inject]`](Assets/FFramework/Core/Architecture/InjectAttribute.cs) 自动注入 [`EventSystem`](Assets/FFramework/Core/EventSystem/EventSystem.cs)。`Awake` 时自动注册到 Architecture 并执行依赖注入。

- `AutoRegister` 属性：子类可重写返回 `false` 来禁用自动注册
- `Initialize()` / `Dispose()` 幂等设计，防止重复调用
- `OnDestroy()` 时自动注销事件并取消注册

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

使用 [`[Inject]`](Assets/FFramework/Core/Architecture/InjectAttribute.cs) 标记字段**或属性**，`Architecture` 在注册对象时自动解析并注入依赖（Model / Singleton / EventSystem / Architecture 等）。注入时机在 `Initialize()` 调用之前。

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

支持的解析顺序：已注册的 Model → 自动注册未注册的 IModel → 已注册的 Singleton → 自动注册非 Mono 的 ISingleton → EventSystem / Architecture 等内置单例。

### 数据绑定（BindableProperty）

[`BindableProperty<T>`](Assets/FFramework/Core/BindableProperty.cs) 包装值类型，值变化时自动触发回调。线程安全（双锁策略），防止死锁。

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

- 线程安全（双锁：valueLock + eventLock，值变更回调移出锁外执行）
- 注册时可选立即回调当前值
- 自动随 GameObject 销毁注销（通过 [`BindablePropertyAutoUnregister`](Assets/FFramework/Core/BindableProperty.cs) 组件）
- 支持 `IUnRegister` 接口统一注销

### 事件系统（EventSystem）

[`EventSystem`](Assets/FFramework/Core/EventSystem/EventSystem.cs) 继承自 [`SingletonMono<EventSystem>`](Assets/FFramework/Core/SingletonMono.cs)，提供全局事件通信，支持 string 和 struct 两种键类型。

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

新增特性：

- **触发历史记录**：Editor 模式下自动记录事件触发位置、调用堆栈、参数信息，方便调试
- **惰性清理**：触发时自动清理已销毁对象的监听器
- **`UnregisterTarget(object)`**：按目标对象批量注销所有事件

### 单例模式

提供两种单例实现，统一实现 [`ISingleton`](Assets/FFramework/Core/Architecture/ISingleton.cs) 接口：

| 类型         | 基类                                                          | 说明                                                       |
| ------------ | ------------------------------------------------------------- | ---------------------------------------------------------- |
| Mono 单例    | [`SingletonMono<T>`](Assets/FFramework/Core/SingletonMono.cs) | MonoBehaviour 单例，自动创建 GameObject，DontDestroyOnLoad |
| 非 Mono 单例 | 实现 `ISingleton` + `Architecture.RegisterInstance<T>()`      | 纯 C# 单例，由 Architecture 管理生命周期                   |

所有单例采用**懒加载**模式，首次访问 `Instance` 时自行初始化，不再需要 Priority 排序。

```csharp
// Mono 单例（自动创建 GameObject，DontDestroyOnLoad）
public class AudioManager : SingletonMono<AudioManager>
{
    protected override void InitializeSingleton() { /* 初始化 */ }
    public void Play(string name) { }
}
AudioManager.Instance.Play("bgm");

// 非 Mono 单例（通过 Architecture 注册）
public class ConfigManager : ISingleton
{
    public void OnSingletonInit() { /* 初始化 */ }
    public void OnSingletonDispose() { /* 清理 */ }
}
Architecture.Instance.RegisterInstance<ConfigManager>();
```

[`SingletonMono<T>`](Assets/FFramework/Core/SingletonMono.cs) 会在实例创建后自动注册到 `Architecture` 进行依赖注入和初始化。

### 数据持久化

[`ArchitectureDataPersistence`](Assets/FFramework/Core/Architecture/ArchitectureDataPersistence.cs) 支持三种模式的成员发现，序列化为 JSON（Newtonsoft）。使用 `ConcurrentDictionary` 缓存反射结果，避免重复扫描。

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

将业务操作封装为独立的命令类，实现操作与执行的分离。支持对象池复用（高频场景）。

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

// 5. 无需手动 new，自动创建实例
this.SendCommand<LogCommand>();
```

- 接口：[`ICommand`](Assets/FFramework/Core/Architecture/Command/ICommand.cs)、[`ICommand<TResult>`](Assets/FFramework/Core/Architecture/Command/ICommand.cs)、[`ICommandSender`](Assets/FFramework/Core/Architecture/Command/ICommand.cs)
- 基类：[`AbstractCommand`](Assets/FFramework/Core/Architecture/Command/AbstractCommand.cs) / [`AbstractCommand<TResult>`](Assets/FFramework/Core/Architecture/Command/AbstractCommand.cs)（提供 `GetModel<T>()` / `SendEvent()` 方法）
- 对象池：[`CommandPool<T>`](Assets/FFramework/Core/Architecture/Command/CommandPool.cs)
- 扩展方法：[`CommandExtensions`](Assets/FFramework/Core/Architecture/Command/CommandExtensions.cs)

### 自定义 ScriptableObject

[`CustomScriptableObject`](Assets/FFramework/Core/CustomScriptableObject.cs) 是 ScriptableObject 的基类，内置 Editor 按钮，可在 Inspector 中一键保存资源。

```csharp
public class GameConfig : CustomScriptableObject
{
    public int maxLevel;
    public float playerSpeed;
}
// Inspector 中显示 [保存资源] 按钮，点击即可保存
```

---

## 工具模块（FFramework.Utility）

### 对象池（ObjectPool）

[`ObjectPool`](Assets/FFramework/Utility/ObjectPool/ObjectPool.cs) 支持通过 Resources 路径或 Prefab 获取 / 回收对象，提供链式扩展方法。每个池独立管理，支持容量限制、惰性清理。

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

> 详细文档见 [`ObjectPool/ObjectPoolDoc.md`](Assets/FFramework/Utility/ObjectPool/ObjectPoolDoc.md)。

### 有限状态机（FSM）

[`FSMStateMachine`](Assets/FFramework/Utility/FSM/FSMStateMachine.cs) 泛型状态机，自动缓存状态实例，支持生命周期回调。状态基类 [`FSMStateBase`](Assets/FFramework/Utility/FSM/FSMStateBase.cs) 实现 [`IFSMState`](Assets/FFramework/Utility/FSM/IFSMState.cs) 接口，支持 OnUpdate、OnFixedUpdate、OnLateUpdate 多种更新模式。

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

// 获取持有者（强类型）
fsm.GetOwner<Enemy>();

// 查询当前状态
if (fsm.IsCurrentState<PatrolState>()) { /* ... */ }
var state = fsm.GetCurrentState<ChaseState>();

// 每帧驱动
void Update() => fsm.Update();
void FixedUpdate() => fsm.FixedUpdate();
void LateUpdate() => fsm.LateUpdate();
```

> 详细文档见 [`FSM/README.md`](Assets/FFramework/Utility/FSM/README.md)。

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

> 详细文档见 [`ResLoad/ResLoadDoc.md`](Assets/FFramework/Utility/ResLoad/ResLoadDoc.md)。

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

> 详细文档见 [`SceneLoad/SceneLoadDoc.md`](Assets/FFramework/Utility/SceneLoad/SceneLoadDoc.md)。

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

> 详细文档见 [`Anima/AnimaDoc.md`](Assets/FFramework/Utility/Anima/AnimaDoc.md)。

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

> 详细文档见 [`Shake/ShakeDoc.md`](Assets/FFramework/Utility/Shake/ShakeDoc.md)。

### UI 系统（UISystem）

[`UISystem`](Assets/FFramework/Utility/UISystem/UISystem.cs) 是 UI 系统核心管理器（继承 [`SingletonMono<UISystem>`](Assets/FFramework/Core/SingletonMono.cs)），负责面板创建、缓存、层级管理和生命周期调度。

特性：

- **六层 UI 层级**：Background / PostProcessing / Content / Popup / Guide / Debug
- **面板缓存**：自动缓存已创建的面板，支持复用
- **面板栈管理**：按打开顺序管理面板，支持 `CurrentPanel` 查询
- **自动锁定/解锁**：同层级面板打开时自动锁定前一个
- **可替换资源加载器**：通过 `IUIResLoader` 接口支持自定义加载策略

```csharp
// 打开面板（从 Resources 加载）
UISystem.Instance.OpenPanel<MainMenuPanel>(UILayer.ContentLayer);

// 打开面板（从 Prefab）
UISystem.Instance.OpenPanel<MainMenuPanel>(mainMenuPrefab, UILayer.ContentLayer);

// 关闭面板
UISystem.Instance.CloseCurrentPanel();
UISystem.Instance.ClosePanel<MainMenuPanel>();

// 查询面板
UIPanel panel = UISystem.Instance.GetPanel<MainMenuPanel>();
bool isTop = UISystem.Instance.IsCurrentPanel<MainMenuPanel>();

// 清理
UISystem.Instance.ClearAllPanels();                   // 清理所有
UISystem.Instance.ClearPanelsInLayer(UILayer.PopupLayer);  // 清理指定层级

// 自定义资源加载器
UISystem.Instance.SetUIResLoader(new MyCustomLoader());
```

| 层级                  | 说明                  |
| --------------------- | --------------------- |
| `BackgroundLayer`     | 背景层 - 静态背景     |
| `PostProcessingLayer` | 后期处理层 - UI 特效  |
| `ContentLayer`        | 内容层 - 主要 UI 功能 |
| `PopupLayer`          | 弹窗层 - 消息弹窗     |
| `GuideLayer`          | 引导层 - 引导操作     |
| `DebugLayer`          | 调试层 - 调试 UI      |

> 详细文档见 [`UISystem/UISystemDoc.md`](Assets/FFramework/Utility/UISystem/UISystemDoc.md)。

### UI 面板（UIPanel）

[`UIPanel`](Assets/FFramework/Utility/UISystem/UIPanel.cs) 继承自 [`ArchitectureViewController`](Assets/FFramework/Core/Architecture/ViewController/ArchitectureViewController.cs)，提供面板的显示 / 隐藏 / 锁定和 CanvasGroup 管理。面板销毁时自动清理所有事件绑定。

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

// 面板属性
panel.SetAlpha(0.5f);
panel.SetInteractable(false);
panel.SetBlocksRaycasts(true);
```

UIPanel 生命周期：`Awake` → `OnEnable` / `OnInitialize` → `Start` → `OnShow` / `OnHide` → `OnDestroy` / `OnPanelDestroy`

### UI 事件绑定（UIEventExtensions）

[`UIEventExtensions`](Assets/FFramework/Utility/UISystem/UIEventExtensions.cs) 提供按钮、开关、滑动条等组件的便捷绑定，自动随面板销毁清理。支持子物体递归查找和路径查找。

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

// 直接组件绑定
Button attackBtn = panel.GetButton("AttackBtn");
attackBtn.BindClick(OnAttack, panel);

// 组件获取（支持路径 "Panel/Sub/Button"）
panel.GetComponent<Button>("StartBtn");
panel.GetButton("StartBtn");
panel.GetImage("Avatar");

// 属性设置（链式）
panel.SetButtonInteractable("StartBtn", false);
panel.SetTMPText("LevelText", "Level 5");
panel.SetImageSprite("Avatar", mySprite);

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

> 详细文档见 [`Localization/LocalizationDoc.md`](Assets/FFramework/Utility/Localization/LocalizationDoc.md)。

| 组件                                                                                       | 说明                                                         |
| ------------------------------------------------------------------------------------------ | ------------------------------------------------------------ |
| [`LocalizationManager`](Assets/FFramework/Utility/Localization/LocalizationManager.cs)     | 核心管理器（单例，CSV 解析，语言切换）                       |
| [`LocalizationComponent`](Assets/FFramework/Utility/Localization/LocalizationComponent.cs) | UI 组件（可拖拽，配置 Key 后自动监听语言变化刷新文本和字体） |
| [`LocalizationConfig`](Assets/FFramework/Utility/Localization/LocalizationConfig.cs)       | ScriptableObject 配置资产，定义 CSV 分组和字体映射           |
| [`LocalizationData`](Assets/FFramework/Utility/Localization/LocalizationData.cs)           | 运行时数据模型（O(1) Key 查询，分组管理）                    |

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

A: 是的。`RegisterViewController<T>(GameObject)` 会在 GameObject 上添加组件并初始化。也可以通过将脚本挂载到 GameObject 上，`Awake` 时会自动注册。

**Q: 如何跨场景共享 Model？**

A: 将 Architecture 所在对象设为 DontDestroyOnLoad，或在新场景重新注册并迁移数据。

**Q: BindableProperty 性能如何？**

A: 仅在值真正变化时触发回调（内部有相等性判断），适用于 UI 更新等场景。线程安全设计，值变更回调在锁外执行防止死锁。

**Q: 事件名如何管理？**

A: 建议集中定义在一个静态类中，避免魔法字符串。

**Q: 如何做数据持久化？**

A: 三种选择：

- Model 自带 `SaveData()` / `LoadData()` 方法，自动序列化 Model 中所有 `BindableProperty<T>`、[`[SaveData]`](Assets/FFramework/Core/Architecture/Model/SaveDataAttribute.cs) 和 `[SerializeField]` 标记的成员
- 使用 [`SaveDataAttribute`](Assets/FFramework/Core/Architecture/Model/SaveDataAttribute.cs) 精确控制需要持久化的非 BindableProperty 成员

**Q: UI 面板如何管理？**

A: 使用 [`UISystem`](Assets/FFramework/Utility/UISystem/UISystem.cs) 统一管理面板的创建、缓存、层级和生命周期。`UIPanel` 继承自 `ArchitectureViewController`，自动获得依赖注入和事件系统支持。

**Q: 如何自定义 UI 资源加载方式？**

A: 实现 [`IUIResLoader`](Assets/FFramework/Utility/UISystem/UISystem.cs) 接口，通过 `UISystem.Instance.SetUIResLoader(loader)` 注入自定义加载器。
