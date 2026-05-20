// =============================================================
// 描述：架构管理器 - 管理Model、ViewController和Singleton的生命周期，支持依赖注入
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：2.1.0
// 修改：
//   v2.0.0 - 移除优先级有序初始化系统（单例均使用懒加载，无需 Architecture 调度）；
//            注册单例时立即执行依赖注入和 OnSingletonInit()；
//            简化 RegisterInstance<T>() 方法，移除 order 参数
//   v2.1.0 - UnloadAll 销毁顺序改为 Model → ViewController → Singleton（基础设施最后销毁）；
//            新增 _isUnloading 标志，ResolveDependency 在卸载期间返回 null 防止递归重新创建
// =============================================================
using System.Collections.Generic;
using UnityEngine;
using System;

namespace FFramework.Core
{
    [DefaultExecutionOrder(-100)]
    public class Architecture : SingletonMono<Architecture>
    {

        // 存储所有Models
        private Dictionary<Type, IModel> models = new Dictionary<Type, IModel>();

        // 存储所有ViewControllers（使用实例ID作为Key）
        private Dictionary<int, IViewController> viewControllers = new Dictionary<int, IViewController>();

        #region Singleton管理 - 存储

        // 按类型快速查找单例（用于依赖注入和生命周期管理）
        private Dictionary<Type, ISingleton> singletonByType
            = new Dictionary<Type, ISingleton>();

        #endregion

        protected override void InitializeSingleton()
        {
            // 设置依赖注入解析器
            InjectHelper.Resolver = ResolveDependency;
        }

        #region Model管理

        /// <summary>
        /// 注册Model
        /// </summary>
        public T RegisterModel<T>() where T : class, IModel, new()
        {
            Type modelType = typeof(T);

            if (models.TryGetValue(modelType, out var existingModel))
            {
                return existingModel as T;
            }

            T model = new T();
            models[modelType] = model;

            // 依赖注入：在 Initialize() 之前注入依赖
            InjectHelper.AutoInject(model);

            try
            {
                model.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError($"Model {modelType.Name} 初始化失败: {e.Message}");
                models.Remove(modelType);
                throw;
            }

            Debug.Log($"注册Model: {modelType.Name}");
            return model;
        }

        /// <summary>
        /// 获取Model（如果不存在则自动注册）
        /// </summary>
        public T GetModel<T>() where T : class, IModel, new()
        {
            Type modelType = typeof(T);

            if (models.TryGetValue(modelType, out var model))
            {
                return model as T;
            }

            return RegisterModel<T>();
        }

        /// <summary>
        /// 注销/释放Model
        /// </summary>
        public void UnregisterModel<T>() where T : class, IModel
        {
            Type modelType = typeof(T);

            if (models.TryGetValue(modelType, out var model))
            {
                try
                {
                    model?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"注销Model {modelType.Name} 时发生异常: {e.Message}");
                }
                models.Remove(modelType);
                Debug.Log($"注销Model: {modelType.Name}");
            }
        }

        #endregion

        #region ViewController管理

        /// <summary>
        /// 注册ViewController（通过 GameObject 获取/创建组件）
        /// </summary>
        public T RegisterViewController<T>(GameObject gameObject) where T : MonoBehaviour, IViewController
        {
            if (gameObject == null)
            {
                Debug.LogError("RegisterViewController: gameObject 不能为空");
                return null;
            }

            T viewController = gameObject.GetComponent<T>();
            if (viewController == null)
            {
                viewController = gameObject.AddComponent<T>();
            }

            // 委托给非泛型版本执行注册、注入、初始化
            RegisterViewController(viewController as IViewController);
            return viewController;
        }

        /// <summary>
        /// 获取ViewController（不存在时自动注册）
        /// </summary>
        public T GetViewController<T>(GameObject gameObject = null) where T : MonoBehaviour, IViewController
        {
            if (gameObject == null) return null;

            T existingController = gameObject.GetComponent<T>();
            if (existingController != null)
            {
                // 委托给非泛型版本（内部已处理已注册/未注册的情况）
                RegisterViewController(existingController as IViewController);
                return existingController;
            }

            return RegisterViewController<T>(gameObject);
        }

        /// <summary>
        /// 注册 ViewController（非泛型，用于自动注册）
        /// </summary>
        public void RegisterViewController(IViewController viewController)
        {
            if (viewController == null)
            {
                Debug.LogError("RegisterViewController: viewController 不能为空");
                return;
            }

            var mono = viewController as MonoBehaviour;
            if (mono == null)
            {
                Debug.LogError($"RegisterViewController: {viewController.GetType().Name} 不是 MonoBehaviour");
                return;
            }

            int instanceId = mono.GetInstanceID();

            if (viewControllers.ContainsKey(instanceId))
                return;

            viewControllers[instanceId] = viewController;

            // 依赖注入：在 Initialize() 之前注入依赖
            InjectHelper.AutoInject(viewController);

            try
            {
                viewController.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError($"ViewController {viewController.GetType().Name} 初始化失败: {e.Message}");
                viewControllers.Remove(instanceId);
                throw;
            }

            Debug.Log($"注册ViewController: {viewController.GetType().Name}({instanceId})");
        }

        /// <summary>
        /// 注销 ViewController（非泛型，用于自动注销）
        /// </summary>
        public void UnregisterViewController(IViewController viewController)
        {
            if (viewController == null) return;

            var mono = viewController as MonoBehaviour;
            if (mono == null) return;

            int instanceId = mono.GetInstanceID();

            if (viewControllers.TryGetValue(instanceId, out var vc))
            {
                try
                {
                    vc?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"注销ViewController({instanceId}) 时发生异常: {e.Message}");
                }
                viewControllers.Remove(instanceId);
                Debug.Log($"注销ViewController: {instanceId}");
            }
        }

        /// <summary>
        /// 注销ViewController（泛型）
        /// </summary>
        public void UnregisterViewController<T>(T viewController) where T : MonoBehaviour, IViewController
        {
            UnregisterViewController(viewController as IViewController);
        }

        #endregion

        #region Singleton管理

        /// <summary>
        /// 注册非 MonoBehaviour 单例（通过 new T() 创建实例）。
        /// 适用于继承 Singleton<T> 的非 MonoBehaviour 类型。
        /// 注册后自动执行依赖注入并调用 OnSingletonInit() 完成初始化。
        /// </summary>
        public T RegisterInstance<T>() where T : class, ISingleton, new()
        {
            Type type = typeof(T);

            if (singletonByType.TryGetValue(type, out var existing))
                return existing as T;

            // 1. 创建实例
            T instance = new T();

            // 2. 注册到字典
            singletonByType[type] = instance;

            // 3. 依赖注入
            InjectHelper.AutoInject(instance);

            // 4. 立即初始化（懒加载模式，注册后立即调用 OnSingletonInit）
            try
            {
                instance.OnSingletonInit();
            }
            catch (Exception e)
            {
                Debug.LogError($"Singleton [{type.Name}] 初始化失败: {e.Message}");
                singletonByType.Remove(type);
                throw;
            }

            Debug.Log($"注册 Singleton: {type.Name}");
            return instance;
        }

        /// <summary>
        /// 注册已存在的单例实例（适用于 SingletonMono<T> 子类）。
        /// 注册后自动执行依赖注入并调用 OnSingletonInit() 完成初始化。
        /// </summary>
        /// <param name="instance">已存在的单例实例</param>
        public void RegisterInstance<T>(T instance) where T : class, ISingleton
        {
            if (instance == null)
            {
                Debug.LogError("RegisterInstance: instance 不能为空");
                return;
            }

            Type type = typeof(T);

            if (singletonByType.TryGetValue(type, out var existing))
            {
                if (ReferenceEquals(existing, instance))
                    return;

                Debug.LogWarning($"Singleton {type.Name} 已注册其他实例，跳过当前注册");
                return;
            }

            // 1. 注册到字典
            singletonByType[type] = instance;

            // 2. 依赖注入
            InjectHelper.AutoInject(instance);

            // 3. 立即初始化
            try
            {
                instance.OnSingletonInit();
            }
            catch (Exception e)
            {
                Debug.LogError($"Singleton [{type.Name}] 初始化失败: {e.Message}");
                singletonByType.Remove(type);
                throw;
            }

            Debug.Log($"注册 Singleton: {type.Name}");
        }

        /// <summary>
        /// 获取已注册的单例实例
        /// </summary>
        public T GetInstance<T>() where T : class, ISingleton
        {
            if (singletonByType.TryGetValue(typeof(T), out var instance))
                return instance as T;
            return null;
        }

        /// <summary>
        /// 注销指定类型的单例
        /// </summary>
        public void UnregisterInstance<T>() where T : class, ISingleton
        {
            Type type = typeof(T);
            RemoveFromSingletonRegistry(type);
        }

        /// <summary>
        /// 检查指定类型是否已注册为单例
        /// </summary>
        public bool HasInstance(Type type)
        {
            return singletonByType.ContainsKey(type);
        }

        /// <summary>
        /// 检查指定类型是否已注册为单例（泛型版本）
        /// </summary>
        public bool HasInstance<T>() where T : class, ISingleton
        {
            return singletonByType.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 内部：注册单例实例（非泛型，供 SingletonMono 内部使用）。
        /// </summary>
        internal void RegisterInstance(ISingleton instance)
        {
            Type type = instance.GetType();
            if (singletonByType.ContainsKey(type))
                return;

            singletonByType[type] = instance;
            InjectHelper.AutoInject(instance);

            try
            {
                instance.OnSingletonInit();
            }
            catch (Exception e)
            {
                Debug.LogError($"Singleton [{type.Name}] 初始化失败: {e.Message}");
                singletonByType.Remove(type);
                throw;
            }

            Debug.Log($"注册 Singleton: {type.Name}");
        }

        /// <summary>
        /// 内部：从单例注册表移除
        /// </summary>
        private void RemoveFromSingletonRegistry(Type type)
        {
            if (singletonByType.TryGetValue(type, out var instance))
            {
                try
                {
                    instance.OnSingletonDispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"注销 Singleton {type.Name} 时发生异常: {e.Message}");
                }

                singletonByType.Remove(type);
                Debug.Log($"注销 Singleton: {type.Name}");
            }
        }

        #endregion

        #region Command 管理

        /// <summary>
        /// 发送一个命令（非 ICommandSender 对象使用）。
        /// ICommandSender 对象推荐通过扩展方法 SendCommand() 调用。
        /// </summary>
        /// <param name="command">需要执行的命令实例</param>
        public void SendCommand(ICommand command)
        {
            command?.Execute();
        }

        /// <summary>
        /// 发送一个带返回值的命令。
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="command">需要执行的命令实例</param>
        public TResult SendCommand<TResult>(ICommand<TResult> command)
        {
            if (command == null) return default;
            return command.Execute();
        }

        #endregion

        #region 数据持久化（批量操作）

        /// <summary>
        /// 保存所有已注册 Model 的数据到指定存档
        /// </summary>
        /// <param name="slotName">存档文件夹名称，由调用方自定义</param>
        public void SaveAllData(string slotName)
        {
            foreach (var model in models.Values)
            {
                try
                {
                    model.SaveData(slotName);
                }
                catch (Exception e)
                {
                    Debug.LogError($"保存 {model.GetType().Name} 失败: {e.Message}");
                }
            }
            Debug.Log($"所有数据已保存到存档 [{slotName}]");
        }

        /// <summary>
        /// 从指定存档加载所有已注册 Model 的数据
        /// </summary>
        /// <param name="slotName">存档文件夹名称，与保存时传入的名称一致</param>
        public void LoadAllData(string slotName)
        {
            foreach (var model in models.Values)
            {
                try
                {
                    model.LoadData(slotName);
                }
                catch (Exception e)
                {
                    Debug.LogError($"加载 {model.GetType().Name} 失败: {e.Message}");
                }
            }
            Debug.Log($"已从存档 [{slotName}] 加载所有数据");
        }

        #endregion

        #region 生命周期管理

        /// <summary>
        /// 强制卸载所有资源（例如回到主菜单时）。
        /// 销毁顺序：Model → ViewController → Singleton
        /// （基础设施最后销毁，防止 ViewController/Model 的 Dispose 中访问已销毁的单例）
        /// </summary>
        public void UnloadAll()
        {
            // 标记正在卸载，防止 Dispose 中递归触发注册
            _isUnloading = true;

            // 1. 先销毁 Model（数据层）
            foreach (var model in models.Values)
                model?.Dispose();
            models.Clear();

            // 2. 再销毁 ViewController（视图层）
            foreach (var vc in viewControllers.Values)
                vc?.Dispose();
            viewControllers.Clear();

            // 3. 最后销毁 Singleton（基础设施层）
            DisposeAllSingletons();

            _isUnloading = false;
            Debug.Log("所有架构资源已卸载");
        }

        /// <summary>
        /// 是否正在执行 UnloadAll，用于防止卸载期间的递归操作。
        /// </summary>
        private bool _isUnloading = false;

        /// <summary>
        /// 销毁所有已注册的单例
        /// </summary>
        private void DisposeAllSingletons()
        {
            foreach (var singleton in singletonByType.Values)
            {
                try
                {
                    singleton.OnSingletonDispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"销毁 Singleton [{singleton.GetType().Name}] 时发生异常: {e.Message}");
                }
            }
            singletonByType.Clear();
        }

        #endregion

        #region 依赖注入

        /// <summary>
        /// 根据类型解析已注册的依赖。
        /// 优先级：已注册的 Model > 自动注册未注册的 IModel > 已注册的 Singleton > 已知的单例类型。
        /// </summary>
        private object ResolveDependency(Type type)
        {
            // 卸载期间不解析依赖，防止递归重新创建
            if (_isUnloading)
                return null;

            // 1. 从 models 字典中查找已注册的 Model
            if (models.TryGetValue(type, out var model))
                return model;

            // 2. 如果是未注册的 IModel 类型（非抽象/非接口），自动注册
            if (typeof(IModel).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            {
                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor != null)
                {
                    return RegisterModelByType(type);
                }
            }

            // 3. 从已注册的 Singleton 中查找
            if (singletonByType.TryGetValue(type, out var singleton))
                return singleton;

            // 4. 如果是未注册的 ISingleton 类型且不是 MonoBehaviour（非 Mono 可自动创建），自动注册
            if (typeof(ISingleton).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface
                && !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor != null)
                {
                    return RegisterInstanceByType(type);
                }
            }

            // 5. 已知的单例类型（硬编码兜底）
            if (type == typeof(EventSystem))
                return EventSystem.Instance;

            if (type == typeof(Architecture))
                return this;

            return null;
        }

        /// <summary>
        /// 通过反射按类型注册 Singleton（非泛型版，供依赖注入使用）
        /// </summary>
        private object RegisterInstanceByType(Type type)
        {
            var instance = Activator.CreateInstance(type) as ISingleton;
            if (instance == null)
            {
                Debug.LogError($"无法创建 Singleton 实例: {type.Name}");
                return null;
            }

            singletonByType[type] = instance;

            // 依赖注入
            InjectHelper.AutoInject(instance);

            // 立即初始化
            try
            {
                instance.OnSingletonInit();
            }
            catch (Exception e)
            {
                Debug.LogError($"Singleton [{type.Name}] 初始化失败: {e.Message}");
                singletonByType.Remove(type);
                throw;
            }

            Debug.Log($"[Inject] 自动注册 Singleton: {type.Name}");
            return instance;
        }

        /// <summary>
        /// 通过反射按类型注册 Model（非泛型版，供依赖注入使用）
        /// </summary>
        private object RegisterModelByType(Type type)
        {
            var model = Activator.CreateInstance(type) as IModel;
            if (model == null)
            {
                Debug.LogError($"无法创建 Model 实例: {type.Name}");
                return null;
            }

            models[type] = model;

            // 依赖注入：在 Initialize() 之前注入依赖
            InjectHelper.AutoInject(model);

            try
            {
                model.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError($"Model {type.Name} 初始化失败: {e.Message}");
                models.Remove(type);
                throw;
            }

            Debug.Log($"[Inject] 自动注册 Model: {type.Name}");
            return model;
        }

        #endregion

        protected override void OnDestroy()
        {
            UnloadAll();
            base.OnDestroy();
        }
    }
}
