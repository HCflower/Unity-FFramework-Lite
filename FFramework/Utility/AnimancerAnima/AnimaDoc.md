# PlayAnima 动画播放工具

> 命名空间：`FFramework.Utility`

基于 **Animancer** 的轻量动画播放封装，提供简洁的 API 用于播放、事件触发、进度控制和定时事件回调。支持 Editor 进度面板可视化调试。

---

## 快速开始

### 1. 添加组件

将 `PlayAnima` 挂载到需要播放动画的 GameObject 上（自动添加 `Animator` 和 `AnimancerComponent`）。

### 2. 播放动画

```csharp
var playAnima = GetComponent<PlayAnima>();

// 方式一：通过 AnimaArgs 参数播放（推荐）
var args = new AnimaArgs(animationClip, transitionTime: 0.15f, speed: 1.0f);
playAnima.PlayAnimaClip(args);

// 方式二：直接传 AnimationClip（使用默认参数）
playAnima.PlayAnimaClip(animationClip);
```

### 3. 带定时事件的动画

```csharp
// 方式一：使用归一化进度（0.0 ~ 1.0）
var args = new AnimaArgs(clip)
    .AddEvent(0.5f, () => { Debug.Log("50% 处触发"); })
    .AddEvent(0.8f, () => { Debug.Log("80% 处触发"); });

playAnima.PlayAnimaClip(args);

// 方式二：使用真实秒数（需指定 AnimaEventMode.Time）
var args2 = new AnimaArgs(clip)
    .AddEvent(1.5f, () => Debug.Log("1.5 秒处触发"), AnimaEventMode.Time);

playAnima.PlayAnimaClip(args2);
```

### 4. 暂停 / 恢复 / 停止

```csharp
playAnima.Pause();   // 暂停
playAnima.Resume();  // 恢复
playAnima.Stop();    // 停止并清理状态
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
| `TimedEvents` | `List<(float, Action, AnimaEventMode)>` | 空列表 | 定时事件列表（存储在 args 上，可复用） |
| `AddEvent(float, Action)` | 方法 | — | 添加定时事件（默认 Progress 归一化进度），返回自身（链式） |
| `AddEvent(float, Action, AnimaEventMode)` | 方法 | — | 添加定时事件，可指定 Progress(归一化) / Time(秒) |
| `ClearTimedEvents()` | 方法 | — | 清除所有定时事件，返回自身 |

### AnimaEventMode 事件时间模式

| 值 | 说明 |
|------|------|
| `Progress` | 归一化进度 0.0 ~ 1.0 |
| `Time` | 真实时间（秒） |

### FadeMode 过渡模式

| 值 | 说明 |
|------|------|
| `FixedDuration` | 固定过渡时间（秒） |
| `NormalizedDuration` | 归一化过渡时间（相对于动画长度） |
| `FixedSpeed` | 固定过渡速度 |
| `NormalizedSpeed` | 归一化过渡速度 |
| `FromStart` | 从开始播放 |
| `NormalizedFromStart` | 归一化从开始播放 |

### PlayAnima 播放方法

| 方法 | 说明 |
|------|------|
| `PlayAnimaClip(AnimaArgs)` | 播放动画片段 |
| `PlayAnimaClip(AnimationClip, ...)` | 兼容旧代码的重载，内部构造 AnimaArgs |
| `PlayAnimaWithEvent(AnimaArgs, float, Action)` | 播放 + 单个时间点事件（Animancer 事件） |
| `PlayAnimaWithEvents(AnimaArgs, IEnumerable<…>)` | 播放 + 多个时间点事件（Animancer 事件） |

### PlayAnima 播放控制

| 方法 | 说明 |
|------|------|
| `SetSpeed(float)` | 设置播放速度，返回自身（链式） |
| `SetLoop(bool)` | 设置是否循环播放，返回自身（链式） |
| `Pause()` | 暂停播放（Speed = 0，保留原速度） |
| `Resume()` | 恢复播放（恢复暂停前的速度） |
| `Stop()` | 停止播放并清理状态 |

### PlayAnima 状态查询

| 属性 | 类型 | 说明 |
|------|------|------|
| `IsPlaying` | `bool` | 是否正在播放中 |
| `IsPaused` | `bool` | 是否已暂停（Speed == 0 且有权重） |
| `IsValid` | `bool` | 当前是否有有效的动画状态 |
| `IsLooping` | `bool` | 当前动画是否循环播放 |
| `EventPoints` | `IReadOnlyList<float>` | 当前 AnimaArgs 的定时事件进度点（Editor 锚点绘制用，Time 模式自动转换为归一化） |
| `PlaybackProgress` | `float` | 播放进度 0.0~1.0（可读写，循环动画自动取余） |
| `CurrentTime` | `float` | 当前播放时间（秒，循环动画返回周期内时间） |
| `TotalDuration` | `float` | 动画总时长（秒） |
| `CurrentClipName` | `string` | 当前动画片段名称 |

---

## Editor 进度面板

`PlayAnima` 在 Play 模式下会自动显示进度面板（通过 [`PlayAnimaEditor`](../EditorTools/Editor/AnimancerExpandEditor/PlayAnimaEditor.cs)）：

- **进度条** — 绿色填充 + 标记线，点击/拖拽可跳转
- **播放/暂停按钮** — 使用 Unity 内置按钮
- **动画名称** — 显示当前播放的 AnimationClip 名称
- **时间标签** — 秒制格式（如 `1.25s / 2.50s`），循环动画追加 `(循环)` 后缀
- **事件锚点** — 橙色竖线标记 `AnimaArgs.TimedEvents` 中注册的事件位置

---

## 注意事项

- 需要项目中导入 **Animancer** 插件
- `PlayAnima` 依赖 `AnimancerComponent`，会自动 `RequireComponent`
- 动画结束回调通过 `AnimancerState.Events.OnEnd` 实现
- 暂停通过 `Speed = 0` 实现（非 Animancer 原生 Pause），恢复时还原原速度
- 循环动画的 `PlaybackProgress` 和 `CurrentTime` 自动取余，始终反映当前周期内状态
- 定时事件存储在 `AnimaArgs` 上，可在多次播放中复用
- `AnimaEventMode.Time` 模式会在播放时自动将秒数转换为归一化进度传入 Animancer
- `EventPoints` 属性会将 Time 模式的事件自动转换为归一化进度（除以 Clip 长度）用于 Editor 显示
