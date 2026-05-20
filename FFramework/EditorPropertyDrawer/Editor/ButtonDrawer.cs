#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
/// <summary>
/// 绘制按钮
/// </summary>
[CustomEditor(typeof(MonoBehaviour), true)]
public class ButtonDrawer : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        MonoBehaviour script = (MonoBehaviour)target;

        var methods = script.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var method in methods)
        {
            var buttonAttrs = method.GetCustomAttributes(typeof(ButtonAttribute), true);
            if (buttonAttrs.Length > 0)
            {
                var attr = (ButtonAttribute)buttonAttrs[0];
                string buttonName = string.IsNullOrEmpty(attr.ButtonName)
                    ? method.Name
                    : attr.ButtonName;

                var originalColor = GUI.color;
                GUI.color = attr.GetUnityColor();
                if (GUILayout.Button(buttonName))
                {
                    method.Invoke(script, null);
                }
                GUI.color = originalColor;
            }
        }
    }
}

[CustomEditor(typeof(ScriptableObject), true)]
public class InspectorButtonSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ScriptableObject script = (ScriptableObject)target;

        var methods = script.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var method in methods)
        {
            var buttonAttrs = method.GetCustomAttributes(typeof(ButtonAttribute), true);
            if (buttonAttrs.Length > 0)
            {
                var attr = (ButtonAttribute)buttonAttrs[0];
                string buttonName = string.IsNullOrEmpty(attr.ButtonName)
                    ? method.Name
                    : attr.ButtonName;

                var originalColor = GUI.color;
                GUI.color = attr.GetUnityColor();
                if (GUILayout.Button(buttonName))
                {
                    method.Invoke(script, null);
                }
                GUI.color = originalColor;
            }
        }
    }
}
#endif