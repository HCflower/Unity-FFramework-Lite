using UnityEngine;
using System;

/// <summary>
/// 用于更改字段显示的属性[Text("显示名称")]
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class TextLabelAttribute : PropertyAttribute
{
    public string displayName;

    public TextLabelAttribute(string displayName)
    {
        this.displayName = displayName;
    }
}

