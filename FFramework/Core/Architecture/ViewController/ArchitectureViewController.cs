// =============================================================
// 描述：ViewController基础类
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：2.0.0
// =============================================================
using UnityEngine;

namespace FFramework.Core
{
    public abstract class ArchitectureViewController : MonoBehaviour, IViewController
    {
        /// <summary>
        /// 事件系统，由 Architecture 在注册时自动注入。
        /// </summary>
        [Inject]
        protected EventSystem eventSystem;

        /// <summary>
        /// 是否在 Awake 时自动注册到 Architecture。
        /// 子类可重写此属性返回 false 来禁用自动注册（例如需要手动控制注册时机时）。
        /// </summary>
        protected virtual bool AutoRegister => true;

        /// <summary>
        /// 是否已初始化，防止重复调用 OnInitialize。
        /// </summary>
        private bool _initialized = false;

        /// <summary>
        /// 是否已销毁，防止重复调用 OnDispose。
        /// </summary>
        private bool _disposed = false;

        public GameObject GameObject => gameObject;

        /// <summary>
        /// Awake 时自动注册到 Architecture（如果 AutoRegister 为 true）。
        /// 子类若重写 Awake，必须调用 base.Awake() 确保自动注册执行。
        /// </summary>
        protected virtual void Awake()
        {
            if (AutoRegister)
            {
                Architecture.Instance.RegisterViewController(this);
            }
        }

        /// <summary>
        /// 初始化，幂等设计：多次调用只执行一次 OnInitialize。
        /// </summary>
        public virtual void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            OnInitialize();
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OnDispose();
        }

        /// <summary>
        /// 子类重写初始化逻辑
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// 子类重写销毁逻辑
        /// </summary>
        protected virtual void OnDispose() { }

        /// <summary>
        /// 获取已注册的 Model（备选方案）。
        /// 推荐优先使用 [Inject] 特性在字段中声明依赖，
        /// 此方法适用于 Model 较多时不想全部声明为字段的场景。
        /// </summary>
        protected T GetModel<T>() where T : class, IModel, new()
        {
            return Architecture.Instance.GetModel<T>();
        }

        /// <summary>
        /// 发送事件
        /// </summary>
        protected void SendEvent(string eventName)
        {
            eventSystem?.Trigger(eventName);
        }

        /// <summary>
        /// 发送事件
        /// </summary>
        protected void SendEvent<T>(string eventName, T parameter)
        {
            eventSystem?.Trigger(eventName, parameter);
        }

        /// <summary>
        /// 注册事件
        /// </summary>
        protected void RegisterEvent(string eventName, System.Action callback)
        {
            eventSystem?.Register(eventName, callback);
        }

        /// <summary>
        /// 注册事件
        /// </summary>
        protected void RegisterEvent<T>(string eventName, System.Action<T> callback)
        {
            eventSystem?.Register(eventName, callback);
        }

        /// <summary>
        /// 注销事件
        /// </summary>
        protected void UnregisterEvent(string eventName, System.Action callback)
        {
            eventSystem?.Unregister(eventName, callback);
        }

        /// <summary>
        /// 注销事件
        /// </summary>
        protected void UnregisterEvent<T>(string eventName, System.Action<T> callback)
        {
            eventSystem?.Unregister(eventName, callback);
        }

        protected virtual void OnDestroy()
        {
            // 在销毁时注销该对象的所有事件监听
            eventSystem?.UnregisterTarget(this);
            // 从 Architecture 中注销（内部会调用 Dispose）
            Architecture.Instance?.UnregisterViewController(this);
        }
    }
}