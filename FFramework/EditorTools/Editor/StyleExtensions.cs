using UnityEngine;
using UnityEngine.UIElements;

namespace FFramework.Editor
{
    public static class VisualElementStyleExtensions
    {
        public static void marginAll(this IStyle style, float value) => style.marginTop = style.marginBottom = style.marginLeft = style.marginRight = value;
        public static void paddingAll(this IStyle style, float value) => style.paddingTop = style.paddingBottom = style.paddingLeft = style.paddingRight = value;
        public static void borderWidthAll(this IStyle style, float value) => style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = value;
        public static void borderColorAll(this IStyle style, Color color) => style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = color;
        public static void borderRadiusAll(this IStyle style, float value) => style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = value;
        public static void backgroundColor(this IStyle style, Color color) => style.backgroundColor = color;
    }
}
