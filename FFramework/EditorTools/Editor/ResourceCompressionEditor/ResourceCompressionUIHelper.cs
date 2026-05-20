using UnityEngine;
using UnityEngine.UIElements;

namespace FFramework.Editor
{
    /// <summary>
    /// 资源压缩工具的公共 UI 辅助方法。
    /// </summary>
    public static class ResourceCompressionUIHelper
    {
        public static VisualElement CreateSection()
        {
            var section = new VisualElement();
            section.style.marginBottom = 4;
            section.style.marginLeft = 0;
            section.style.marginRight = 0;
            section.style.paddingLeft = 5;
            section.style.paddingRight = 5;
            section.style.paddingTop = 5;
            section.style.paddingBottom = 5;
            section.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            section.style.borderTopLeftRadius = 3;
            section.style.borderTopRightRadius = 3;
            section.style.borderBottomLeftRadius = 3;
            section.style.borderBottomRightRadius = 3;
            section.style.borderLeftWidth = 1;
            section.style.borderRightWidth = 1;
            section.style.borderTopWidth = 1;
            section.style.borderBottomWidth = 1;
            section.style.borderLeftColor = new Color(0.29f, 0.29f, 0.29f);
            section.style.borderRightColor = new Color(0.29f, 0.29f, 0.29f);
            section.style.borderTopColor = new Color(0.29f, 0.29f, 0.29f);
            section.style.borderBottomColor = new Color(0.29f, 0.29f, 0.29f);
            return section;
        }

        public static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        public static VisualElement CreateFlexibleSpace()
        {
            var space = new VisualElement();
            space.style.flexGrow = 1;
            return space;
        }

        public static VisualElement CreateSpacer(float height = 4)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            return spacer;
        }

        public static Label CreateSectionLabel(string text, int fontSize = 11, FontStyle fontStyle = FontStyle.Bold)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.color = new Color(0.85f, 0.85f, 0.85f);
            label.style.marginBottom = 2;
            return label;
        }

        public static Label CreateInfoLabel(string text, float width, int fontSize)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.fontSize = fontSize;
            label.style.color = new Color(0.85f, 0.85f, 0.85f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.flexShrink = 0;
            return label;
        }

        public static void ApplyCacheItemStyle(VisualElement item)
        {
            item.style.marginBottom = 2;
            item.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            item.style.borderTopLeftRadius = 3;
            item.style.borderTopRightRadius = 3;
            item.style.borderBottomLeftRadius = 3;
            item.style.borderBottomRightRadius = 3;
            item.style.borderLeftWidth = 1;
            item.style.borderRightWidth = 1;
            item.style.borderTopWidth = 1;
            item.style.borderBottomWidth = 1;
            item.style.borderLeftColor = new Color(0.29f, 0.29f, 0.29f);
            item.style.borderRightColor = new Color(0.29f, 0.29f, 0.29f);
            item.style.borderTopColor = new Color(0.29f, 0.29f, 0.29f);
            item.style.borderBottomColor = new Color(0.29f, 0.29f, 0.29f);
        }

        // ================================================================
        // UI 辅助构建方法
        // ================================================================

        public static VisualElement CreateEnumRow(string labelText, System.Enum value, System.Action<System.Enum> onChanged)
        {
            var row = CreateRow();
            row.style.minHeight = 20;
            row.style.marginBottom = 1;

            var label = new Label(labelText);
            label.style.width = 100;
            label.style.fontSize = 11;
            label.style.color = new Color(0.8f, 0.8f, 0.8f);
            label.style.flexShrink = 0;
            row.Add(label);

            var field = new EnumField(value);
            field.style.flexGrow = 1;
            field.style.height = 18;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            row.Add(field);

            return row;
        }

        public static VisualElement CreateToggleRow(string labelText, bool value, System.Action<bool> onChanged)
        {
            var row = CreateRow();
            row.style.minHeight = 20;
            row.style.marginBottom = 1;

            var toggle = new Toggle(labelText);
            toggle.value = value;
            toggle.style.fontSize = 11;
            toggle.style.flexGrow = 1;
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            row.Add(toggle);

            return row;
        }

        public static VisualElement CreateIntFieldRow(string labelText, int value, System.Action<int> onChanged)
        {
            var row = CreateRow();
            row.style.minHeight = 20;
            row.style.marginBottom = 1;

            var label = new Label(labelText);
            label.style.width = 100;
            label.style.fontSize = 11;
            label.style.color = new Color(0.8f, 0.8f, 0.8f);
            label.style.flexShrink = 0;
            row.Add(label);

            var field = new IntegerField();
            field.value = value;
            field.style.flexGrow = 1;
            field.style.height = 18;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            row.Add(field);

            return row;
        }

        public static VisualElement CreateSliderRow(string labelText, float value, float min, float max, System.Action<float> onChanged)
        {
            var row = CreateRow();
            row.style.minHeight = 20;
            row.style.marginBottom = 1;

            var label = new Label(labelText);
            label.style.width = 100;
            label.style.fontSize = 11;
            label.style.color = new Color(0.8f, 0.8f, 0.8f);
            label.style.flexShrink = 0;
            row.Add(label);

            var slider = new Slider(min, max);
            slider.value = value;
            slider.style.flexGrow = 1;
            slider.style.height = 18;
            slider.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            row.Add(slider);

            var valueLabel = new Label(value.ToString("0.##"));
            valueLabel.style.width = 30;
            valueLabel.style.fontSize = 10;
            valueLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            valueLabel.style.flexShrink = 0;
            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = evt.newValue.ToString("0.##");
            });
            row.Add(valueLabel);

            return row;
        }
    }
}
