using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Method)]
public class ButtonAttribute : Attribute
{
    public string ButtonName { get; }
    public ButtonColor ColorType { get; }

    public ButtonAttribute(string name = null, ButtonColor colorType = ButtonColor.White)
    {
        ButtonName = name;
        ColorType = colorType;
    }

#if UNITY_EDITOR
    public Color GetUnityColor()
    {
        switch (ColorType)
        {
            case ButtonColor.Yellow: return Color.yellow;
            case ButtonColor.Red: return Color.red;
            case ButtonColor.Green: return Color.green;
            case ButtonColor.Blue: return Color.blue;
            case ButtonColor.Black: return Color.black;
            case ButtonColor.Cyan: return Color.cyan;
            case ButtonColor.Magenta: return Color.magenta;
            case ButtonColor.Gray: return Color.gray;
            default: return Color.white;
        }
    }
#endif
}

/// <summary>
/// 按钮颜色枚举
/// </summary>
public enum ButtonColor
{
    White,
    Yellow,
    Red,
    Green,
    Blue,
    Black,
    Cyan,
    Magenta,
    Gray
}