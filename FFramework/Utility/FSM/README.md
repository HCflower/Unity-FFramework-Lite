# FFramework FSM（有限状态机）

一个轻量级、泛型友好的有限状态机工具，适用于 Unity 项目中的游戏角色 AI、UI 流程控制、场景状态管理等场景。

## 架构

```
┌──────────────────────────────────────────────────┐
│                   IFSMState                      │  ← 状态接口
│  OnEnter / OnUpdate / OnFixedUpdate              │
│  OnLateUpdate / OnExit                           │
└──────────────────┬───────────────────────────────┘
                   │  implements
┌──────────────────▼───────────────────────────────┐
│                  FSMStateBase                    │  ← 抽象基类
│  - owner: object                                 │
│  + GetOwner<T>()                                 │
│  + OnEnter / OnUpdate / OnExit (abstract)        │
│  + OnFixedUpdate / OnLateUpdate (virtual)        │
└──────────────────┬───────────────────────────────┘
                   │  used by
┌──────────────────▼───────────────────────────────┐
│                FSMStateMachine                   │  ← 状态机
│  + ChangeState<T>() / SetDefault<T>()            │
│  + Update() / FixedUpdate() / LateUpdate()       │
│  + GetCurrentState<T>() / IsCurrentState<T>()    │
└──────────────────────────────────────────────────┘
```

## 核心 API

### `IFSMState` — 状态接口

所有状态必须实现的接口，定义了 5 个生命周期方法：

| 方法                                                             | 触发时机         | 说明                           |
| ---------------------------------------------------------------- | ---------------- | ------------------------------ |
| [`OnEnter`](Assets/FFramework/Utility/FSM/IFSMState.cs:11)       | 状态进入时       | 初始化状态数据、播放入场动画等 |
| [`OnUpdate`](Assets/FFramework/Utility/FSM/IFSMState.cs:12)      | 每帧更新         | 状态核心逻辑                   |
| [`OnFixedUpdate`](Assets/FFramework/Utility/FSM/IFSMState.cs:13) | 固定时间间隔更新 | 物理相关逻辑                   |
| [`OnLateUpdate`](Assets/FFramework/Utility/FSM/IFSMState.cs:14)  | 延迟更新         | 摄像机跟随等                   |
| [`OnExit`](Assets/FFramework/Utility/FSM/IFSMState.cs:15)        | 状态退出时       | 清理资源、重置状态             |

### `FSMStateBase` — 抽象基类

推荐直接继承此类而非实现 [`IFSMState`](Assets/FFramework/Utility/FSM/IFSMState.cs)，提供了持有者访问和初始化能力。

```csharp
public class IdleState : FSMStateBase
{
    public override void OnEnter(FSMStateMachine machine)
    {
        var player = GetOwner<Player>();  // 获取强类型持有者
        player.StopMoving();
    }

    public override void OnUpdate(FSMStateMachine machine)
    {
        // 每帧逻辑
    }

    public override void OnExit(FSMStateMachine machine)
    {
        // 清理
    }
}
```

**成员说明：**

| 成员                                                                | 描述                                                   |
| ------------------------------------------------------------------- | ------------------------------------------------------ |
| [`owner`](Assets/FFramework/Utility/FSM/FSMStateBase.cs:14)         | 状态持有者引用，由状态机自动注入                       |
| [`GetOwner<T>()`](Assets/FFramework/Utility/FSM/FSMStateBase.cs:21) | 获取强类型持有者                                       |
| [`OnInit()`](Assets/FFramework/Utility/FSM/FSMStateBase.cs:38)      | 虚方法，状态实例创建时调用一次，适合只初始化一次的逻辑 |

### `FSMStateMachine` — 状态机

核心调度器，管理状态注册、切换和生命周期调用。

**构造与持有者：**

```csharp
var player = GetComponent<Player>();
var machine = new FSMStateMachine(player);
```

**设置默认状态：**

```csharp
// 方式一：泛型方式（要求 new() 约束）
machine.SetDefault<IdleState>();

// 方式二：传入实例（可复用已配置的状态对象）
var idle = new IdleState();
machine.SetDefault(idle);
```

**切换状态：**

```csharp
// 方式一：泛型方式
machine.ChangeState<RunningState>();

// 方式二：实例方式
machine.ChangeState(new RunningState());

// 安全特性：自动跳过重复状态切换（同类型不会重新进入）
```

**驱动更新：**

```csharp
// 在 MonoBehaviour 中手动调用
void Update()        => machine.Update();
void FixedUpdate()   => machine.FixedUpdate();
void LateUpdate()    => machine.LateUpdate();
```

**查询接口：**

| 方法                                                                            | 描述                       |
| ------------------------------------------------------------------------------- | -------------------------- |
| [`GetCurrentState<T>()`](Assets/FFramework/Utility/FSM/FSMStateMachine.cs:127)  | 获取当前状态（类型转换）   |
| [`IsCurrentState<T>()`](Assets/FFramework/Utility/FSM/FSMStateMachine.cs:137)   | 判断当前是否为指定状态     |
| [`GetCurrentStateType()`](Assets/FFramework/Utility/FSM/FSMStateMachine.cs:149) | 获取当前状态类型（调试用） |
| [`GetCurrentStateName()`](Assets/FFramework/Utility/FSM/FSMStateMachine.cs:151) | 获取当前状态名称（调试用） |

## 完整示例

```csharp
using FFramework.Utility;
using UnityEngine;

public class Player : MonoBehaviour
{
    private FSMStateMachine fsm;

    void Start()
    {
        fsm = new FSMStateMachine(this);
        fsm.SetDefault<IdleState>();
    }

    void Update()
    {
        fsm.Update();

        if (Input.GetKeyDown(KeyCode.Space))
            fsm.ChangeState<JumpState>();
        else if (Input.GetKey(KeyCode.W))
            fsm.ChangeState<RunState>();
        else if (fsm.IsCurrentState<RunState>() && !Input.GetKey(KeyCode.W))
            fsm.ChangeState<IdleState>();
    }

    void FixedUpdate() => fsm.FixedUpdate();
}

public class IdleState : FSMStateBase
{
    public override void OnEnter(FSMStateMachine m)
    {
        var p = GetOwner<Player>();
        Debug.Log($"{p.name} 进入待机");
    }
    public override void OnUpdate(FSMStateMachine m) { }
    public override void OnExit(FSMStateMachine m) { }
}

public class RunState : FSMStateBase
{
    public override void OnEnter(FSMStateMachine m) { }
    public override void OnUpdate(FSMStateMachine m) { }
    public override void OnExit(FSMStateMachine m) { }
}

public class JumpState : FSMStateBase
{
    public override void OnEnter(FSMStateMachine m) { }
    public override void OnUpdate(FSMStateMachine m) { }
    public override void OnExit(FSMStateMachine m) { }
}
```

## 设计要点

- **手动更新**：状态机不依赖 MonoBehaviour 生命周期，需要持有者在 `Update`/`FixedUpdate`/`LateUpdate` 中手动调用
- **状态缓存**：已切换过的状态会缓存，避免重复实例化
- **重复保护**：切换到当前已激活的状态类型时，操作被忽略
- **泛型持有者**：通过 [`GetOwner<T>()`](Assets/FFramework/Utility/FSM/FSMStateBase.cs:21) 在状态中安全获取持有者，无需强制转换
