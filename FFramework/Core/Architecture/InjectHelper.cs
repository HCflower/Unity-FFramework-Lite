// =============================================================
// 描述：依赖注入工具类 - 提供 AutoInject 和依赖解析功能
// 作者：HCFlower
// 创建时间：2026-05-11
// 版本：1.0.0
// =============================================================
using System.Reflection;
using UnityEngine;
using System;

namespace FFramework.Core
{
    /// <summary>
    /// 依赖注入工具类。
    /// 提供 AutoInject 方法，用于扫描目标对象上的 [Inject] 标记字段/属性并注入依赖。
    /// 依赖解析通过 Resolver 委托由调用方（Architecture）提供。
    /// </summary>
    public static class InjectHelper
    {
        /// <summary>
        /// 依赖解析委托。接收一个 Type，返回该类型的已注册实例。
        /// 由 Architecture 提供具体实现。
        /// </summary>
        public static Func<Type, object> Resolver { get; set; }

        private const BindingFlags FieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private const BindingFlags PropertyFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// 自动注入目标对象上所有标记了 [Inject] 的字段和属性。
        /// </summary>
        /// <param name="target">需要注入的目标对象</param>
        public static void AutoInject(object target)
        {
            if (target == null)
            {
                Debug.LogError("InjectHelper.AutoInject: target 不能为 null");
                return;
            }

            if (Resolver == null)
            {
                Debug.LogError("InjectHelper.AutoInject: Resolver 未设置，请确保在 Architecture 初始化时设置");
                return;
            }

            Type targetType = target.GetType();

            // 注入字段
            InjectFields(target, targetType);

            // 注入属性
            InjectProperties(target, targetType);
        }

        /// <summary>
        /// 扫描并注入 [Inject] 标记的字段
        /// </summary>
        private static void InjectFields(object target, Type targetType)
        {
            FieldInfo[] fields = targetType.GetFields(FieldFlags);

            foreach (FieldInfo field in fields)
            {
                if (!field.IsDefined(typeof(InjectAttribute), inherit: true))
                    continue;

                Type fieldType = field.FieldType;

                // 跳过 Unity 序列化可能产生的特殊字段
                if (fieldType.IsPointer || fieldType.IsByRef)
                    continue;

                object resolved = Resolver.Invoke(fieldType);
                if (resolved != null)
                {
                    field.SetValue(target, resolved);
                }
                else
                {
                    Debug.LogWarning($"[InjectHelper] 无法解析依赖 [{fieldType.Name} {field.Name}] 在 {targetType.Name} 中");
                }
            }
        }

        /// <summary>
        /// 扫描并注入 [Inject] 标记的属性
        /// </summary>
        private static void InjectProperties(object target, Type targetType)
        {
            PropertyInfo[] properties = targetType.GetProperties(PropertyFlags);

            foreach (PropertyInfo prop in properties)
            {
                if (!prop.IsDefined(typeof(InjectAttribute), inherit: true))
                    continue;

                if (!prop.CanWrite)
                {
                    Debug.LogWarning($"[InjectHelper] 属性 [{prop.Name}] 标记了 [Inject] 但没有 setter,跳过");
                    continue;
                }

                Type propType = prop.PropertyType;

                object resolved = Resolver.Invoke(propType);
                if (resolved != null)
                {
                    prop.SetValue(target, resolved);
                }
                else
                {
                    Debug.LogWarning($"[InjectHelper] 无法解析属性依赖 [{propType.Name} {prop.Name}] 在 {targetType.Name} 中");
                }
            }
        }
    }
}
