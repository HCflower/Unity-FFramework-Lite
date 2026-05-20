// =============================================================
// 描述：数据绑定属性类
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.1.0
// 修改：v1.1.0 - 修复锁粒度问题：值变更回调移出锁外执行防止死锁；
//                  使用双锁策略（valueLock + eventLock）保护委托操作；
//                  Register/UnRegister 增加线程安全保护
// =============================================================
using System.Collections.Generic;
using UnityEngine;
using System;

namespace FFramework.Core
{
    // 事件注销接口
    public interface IUnRegister
    {
        void UnRegister();
    }

    /// <summary>
    /// 绑定属性 
    /// </summary>
    [Serializable]
    public class BindableProperty<T>
    {
        [SerializeField]
        private T value = default(T);
        // 用于 value 读写的锁（仅保护赋值操作，不保护回调执行，防止死锁）
        private readonly object valueLock = new object();
        // 用于 Register/UnRegister 的锁（保护委托的增删操作）
        private readonly object eventLock = new object();

        // 属性值变化事件
        private event Action<T> ValueChanged;

        public T Value
        {
            get
            {
                lock (valueLock) { return value; }
            }
            set
            {
                Action<T> changedHandler;
                bool hasChanged;

                lock (valueLock)
                {
                    hasChanged = !EqualityComparer<T>.Default.Equals(value, this.value);
                    if (hasChanged)
                    {
                        this.value = value;
                    }
                    // 在锁内读取委托引用，锁外安全调用
                    changedHandler = ValueChanged;
                }

                // ★ 锁外调用回调，防止回调中嵌套设置值导致死锁
                if (hasChanged)
                {
                    changedHandler?.Invoke(value);
                }
            }
        }

        public BindableProperty(T defaultValue = default(T))
        {
            value = defaultValue;
        }

        /// <summary>
        /// 注册事件
        /// onValueChange -> 值变化事件
        /// isInit -> 是否初始化调用
        /// gameObject -> 绑定的GameObject，销毁时自动注销
        /// </summary>
        public IUnRegister Register(Action<T> onValueChange, bool isInit = true, GameObject gameObject = null)
        {
            lock (eventLock)
            {
                ValueChanged += onValueChange;
            }

            // 手动调用一次
            if (isInit) onValueChange?.Invoke(value);

            // 创建注销器
            var unRegister = new BindablePropertyUnRegister<T>(this, onValueChange);

            // 自动绑定GameObject销毁事件
            if (gameObject != null)
            {
                unRegister.UnRegisterWhenGameObjectDestroy(gameObject);
            }

            return unRegister;
        }

        /// <summary>
        /// 注册事件（MonoBehaviour重载）
        /// 自动绑定到MonoBehaviour的GameObject
        /// </summary>
        public IUnRegister Register(Action<T> onValueChange, MonoBehaviour monoBehaviour, bool isInit = true)
        {
            return Register(onValueChange, isInit, monoBehaviour?.gameObject);
        }

        /// <summary>
        /// 绑定属性注销结构体
        /// </summary>
        public struct BindablePropertyUnRegister<U> : IUnRegister
        {
            private BindableProperty<U> bindableProperty;
            private Action<U> onValueChange;

            public BindablePropertyUnRegister(BindableProperty<U> bindableProperty, Action<U> onValueChange)
            {
                this.bindableProperty = bindableProperty;
                this.onValueChange = onValueChange;
            }

            public void UnRegister()
            {
                bindableProperty.UnRegister(onValueChange);
            }
        }

        /// <summary>
        /// 注销事件
        /// </summary>
        public void UnRegister(Action<T> onValueChange)
        {
            if (onValueChange != null)
            {
                lock (eventLock)
                {
                    ValueChanged -= onValueChange;
                }
            }
        }

        /// <summary>
        /// 注销所有值修改事件
        /// </summary>
        public void UnregisterAll()
        {
            lock (eventLock)
            {
                ValueChanged = null;
            }
        }

        //字符串转换
        public override string ToString()
        {
            return Value?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// 绑定属性扩展方法
    /// </summary>
    public static class BindablePropertyExtensions
    {
        /// <summary>
        /// 当GameObject销毁时自动注销事件
        /// </summary>
        public static void UnRegisterWhenGameObjectDestroy(this IUnRegister unRegister, GameObject gameObject)
        {
            if (gameObject == null) return;

            // 添加自动注销组件
            var autoUnregister = gameObject.GetComponent<BindablePropertyAutoUnregister>();
            if (autoUnregister == null)
            {
                autoUnregister = gameObject.AddComponent<BindablePropertyAutoUnregister>();
            }

            autoUnregister.AddUnRegister(unRegister);
        }
    }

    // 自动注销组件
    public class BindablePropertyAutoUnregister : MonoBehaviour
    {
        private HashSet<IUnRegister> unRegisters = new HashSet<IUnRegister>();

        /// <summary>
        /// 添加注销器
        /// </summary>
        public void AddUnRegister(IUnRegister unRegister)
        {
            unRegisters.Add(unRegister);
        }

        private void OnDestroy()
        {
            foreach (var unRegister in unRegisters)
            {
                unRegister.UnRegister();
            }
            unRegisters.Clear();
        }
    }
}