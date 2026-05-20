// =============================================================
// 描述：依赖注入标记特性 - Architecture 在注册对象时自动注入
// 作者：HCFlower
// 创建时间：2026-05-11
// 版本：1.0.0
// =============================================================
using System;

namespace FFramework.Core
{
    /// <summary>
    /// 标记需要 Architecture 自动注入的字段或属性。
    /// 支持注入的类型包括：Model、EventSystem、Architecture（自身）等已注册的依赖。
    /// 注入时机：RegisterModel / RegisterViewController 时，在 Initialize() 调用之前执行。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class InjectAttribute : Attribute { }
}
