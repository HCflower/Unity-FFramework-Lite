// =============================================================
// 描述：Model基础类
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：3.2.0
// 修改：v3.2.0 - Dispose() 中添加 eventSystem?.UnregisterTarget(this) 自动注销事件
// =============================================================
using System;

namespace FFramework.Core
{
    public abstract class ArchitectureModel : IModel
    {
        /// <summary>
        /// 事件系统，由 Architecture 在注册时自动注入。
        /// 子类可通过 [Inject] 声明其他依赖。
        /// </summary>
        [Inject]
        protected EventSystem eventSystem;

        public virtual void Initialize()
        {
            OnInitialize();
        }

        public virtual void Dispose()
        {
            // 自动注销所有通过 RegisterEvent 注册的事件监听
            eventSystem?.UnregisterTarget(this);
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

        #region 数据持久化

        /// <summary>
        /// 保存模型数据到指定存档
        /// </summary>
        /// <param name="slotName">存档文件夹名称，由调用方自定义</param>
        public virtual void SaveData(string slotName)
        {
            ArchitectureDataPersistence.SaveData(this, slotName);
        }

        /// <summary>
        /// 从指定存档加载模型数据
        /// </summary>
        /// <param name="slotName">存档文件夹名称，与保存时传入的名称一致</param>
        public virtual void LoadData(string slotName)
        {
            ArchitectureDataPersistence.LoadData(this, slotName);
        }

        #endregion
    }
}
