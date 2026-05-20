# ObjectPool 对象池系统文档

## 概述

ObjectPool 是 FFramework 中的一个通用对象池系统，基于 `SingletonMono` 单例模式实现，支持通过 Resources 路径或预制体两种方式进行对象的获取、归还、预热和状态查询。提供丰富的链式调用扩展方法，简化日常开发中的对象复用需求。

---

## 架构设计

### 类关系图

```
SingletonMono<ObjectPool>
        |
    ObjectPool (单例入口)
        |
        |-- PoolData (内部类，管理单个对象池)
        |       |-- List<GameObject> objectList      (空闲对象列表)
        |       |-- HashSet<GameObject> objectsInPool (池中状态跟踪)
        |       |-- GameObject parent                (池父节点)
        |       |-- GameObject prefab                (预制体引用)
        |
        |-- Dictionary<string, PoolData> poolDic     (池字典)
        |-- Dictionary<GameObject, string> objectToPoolName (对象映射缓存)
        |
    ObjectPoolExtensions (静态扩展类)
        |-- GameObject 扩展方法 (链式调用)
        |-- Component 扩展方法 (泛型链式调用)
        |-- string 扩展方法 (Resources路径快速获取)
```

### 核心接口

```csharp
public interface IPoolObject
{
    void OnBeforeGetFromPool();   // 从池中取出时调用
    void OnAfterReturnToPool();   // 归还到池中时调用
}
```

---

## 核心功能

### 1. 对象获取

#### 通过 Resources 路径获取

```csharp
// 获取 GameObject
GameObject obj = ObjectPool.Instance.GetResourcesObjectFromPool("Enemy/Bullet");

// 获取并返回指定组件
Bullet bullet = ObjectPool.Instance.GetResourcesObjectFromPool<Bullet>("Enemy/Bullet");
```

#### 通过预制体获取

```csharp
// 获取 GameObject
GameObject obj = ObjectPool.Instance.GetAssetsObjectFromPool(bulletPrefab);

// 获取并返回指定组件
Bullet bullet = ObjectPool.Instance.GetAssetsObjectFromPool<Bullet>(bulletPrefab);
```

#### 扩展方法快速获取

```csharp
// 通过字符串名称 (Resources路径)
GameObject obj = "Enemy/Bullet".GetPoolObject();
Bullet bullet = "Enemy/Bullet".GetPoolObject<Bullet>();

// 通过预制体
GameObject obj = bulletPrefab.GetPoolObject();
Bullet bullet = bulletPrefab.GetPoolObject<Bullet>();
```

### 2. 对象归还

```csharp
// 普通归还
ObjectPool.Instance.ReturnObjectToPool(obj);

// 扩展方法链式归还
obj.ReturnPool();

// 延迟归还 (秒)
obj.ReturnPool(2.0f);

// Component 扩展归还
bullet.ReturnPool();
bullet.ReturnPool(2.0f);
```

### 3. 对象预热

```csharp
// 预热 Resources 对象
ObjectPool.Instance.PrewarmResourcesPool("Enemy/Bullet", 10);

// 预热预制体对象
ObjectPool.Instance.PrewarmAssetsPool(bulletPrefab, 10);

// 扩展方法预热
"Enemy/Bullet".PrewarmPool(10);
bulletPrefab.PrewarmPool(10);

// 批量预热
string[] names = {"Enemy/Bullet", "Effect/Explosion"};
int[] counts = {10, 5};
names.PrewarmPools(counts);
```

### 4. 链式调用

```csharp
// 一套完整的获取 -> 设置 -> 使用 -> 归还流程
bulletPrefab.GetPoolObject()
    .SetPosition(new Vector3(10, 0, 0))
    .SetRotation(Quaternion.identity)
    .SetLocalScale(1.5f)
    .Do<Rigidbody>(rb => rb.velocity = Vector3.forward * 10)
    .ReturnPool(3.0f);
```

---

## 优化建议

### 1. 内存泄漏修复

#### objectToPoolName 缓存泄漏

当前 [`ObjectPool.objectToPoolName`](Assets/FFramework/Utility/ObjectPool/ObjectPool.cs:401) 字典存储了从 GameObject 到池名称的映射，但当对象被外部销毁或场景卸载时，字典中的条目不会被移除，导致内存泄漏。

改进方案：

- 在 `ReturnObjectToPool` 中检查对象是否已被销毁
- 添加 `OnDestroy` 事件监听或使用 `WeakReference` 避免强引用
- 定期清理无效引用
- 在 `ClearPool` 等方法中同步清理缓存

#### delayReturnCoroutines 协程跟踪泄漏

[`ObjectPoolExtensions.delayReturnCoroutines`](Assets/FFramework/Utility/ObjectPool/ObjectPoolExtensions.cs:552) 静态字典在对象被提前销毁时，关联的协程条目永远不会被清理。

改进方案：

- 使用 `ConditionalWeakTable` 或者在对象销毁时清理条目
- 为对象添加 `OnDestroy` 钩子来移除协程跟踪
- 改用非协程的 Timer 系统

### 2. 性能优化

#### GetObjectFromPool 中的循环清理

[`PoolData.GetObjectFromPool`](Assets/FFramework/Utility/ObjectPool/ObjectPool.cs:51) 每次获取对象时都遍历整个列表清理空引用，时间复杂度 O(n)。

改进方案：

- 改为惰性清理：只在获取到空对象时移除
- 添加独立的清理方法，在预热、归还或定时清理时调用
- 使用 LinkedList 替代 List，利用迭代器安全移除

#### WaitForSeconds 对象分配

[`ReturnToPoolCoroutine`](Assets/FFramework/Utility/ObjectPool/ObjectPoolExtensions.cs:554) 每次延迟调用都创建新的 `WaitForSeconds` 对象，产生 GC 分配。

改进方案：

- 缓存常用的 `WaitForSeconds` 实例
- 使用 `WaitForSecondsRealtime` 配合缓存
- 迁移到 `UniTask` 或自定义计时器系统

#### Resources.Load 同步加载

[`PoolData` 构造函数](Assets/FFramework/Utility/ObjectPool/ObjectPool.cs:39) 中同步调用 `Resources.Load` 可能导致瞬间卡顿。

改进方案：

- 支持异步加载 (`Resources.LoadAsync`)
- 对接 Addressables 系统
- 保留 Resources 方式作为备选，推荐预制体方式

### 3. 架构设计优化

#### PoolData 结构与灵活性不足

`PoolData` 内部硬编码了 `Resources.Load` 逻辑和 `HashSet` 跟踪，职责不够单一，扩展困难。

改进方案：

- 使用工厂模式创建对象，将 `Resources.Load`、`Instantiate` 等逻辑抽离
- 支持自定义创建/销毁回调
- 提供泛型 `PoolData<T>` 支持类型化对象池

#### 协程系统侵入性强

[`StopAllCoroutinesOnReturn`](Assets/FFramework/Utility/ObjectPool/ObjectPool.cs:283) 在归还时停止对象上的所有协程，可能误停其他业务协程。

改进方案：

- 只跟踪和停止延迟返回相关的协程
- 使用独立的协程管理器
- 使用 `MonoBehaviour` 的 `StopCoroutine(IEnumerator)` 精确停止

#### 单例模式的限制

当前基于 `SingletonMono<ObjectPool>`，一个进程内只能有一个对象池。

改进方案：

- 支持多实例模式，不同场景使用不同对象池
- 在 `ObjectPool` 内部管理多个池组 (PoolGroup)
- 支持场景绑定的对象池自动释放

### 4. 功能增强

#### 缺少池容量限制

当前池没有最大容量限制，在高频创建对象时可能无限增长。

```csharp
// 建议增加
public class PoolData
{
    public int maxCapacity = -1;  // -1 表示无限制
    // 当超过容量时，多余的归还对象直接销毁
}
```

#### 缺少对象老化与自动清理

长时间不使用的对象应支持自动清理，释放内存。

```csharp
// 建议增加
public class PoolData
{
    public float idleTimeout = -1;  // 空闲超时时间
    private Dictionary<GameObject, float> returnTimes; // 记录归还时间
    // 定期检查并清理超时空闲对象
}
```

#### 场景切换支持不完善

[`EnsurePoolRoot`](Assets/FFramework/Utility/ObjectPool/ObjectPool.cs:431) 中 `DontDestroyOnLoad` 被注释掉，池根节点不会跨场景持久化。

改进方案：

- 将 `DontDestroyOnLoad` 作为配置项开放
- 支持场景绑定的池（场景卸载时自动清理）
- 提供场景切换时的池管理策略

#### 调试与监控能力不足

当前只能查询池数量和池名称，缺乏运行时监控手段。

改进方案：

- 添加 PoolProfile 工具，实时显示各池状态
- 支持池使用统计（获取次数、创建次数、归还次数）
- 添加 EditorWindow 可视化面板
- 支持运行时动态修改池参数

### 5. 错误处理与健壮性

#### 命名冲突风险

对象池名称基于 `obj.name`，如果场景中有同名但不同预制体的对象，会导致池混淆。

改进方案：

- 使用预制体引用（`UnityEngine.Object` 的引用）作为键
- 使用 GUID 或 AssetReference 作为唯一标识
- 在 `ReturnObjectToPool` 中验证对象确实属于该池

#### 异常处理不足

`GetResourcesObjectFromPool` 中 Resources.Load 失败后只是 LogError，没有足够的降级策略。

改进方案：

- 支持注册失败回调
- 自动尝试从 AssetBundle/Addressables 加载
- 提供默认创建策略

### 6. 代码质量改进

#### 命名修正

- [`ReturnToPoolDalay`](Assets/FFramework/Utility/ObjectPool/ObjectPoolExtensions.cs:121) -> 应改为 `ReturnToPoolDelay`
- [`PrewarmPools`](Assets/FFramework/Utility/ObjectPool/ObjectPoolExtensions.cs:401) -> 应改为 `PrewarmPools`
- 统一 `Prewarm` / `PreWarm` 命名风格

#### 代码组织

- [`objectToPoolName`](Assets/FFramework/Utility/ObjectPool/ObjectPool.cs:401) 声明在区域中间，应移到类顶部字段区域
- 减少 `PoolData` 构造函数的重载，使用 Builder 模式或参数对象
- 将 `IPoolObject` 接口移到独立文件

---

## 性能分析

### 当前性能瓶颈

| 操作               | 时间复杂度 | 问题                           |
| ------------------ | ---------- | ------------------------------ |
| GetObjectFromPool  | O(n)       | 每次遍历清理空引用             |
| ReturnObjectToPool | O(1)       | 正常，但 Contains 检查有开销   |
| PrewarmObjects     | O(n)       | Instantiate 本身开销大，可接受 |
| IsObjectInPool     | O(1)       | HashSet 查找，正常             |
| 延迟归还           | 有GC       | 每次创建 WaitForSeconds        |

### GC 分配分析

| 操作       | 分配原因                         | 频率     |
| ---------- | -------------------------------- | -------- |
| 获取对象   | Instantiate (创建时)             | 池为空时 |
| 归还对象   | 无分配                           | 每次     |
| 延迟归还   | WaitForSeconds + Coroutine       | 每次     |
| 创建池     | new PoolData + new GameObject    | 首次     |
| 创建根节点 | new GameObject("ObjectPoolRoot") | 首次     |

---

## 使用规范

### 推荐实践

1. **优先使用预制体方式**：`GetAssetsObjectFromPool` 避免 Resources 路径错误和同步加载
2. **提前预热**：在场景加载时预热高频使用的对象池
3. **实现 IPoolObject 接口**：在获取/归还时进行状态重置，避免对象状态污染
4. **合理设置延迟归还**：使用 `ReturnPool(delay)` 替代手动计时
5. **及时清理**：场景切换时调用 `ClearPool()` 释放不再使用的池

### 注意事项

1. `ReturnPool` 链式调用时，返回值不再代表原对象（已归还到池中）
2. 延迟归还期间，`StopAllCoroutinesOnReturn` 会停止对象上的所有协程
3. 对象名称会作为池的唯一标识，确保不同预制体使用不同名称
4. 当前不支持 Addressables，加载 Resources 外部的资源需手动创建对象池

---

## 版本记录

| 版本  | 日期       | 描述                              |
| ----- | ---------- | --------------------------------- |
| 1.0.0 | 2025-11-15 | 初始版本                          |
| 1.0.1 | -          | 添加 IsObjectInPool 状态查询功能  |
| 1.0.2 | -          | (优化计划) 修复内存泄漏，改进性能 |
