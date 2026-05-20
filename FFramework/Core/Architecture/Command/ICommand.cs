// =============================================================
// 描述：命令模式接口定义
// 作者：HCFlower
// 创建时间：2026-05-13
// 版本：1.0.0
// =============================================================

namespace FFramework.Core
{
    /// <summary>
    /// 命令接口 - 所有命令的基接口。
    /// 实现此接口可被 Architecture 执行。
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// 带返回值的命令接口。
    /// 继承自 ICommand，因此实现类也满足 ICommand 约束，可与对象池等设施兼容。
    /// 实现类需要同时实现 ICommand.Execute()（void）和 ICommand{TResult}.Execute()（TResult），
    /// 可通过 AbstractCommand{TResult} 基类提供的显式接口实现简化。
    /// </summary>
    /// <typeparam name="TResult">返回值类型</typeparam>
    public interface ICommand<TResult> : ICommand
    {
        /// <summary>
        /// 执行命令并返回结果
        /// new 关键字隐藏基接口的 void Execute()，允许定义不同返回值
        /// </summary>
        new TResult Execute();
    }

    /// <summary>
    /// 标记接口 - 表示该对象可以发送命令。
    /// 实现此接口的对象可通过扩展方法调用 SendCommand()。
    /// </summary>
    public interface ICommandSender { }
}
