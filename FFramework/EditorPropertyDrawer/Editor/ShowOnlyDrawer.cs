#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 此类包含 ShowOnly 属性的自定义抽屉，支持数组和 List。
/// </summary>
[CustomPropertyDrawer(typeof(ShowOnlyAttribute))]
public class ShowOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label, true); // 展开显示
        }
        else
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
        GUI.enabled = true;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
        else
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
#endif