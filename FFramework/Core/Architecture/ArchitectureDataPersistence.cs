// =============================================================
// 描述：架构数据持久化工具类 - 负责 Model 数据的序列化、文件读写
// 作者：HCFlower
// 创建时间：2026-05-10
// 版本：2.0.0
// 修改：v2.0.0 - 扩展数据持久化能力，支持 BindableProperty<T> 自动检测、
//                [SaveData] 显式标记和 [SerializeField] 自动识别三种模式
// =============================================================
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using System.IO;
using System;

namespace FFramework.Core
{
    /// <summary>
    /// 架构数据持久化工具类。
    /// 负责 Model 数据的 JSON 序列化/反序列化及文件读写。
    /// 支持三种持久化成员识别方式：
    /// <list type="bullet">
    ///   <item><see cref="BindableProperty{T}"/> 自动检测 — public 字段/属性，自动提取 .Value</item>
    ///   <item><see cref="SaveDataAttribute"/> 显式标记 — 精确控制任意可访问性的成员</item>
    ///   <item><see cref="SerializeField"/> 自动识别 — 复用 Unity 序列化标记，自动排除 UnityEngine.Object 引用</item>
    /// </list>
    /// </summary>
    public static class ArchitectureDataPersistence
    {
        #region 成员分类

        /// <summary>
        /// 持久化成员类别
        /// </summary>
        private enum MemberCategory
        {
            /// <summary>BindableProperty<T> 类型，需要提取 .Value 属性</summary>
            BindableProperty,

            /// <summary>[SaveData] 或 [SerializeField] 标记的直接值成员</summary>
            DirectValue
        }

        /// <summary>
        /// 持久化成员信息（统一处理字段和属性）
        /// </summary>
        private struct PersistentMember
        {
            public readonly string Name;
            public readonly Type DeclaredType;
            public readonly MemberCategory Category;

            private readonly Func<object, object> getter;
            private readonly Action<object, object> setter;

            public PersistentMember(FieldInfo field, MemberCategory category)
            {
                Name = field.Name;
                DeclaredType = field.FieldType;
                Category = category;
                getter = field.GetValue;
                setter = field.SetValue;
            }

            public PersistentMember(PropertyInfo property, MemberCategory category)
            {
                Name = property.Name;
                DeclaredType = property.PropertyType;
                Category = category;
                getter = property.GetValue;

                // 仅当属性可写时才支持 setter
                if (property.CanWrite)
                {
                    setter = property.SetValue;
                }
                else
                {
                    setter = null; // 只读属性，加载时跳过
                }
            }

            public object GetValue(object target) => getter(target);

            /// <summary>
            /// 设置成员值（只读成员调用无效）
            /// </summary>
            public void SetValue(object target, object value)
            {
                setter?.Invoke(target, value);
            }
        }

        #endregion

        #region 类型判断辅助

        /// <summary>
        /// 判断类型是否为 BindableProperty<T>
        /// </summary>
        private static bool IsBindablePropertyType(Type type)
        {
            return type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(BindableProperty<>);
        }

        /// <summary>
        /// 判断类型是否为 UnityEngine.Object 或派生类型（不可跨会话持久化）
        /// </summary>
        private static bool IsUnityObjectType(Type type)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        /// <summary>
        /// 判断成员是否标记了 [SaveData]
        /// </summary>
        private static bool HasSaveDataAttribute(MemberInfo member)
        {
            return member.GetCustomAttribute<SaveDataAttribute>() != null;
        }

        /// <summary>
        /// 判断字段是否标记了 [SerializeField]
        /// </summary>
        private static bool HasSerializeFieldAttribute(FieldInfo field)
        {
            return field.GetCustomAttribute<SerializeField>() != null;
        }

        #endregion

        #region 反射缓存

        /// <summary>
        /// 反射结果缓存，避免每次保存/加载都执行反射扫描
        /// </summary>
        private static readonly ConcurrentDictionary<Type, PersistentMember[]> memberCache
            = new ConcurrentDictionary<Type, PersistentMember[]>();

        /// <summary>
        /// 获取 Model 中所有需要持久化的成员。
        /// 按以下优先级检测：
        /// 1. BindableProperty<T> 自动检测
        /// 2. [SaveData] 显式标记
        /// 3. [SerializeField] 自动识别（排除 UnityEngine.Object 引用类型）
        /// </summary>
        private static PersistentMember[] GetPersistentMembers(Type modelType)
        {
            return memberCache.GetOrAdd(modelType, type =>
            {
                var members = new List<PersistentMember>();
                var addedNames = new HashSet<string>(); // 去重

                const BindingFlags publicFlags = BindingFlags.Public | BindingFlags.Instance;
                const BindingFlags allFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // --- 1. 扫描 BindableProperty<T>（原有逻辑，保持向后兼容）---
                foreach (var field in type.GetFields(publicFlags))
                {
                    if (IsBindablePropertyType(field.FieldType) && addedNames.Add(field.Name))
                    {
                        members.Add(new PersistentMember(field, MemberCategory.BindableProperty));
                    }
                }
                foreach (var prop in type.GetProperties(publicFlags))
                {
                    if (IsBindablePropertyType(prop.PropertyType) && addedNames.Add(prop.Name))
                    {
                        members.Add(new PersistentMember(prop, MemberCategory.BindableProperty));
                    }
                }

                // --- 2. 扫描 [SaveData] 标记的成员（精确控制）---
                foreach (var field in type.GetFields(allFlags))
                {
                    if (HasSaveDataAttribute(field) && addedNames.Add(field.Name))
                    {
                        members.Add(new PersistentMember(field, MemberCategory.DirectValue));
                    }
                }
                foreach (var prop in type.GetProperties(allFlags))
                {
                    if (HasSaveDataAttribute(prop) && addedNames.Add(prop.Name))
                    {
                        members.Add(new PersistentMember(prop, MemberCategory.DirectValue));
                    }
                }

                // --- 3. 扫描 [SerializeField] 标记的字段（Unity 序列化字段自动识别）---
                // 注意：[SerializeField] 仅用于字段，不用于属性
                foreach (var field in type.GetFields(allFlags))
                {
                    if (HasSerializeFieldAttribute(field) && addedNames.Add(field.Name))
                    {
                        // 排除 UnityEngine.Object 引用类型（不能跨会话保存）
                        if (!IsUnityObjectType(field.FieldType))
                        {
                            members.Add(new PersistentMember(field, MemberCategory.DirectValue));
                        }
                    }
                }

                return members.ToArray();
            });
        }

        /// <summary>
        /// 清空反射缓存（通常用于编辑器重编译后，或测试时）
        /// </summary>
        public static void ClearMemberCache()
        {
            memberCache.Clear();
        }

        #endregion

        #region 保存 / 加载

        /// <summary>
        /// 保存模型数据到指定存档
        /// </summary>
        /// <param name="model">模型实例</param>
        /// <param name="slotName">存档文件夹名称</param>
        public static void SaveData(IModel model, string slotName)
        {
            Type modelType = model.GetType();
            try
            {
                JObject jObject = new JObject();
                jObject["@ModelType"] = modelType.FullName;

                // 遍历所有需要持久化的成员
                foreach (var member in GetPersistentMembers(modelType))
                {
                    object value = member.GetValue(model);
                    if (value == null)
                    {
                        jObject[member.Name] = JValue.CreateNull();
                        continue;
                    }

                    if (member.Category == MemberCategory.BindableProperty)
                    {
                        // BindableProperty<T>：提取 .Value 属性
                        var valueProperty = member.DeclaredType.GetProperty("Value");
                        if (valueProperty == null) continue;

                        var rawValue = valueProperty.GetValue(value);
                        jObject[member.Name] = rawValue != null
                            ? JToken.FromObject(rawValue)
                            : JValue.CreateNull();
                    }
                    else // DirectValue
                    {
                        // [SaveData] / [SerializeField]：直接序列化值本身
                        jObject[member.Name] = JToken.FromObject(value);
                    }
                }

                // 序列化并写入文件
                string json = jObject.ToString(Formatting.Indented);
                string filePath = GetSaveFilePath(modelType, slotName);
                File.WriteAllText(filePath, json);
                Debug.Log($"[{modelType.Name}] 数据已保存到存档 [{slotName}]");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{modelType.Name}] 保存数据失败: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 从指定存档加载模型数据
        /// </summary>
        /// <param name="model">模型实例</param>
        /// <param name="slotName">存档文件夹名称</param>
        public static void LoadData(IModel model, string slotName)
        {
            Type modelType = model.GetType();
            try
            {
                string filePath = GetSaveFilePath(modelType, slotName);

                if (!File.Exists(filePath))
                {
                    Debug.Log($"[{modelType.Name}] 存档 [{slotName}] 不存在，跳过加载");
                    return;
                }

                string json = File.ReadAllText(filePath);

                JObject jObject = JObject.Parse(json);

                // 遍历所有需要持久化的成员
                foreach (var member in GetPersistentMembers(modelType))
                {
                    if (!jObject.TryGetValue(member.Name, out JToken jToken)) continue;

                    if (member.Category == MemberCategory.BindableProperty)
                    {
                        // BindableProperty<T>：设置 .Value 属性
                        var bindableProperty = member.GetValue(model);
                        if (bindableProperty == null) continue;

                        Type genericArg = member.DeclaredType.GetGenericArguments()[0];
                        var value = jToken.ToObject(genericArg);

                        var valueProperty = member.DeclaredType.GetProperty("Value");
                        valueProperty?.SetValue(bindableProperty, value);
                    }
                    else // DirectValue
                    {
                        // [SaveData] / [SerializeField]：直接反序列化到字段/属性
                        // 跳过 null token（保持默认值）
                        if (jToken.Type == JTokenType.Null) continue;

                        // 跳过只读成员（setter == null）
                        var value = jToken.ToObject(member.DeclaredType);
                        member.SetValue(model, value);
                    }
                }

                Debug.Log($"[{modelType.Name}] 已从存档 [{slotName}] 加载数据");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{modelType.Name}] 加载数据失败: {e.Message}\n{e.StackTrace}");
            }
        }

        #endregion

        #region 文件路径

        /// <summary>
        /// 获取存档文件夹路径
        /// </summary>
        public static string GetSlotDirectory(string slotName)
        {
            string directory = Path.Combine(
                Application.persistentDataPath,
                "SaveData",
                slotName
            );
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>
        /// 获取指定 Model 类型的存档文件路径
        /// </summary>
        public static string GetSaveFilePath(Type modelType, string slotName)
        {
            return Path.Combine(GetSlotDirectory(slotName), $"{modelType.Name}.json");
        }

        #endregion
    }
}
