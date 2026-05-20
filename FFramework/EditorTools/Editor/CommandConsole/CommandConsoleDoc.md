# 命令控制台 (Command Console)

## 概述

命令控制台是一个嵌入 Unity Game 视图的编辑器工具，允许你在 **Play 模式**下通过热键呼出控制台，输入并执行自定义命令。适用于调试、测试和快速验证游戏逻辑。

## 快速开始

### 打开控制台

| 方式     | 操作                                                |
| -------- | --------------------------------------------------- |
| **热键** | 在 Play 模式下按 **`LeftCtrl + Tab`** 切换显示/隐藏 |
| **菜单** | `Tools → 命令控制台 (嵌入 Game 视图)`               |

### 基本操作

```
> help          ← 查看所有可用命令
> clear         ← 清空输出
> add_coin 100  ← 执行命令（增加 100 金币）
```

- **↑/↓** 方向键浏览历史命令
- **点击命令列表**中的命令名，自动填入输入框
- **Enter** 执行命令

## 如何创建自定义命令

### 方式一：类命令（推荐）

继承 `AbstractCommand`，标记 `[ConsoleCommand]` 特性，定义公共可写属性作为参数：

```csharp
using FFramework.Core;

[ConsoleCommand("add_coin", "增加金币数量", "add_coin <数量>")]
public class RefreshCoinCountCommand : AbstractCommand
{
    /// <summary>要增加的金币数量</summary>
    public int Count { get; set; }

    protected override void OnExecute()
    {
        GetModel<GameResourceModel>().CoinCount.Value += Count;
    }
}
```

### 方式二：多参数命令

定义多个属性，控制台会自动按声明顺序匹配参数：

```csharp
[ConsoleCommand("spawn_unit", "生成单位")]
public class SpawnUnitCommand : AbstractCommand
{
    public string UnitType { get; set; }   // 第1个参数
    public int    Count    { get; set; }   // 第2个参数
    public int    Level    { get; set; }   // 第3个参数

    protected override void OnExecute()
    {
        // 业务逻辑...
    }
}
```

控制台用法：

```
> spawn_unit Archer 10 3
```

**控制台显示效果：**

| 命令列表 | 显示                                                   |
| -------- | ------------------------------------------------------ |
| 命令名   | `spawn_unit`                                           |
| 参数签名 | `<UnitType: string> <Count: int> <Level: int>`（绿色） |
| 描述     | `生成单位`（灰色）                                     |

**执行效果：**

| 输入                         | 输出                                                                        |
| ---------------------------- | --------------------------------------------------------------------------- |
| `> spawn_unit`               | `缺少参数！该命令需要以下参数：用法: spawn_unit <UnitType> <Count> <Level>` |
| `> spawn_unit Archer 10 3`   | `生成单位 (参数: UnitType: Archer, Count: 10, Level: 3)`                    |
| `> spawn_unit Archer abc 3`  | `参数 'Count' 解析失败: 'abc' 不是有效的 整数 (int)`                        |
| `> spawn_unit Archer 10 3 5` | `参数过多！期望 3 个参数，实际收到 4 个`                                    |

### 方式三：静态方法命令

标记静态方法，签名必须为 `(string[] args)`：

```csharp
[ConsoleCommand("my_cmd", "我的命令")]
static void MyCommand(string[] args)
{
    // args[0], args[1] ...
}
```

## 特性说明

### `[ConsoleCommand]` 特性

| 参数          | 类型     | 说明                                       |
| ------------- | -------- | ------------------------------------------ |
| `commandName` | `string` | 命令名称（不区分大小写）                   |
| `description` | `string` | 命令描述，显示在 help 和命令列表中         |
| `usage`       | `string` | 用法提示（可选，类命令会自动生成参数签名） |

### 支持的参数类型

| 类型     | 示例输入                                  |
| -------- | ----------------------------------------- |
| `int`    | `100`, `-5`                               |
| `float`  | `3.14`, `-0.5`                            |
| `double` | `2.718`                                   |
| `long`   | `999999`                                  |
| `bool`   | `true`, `false`, `1`, `0`                 |
| `string` | `hello`                                   |
| `uint`   | `50`                                      |
| `short`  | `10`                                      |
| `byte`   | `255`                                     |
| `Enum`   | `UnitType.Archer`, `Archer`（忽略大小写） |

> **提示**：如果参数值包含空格，用双引号包裹：`spawn_unit "Long Bow Man" 10 3`

## 命令列表显示

控制台的命令列表会显示每条命令的：

- **命令名**（白色）
- **参数签名**（绿色，自动从属性反射生成）
- **描述**（灰色）

点击命令行会自动填入命令名并显示用法提示。

## 内置命令

| 命令            | 说明                       |
| --------------- | -------------------------- |
| `help` / `?`    | 显示所有可用命令及参数信息 |
| `clear` / `cls` | 清空控制台输出             |

## 热键

- **`LeftCtrl + Tab`**：切换控制台显示/隐藏（仅 Play 模式）
- 使用 WinAPI `GetAsyncKeyState` 实现全局热键检测，Game 视图聚焦时也可用

## 架构说明

```
CommandConsoleEditor (静态类)
├── WinAPI 热键检测 (GetAsyncKeyState)
├── UI Toolkit 界面 (嵌入 Game 视图)
│   ├── 标题栏 + 关闭按钮
│   ├── 输出区域 (ScrollView)
│   ├── 命令列表 (可折叠)
│   └── 输入区域 (TextField + 执行按钮)
├── 命令引擎
│   ├── 反射扫描 [ConsoleCommand] 特性
│   ├── 参数自动发现与类型解析
│   └── 通过 Architecture.SendCommand 执行
└── 输出管理 (颜色区分 Info/Success/Error/Command)
```

## 注意事项

1. **仅在 Play 模式下可用**，命令执行依赖 `Architecture` 实例
2. 类命令通过 `Activator.CreateInstance` 创建，每次执行都是新实例
3. 实现了 `IPoolableCommand` 接口的命令支持对象池回收
4. 命令列表最多显示 6 行，超出可滚动查看
5. 输出历史最多保留 200 条
