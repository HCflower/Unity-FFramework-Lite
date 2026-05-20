// =============================================================
// 描述：命令对象池 - 减少高频场景下的 GC 分配
// 作者：HCFlower
// 创建时间：2026-05-13
// 版本：1.0.0
// =============================================================
using System.Collections.Generic;

namespace FFramework.Core
{
    /// <summary>
    /// 命令对象池。
    /// 用于高频调用场景（如每帧执行的命令），复用 Command 实例以减少 GC 分配。
    /// 内部使用 Stack{T} 实现，线程不安全（仅在 Unity 主线程使用）。
    /// </summary>
    /// <typeparam name="T">命令类型</typeparam>
    public static class CommandPool<T> where T : class, ICommand, new()
    {
        private static readonly Stack<T> pool = new Stack<T>(4);
        private static readonly object lockObj = new object();

        /// <summary>
        /// 从池中获取一个命令实例。
        /// 池为空时自动创建新实例。
        /// </summary>
        public static T Get()
        {
            lock (lockObj)
            {
                if (pool.Count > 0)
                {
                    return pool.Pop();
                }
            }
            return new T();
        }

        /// <summary>
        /// 将命令实例回收到池中。
        /// 命令执行完毕后调用，实例会被重置以便下次复用。
        /// </summary>
        public static void Release(T command)
        {
            if (command == null) return;

            // 如果是可重置的命令，调用重置接口
            if (command is IPoolableCommand poolable)
            {
                poolable.OnRecycle();
            }

            lock (lockObj)
            {
                // 限制池大小，防止无限增长
                if (pool.Count < 16)
                {
                    pool.Push(command);
                }
            }
        }

        /// <summary>
        /// 清空对象池。
        /// </summary>
        public static void Clear()
        {
            lock (lockObj)
            {
                pool.Clear();
            }
        }
    }

    /// <summary>
    /// 可池化命令的回收回调接口。
    /// 实现此接口的命令在回池时会调用 OnRecycle()，
    /// 用于清理字段状态防止野引用。
    /// </summary>
    public interface IPoolableCommand
    {
        /// <summary>
        /// 回池时调用，清理命令内部状态。
        /// </summary>
        void OnRecycle();
    }
}
