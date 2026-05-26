# PlayAnima 动画播放工具

> 命名空间：`FFramework.Utility`（`PlayAnima` 在全局命名空间）

基于 **Animancer** 的轻量动画播放封装，提供简洁的 API 用于播放、事件触发和动画控制。

---

## 快速开始

### 1. 添加组件

将 `PlayAnima` 挂载到需要播放动画的 GameObject 上（自动添加 `Animator` 和 `AnimancerComponent`）。

### 2. 播放动画

```csharp
// 创建动画参数
var args = new AnimaArgs(animationClip, transitionTime: 0.15f);
GetComponent<PlayAnima>().PlayAnimaClip(args);
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

---

## API 参考

### AnimaArgs 参数类

| 参数             | 类型            | 默认值        | 说明         |
| ---------------- | --------------- | ------------- | ------------ |
| `Clip`           | `AnimationClip` | 必填          | 动画片段     |
| `TransitionTime` | `float`         | 0.15f         | 过渡时间     |
| `FadeMode`       | `FadeMode`      | FixedDuration | 过渡模式     |
| `StartTime`      | `float`         | 0.0f          | 起始播放时间 |
| `Speed`          | `float`         | 1.0f          | 播放速度     |
| `OnEnd`          | `Action`        | null          | 播放结束回调 |

### PlayAnima 方法

| 方法                                                           | 说明                  |
| -------------------------------------------------------------- | --------------------- |
| `PlayAnimaClip(AnimaArgs)`                                     | 播放动画片段          |
| `PlayAnimaWithEvent(AnimaArgs, float, Action)`                 | 播放 + 单个时间点事件 |
| `PlayAnimaWithEvents(AnimaArgs, IEnumerable<(float, Action)>)` | 播放 + 多个时间点事件 |
| `PlayAnimaClip(AnimationClip, ...)`                            | 兼容旧代码的重载版本  |

---

## 注意事项

- 需要项目中导入 **Animancer** 插件
- `PlayAnima` 依赖 `AnimancerComponent`，会自动 `RequireComponent`
- 动画结束回调通过 `AnimancerState.Events.OnEnd` 实现
