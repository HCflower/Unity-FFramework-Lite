// =============================================================
// 描述：命令扩展方法 - 提供 SendCommand 便捷调用
// 作者：HCFlower
// 创建时间：2026-05-13
// 版本：1.0.0
// =============================================================

namespace FFramework.Core
{
    /// <summary>
    /// ICommandSender 的扩展方法。
    /// 为所有实现了 ICommandSender 的类型提供 SendCommand 调用入口。
    /// </summary>
    public static class CommandExtensions
    {
        #region 直接创建（适用于低频操作）

        /// <summary>
        /// 发送一个命令（自动创建实例并执行）。
        /// 适用于按钮点击等低频操作。
        /// </summary>
        public static void SendCommand<T>(this ICommandSender self) where T : ICommand, new()
        {
            var command = new T();
            command.Execute();
        }

        /// <summary>
        /// 发送一个已构建好的命令实例。
        /// 适用于需要传参的命令。
        /// </summary>
        public static void SendCommand(this ICommandSender self, ICommand command)
        {
            command?.Execute();
        }

        /// <summary>
        /// 发送一个带返回值的命令。
        /// </summary>
        public static TResult SendCommand<TResult>(this ICommandSender self, ICommand<TResult> command)
        {
            if (command == null) return default;
            return command.Execute();
        }

        #endregion

        #region 对象池版本（适用于高频操作）

        /// <summary>
        /// 从对象池中获取命令实例并通过回调设置参数，然后执行。
        /// 执行完毕后自动回池。
        /// 适用于每帧执行的高频操作场景。
        /// </summary>
        /// <typeparam name="T">命令类型（必须有无参构造函数）</typeparam>
        /// <param name="self">命令发送者</param>
        /// <param name="onSetup">设置命令参数的回调</param>
        public static void SendCommand<T>(this ICommandSender self, System.Action<T> onSetup)
            where T : class, ICommand, new()
        {
            var command = CommandPool<T>.Get();
            try
            {
                onSetup?.Invoke(command);
                command.Execute();
            }
            finally
            {
                CommandPool<T>.Release(command);
            }
        }

        /// <summary>
        /// 从对象池中获取带返回值的命令实例并通过回调设置参数，然后执行并返回结果。
        /// 执行完毕后自动回池。
        /// </summary>
        /// <typeparam name="T">命令类型</typeparam>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="self">命令发送者</param>
        /// <param name="onSetup">设置命令参数的回调</param>
        public static TResult SendCommand<T, TResult>(this ICommandSender self, System.Action<T> onSetup)
            where T : class, ICommand<TResult>, new()
        {
            var command = CommandPool<T>.Get();
            try
            {
                onSetup?.Invoke(command);
                return command.Execute();
            }
            finally
            {
                CommandPool<T>.Release(command);
            }
        }

        /// <summary>
        /// 从对象池中获取命令实例并执行（无需设置参数）。
        /// 执行完毕后自动回池。
        /// </summary>
        public static void SendCommandFromPool<T>(this ICommandSender self)
            where T : class, ICommand, new()
        {
            var command = CommandPool<T>.Get();
            try
            {
                command.Execute();
            }
            finally
            {
                CommandPool<T>.Release(command);
            }
        }

        #endregion
    }
}
