# ResLoad 资源加载工具

> 命名空间：`FFramework.Utility`

基于 `SingletonMono` 的资源加载管理器，提供 Resources 和 AssetDatabase 两种加载方式，支持同步/异步加载、缓存、实例化控制。

---

## 快速开始

### 1. 同步加载

```csharp
// 加载 Resources 中的预制体（返回实例化的 GameObject）
GameObject go = ResLoad.Instance.LoadRes<GameObject>("Prefabs/Enemy/Bullet");

// 不实例化，返回原始资源
GameObject prefab = ResLoad.Instance.LoadRes<GameObject>("Prefabs/Enemy/Bullet", instantiate: false);

// 加载其他类型资源
Sprite sprite = ResLoad.Instance.LoadRes<Sprite>("Textures/Icon");
TextAsset text = ResLoad.Instance.LoadRes<TextAsset>("Configs/settings");
```

### 2. 异步加载

```csharp
// 异步加载预制体
ResLoad.Instance.LoadResAsync<GameObject>("Prefabs/Enemy/Bullet", (result) => {
    // result 是实例化后的 GameObject
    result.transform.position = Vector3.zero;
});

// 带进度回调
ResLoad.Instance.LoadResAsync<GameObject>("Prefabs/UI/MainPanel",
    (result) => { /* 完成回调 */ },
    progress: (p) => { /* 加载进度 0~1 */ });
```

### 3. 从文件路径加载（AssetDatabase，仅 Editor）

```csharp
// 使用完整 Assets 路径
GameObject go = ResLoad.Instance.LoadAssetFromPath<GameObject>(
    "Assets/Game/Prefabs/Character.prefab");

// 异步
ResLoad.Instance.LoadAssetFromPathAsync<GameObject>(
    "Assets/Game/Prefabs/Character.prefab",
    (result) => { /* ... */ });
```

### 4. 缓存管理

```csharp
// 预热资源（提前加载到缓存）
ResLoad.Instance.PreloadResource<GameObject>("Prefabs/Common/PooledObj");

// 清除单个缓存
ResLoad.Instance.ClearCache("Prefabs/Common/PooledObj");

// 清除所有缓存
ResLoad.Instance.ClearAllCache();

// 获取缓存路径列表
string[] cachedPaths = ResLoad.Instance.GetCachedPaths();

// 获取缓存数量
int count = ResLoad.Instance.GetCacheCount();
```

---

## API 参考

### 同步加载

| 方法                                         | 说明                     |
| -------------------------------------------- | ------------------------ |
| `LoadRes<T>(path, useCache, instantiate)`    | 从 Resources 加载        |
| `LoadRes(path, type, useCache, instantiate)` | 非泛型版本               |
| `LoadPrefab<T>(path, useCache)`              | 加载预制体（自动实例化） |
| `LoadGameObject(path, useCache)`             | 加载 GameObject          |
| `LoadAssetFromPath<T>(assetPath, ...)`       | 从 Assets 路径加载       |

### 异步加载

| 方法                                                               | 说明                   |
| ------------------------------------------------------------------ | ---------------------- |
| `LoadResAsync<T>(path, callback, useCache, instantiate, progress)` | 异步加载 Resources     |
| `LoadPrefabAsync<T>(path, callback, useCache, progress)`           | 异步加载预制体         |
| `LoadGameObjectAsync(path, callback, useCache, progress)`          | 异步加载 GameObject    |
| `LoadAssetFromPathAsync<T>(assetPath, callback, ...)`              | 异步从 Assets 路径加载 |
| `LoadMultipleResAsync(paths, callback, progress, useCache)`        | 批量异步加载           |

### 缓存管理

| 方法                                    | 说明             |
| --------------------------------------- | ---------------- |
| `PreloadResource<T>(path, instantiate)` | 预热资源到缓存   |
| `ClearCache(path)`                      | 清除指定缓存     |
| `ClearAllCache()`                       | 清除所有缓存     |
| `GetCachedPaths()`                      | 获取所有缓存路径 |
| `GetCacheCount()`                       | 获取缓存数量     |
| `ResourceExists(path)`                  | 检查资源是否存在 |
| `UnloadUnusedAssets()`                  | 卸载未使用的资源 |

---

## 注意事项

- Resources 路径相对于 `Assets/Resources` 文件夹，不含后缀
- AssetDatabase 方式仅在 Unity Editor 中可用
- `instantiate` 参数仅对 `GameObject` 类型有效
- 异步加载使用协程实现，需要在 MonoBehaviour 上运行
- 缓存默认启用（`useCache = true`）
