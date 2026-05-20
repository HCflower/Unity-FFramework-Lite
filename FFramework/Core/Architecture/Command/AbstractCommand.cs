// =============================================================
// 描述：命令基类 - 提供 GetModel / SendEvent 等便捷访问方法
// 作者：HCFlower
// 创建时间：2026-05-13
// 版本：1.0.0
// =============================================================
using UnityEngine;

namespace FFramework.Core
{
    /// <summary>
    /// 命令基类。
    /// 继承此类并实现 OnExecute() 来编写业务逻辑。
    /// 在 OnExecute() 中可通过 GetModel() 获取 Model、通过 SendEvent() 发送事件。
    /// </summary>
    public abstract class AbstractCommand : ICommand, ICommandSender
    {
        void ICommand.Execute()
        {
            try
            {
                OnExecute();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Command [{GetType().Name}] 执行失败: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 子类在此编写命令的业务逻辑。
        /// </summary>
        protected abstract void OnExecute();

        #region 便捷访问方法

        /// <summary>
        /// 获取已注册的 Model。
        /// 等价于 Architecture.Instance.GetModel{T}()。
        /// </summary>
        protected T GetModel<T>() where T : class, IModel, new()
        {
            return Architecture.Instance.GetModel<T>();
        }

        /// <summary>
        /// 通过 EventSystem 发送无参事件。
        /// </summary>
        protected void SendEvent(string eventName)
        {
            EventSystem.Instance?.Trigger(eventName);
        }

        /// <summary>
        /// 通过 EventSystem 发送带参事件。
        /// </summary>
        protected void SendEvent<TData>(string eventName, TData data)
        {
            EventSystem.Instance?.Trigger(eventName, data);
        }

        #endregion
    }

    /// <summary>
    /// 带返回值的命令基类。
    /// 继承此类并实现 OnExecute() 来编写业务逻辑并返回结果。
    /// ICommand{TResult} 继承自 ICommand，因此本类通过显式接口实现同时满足两个接口：
    ///   - ICommand.Execute() → 调用 Execute() → 丢弃返回值（void）
    ///   - ICommand{TResult}.Execute() → 调用 Execute() → 返回 TResult
    /// </summary>
    public abstract class AbstractCommand<TResult> : ICommand<TResult>, ICommandSender
    {
        /// <summary>
        /// ICommand.Execute() 的显式实现。
        /// 通过 ICommand 引用调用时（如对象池中的 ICommand 约束），自动转发到带返回值版本。
        /// </summary>
        void ICommand.Execute()
        {
            Execute();
        }

        /// <summary>
        /// ICommand{TResult}.Execute() 的显式实现。
        /// </summary>
        TResult ICommand<TResult>.Execute()
        {
            return Execute();
        }

        /// <summary>
        /// 内部执行入口，统一处理异常和日志。
        /// </summary>
        private TResult Execute()
        {
            try
            {
                return OnExecute();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Command<{typeof(TResult).Name}> [{GetType().Name}] 执行失败: {e.Message}\n{e.StackTrace}");
                return default;
            }
        }

        /// <summary>
        /// 子类在此编写命令的业务逻辑并返回结果。
        /// </summary>
        protected abstract TResult OnExecute();

        #region 便捷访问方法

        /// <summary>
        /// 获取已注册的 Model。
        /// 等价于 Architecture.Instance.GetModel{T}()。
        /// </summary>
        protected T GetModel<T>() where T : class, IModel, new()
        {
            return Architecture.Instance.GetModel<T>();
        }

        /// <summary>
        /// 通过 EventSystem 发送无参事件。
        /// </summary>
        protected void SendEvent(string eventName)
        {
            EventSystem.Instance?.Trigger(eventName);
        }

        /// <summary>
        /// 通过 EventSystem 发送带参事件。
        /// </summary>
        protected void SendEvent<TData>(string eventName, TData data)
        {
            EventSystem.Instance?.Trigger(eventName, data);
        }

        #endregion
    }
}
