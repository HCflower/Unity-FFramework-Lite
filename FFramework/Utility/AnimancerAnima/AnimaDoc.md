# PlayAnima 动画播放工具

> 命名空间：`PlayAnima` 及关联类型在全局命名空间

基于 **Animancer** 的轻量动画播放封装，提供简洁的 API 用于播放、事件触发、进度控制和触发点回调。支持 Editor 进度面板可视化调试。

---

## 快速开始

### 1. 添加组件

将 `PlayAnima` 挂载到需要播放动画的 GameObject 上（自动添加 `Animator` 和 `AnimancerComponent`）。

### 2. 播放动画

```csharp
var playAnima = GetComponent<PlayAnima>();

// 创建动画参数
var args = new AnimaArgs(animationClip, transitionTime: 0.15f, speed: 1.0f);
playAnima.PlayAnimaClip(args);
```

### 3. 带事件的动画

```csharp
// 在动画的 0.5 秒处触发回调
playAnima.PlayAnimaWithEvent(args, 0.5f, () => {
    Debug.Log("动画事件触发！");
});

// 多个事件
var events = new (float, Action)[] {
    (0.3f, () => PlaySound()),
    (0.7f, () => SpawnEffect())
};
playAnima.PlayAnimaWithEvents(args, events);
```

### 4. 暂停 / 恢复 / 停止

```csharp
playAnima.Pause();   // 暂停
playAnima.Resume();  // 恢复
playAnima.Stop();    // 停止并清理
```

### 5. 触发点系统（持久化，Editor 可见）

```csharp
playAnima
    .ClearTriggers()
    .AddTrigger(AnimaTriggerMode.Progress, 0.3f, () => Debug.Log("30% 处触发"))
    .AddTrigger(AnimaTriggerMode.Time, 1.5f, () => Debug.Log("第 1.5 秒触发"))
    .PlayAnimaClip(args);

// 也可以通过事件订阅
playAnima.OnReached += point => Debug.Log($"触发点: {point.mode} = {point.value}");
```

---

## API 参考

### AnimaArgs 参数类

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Clip` | `AnimationClip` | 必填 | 动画片段 |
| `TransitionTime` | `float` | `0.15f` | 过渡时间 |
| `FadeMode` | `FadeMode` | `FixedDuration` | 过渡模式（见下方枚举表） |
| `StartTime` | `float` | `0.0f` | 起始播放时间（秒） |
| `Speed` | `float` | `1.0f` | 播放速度 |
| `OnEnd` | `Action` | `null` | 播放结束回调 |
| `TimedEvents` | `List<(float, Action)>` | 空列表 | 定时事件列表（存储在 args 上，可复用） |
| `AddEvent(float, Action)` | 方法 | — | 添加定时事件，返回自身（链式） |
| `ClearTimedEvents()` | 方法 | — | 清除所有定时事件，返回自身 |

### FadeMode 过渡模式

| 值 | 说明 |
|------|------|
| `FixedDuration` | 固定过渡时间（秒） |
| `NormalizedDuration` | 归一化过渡时间（相对于动画长度） |
| `FixedSpeed` | 固定过渡速度 |
| `NormalizedSpeed` | 归一化过渡速度 |
| `FromStart` | 从开始播放 |
| `NormalizedFromStart` | 归一化从开始播放 |

### PlayAnima 方法（播放）

| 方法 | 说明 |
|------|------|
| `PlayAnimaClip(AnimaArgs)` | 播放动画片段 |
| `PlayAnimaClip(AnimationClip, ...)` | 兼容旧代码的重载版本 |
| `PlayAnimaWithEvent(AnimaArgs, float, Action)` | 播放 + 单个时间点事件（Animancer 事件） |
| `PlayAnimaWithEvents(AnimaArgs, IEnumerable<…>)` | 播放 + 多个时间点事件（Animancer 事件） |

### PlayAnima 方法（播放控制）★ 新增

| 方法 | 说明 |
|------|------|
| `Pause()` | 暂停播放（Speed = 0，保留原速度） |
| `Resume()` | 恢复播放（恢复暂停前的速度） |
| `Stop()` | 停止播放并清理状态 |

### PlayAnima 属性（状态查询）★ 新增

| 属性 | 类型 | 说明 |
|------|------|------|
| `IsPlaying` | `bool` | 是否正在播放 |
| `IsPaused` | `bool` | 是否已暂停（Speed == 0） |
| `IsValid` | `bool` | 当前是否有有效的动画状态（等同于 IsPlaying） |
| `IsLooping` | `bool` | 当前动画是否循环播放 |
| `PlaybackProgress` | `float` | 播放进度 0.0 ~ 1.0（可读写，循环动画自动取余） |
| `CurrentTime` | `float` | 当前播放时间（秒，循环动画返回周期内时间） |
| `TotalDuration` | `float` | 动画总时长（秒） |
| `CurrentClipName` | `string` | 当前动画片段名称 |

### PlayAnima 触发点 ★ 新增

| 成员 | 类型 | 说明 |
|------|------|------|
| `AddTrigger(mode, value, callback)` | 方法 | 添加触发点，返回自身（链式） |
| `ClearTriggers()` | 方法 | 移除所有触发点，返回自身 |
| `ResetTriggers()` | 方法 | 重置触发状态（播放/循环时自动调用） |
| `TriggerPoints` | `IReadOnlyList<AnimaTriggerPoint>` | 已注册的触发点列表（Editor 绘制用） |
| `OnReached` | `event` | 触发点到达时的事件 |

### AnimaTriggerMode 枚举

| 值 | 说明 | value 含义 |
|------|------|------------|
| `Progress` | 按进度触发 | 归一化进度 0.0 ~ 1.0 |
| `Time` | 按时间触发 | 秒数 |

### AnimaTriggerPoint 结构体

| 字段 | 类型 | 说明 |
|------|------|------|
| `mode` | `AnimaTriggerMode` | 触发模式 |
| `value` | `float` | 触发值 |
| `callback` | `Action` | 触发回调 |

---

## Editor 进度面板

`PlayAnima` 在 Play 模式下会自动显示进度面板（通过 [`PlayAnimaEditor`](../EditorTools/Editor/AnimancerExpandEditor/PlayAnimaEditor.cs)）：

- **进度条** — 绿色填充 + 标记线，点击/拖拽可跳转
- **播放/暂停按钮** — 使用 Unity 内置按钮
- **动画名称** — 显示当前播放的 AnimationClip 名称
- **时间标签** — 秒制格式（如 `1.25s / 2.50s`），循环动画追加 `(循环)` 后缀
- **触发点可视化** — 橙色竖线标记触发点位置

---

## 两种事件机制对比

| 特性 | Animancer 事件 (`PlayAnimaWithEvent`) | 触发点系统 (`AddTrigger`) |
|------|------|------|
| 生命周期 | 跟随 AnimancerState，一次性 | 持久存储在 PlayAnima 上 |
| 重复播放 | 每次播放需重新设置 | 自动重置，可复用 |
| Editor 可见 | ❌ | ✅ 橙色竖线可视化 |
| 适用场景 | 一次性特定动画的精确回调 | 通用的常驻动画事件 |

---

## 注意事项

- 需要项目中导入 **Animancer** 插件
- `PlayAnima` 依赖 `AnimancerComponent`，会自动 `RequireComponent`
- 动画结束回调通过 `AnimancerState.Events.OnEnd` 实现
- 暂停通过 `Speed = 0` 实现（非 Animancer 原生 Pause），恢复时还原原速度
- 循环动画的 `PlaybackProgress` 和 `CurrentTime` 自动取余，始终反映当前周期内状态
- `Update()` 仅在存在触发点时运行，无触发点时零开销
