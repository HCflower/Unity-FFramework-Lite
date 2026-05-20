// =============================================================
// 描述：Mono单例基类
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：2.0.0
// 修改记录：
//   v1.0.2 - 单例初始化方法改为抽象,子类必须重写
//   v1.0.3 - 添加 isApplicationQuitting 标志，防止退出运行模式时
//            其他对象的 OnDestroy 中访问 Instance 导致重新创建对象
//   v1.1.0 - 实现 ISingleton 接口，支持 Architecture 统一管理；
//            新增 Priority 抽象属性、OnSingletonInit 和 OnSingletonDispose 方法
//   v2.0.0 - 移除 Priority 抽象属性（懒加载模式不需要按优先级有序初始化）；
//            改为继承 ISingleton 接口，OnSingletonInit() 调用 InitializeSingleton()；
//            Instance 创建后立即注册到 Architecture 进行依赖注入和初始化
// =============================================================
using UnityEngine;

namespace FFramework.Core
{
    /// <summary>
    /// 修复的单例MonoBehaviour基类
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public abstract class SingletonMono<T> : MonoBehaviour, ISingleton where T : MonoBehaviour
    {
        private static T instance;
        private static bool isInitialized = false;
        private static bool isApplicationQuitting = false;

        public static T Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return null;
#endif
                // 应用正在退出时，不再创建新实例，避免 "Some objects were not cleaned up" 警告
                if (isApplicationQuitting)
                    return null;

                if (instance == null)
                {
                    instance = FindObjectOfType<T>();
                    if (instance == null)
                    {
                        GameObject singleton = new GameObject(typeof(T).Name);
                        instance = singleton.AddComponent<T>();
#if UNITY_EDITOR
                        if (Application.isPlaying)
#endif
                        {
                            DontDestroyOnLoad(singleton);
                        }
                    }
                    if (!isInitialized)
                    {
                        isInitialized = true;

                        if (instance is Architecture arch)
                        {
                            // Architecture 自身：设置依赖注入解析器
                            arch.InitializeSingleton();
                        }
                        else if (instance is ISingleton singleton)
                        {
                            // 其他单例：注册到 Architecture（自动注入依赖 + 调用 OnSingletonInit）
                            Architecture.Instance.RegisterInstance(singleton);
                        }
                    }
                }
                return instance;
            }
        }

        protected virtual void Awake()
        {
            // 如果场景中有重复的单例对象，销毁自己
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // 场景中直接放置的组件：Instance 可能未被访问过，但场景中已有实例
            // 通过 FindObjectsOfType 检查是否有其他同类型实例
            if (instance == null)
            {
                var allInstances = Resources.FindObjectsOfTypeAll<T>();
                int count = 0;
                foreach (var obj in allInstances)
                {
                    if (obj != null && obj.gameObject.scene.name != null)
                    {
                        count++;
                        if (count > 1) break;
                    }
                }
                if (count > 1)
                {
                    Destroy(gameObject);
                }
            }
        }

        /// <summary>
        /// 单例初始化时调用，子类可重写。
        /// 由 OnSingletonInit() 触发，无需手动调用。
        /// </summary>
        protected virtual void InitializeSingleton() { }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                isApplicationQuitting = true;
                instance = null;
                isInitialized = false;
            }
        }

        /// <summary>
        /// 确保单例已初始化
        /// </summary>
        public static void EnsureInitialized()
        {
            T _ = Instance; // 访问Instance属性触发初始化
        }

        #region ISingleton 实现

        /// <summary>
        /// ISingleton 初始化方法。
        /// 默认行为：调用 InitializeSingleton()（向后兼容）。
        /// </summary>
        public void OnSingletonInit()
        {
            if (instance == this || instance == null)
            {
                InitializeSingleton();
            }
        }

        /// <summary>
        /// ISingleton 销毁方法（由 Architecture.UnloadAll 触发）。
        /// 子类可重写此方法添加清理逻辑。
        /// </summary>
        public virtual void OnSingletonDispose() { }

        #endregion
    }
}