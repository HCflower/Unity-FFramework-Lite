# 事件系统使用说明

## 基本功能

本事件系统支持**无参数**和**泛型参数**事件的注册、注销和触发，适用于解耦各类模块间的消息通信。

---

## 快速上手

### 1. 注册事件

```csharp
// 无参数事件
EventSystem.Instance.Register("GameStart", OnGameStart);

// 泛型事件
EventSystem.Instance.Register<int>("ScoreChanged", OnScoreChanged);
EventSystem.Instance.Register<string>("PlayerNameChanged", OnPlayerNameChanged);
```

### 2. 触发事件

```csharp
EventSystem.Instance.Trigger("GameStart");
EventSystem.Instance.Trigger<int>("ScoreChanged", 100);
```

### 3. 注销事件

```csharp
EventSystem.Instance.Unregister("GameStart", OnGameStart);
EventSystem.Instance.Unregister<int>("ScoreChanged", OnScoreChanged);
```

---

## 自动注销（推荐）

通过扩展方法，可让事件在 GameObject 销毁时自动注销，无需手动管理：

```csharp
// 注册并自动注销（无参数）
EventSystem.Instance.RegisterEvent("GameStart", OnGameStart, gameObject);

// 注册并自动注销（泛型参数）
EventSystem.Instance.RegisterEvent<int>("ScoreChanged", OnScoreChanged, gameObject);
```

### 一次性事件

```csharp
// 触发一次后自动注销
EventSystem.Instance.RegisterOnceEvent("GameStart", OnGameStart, gameObject);
EventSystem.Instance.RegisterOnceEvent<int>("ScoreChanged", OnScoreChanged, gameObject);
```

---

## 进阶用法

### 使用枚举作为事件键

```csharp
public enum GameEvent
{
    GameStart,
    GameOver,
    ScoreChanged
}

// 注册
EventSystem.Instance.Register(GameEvent.GameStart, OnGameStart);
EventSystem.Instance.Register<GameEvent, int>(GameEvent.ScoreChanged, OnScoreChanged);

// 触发
EventSystem.Instance.Trigger(GameEvent.GameStart);
EventSystem.Instance.Trigger<GameEvent, int>(GameEvent.ScoreChanged, 100);

// 注销
EventSystem.Instance.Unregister(GameEvent.GameStart, OnGameStart);
EventSystem.Instance.Unregister<GameEvent, int>(GameEvent.ScoreChanged, OnScoreChanged);
```

### 静态 API（无需获取 Instance）

```csharp
// 注册
EventSystem.S_Register("GameStart", OnGameStart);
EventSystem.S_Register<int>("ScoreChanged", OnScoreChanged);

// 触发
EventSystem.S_Trigger("GameStart");
EventSystem.S_Trigger<int>("ScoreChanged", 100);

// 注销
EventSystem.S_Unregister("GameStart", OnGameStart);
EventSystem.S_Unregister<int>("ScoreChanged", OnScoreChanged);
```

### 事件触发追踪（调试用）

```csharp
// 获取触发历史
var history = EventSystem.Instance.GetTriggerHistory();
foreach (var record in history)
{
    Debug.Log(record.GetDetailedInfo());
}

// 清除历史
EventSystem.Instance.ClearTriggerHistory();
```

### 查询方法

```csharp
// 检查事件是否存在监听者
bool hasEvent = EventSystem.Instance.HasEvent("GameStart");

// 获取监听者数量
int count = EventSystem.Instance.GetListenerCount("ScoreChanged");
```

### 调试打印

```csharp
// 打印所有事件概览
EventSystem.Instance.DebugPrint();
```

---

## 注意事项

- 建议所有事件注册都配合自动注销（通过 `gameObject` 参数），避免内存泄漏
- 支持多参数类型事件，但事件名需唯一
- 自动注销通过 `AutoEventUnregister` 组件实现，GameObject 销毁时自动清理

---

## 示例

```csharp
void Start()
{
    // 推荐：使用自动注销
    EventSystem.Instance.RegisterEvent("GameStart", OnGameStart, gameObject);
    EventSystem.Instance.RegisterEvent<int>("ScoreChanged", OnScoreChanged, gameObject);

    EventSystem.Instance.Trigger("GameStart");
    EventSystem.Instance.Trigger<int>("ScoreChanged", 100);
}

void OnGameStart() { Debug.Log("游戏开始！"); }
void OnScoreChanged(int score) { Debug.Log($"分数变化: {score}"); }
```

---

## API 参考

### EventSystem 核心 API

| 方法                           | 说明             |
| ------------------------------ | ---------------- |
| `Register` / `Register<T>`     | 注册事件监听     |
| `Unregister` / `Unregister<T>` | 注销事件监听     |
| `Trigger` / `Trigger<T>`       | 触发事件         |
| `HasEvent` / `HasEvent<TKey>`  | 检查事件是否存在 |
| `GetListenerCount`             | 获取监听者数量   |
| `GetTriggerHistory`            | 获取触发历史     |
| `ClearTriggerHistory`          | 清除触发历史     |

### EventSystem 静态 API

| 方法                               | 说明     |
| ---------------------------------- | -------- |
| `S_Register` / `S_Register<T>`     | 静态注册 |
| `S_Unregister` / `S_Unregister<T>` | 静态注销 |
| `S_Trigger` / `S_Trigger<T>`       | 静态触发 |
| `S_HasEvent`                       | 静态检查 |

### 扩展方法（自动注销）

| 方法                                               | 说明                  |
| -------------------------------------------------- | --------------------- |
| `RegisterEvent(name, callback, gameObject)`        | 注册 + 自动注销       |
| `RegisterEvent<T>(name, callback, gameObject)`     | 注册泛型 + 自动注销   |
| `RegisterOnceEvent(name, callback, gameObject)`    | 一次性事件 + 自动注销 |
| `RegisterOnceEvent<T>(name, callback, gameObject)` | 一次性泛型 + 自动注销 |

如需更多高级用法，请参考源码注释。
