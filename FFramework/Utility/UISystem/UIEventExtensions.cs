// =============================================================
// 描述：UI事件绑定静态扩展类 
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.0
// =============================================================
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using System;

namespace FFramework.Utility
{
    public static class UIEventExtensions
    {
        #region 内部方法

        /// <summary>
        /// 通用事件绑定方法（内部使用）
        /// </summary>
        private static T BindEvent<T>(UIPanel panel, string componentName, Action<T> bindAction, Action cleanupAction, bool autoTrack) where T : Component
        {
            T component = UISystem.Instance.GetChildComponent<T>(panel.gameObject, componentName);
            if (component == null)
            {
                Debug.LogError($"[UIEventExtensions] 未找到名为 {componentName} 的 {typeof(T).Name} 组件");
                return null;
            }

            bindAction?.Invoke(component);

            if (autoTrack && panel != null)
            {
                panel.AddEventCleanup(() =>
                {
                    try
                    {
                        if (panel != null && panel.gameObject != null)
                        {
                            cleanupAction?.Invoke();
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[UIEventExtensions] 清理 {typeof(T).Name}.{componentName} 事件失败: {e.Message}");
                    }
                }, $"{typeof(T).Name}.{componentName}");
            }

            return component;
        }

        #endregion

        #region 核心事件绑定

        /// <summary>
        /// 绑定Button点击事件
        /// </summary>
        public static Button BindButton(this UIPanel panel, string buttonName, UnityAction action, bool autoTrack = true)
        {
            return BindEvent<Button>(panel, buttonName,
                button => button.onClick.AddListener(action),
                () => panel.GetComponent<Button>(buttonName)?.onClick.RemoveListener(action),
                autoTrack);
        }

        /// <summary>
        /// 绑定Toggle值变化事件
        /// </summary>
        public static Toggle BindToggle(this UIPanel panel, string toggleName, UnityAction<bool> action, bool autoTrack = true)
        {
            return BindEvent<Toggle>(panel, toggleName,
                toggle => toggle.onValueChanged.AddListener(action),
                () => panel.GetComponent<Toggle>(toggleName)?.onValueChanged.RemoveListener(action),
                autoTrack);
        }

        /// <summary>
        /// 绑定Slider值变化事件
        /// </summary>
        public static Slider BindSlider(this UIPanel panel, string sliderName, UnityAction<float> action, bool autoTrack = true)
        {
            return BindEvent<Slider>(panel, sliderName,
                slider => slider.onValueChanged.AddListener(action),
                () => panel.GetComponent<Slider>(sliderName)?.onValueChanged.RemoveListener(action),
                autoTrack);
        }

        /// <summary>
        /// 绑定InputField值变化事件
        /// </summary>
        public static InputField BindInputField(this UIPanel panel, string inputFieldName, UnityAction<string> action, bool autoTrack = true)
        {
            return BindEvent<InputField>(panel, inputFieldName,
                input => input.onValueChanged.AddListener(action),
                () => panel.GetComponent<InputField>(inputFieldName)?.onValueChanged.RemoveListener(action),
                autoTrack);
        }

        /// <summary>
        /// 绑定Dropdown值变化事件
        /// </summary>
        public static Dropdown BindDropdown(this UIPanel panel, string dropdownName, UnityAction<int> action, bool autoTrack = true)
        {
            return BindEvent<Dropdown>(panel, dropdownName,
                dropdown => dropdown.onValueChanged.AddListener(action),
                () => panel.GetComponent<Dropdown>(dropdownName)?.onValueChanged.RemoveListener(action),
                autoTrack);
        }

        /// <summary>
        /// 绑定TMP_InputField值变化事件
        /// </summary>
        public static TMP_InputField BindTMPInputField(this UIPanel panel, string inputFieldName, UnityAction<string> action, bool autoTrack = true)
        {
            return BindEvent<TMP_InputField>(panel, inputFieldName,
                input => input.onValueChanged.AddListener(action),
                () => panel.GetComponent<TMP_InputField>(inputFieldName)?.onValueChanged.RemoveListener(action),
                autoTrack);
        }

        /// <summary>
        /// 绑定TMP_Dropdown值变化事件
        /// </summary>
        public static TMP_Dropdown BindTMPDropdown(this UIPanel panel, string dropdownName, UnityAction<int> action, bool autoTrack = true)
        {
            return BindEvent<TMP_Dropdown>(panel, dropdownName,
                dropdown => dropdown.onValueChanged.AddListener(action),
                () => panel.GetComponent<TMP_Dropdown>(dropdownName)?.onValueChanged.RemoveListener(action),
                autoTrack);
        }

        #endregion

        #region 直接组件事件绑定

        /// <summary>
        /// 绑定Button点击事件（直接传入Button）
        /// </summary>
        public static Button BindClick(this Button button, UnityAction action, UIPanel panel = null)
        {
            if (button == null) return null;
            button.onClick.AddListener(action);
            if (panel != null)
            {
                panel.AddEventCleanup(() => button?.onClick.RemoveListener(action), "Button");
            }
            return button;
        }

        /// <summary>
        /// 绑定Toggle值变化事件（直接传入Toggle）
        /// </summary>
        public static Toggle BindValueChanged(this Toggle toggle, UnityAction<bool> action, UIPanel panel = null)
        {
            if (toggle == null) return null;
            toggle.onValueChanged.AddListener(action);
            if (panel != null)
            {
                panel.AddEventCleanup(() => toggle?.onValueChanged.RemoveListener(action), "Toggle");
            }
            return toggle;
        }

        /// <summary>
        /// 绑定Slider值变化事件（直接传入Slider）
        /// </summary>
        public static Slider BindValueChanged(this Slider slider, UnityAction<float> action, UIPanel panel = null)
        {
            if (slider == null) return null;
            slider.onValueChanged.AddListener(action);
            if (panel != null)
            {
                panel.AddEventCleanup(() => slider?.onValueChanged.RemoveListener(action), "Slider");
            }
            return slider;
        }

        #endregion

        #region 组件获取

        /// <summary>
        /// 获取子物体组件（通用版本）
        /// </summary>
        public static T GetComponent<T>(this UIPanel panel, string objectName, bool recursive = true) where T : Component
        {
            return UISystem.Instance.GetChildComponent<T>(panel.gameObject, objectName, recursive);
        }

        // 常用组件快捷获取
        public static Button GetButton(this UIPanel panel, string buttonName) => panel.GetComponent<Button>(buttonName);
        public static Toggle GetToggle(this UIPanel panel, string toggleName) => panel.GetComponent<Toggle>(toggleName);
        public static Slider GetSlider(this UIPanel panel, string sliderName) => panel.GetComponent<Slider>(sliderName);
        public static InputField GetInputField(this UIPanel panel, string inputFieldName) => panel.GetComponent<InputField>(inputFieldName);
        public static Dropdown GetDropdown(this UIPanel panel, string dropdownName) => panel.GetComponent<Dropdown>(dropdownName);
        public static Image GetImage(this UIPanel panel, string imageName) => panel.GetComponent<Image>(imageName);
        public static Text GetText(this UIPanel panel, string textName) => panel.GetComponent<Text>(textName);
        public static TextMeshProUGUI GetTMPText(this UIPanel panel, string textName) => panel.GetComponent<TextMeshProUGUI>(textName);
        public static TMP_InputField GetTMPInputField(this UIPanel panel, string inputFieldName) => panel.GetComponent<TMP_InputField>(inputFieldName);
        public static TMP_Dropdown GetTMPDropdown(this UIPanel panel, string dropdownName) => panel.GetComponent<TMP_Dropdown>(dropdownName);

        #endregion

        #region 便捷设置方法

        /// <summary>
        /// 设置组件属性（通用版本）
        /// </summary>
        public static T SetProperty<T>(this UIPanel panel, string componentName, Action<T> setAction) where T : Component
        {
            T component = panel.GetComponent<T>(componentName);
            setAction?.Invoke(component);
            return component;
        }

        // 常用设置方法
        public static Button SetButtonInteractable(this UIPanel panel, string buttonName, bool interactable)
            => panel.SetProperty<Button>(buttonName, btn => btn.interactable = interactable);

        public static Toggle SetToggleValue(this UIPanel panel, string toggleName, bool value, bool sendCallback = true)
            => panel.SetProperty<Toggle>(toggleName, toggle =>
            {
                if (sendCallback) toggle.isOn = value;
                else toggle.SetIsOnWithoutNotify(value);
            });

        public static Slider SetSliderValue(this UIPanel panel, string sliderName, float value, bool sendCallback = true)
            => panel.SetProperty<Slider>(sliderName, slider =>
            {
                if (sendCallback) slider.value = value;
                else slider.SetValueWithoutNotify(value);
            });

        public static Text SetText(this UIPanel panel, string textName, string text)
            => panel.SetProperty<Text>(textName, textComp => textComp.text = text);

        public static TextMeshProUGUI SetTMPText(this UIPanel panel, string textName, string text)
            => panel.SetProperty<TextMeshProUGUI>(textName, textComp => textComp.text = text);

        public static Image SetImageSprite(this UIPanel panel, string imageName, Sprite sprite)
            => panel.SetProperty<Image>(imageName, img => img.sprite = sprite);

        public static Image SetImageColor(this UIPanel panel, string imageName, Color color)
            => panel.SetProperty<Image>(imageName, img => img.color = color);

        #endregion

        #region 批量操作

        /// <summary>
        /// 批量绑定按钮事件
        /// </summary>
        public static void BindButtons(this UIPanel panel, Dictionary<string, UnityAction> buttonEvents, bool autoTrack = true)
        {
            if (buttonEvents == null) return;
            foreach (var kvp in buttonEvents)
            {
                panel.BindButton(kvp.Key, kvp.Value, autoTrack);
            }
        }

        /// <summary>
        /// 清除所有UI事件（别名方法，与ClearAllEvents功能相同）
        /// </summary>
        public static void UnbindAllEvents(this UIPanel panel)
        {
            panel.ClearAllEvents();
        }

        /// <summary>
        /// 清除所有UI事件
        /// </summary>
        public static void ClearAllEvents(this UIPanel panel)
        {
            if (panel == null) return;

            // 清理常用组件事件
            var buttons = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons) btn?.onClick.RemoveAllListeners();

            var toggles = panel.GetComponentsInChildren<Toggle>(true);
            foreach (var toggle in toggles) toggle?.onValueChanged.RemoveAllListeners();

            var sliders = panel.GetComponentsInChildren<Slider>(true);
            foreach (var slider in sliders) slider?.onValueChanged.RemoveAllListeners();

            var inputFields = panel.GetComponentsInChildren<InputField>(true);
            foreach (var input in inputFields)
            {
                input?.onValueChanged.RemoveAllListeners();
                input?.onEndEdit.RemoveAllListeners();
            }

            var dropdowns = panel.GetComponentsInChildren<Dropdown>(true);
            foreach (var dropdown in dropdowns) dropdown?.onValueChanged.RemoveAllListeners();

            // TMP组件
            var tmpInputs = panel.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var input in tmpInputs)
            {
                input?.onValueChanged.RemoveAllListeners();
                input?.onEndEdit.RemoveAllListeners();
            }

            var tmpDropdowns = panel.GetComponentsInChildren<TMP_Dropdown>(true);
            foreach (var dropdown in tmpDropdowns) dropdown?.onValueChanged.RemoveAllListeners();

            Debug.Log($"[UIEventExtensions] 清理面板 {panel.name} 的所有UI事件");
        }

        #endregion
    }
}