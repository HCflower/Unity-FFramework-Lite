// =============================================================
// 描述：事件系统（优化版）
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：2.1.0
// 修改：v2.1.0 - 将 TriggerRecord 的堆栈追踪逻辑包裹到 UNITY_EDITOR 条件编译中，
//                  避免生产环境的性能开销；静态 API 增加 Instance 空值检查与错误日志
// =============================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

namespace FFramework.Core
{
    public class EventSystem : SingletonMono<EventSystem>
    {
        #region 内部类

        /// <summary>
        /// 监听器信息
        /// </summary>
        public class Listener
        {
            public object Target { get; private set; }
            public Delegate Callback { get; private set; }
            public bool IsActive { get; set; } = true;

            public Listener(Delegate callback)
            {
                Callback = callback ?? throw new ArgumentNullException(nameof(callback));
                Target = callback.Target;
            }

            public bool IsValid()
            {
                return IsActive && Target switch
                {
                    null => true, // 静态方法
                    UnityEngine.Object unityObj => unityObj != null,
                    _ => true
                };
            }
        }

        /// <summary>
        /// 事件容器基类
        /// </summary>
        public abstract class EventBase
        {
            public readonly List<Listener> listeners = new List<Listener>(); // 修复：改为public

            public int Count => listeners.Count;
            public bool IsEmpty => listeners.Count == 0;

            public void Clear() => listeners.Clear();

            public void CleanupInvalid()
            {
                for (int i = listeners.Count - 1; i >= 0; i--)
                {
                    if (!listeners[i].IsValid())
                    {
                        listeners.RemoveAt(i);
                    }
                }
            }

            public bool Remove(Delegate callback)
            {
                for (int i = listeners.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(listeners[i].Callback, callback))
                    {
                        listeners.RemoveAt(i);
                        return true;
                    }
                }
                return false;
            }

            public abstract void Invoke(object parameter = null);
        }

        /// <summary>
        /// 无参事件容器
        /// </summary>
        public class Event : EventBase
        {
            public void Add(Action callback) => listeners.Add(new Listener(callback));

            public override void Invoke(object parameter = null)
            {
                CleanupInvalid();

                for (int i = 0; i < listeners.Count; i++)
                {
                    var listener = listeners[i];
                    if (!listener.IsValid())
                        continue;

                    try
                    {
                        (listener.Callback as Action)?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"EventSystem: 执行无参回调异常: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 有参事件容器
        /// </summary>
        public class Event<T> : EventBase
        {
            public void Add(Action<T> callback) => listeners.Add(new Listener(callback));

            public override void Invoke(object parameter = null)
            {
                CleanupInvalid();

                for (int i = 0; i < listeners.Count; i++)
                {
                    var listener = listeners[i];
                    if (!listener.IsValid())
                        continue;

                    if (listener.Callback is not Action<T> callback)
                        continue;

                    try
                    {
                        T typedParam = ConvertParameter<T>(parameter);
                        callback.Invoke(typedParam);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"EventSystem: 执行回调异常({typeof(T).Name}): {ex.Message}");
                    }
                }
            }

            private static TParam ConvertParameter<TParam>(object param)
            {
                if (param == null)
                {
                    // 检查T是否可以为null（引用类型或可空值类型）
                    if (!typeof(TParam).IsValueType || Nullable.GetUnderlyingType(typeof(TParam)) != null)
                        return default(TParam);

                    throw new ArgumentException($"无法将 null 转换为值类型 {typeof(TParam).Name}");
                }

                // 直接类型匹配
                if (param is TParam directCast)
                    return directCast;

                // 处理可空类型
                var underlyingType = Nullable.GetUnderlyingType(typeof(TParam));
                if (underlyingType != null)
                {
                    if (param.GetType() == underlyingType)
                    {
                        return (TParam)param;
                    }
                }

                // 尝试基础类型转换
                try
                {
                    var targetType = underlyingType ?? typeof(TParam);

                    // 特殊处理枚举类型
                    if (targetType.IsEnum)
                    {
                        if (param is string enumStr)
                        {
                            return (TParam)Enum.Parse(targetType, enumStr, true);
                        }
                        else if (param.GetType().IsPrimitive)
                        {
                            return (TParam)Enum.ToObject(targetType, param);
                        }
                    }

                    // 使用Convert.ChangeType进行基础类型转换
                    var convertedValue = Convert.ChangeType(param, targetType);
                    return (TParam)convertedValue;
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"无法将 {param.GetType().Name} 转换为 {typeof(TParam).Name}: {ex.Message}");
                }
            }
        }

        #endregion

        #region 字段

        private readonly Dictionary<object, EventBase> events = new Dictionary<object, EventBase>();
        #endregion

        #region 触发记录相关（仅在编辑器模式下可用，避免生产环境性能开销）
        [System.Serializable]
        public class TriggerRecord
        {
            public string EventName { get; set; }
            public System.DateTime TriggerTime { get; set; }
            public string ParameterType { get; set; }
            public string ParameterValue { get; set; }
            public int ListenerCount { get; set; }

#if UNITY_EDITOR
            // 触发位置信息（仅在编辑器中捕获，生产环境跳过以节省性能）
            public string TriggerLocation { get; set; }
            public string StackTrace { get; set; }
            public string CallerClass { get; set; }
            public string CallerMethod { get; set; }
            public string CallerFilePath { get; set; }
            public int CallerLineNumber { get; set; }
#endif

            public TriggerRecord(string eventName, object parameter, int listenerCount)
            {
                EventName = eventName;
                TriggerTime = System.DateTime.Now;
                ListenerCount = listenerCount;

                if (parameter != null)
                {
                    ParameterType = parameter.GetType().Name;
                    ParameterValue = parameter.ToString();
                }
                else
                {
                    ParameterType = "无参数";
                    ParameterValue = "";
                }

#if UNITY_EDITOR
                // 获取调用堆栈信息（仅在编辑器模式下执行，避免性能开销）
                CaptureCallerInfo();
#endif
            }

#if UNITY_EDITOR
            private void CaptureCallerInfo()
            {
                var stackTrace = new System.Diagnostics.StackTrace(true);

                // 跳过EventSystem内部调用，找到真正的触发位置
                for (int i = 1; i < stackTrace.FrameCount; i++)
                {
                    var frame = stackTrace.GetFrame(i);
                    var method = frame.GetMethod();

                    // 跳过EventSystem内部方法
                    if (method?.DeclaringType?.Name == "EventSystem" ||
                        method?.DeclaringType?.FullName?.Contains("FFramework.Core.EventSystem") == true)
                        continue;

                    // 找到真正的调用者
                    CallerClass = method?.DeclaringType?.Name ?? "Unknown";
                    CallerMethod = method?.Name ?? "Unknown";
                    CallerFilePath = frame.GetFileName() ?? "Unknown";
                    CallerLineNumber = frame.GetFileLineNumber();

                    // 简化的位置信息
                    if (!string.IsNullOrEmpty(CallerFilePath))
                    {
                        var fileName = System.IO.Path.GetFileName(CallerFilePath);
                        TriggerLocation = $"{CallerClass}.{CallerMethod}() at {fileName}:{CallerLineNumber}";
                    }
                    else
                    {
                        TriggerLocation = $"{CallerClass}.{CallerMethod}()";
                    }

                    // 完整堆栈跟踪（调试用）
                    StackTrace = stackTrace.ToString();
                    break;
                }

                if (string.IsNullOrEmpty(TriggerLocation))
                {
                    TriggerLocation = "Unknown Location";
                    StackTrace = stackTrace.ToString();
                }
            }
#endif

            public string GetDetailedInfo()
            {
                string info = $"事件: {EventName}\n" +
                       $"时间: {TriggerTime:yyyy-MM-dd HH:mm:ss.fff}\n" +
#if UNITY_EDITOR
                       $"触发位置: {TriggerLocation}\n" +
                       $"调用类: {CallerClass}\n" +
                       $"调用方法: {CallerMethod}\n" +
                       $"文件路径: {CallerFilePath}\n" +
                       $"行号: {CallerLineNumber}\n" +
#endif
                       $"监听者数量: {ListenerCount}\n" +
                       $"参数类型: {ParameterType}\n" +
                       $"参数值: {ParameterValue}";
                return info;
            }
        }

        private readonly List<TriggerRecord> triggerHistory = new List<TriggerRecord>();
        private int maxTriggerHistoryCount = 100; // 最大记录数量

        public List<TriggerRecord> GetTriggerHistory() => triggerHistory.ToList();
        public void ClearTriggerHistory() => triggerHistory.Clear();

        private void RecordTrigger(string eventName, object parameter = null)
        {
#if UNITY_EDITOR
            int listenerCount = GetListenerCount(eventName);
            var record = new TriggerRecord(eventName, parameter, listenerCount);

            triggerHistory.Insert(0, record); // 插入到开头，最新的在前面

            // 保持记录数量在限制内
            if (triggerHistory.Count > maxTriggerHistoryCount)
            {
                triggerHistory.RemoveAt(triggerHistory.Count - 1);
            }
#else
            // 生产环境不记录触发历史，避免性能开销
#endif
        }

        #endregion

        #region 注册方法

        public void Register(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            GetOrCreateEvent<Event>(eventName)?.Add(callback);
        }

        public void Register<TKey>(TKey eventKey, Action callback) where TKey : struct
        {
            if (callback == null) return;
            GetOrCreateEvent<Event>(eventKey)?.Add(callback);
        }

        public void Register<T>(string eventName, Action<T> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            GetOrCreateEvent<Event<T>>(eventName)?.Add(callback);
        }

        public void Register<TKey, T>(TKey eventKey, Action<T> callback) where TKey : struct
        {
            if (callback == null) return;
            GetOrCreateEvent<Event<T>>(eventKey)?.Add(callback);
        }

        #endregion

        #region 注销方法

        public void Unregister(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            if (events.TryGetValue(eventName, out var eventBase))
            {
                eventBase.Remove(callback);
            }
        }

        public void Unregister<TKey>(TKey eventKey, Action callback) where TKey : struct
        {
            if (callback == null) return;
            if (events.TryGetValue(eventKey, out var eventBase))
            {
                eventBase.Remove(callback);
            }
        }

        public void Unregister<T>(string eventName, Action<T> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            if (events.TryGetValue(eventName, out var eventBase))
            {
                eventBase.Remove(callback);
            }
        }

        public void Unregister<TKey, T>(TKey eventKey, Action<T> callback) where TKey : struct
        {
            if (callback == null) return;
            if (events.TryGetValue(eventKey, out var eventBase))
            {
                eventBase.Remove(callback);
            }
        }

        #endregion

        #region 触发方法

        public void Trigger(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            RecordTrigger(eventName);
            if (events.TryGetValue(eventName, out var eventBase))
            {
                eventBase.Invoke();
            }
        }

        public void Trigger<TKey>(TKey eventKey) where TKey : struct
        {
            RecordTrigger(eventKey.ToString());
            if (events.TryGetValue(eventKey, out var eventBase))
            {
                eventBase.Invoke();
            }
        }

        public void Trigger<T>(string eventName, T parameter)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            RecordTrigger(eventName, parameter);
            if (events.TryGetValue(eventName, out var eventBase))
            {
                eventBase.Invoke(parameter);
            }
        }

        public void Trigger<TKey, T>(TKey eventKey, T parameter) where TKey : struct
        {
            RecordTrigger(eventKey.ToString(), parameter);
            if (events.TryGetValue(eventKey, out var eventBase))
            {
                eventBase.Invoke(parameter);
            }
        }

        #endregion

        #region 查询方法

        public bool HasEvent(string eventName)
        {
            return !string.IsNullOrEmpty(eventName) &&
                   events.TryGetValue(eventName, out var eventBase) &&
                   !eventBase.IsEmpty;
        }

        public bool HasEvent<TKey>(TKey eventKey) where TKey : struct
        {
            return events.TryGetValue(eventKey, out var eventBase) && !eventBase.IsEmpty;
        }

        public int GetListenerCount(string eventName)
        {
            return string.IsNullOrEmpty(eventName) || !events.TryGetValue(eventName, out var eventBase)
                ? 0 : eventBase.Count;
        }

        public int GetListenerCount<TKey>(TKey eventKey) where TKey : struct
        {
            return events.TryGetValue(eventKey, out var eventBase) ? eventBase.Count : 0;
        }

        #endregion

        #region 清理方法

        public void CleanupInvalid()
        {
            foreach (var eventBase in events.Values)
            {
                eventBase.CleanupInvalid();
            }
        }

        public void UnregisterTarget(object target)
        {
            if (target == null) return;

            foreach (var eventBase in events.Values)
            {
                for (int i = eventBase.listeners.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(eventBase.listeners[i].Target, target))
                    {
                        eventBase.listeners.RemoveAt(i);
                    }
                }
            }
        }

        public void Clear(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (events.TryGetValue(eventName, out var eventBase))
            {
                eventBase.Clear();
            }
        }

        public void Clear<TKey>(TKey eventKey) where TKey : struct
        {
            if (events.TryGetValue(eventKey, out var eventBase))
            {
                eventBase.Clear();
            }
        }

        public void ClearAll()
        {
            events.Clear();
        }

        #endregion

        #region 调试方法

        public void DebugPrint()
        {
            Debug.Log("=== EventSystem Overview ===");
            Debug.Log($"Total Events: {events.Count}");
            Debug.Log($"Active Events: {events.Values.Count(e => !e.IsEmpty)}");

            foreach (var kvp in events.Where(kvp => !kvp.Value.IsEmpty))
            {
                var keyStr = kvp.Key is string str ? $"'{str}'" : kvp.Key.ToString();
                Debug.Log($"{keyStr} -> Listeners: {kvp.Value.Count}");
            }
        }

        #endregion

        #region 内部方法

        private T GetOrCreateEvent<T>(object key) where T : EventBase, new()
        {
            if (key == null)
            {
                Debug.LogError("EventSystem: Event key cannot be null");
                return null;
            }

            if (!events.TryGetValue(key, out var eventBase))
            {
                eventBase = new T();
                events[key] = eventBase;
            }

            if (eventBase is T typedEvent)
            {
                return typedEvent;
            }

            var keyStr = key is string str ? $"'{str}'" : key.ToString();
            Debug.LogError($"EventSystem: Event {keyStr} type mismatch - existing: {eventBase.GetType().Name}, requested: {typeof(T).Name}");
            return null;
        }

        #endregion

        #region Unity生命周期

        protected override void InitializeSingleton() { }

        protected override void OnDestroy()
        {
            ClearAll();
            base.OnDestroy();
        }

        #endregion
    }
}