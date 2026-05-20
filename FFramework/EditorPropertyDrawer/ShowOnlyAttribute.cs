using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]

/// <summary>
///Read Only 属性。
///Attribute 仅用于标记 ReadOnly 属性。
/// </summary>
public class ShowOnlyAttribute : PropertyAttribute { }