// =============================================================
// 描述：数据持久化标记特性 - 标记需要持久化的字段或属性
//       配合 ArchitectureDataPersistence 使用，支持非 BindableProperty 类型的数据序列化
// 作者：HCFlower
// 创建时间：2026-05-12
// 版本：1.0.0
// =============================================================
using System;

namespace FFramework.Core
{
    /// <summary>
    /// 标记需要持久化的字段或属性。
    /// 用于 ArchitectureModel 中非 BindableProperty 类型的数据成员，
    /// 使 ArchitectureDataPersistence 在保存/加载时自动处理该成员。
    /// <para>
    /// 注意：
    /// <list type="bullet">
    ///   <item>BindableProperty<T> 类型的成员会自动被检测，无需此标记</item>
    ///   <item>支持标记在 public/private 字段或属性上</item>
    ///   <item>子类会继承父类的标记（Inherited = true）</item>
    ///   <item>只读成员（readonly字段 / 仅 get 属性）仅保存不加载</item>
    ///   <item>static 成员不会被处理</item>
    /// </list>
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false, Inherited = true)]
    public class SaveDataAttribute : Attribute
    {
    }
}
