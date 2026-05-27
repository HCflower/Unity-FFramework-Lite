# FMODExpand 音频播放模块

> 命名空间：`FFramework.Utility`

提供两种 FMOD 音频播放方案：轻量级一次性音效工具（`FMODSimpleAudio`）和功能完整的音频发射器组件（`FMODSoundEmitter`）。

---

## 快速选择

| 场景 | 推荐 |
|------|------|
| 播放一次性 SFX（打击、拾取、UI 点击等），需要设音量 | [`FMODSimpleAudio`](#fmodsimpleaudio) |
| 需要持续控制（暂停/恢复/停止/循环/3D 空间音频） | [`FMODSoundEmitter`](#fmodsoundemitter) |
| 需要在 Inspector 中配置音频事件，支持触发点回调 | [`FMODSoundEmitter`](#fmodsoundemitter) |

---

## FMODSimpleAudio

> 静态工具类，无需挂载到 GameObject

### 基本用法

```csharp
using FFramework.Utility;

// 播放音效，默认音量 1.0
FMODSimpleAudio.PlayOneShot("event:/Hit1");

// 播放音效，50% 音量
FMODSimpleAudio.PlayOneShot("event:/Hit1", 0.5f);

// 使用 Inspector 赋值的 EventReference
public EventReference hitSound;
FMODSimpleAudio.PlayOneShot(hitSound, 0.8f);
```

### API

| 方法 | 说明 |
|------|------|
| `PlayOneShot(string eventPath, float volume = 1f)` | 播放事件路径指定的音效 |
| `PlayOneShot(EventReference eventRef, float volume = 1f)` | 播放 EventReference 指定的音效 |

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `eventPath` | `string` | 必填 | FMOD 事件路径，如 `"event:/Hit1"` |
| `eventRef` | `EventReference` | 必填 | FMOD 事件引用，可在 Inspector 中拖拽赋值 |
| `volume` | `float` | `1f` | 音量 0.0 ~ 1.0，自动钳制 |

### 原理

`RuntimeManager.PlayOneShot` 内部使用 `CreateInstance → start → release` 的标准 FMOD one-shot 模式，但不暴露音量参数。本工具在 `start` 之前插入 `setVolume()`，其余流程完全一致。

> ⚠️ 仅适用于**一次性音效**（非循环事件）。循环事件请使用 `FMODSoundEmitter`，否则 `release()` 会导致循环播放在下一轮开始前被释放。

---

## FMODSoundEmitter

> MonoBehaviour 组件，挂载到 GameObject 使用

### 快速开始

1. 将 `FMODSoundEmitter` 组件挂载到 GameObject
2. 在 Inspector 中拖拽 FMOD Event 到 `Fmod Event` 字段
3. 通过代码或 Inspector 面板控制播放

```csharp
var emitter = GetComponent<FMODSoundEmitter>();

// 播放 Inspector 中配置的事件
emitter.Play();

// 播放指定路径的事件
emitter.Play("event:/BGM");

// 链式调用
emitter
    .SetVolume(0.8f)
    .SetLoop(true)
    .Play();
```

### Inspector 配置

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Fmod Event` | `EventReference` | 空 | FMOD Studio 事件引用 |
| `Is 3D` | `bool` | `true` | 启用 3D 空间音频（关闭为 2D 全局音频） |
| `Volume` | `float` | `1f` | 默认音量 (0.0 ~ 1.0) |
| `Play On Awake` | `bool` | `true` | 启动时自动播放 |
| `Loop` | `bool` | `false` | 自动循环播放 |

### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `IsValid` | `bool` | EventInstance 是否有效 |
| `IsPlaying` | `bool` | 是否正在播放 |
| `IsPaused` | `bool` | 是否已暂停 |
| `PlaybackProgress` | `float` | 当前播放进度 0.0 ~ 1.0（可读写，支持拖拽跳转） |
| `TimelinePositionMs` | `int` | 当前播放位置（毫秒），只读 |
| `EventLengthMs` | `int` | 事件总时长（毫秒），-1 表示尚未获取 |
| `CurrentEventPath` | `string` | 当前播放的事件路径 |
| `TriggerPoints` | `IReadOnlyList<TriggerPoint>` | 已注册的触发点列表 |

### 方法

| 方法 | 说明 |
|------|------|
| `Play(string eventPath = null)` | 播放（可选指定事件路径），返回自身 |
| `Stop(STOP_MODE mode)` | 停止播放（默认淡出） |
| `Pause()` | 暂停 |
| `Resume()` | 恢复 |
| `TogglePause()` | 切换暂停/恢复 |
| `SetVolume(float volume)` | 设置音量 (0.0 ~ 1.0) |
| `SetLoop(bool enabled)` | 设置循环 |

### 触发点系统

在音频的特定位置触发回调，支持两种模式：

```csharp
emitter
    .AddTrigger(AudioPlayTriggerMode.Progress, 0.5f, () => Debug.Log("播放到 50%"))
    .AddTrigger(AudioPlayTriggerMode.Time, 2.0f, () => Debug.Log("播放到第 2 秒"))
    .Play();
```

### 触发点类型

| 类型 | 枚举值 | value 含义 | 示例 |
|------|--------|------------|------|
| 按进度 | `AudioPlayTriggerMode.Progress` | 归一化进度 0.0 ~ 1.0 | `0.5f` = 50% 处触发 |
| 按时间 | `AudioPlayTriggerMode.Time` | 秒数 | `2.0f` = 第 2 秒触发 |

---

## 事件路径格式

项目中 FMOD 事件路径遵循 `event:/EventName` 格式：

```
event:/BGM
event:/Dead
event:/Hit0
event:/Hit1
event:/LevelUp
event:/Lose
event:/Melee0
event:/Melee1
event:/Range
event:/Select
event:/Win
```

---

## 注意事项

- `FMODSimpleAudio.PlayOneShot` 内部对每个调用创建新的 `EventInstance`，适合**短小的一次性音效**，不适合高频调用或长音频
- `FMODSoundEmitter` 复用 `EventInstance`，适合**持续播放、循环、3D 空间音频**等场景
- `FMODSoundEmitter` 的触发点在循环播放时会自动重置
- 3D 音频模式依赖 `Rigidbody` / `Rigidbody2D` 获取速度信息，物理组件非必需
