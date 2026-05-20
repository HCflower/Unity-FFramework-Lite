// =============================================================
// 描述：EventSystem 静态扩展 - 支持泛型事件的自动注销
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：2.0.0
// =============================================================
using System.Collections.Generic;
using UnityEngine;
using System;

namespace FFramework.Core
{
    public static class EventSystemExtensions
    {
        /// <summary>
        /// 注册事件并在GameObject销毁时自动注销（无参数版本）
        /// </summary>
        /// <param name="eventSystem">事件系统实例</param>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">事件回调</param>
        /// <param name="gameObject">关联的GameObject</param>
        /// <returns>返回AutoEventUnregister组件以支持链式调用</returns>
        public static AutoEventUnregister RegisterEvent(this EventSystem eventSystem, string eventName, Action callback, GameObject gameObject)
        {
            if (!ValidateParameters(eventSystem, gameObject)) return null;

            // 修复1：使用EventSystem的正确方法名Register
            eventSystem.Register(eventName, callback);

            // 获取或添加自动注销组件
            AutoEventUnregister autoUnregister = GetOrAddAutoUnregister(gameObject);

            // 添加事件信息到组件
            autoUnregister.AddEventInfo(eventSystem, eventName, callback);

            return autoUnregister;
        }

        /// <summary>
        /// 注册object参数事件并在GameObject销毁时自动注销（向后兼容）
        /// </summary>
        /// <param name="eventSystem">事件系统实例</param>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">事件回调</param>
        /// <param name="gameObject">关联的GameObject</param>
        /// <returns>返回AutoEventUnregister组件以支持链式调用</returns>
        public static AutoEventUnregister RegisterEvent(this EventSystem eventSystem, string eventName, Action<object> callback, GameObject gameObject)
        {
            if (!ValidateParameters(eventSystem, gameObject)) return null;

            // 修复2：使用EventSystem的正确方法名Register
            eventSystem.Register(eventName, callback);

            // 获取或添加自动注销组件
            AutoEventUnregister autoUnregister = GetOrAddAutoUnregister(gameObject);

            // 添加事件信息到组件
            autoUnregister.AddEventInfo(eventSystem, eventName, callback);

            return autoUnregister;
        }

        /// <summary>
        /// 注册强类型事件并在GameObject销毁时自动注销
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="eventSystem">事件系统实例</param>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">事件回调</param>
        /// <param name="gameObject">关联的GameObject</param>
        /// <returns>返回AutoEventUnregister组件以支持链式调用</returns>
        public static AutoEventUnregister RegisterEvent<T>(this EventSystem eventSystem, string eventName, Action<T> callback, GameObject gameObject)
        {
            if (!ValidateParameters(eventSystem, gameObject)) return null;

            // 修复3：使用EventSystem的正确方法名Register
            eventSystem.Register<T>(eventName, callback);

            // 获取或添加自动注销组件
            AutoEventUnregister autoUnregister = GetOrAddAutoUnregister(gameObject);

            // 添加事件信息到组件
            autoUnregister.AddEventInfo<T>(eventSystem, eventName, callback);

            return autoUnregister;
        }

        /// <summary>
        /// 注册一次性事件并在GameObject销毁时自动注销（无参数版本）
        /// </summary>
        /// <param name="eventSystem">事件系统实例</param>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">事件回调</param>
        /// <param name="gameObject">关联的GameObject</param>
        /// <returns>返回AutoEventUnregister组件以支持链式调用</returns>
        public static AutoEventUnregister RegisterOnceEvent(this EventSystem eventSystem, string eventName, Action callback, GameObject gameObject)
        {
            if (!ValidateParameters(eventSystem, gameObject)) return null;

            // 创建一次性回调包装
            Action onceCallback = null;
            AutoEventUnregister autoUnregister = GetOrAddAutoUnregister(gameObject);

            onceCallback = () =>
            {
                callback?.Invoke();
                // 从EventSystem中注销事件
                eventSystem.Unregister(eventName, onceCallback);
                // 从自动注销组件中移除
                autoUnregister.UnregisterEvent(eventName, onceCallback);
            };

            // 修复4：使用EventSystem的正确方法名Register
            eventSystem.Register(eventName, onceCallback);

            // 添加到自动注销组件（防止GameObject提前销毁）
            autoUnregister.AddEventInfo(eventSystem, eventName, onceCallback);

            return autoUnregister;
        }

        /// <summary>
        /// 注册一次性泛型事件并在GameObject销毁时自动注销
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="eventSystem">事件系统实例</param>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">事件回调</param>
        /// <param name="gameObject">关联的GameObject</param>
        /// <returns>返回AutoEventUnregister组件以支持链式调用</returns>
        public static AutoEventUnregister RegisterOnceEvent<T>(this EventSystem eventSystem, string eventName, Action<T> callback, GameObject gameObject)
        {
            if (!ValidateParameters(eventSystem, gameObject)) return null;

            // 创建一次性回调包装
            Action<T> onceCallback = null;
            AutoEventUnregister autoUnregister = GetOrAddAutoUnregister(gameObject);

            onceCallback = (param) =>
            {
                callback?.Invoke(param);
                // 从EventSystem中注销事件
                eventSystem.Unregister<T>(eventName, onceCallback);
                // 从自动注销组件中移除
                autoUnregister.UnregisterEvent<T>(eventName, onceCallback);
            };

            // 修复5：使用EventSystem的正确方法名Register
            eventSystem.Register<T>(eventName, onceCallback);

            // 添加到自动注销组件（防止GameObject提前销毁）
            autoUnregister.AddEventInfo<T>(eventSystem, eventName, onceCallback);

            return autoUnregister;
        }

        // ...existing code... (ValidateParameters 和 GetOrAddAutoUnregister 方法保持不变)

        private static bool ValidateParameters(EventSystem eventSystem, GameObject gameObject)
        {
            if (eventSystem == null)
            {
                Debug.LogError("EventSystemExtensions: EventSystem实例不能为空");
                return false;
            }

            if (gameObject == null)
            {
                Debug.LogError("EventSystemExtensions: GameObject不能为空");
                return false;
            }

            return true;
        }

        private static AutoEventUnregister GetOrAddAutoUnregister(GameObject gameObject)
        {
            AutoEventUnregister autoUnregister = gameObject.GetComponent<AutoEventUnregister>();
            if (autoUnregister == null)
            {
                autoUnregister = gameObject.AddComponent<AutoEventUnregister>();
            }
            return autoUnregister;
        }
    }

    /// <summary>
    /// 自动事件注销组件 - 用于在GameObject销毁时自动注销事件
    /// </summary>
    public class AutoEventUnregister : MonoBehaviour
    {
        // ...existing code... (内部类保持不变)

        /// <summary>
        /// 事件信息基类
        /// </summary>
        [System.Serializable]
        private abstract class EventInfoBase
        {
            public EventSystem eventSystem;
            public string eventName;

            protected EventInfoBase(EventSystem eventSystem, string eventName)
            {
                this.eventSystem = eventSystem;
                this.eventName = eventName;
            }

            public abstract void Unregister();
            public abstract bool Equals(EventSystem system, string name, object callback);
            public abstract string GetCallbackTypeName();
        }

        /// <summary>
        /// 无参数事件信息
        /// </summary>
        [System.Serializable]
        private class ActionEventInfo : EventInfoBase
        {
            public Action callback;

            public ActionEventInfo(EventSystem eventSystem, string eventName, Action callback)
                : base(eventSystem, eventName)
            {
                this.callback = callback;
            }

            public override void Unregister()
            {
                if (eventSystem != null && callback != null)
                {
                    // 修复6：使用EventSystem的正确方法名Unregister
                    eventSystem.Unregister(eventName, callback);
                }
            }

            public override bool Equals(EventSystem system, string name, object callback)
            {
                return eventSystem == system &&
                       eventName == name &&
                       this.callback != null &&
                       this.callback.Equals(callback);
            }

            public override string GetCallbackTypeName()
            {
                return "Action";
            }
        }

        /// <summary>
        /// object参数事件信息
        /// </summary>
        [System.Serializable]
        private class ActionObjectEventInfo : EventInfoBase
        {
            public Action<object> callback;

            public ActionObjectEventInfo(EventSystem eventSystem, string eventName, Action<object> callback)
                : base(eventSystem, eventName)
            {
                this.callback = callback;
            }

            public override void Unregister()
            {
                if (eventSystem != null && callback != null)
                {
                    eventSystem.Unregister<object>(eventName, callback);
                }
            }

            public override bool Equals(EventSystem system, string name, object callback)
            {
                return eventSystem == system &&
                       eventName == name &&
                       this.callback != null &&
                       this.callback.Equals(callback);
            }

            public override string GetCallbackTypeName()
            {
                return "Action<object>";
            }
        }

        /// <summary>
        /// 泛型事件信息
        /// （不标记 [Serializable]，泛型类型 Unity 序列化系统不支持）
        /// </summary>
        private class GenericEventInfo<T> : EventInfoBase
        {
            public Action<T> callback;

            public GenericEventInfo(EventSystem eventSystem, string eventName, Action<T> callback)
                : base(eventSystem, eventName)
            {
                this.callback = callback;
            }

            public override void Unregister()
            {
                if (eventSystem != null && callback != null)
                {
                    // 修复8：使用EventSystem的正确方法名Unregister
                    eventSystem.Unregister<T>(eventName, callback);
                }
            }

            public override bool Equals(EventSystem system, string name, object callback)
            {
                return eventSystem == system &&
                       eventName == name &&
                       this.callback != null &&
                       this.callback.Equals(callback);
            }

            public override string GetCallbackTypeName()
            {
                return $"Action<{typeof(T).Name}>";
            }
        }

        // 存储需要注销的事件信息
        private List<EventInfoBase> eventInfos = new List<EventInfoBase>();

        // ...existing code... (其他方法保持不变，但需要使用正确的EventSystem方法名)

        public void AddEventInfo(EventSystem eventSystem, string eventName, Action callback)
        {
            if (!ValidateEventInfo(eventSystem, eventName, callback)) return;

            if (HasEventInfo(eventSystem, eventName, callback)) return;

            eventInfos.Add(new ActionEventInfo(eventSystem, eventName, callback));
            Debug.Log($"AutoEventUnregister: 添加自动注销事件 {eventName} (Action) on {gameObject.name}");
        }

        public void AddEventInfo(EventSystem eventSystem, string eventName, Action<object> callback)
        {
            if (!ValidateEventInfo(eventSystem, eventName, callback)) return;

            if (HasEventInfo(eventSystem, eventName, callback)) return;

            eventInfos.Add(new ActionObjectEventInfo(eventSystem, eventName, callback));
            Debug.Log($"AutoEventUnregister: 添加自动注销事件 {eventName} (Action<object>) on {gameObject.name}");
        }

        public void AddEventInfo<T>(EventSystem eventSystem, string eventName, Action<T> callback)
        {
            if (!ValidateEventInfo(eventSystem, eventName, callback)) return;

            if (HasEventInfo(eventSystem, eventName, callback)) return;

            eventInfos.Add(new GenericEventInfo<T>(eventSystem, eventName, callback));
            Debug.Log($"AutoEventUnregister: 添加自动注销事件 {eventName} (Action<{typeof(T).Name}>) on {gameObject.name}");
        }

        private bool ValidateEventInfo(EventSystem eventSystem, string eventName, object callback)
        {
            if (eventSystem == null)
            {
                Debug.LogError("AutoEventUnregister: 事件系统不能为空");
                return false;
            }

            if (string.IsNullOrEmpty(eventName))
            {
                Debug.LogError("AutoEventUnregister: 事件名称不能为空");
                return false;
            }

            if (callback == null)
            {
                Debug.LogError("AutoEventUnregister: 回调函数不能为空");
                return false;
            }

            return true;
        }

        private bool HasEventInfo(EventSystem eventSystem, string eventName, object callback)
        {
            foreach (var info in eventInfos)
            {
                if (info.Equals(eventSystem, eventName, callback))
                {
                    Debug.LogWarning($"AutoEventUnregister: 事件 {eventName} 已经添加过了");
                    return true;
                }
            }
            return false;
        }

        public void UnregisterAllEvents()
        {
            int count = eventInfos.Count;
            foreach (var info in eventInfos)
            {
                try
                {
                    info.Unregister();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"AutoEventUnregister: 注销事件 {info.eventName} 时发生异常: {ex.Message}");
                }
            }
            eventInfos.Clear();

            if (count > 0)
            {
                Debug.Log($"AutoEventUnregister: 已注销 {count} 个事件");
            }
        }

        public void UnregisterEvent(string eventName, Action callback)
        {
            RemoveEventInfo(eventName, callback);
        }

        public void UnregisterEvent(string eventName, Action<object> callback)
        {
            RemoveEventInfo(eventName, callback);
        }

        public void UnregisterEvent<T>(string eventName, Action<T> callback)
        {
            RemoveEventInfo(eventName, callback);
        }

        private void RemoveEventInfo(string eventName, object callback)
        {
            for (int i = eventInfos.Count - 1; i >= 0; i--)
            {
                var info = eventInfos[i];
                if (info.eventName == eventName && info.Equals(info.eventSystem, eventName, callback))
                {
                    try
                    {
                        info.Unregister();
                        eventInfos.RemoveAt(i);
                        Debug.Log($"AutoEventUnregister: 注销指定事件 {eventName} ({info.GetCallbackTypeName()})");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"AutoEventUnregister: 注销事件 {eventName} 时发生异常: {ex.Message}");
                    }
                    break;
                }
            }
        }

        public int GetEventCount() => eventInfos.Count;

        public string[] GetEventNames()
        {
            List<string> names = new List<string>();
            foreach (var info in eventInfos)
            {
                if (!names.Contains(info.eventName))
                {
                    names.Add(info.eventName);
                }
            }
            return names.ToArray();
        }

        public bool HasEvent(string eventName)
        {
            foreach (var info in eventInfos)
            {
                if (info.eventName == eventName)
                {
                    return true;
                }
            }
            return false;
        }

        public void DebugPrintEvents()
        {
            Debug.Log($"<color=yellow>=== AutoEventUnregister on {gameObject.name} 管理的事件 (共{eventInfos.Count}个) ===</color>");
            foreach (var info in eventInfos)
            {
                Debug.Log($"事件: {info.eventName}, 类型: {info.GetCallbackTypeName()}");
            }
            Debug.Log($"<color=yellow>=== AutoEventUnregister 事件信息结束 ===</color>");
        }

        private void OnDestroy()
        {
            if (eventInfos.Count > 0)
            {
                Debug.Log($"<color=yellow>AutoEventUnregister: GameObject {gameObject.name} 销毁,自动注销 {eventInfos.Count} 个事件</color>");
                UnregisterAllEvents();
            }
        }
    }

    /// <summary>
    /// 进一步扩展 - 支持链式调用
    /// </summary>
    public static class AutoEventUnregisterExtensions
    {
        public static AutoEventUnregister UnRegisterEventInGameObjectDestroy(this AutoEventUnregister autoUnregister)
        {
            return autoUnregister;
        }

        public static AutoEventUnregister WithDebug(this AutoEventUnregister autoUnregister, bool enableDebug = true)
        {
            if (enableDebug && autoUnregister != null)
            {
                autoUnregister.DebugPrintEvents();
            }
            return autoUnregister;
        }

        public static AutoEventUnregister AndRegisterEvent(this AutoEventUnregister autoUnregister, string eventName, Action callback)
        {
            if (autoUnregister != null)
            {
                // 修复9：使用扩展方法而不是直接调用EventSystem.Instance
                EventSystem.Instance?.RegisterEvent(eventName, callback, autoUnregister.gameObject);
            }
            return autoUnregister;
        }

        public static AutoEventUnregister AndRegisterEvent<T>(this AutoEventUnregister autoUnregister, string eventName, Action<T> callback)
        {
            if (autoUnregister != null)
            {
                // 修复10：使用扩展方法而不是直接调用EventSystem.Instance
                EventSystem.Instance?.RegisterEvent<T>(eventName, callback, autoUnregister.gameObject);
            }
            return autoUnregister;
        }
    }
}  