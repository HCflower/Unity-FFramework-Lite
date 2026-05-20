# SceneLoad 场景加载工具

> 命名空间：`FFramework.Utility`

基于 `SingletonMono` 的场景加载管理器，支持同步/异步加载、预加载、进度监控等。

---

## 快速开始

### 1. 同步加载场景

```csharp
// 加载场景（默认 Single 模式）
SceneLoad.Instance.LoadScene("GameScene");

// 叠加模式加载
SceneLoad.Instance.LoadScene("UIScene", LoadSceneMode.Additive);

// 加载完成后回调
SceneLoad.Instance.LoadScene("GameScene", LoadSceneMode.Single, () => {
    Debug.Log("场景加载完成");
});
```

### 2. 异步加载场景

```csharp
// 异步加载，带进度回调
SceneLoad.Instance.LoadSceneAsync("GameScene", LoadSceneMode.Single,
    (progress) => {
        // 进度 0~1
        loadingSlider.value = progress;
    },
    () => {
        // 加载完成
        loadingPanel.Hide();
    });
```

### 3. 预加载与激活

```csharp
// 预加载场景（加载完成后不自动激活）
AsyncOperation op = SceneLoad.Instance.PreloadScene("LargeScene",
    (progress) => { /* 后台加载进度 */ },
    () => { /* 预加载完成 */ });

// 稍后激活
SceneLoad.Instance.ActivatePreloadedScene(op);
```

### 4. 卸载场景

```csharp
SceneLoad.Instance.UnloadScene("UIScene", () => {
    Debug.Log("场景已卸载");
});
```

---

## API 参考

| 方法                                                     | 说明                     |
| -------------------------------------------------------- | ------------------------ |
| `LoadScene(sceneName, mode, complete)`                   | 同步加载场景             |
| `LoadSceneAsync(sceneName, mode, progress, complete)`    | 异步加载场景             |
| `LoadSceneAsyncWithCoroutine(sceneName, mode, complete)` | 协程方式异步加载         |
| `PreloadScene(sceneName, progress, complete)`            | 预加载场景（不自动激活） |
| `ActivatePreloadedScene(AsyncOperation)`                 | 激活已预加载的场景       |
| `UnloadScene(sceneName, complete)`                       | 卸载场景                 |
| `GetActiveSceneName()`                                   | 获取当前激活场景名称     |
| `IsSceneLoaded(sceneName)`                               | 检查场景是否已加载       |

---

## 注意事项

- 场景名称需与 Build Settings 中注册的场景名一致
- 预加载场景需要在适当时机手动调用 `ActivatePreloadedScene`
- 异步加载使用协程实现，进度回调在加载期间持续触发
- 内部使用 `CoroutineRunner` 单例来执行协程
