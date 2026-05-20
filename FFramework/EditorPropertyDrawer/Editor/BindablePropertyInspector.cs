using FFramework.Core;
using UnityEditor;
using UnityEngine;

namespace FFramework.Editor
{
    [CustomPropertyDrawer(typeof(BindableProperty<>), true)]
    public class BindablePropertyInspector : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 获取 value 字段
            var valueProperty = property.FindPropertyRelative("value");

            if (valueProperty != null)
            {
                // 使用原有的标签，但只显示 value 字段
                EditorGUI.PropertyField(position, valueProperty, label, true);
            }
            else
            {
                // 如果找不到 value 字段，显示警告
                EditorGUI.LabelField(position, label.text, "BindableProperty value not found");
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var valueProperty = property.FindPropertyRelative("value");

            if (valueProperty != null)
            {
                // 返回 value 字段的高度
                return EditorGUI.GetPropertyHeight(valueProperty, label, true);
            }

            // 默认单行高度
            return EditorGUIUtility.singleLineHeight;
        }
    }
}