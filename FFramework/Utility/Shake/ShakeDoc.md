# Shake 震动系统

> 命名空间：`FFramework.Utility`

完整的位置/旋转震动系统，支持预设配置、多种噪声类型和淡入淡出控制。

---

## 架构

```
ShakePreset (ScriptableObject)   ← 可复用的震动配置资产
    ↑ 引用
ShakeBase (抽象 MonoBehaviour)    ← 基础震动逻辑
    ↑ 继承
SmoothShake                      ← 通用震动实现（支持任意 Transform）
```

## 快速开始

### 1. 创建震动预设

在 Project 窗口中：右键 → `Create → FFramework → Shake → Shake Preset`

配置参数：

- `PositionShake` / `RotationShake`：位置/旋转的振幅、频率、噪声类型
- `FadeInDuration` / `HoldDuration` / `FadeOutDuration`：淡入→保持→淡出
- `FadeInCurve` / `FadeOutCurve`：自定义曲线

### 2. 添加震动组件

将 `SmoothShake` 挂载到需要震动的 GameObject 上，在 Inspector 中：

1. 拖拽 `ShakePreset` 到 `Shake Preset` 字段
2. 可选：指定 `Shake Target`（为空则震动自身）

### 3. 触发震动

```csharp
// 使用 Inspector 中设置的预设
smoothShake.PlayShake();

// 使用指定的预设
smoothShake.PlayShake(heavyShakePreset);

// 指定持续时间
smoothShake.PlayShake(1.5f);

// 自定义参数
smoothShake.PlayShake(
    new Vector3(0.1f, 0.1f, 0f),     // 位置振幅
    new Vector3(1f, 1f, 0.5f),       // 旋转振幅
    0.5f                              // 持续时间
);

// 快速震动（代码创建临时预设）
smoothShake.StartQuickShake(
    positionIntensity: 0.2f,
    rotationIntensity: 2.0f,
    duration: 0.5f
);
```

### 4. 停止震动

```csharp
smoothShake.StopShake();
```

### 5. 代码创建预设

```csharp
// 使用内置预设
var lightShake = ShakePreset.Presets.CreateLightShake();
var mediumShake = ShakePreset.Presets.CreateMediumShake();
var heavyShake = ShakePreset.Presets.CreateHeavyShake();
var continuousShake = ShakePreset.Presets.CreateContinuousShake();

// 自定义预设
var customPreset = ScriptableObject.CreateInstance<ShakePreset>();
customPreset.positionShake.amplitude = new Vector3(0.1f, 0.1f, 0f);
customPreset.rotationShake.amplitude = new Vector3(1f, 1f, 0.5f);
customPreset.holdDuration = 0.5f;
```

---

## 噪声类型

| 类型          | 说明                       |
| ------------- | -------------------------- |
| `SineWave`    | 正弦波震荡（均匀平滑）     |
| `WhiteNoise`  | 白噪声（随机抖动）         |
| `PerlinNoise` | 柏林噪声（自然抖动，推荐） |
| `Cosine`      | 余弦波震荡                 |

---

## API 参考

### ShakeBase（基类）

| 方法/属性                            | 说明                 |
| ------------------------------------ | -------------------- |
| `PlayShake()`                        | 使用当前预设播放震动 |
| `PlayShake(ShakePreset)`             | 使用指定预设         |
| `PlayShake(float)`                   | 指定持续时间         |
| `PlayShake(Vector3, Vector3, float)` | 自定义参数           |
| `StopShake()`                        | 停止震动             |
| `IsShaking`                          | 是否正在震动         |
| `TotalDuration`                      | 获取总震动时长       |

### SmoothShake

| 方法                                   | 说明             |
| -------------------------------------- | ---------------- |
| `SetShakeTarget(Transform)`            | 设置震动目标对象 |
| `StartQuickShake(float, float, float)` | 快速启动震动     |

### ShakePreset

| 方法                                | 说明                 |
| ----------------------------------- | -------------------- |
| `ApplyToShakeComponent(ShakeBase)`  | 将预设应用到震动组件 |
| `CopyFromShakeComponent(ShakeBase)` | 从震动组件复制设置   |
| `Presets.CreateLightShake()`        | 创建轻微震动预设     |
| `Presets.CreateMediumShake()`       | 创建中等震动预设     |
| `Presets.CreateHeavyShake()`        | 创建强烈震动预设     |
| `Presets.CreateContinuousShake()`   | 创建持续震动预设     |

---

## 注意事项

- 震动使用协程实现，淡入→保持→淡出三段式
- 支持同时震动位置和旋转（可独立配置）
- Inspector 中 `[Button]` 属性可在编辑器中测试震动效果
- 更换震动目标后会自动重新保存原始变换
- 对象销毁时自动停止震动
