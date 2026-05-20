// =============================================================
// 描述：控制台命令特性 - 标记方法或类可作为编辑器控制台命令
// 作者：HCFlower
// 创建时间：2026-05-17
// =============================================================
using System;

namespace FFramework.Core
{
    /// <summary>
    /// 标记静态方法或类作为编辑器控制台命令。
    /// 
    /// 用法一（静态方法）：
    ///   [ConsoleCommand("命令名", "描述", "用法")]
    ///   public static void MethodName(string[] args) { }
    /// 
    /// 用法二（类，需实现 ICommand 接口）：
    ///   [ConsoleCommand("命令名", "描述", "用法")]
    ///   public class MyCommand : AbstractCommand { ... }
    ///   通过 args[0] 自动设置 Count 属性（如果存在）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ConsoleCommandAttribute : Attribute
    {
        /// <summary>命令名称（在控制台中输入的名称）</summary>
        public string CommandName { get; }

        /// <summary>命令描述</summary>
        public string Description { get; }

        /// <summary>命令用法示例</summary>
        public string Usage { get; }

        public ConsoleCommandAttribute(string commandName, string description = "", string usage = "")
        {
            CommandName = commandName;
            Description = description;
            Usage = usage;
        }
    }
}
