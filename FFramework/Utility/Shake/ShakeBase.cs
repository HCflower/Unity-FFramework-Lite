// =============================================================
// 描述：震动效果基类
// 作者：HCFlower
// 创建时间：2025-11-16 00:44:00
// 版本：1.0.1
// 更新记录：移除与 ShakePreset 重复的 ShakeSettings 和 NoiseType 定义，统一使用 ShakePreset 中的定义
// =============================================================
using System.Collections;
using UnityEngine;
using System;

namespace FFramework.Utility
{
    public abstract class ShakeBase : MonoBehaviour
    {
        [Header("震动预设")]
        [Tooltip("震动设置预设文件")]
        public ShakePreset shakePreset;

        // 内部状态
        protected Vector3 originalPosition;
        protected Vector3 originalRotation;
        protected bool isShaking = false;

        // 协程句柄
        private Coroutine shakeCoroutine;

        // 性能优化：缓存计算结果，减少GC分配
        private Vector3 cachedPositionOffset;
        private Vector3 cachedRotationOffset;
        private float lastCalculatedTime = -1f;
        private float lastCalculatedIntensity = 0f;

        // 便捷访问属性（直接从SO文件获取，提供默认值）
        public ShakePreset.ShakeSettings positionShake => shakePreset?.positionShake ?? new ShakePreset.ShakeSettings();
        public ShakePreset.ShakeSettings rotationShake => shakePreset?.rotationShake ?? new ShakePreset.ShakeSettings();
        public float fadeInDuration => shakePreset?.fadeInDuration ?? 0.1f;
        public float holdDuration => shakePreset?.holdDuration ?? 0.5f;
        public float fadeOutDuration => shakePreset?.fadeOutDuration ?? 0.2f;
        public AnimationCurve fadeInCurve => shakePreset?.fadeInCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve fadeOutCurve => shakePreset?.fadeOutCurve ?? AnimationCurve.EaseInOut(0, 1, 1, 0);

        protected virtual void Awake()
        {
            SaveOriginalTransform();
        }

        /// <summary>
        /// 保存原始变换状态 - 子类需要实现具体的保存逻辑
        /// </summary>
        public abstract void SaveOriginalTransform();

        /// <summary>
        /// 应用震动变换 - 子类需要实现具体的应用逻辑
        /// </summary>
        /// <param name="positionOffset">位置偏移</param>
        /// <param name="rotationOffset">旋转偏移</param>
        protected abstract void ApplyShake(Vector3 positionOffset, Vector3 rotationOffset);

        /// <summary>
        /// 重置变换到原始状态 - 子类需要实现具体的重置逻辑
        /// </summary>
        public abstract void ResetTransform();

        /// <summary>
        /// 播放震动
        /// </summary>
        public virtual void PlayShake()
        {
            if (shakePreset == null)
            {
                Debug.LogWarning($"ShakeBase: 没有设置震动预设文件，无法开始震动");
                return;
            }

            if (isShaking)
            {
                StopShake();
            }

            SaveOriginalTransform();

            shakeCoroutine = StartCoroutine(ShakeCoroutine());
        }

        /// <summary>
        /// 使用预设开始震动
        /// </summary>
        /// <param name="preset">震动预设</param>
        public virtual void PlayShake(ShakePreset preset)
        {
            if (preset != null)
            {
                shakePreset = preset;
            }
            PlayShake();
        }

        /// <summary>
        /// 开始震动（指定持续时间）
        /// </summary>
        /// <param name="duration">震动持续时间</param>
        public virtual void PlayShake(float duration)
        {
            if (shakePreset != null)
            {
                shakePreset.holdDuration = duration;
            }
            PlayShake();
        }

        /// <summary>
        /// 开始震动（自定义设置）
        /// </summary>
        /// <param name="posAmplitude">位置震动强度</param>
        /// <param name="rotAmplitude">旋转震动强度</param>
        /// <param name="duration">持续时间</param>
        public virtual void PlayShake(Vector3 posAmplitude, Vector3 rotAmplitude, float duration)
        {
            if (shakePreset != null)
            {
                shakePreset.positionShake.amplitude = posAmplitude;
                shakePreset.rotationShake.amplitude = rotAmplitude;
                shakePreset.holdDuration = duration;
            }
            PlayShake();
        }

        /// <summary>
        /// 停止震动
        /// </summary>
        public virtual void StopShake()
        {
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                shakeCoroutine = null;
            }

            isShaking = false;
            ResetTransform();
        }

        private IEnumerator ShakeCoroutine()
        {
            isShaking = true;
            float totalDuration = fadeInDuration + holdDuration + fadeOutDuration;
            float elapsed = 0f;

            while (elapsed < totalDuration && isShaking)
            {
                if (Mathf.Abs(elapsed - lastCalculatedTime) > 0.001f)
                {
                    lastCalculatedTime = elapsed;
                    lastCalculatedIntensity = CalculateIntensity(elapsed);

                    cachedPositionOffset = positionShake.Evaluate(elapsed) * lastCalculatedIntensity;
                    cachedRotationOffset = rotationShake.Evaluate(elapsed) * lastCalculatedIntensity;
                }

                ApplyShake(cachedPositionOffset, cachedRotationOffset);

                elapsed += Time.deltaTime;
                yield return null;
            }

            ResetTransform();
            isShaking = false;

            lastCalculatedTime = -1f;
            lastCalculatedIntensity = 0f;
            cachedPositionOffset = Vector3.zero;
            cachedRotationOffset = Vector3.zero;
        }

        /// <summary>
        /// 计算当前时间点的震动强度
        /// </summary>
        protected virtual float CalculateIntensity(float elapsed)
        {
            if (elapsed < fadeInDuration)
            {
                // 淡入阶段
                float t = elapsed / fadeInDuration;
                return fadeInCurve.Evaluate(t);
            }
            else if (elapsed < fadeInDuration + holdDuration)
            {
                // 保持阶段
                return 1f;
            }
            else
            {
                // 淡出阶段
                float t = (elapsed - fadeInDuration - holdDuration) / fadeOutDuration;
                return fadeOutCurve.Evaluate(t);
            }
        }

        /// <summary>
        /// 检查是否正在震动
        /// </summary>
        public bool IsShaking => isShaking;

        /// <summary>
        /// 获取总震动时长
        /// </summary>
        public float TotalDuration => fadeInDuration + holdDuration + fadeOutDuration;

        protected virtual void OnDestroy()
        {
            StopShake();
        }

#if UNITY_EDITOR

        // 测试震动
        [Button("Test震动")]
        public void TestShake()
        {
            if (Application.isPlaying)
                PlayShake();
            else
                Debug.LogWarning("请在播放模式下测试震动");
        }

        [Button("打开预设")]
        public void OpenPresetSO()
        {
            // 打开预设SO面板
            if (shakePreset != null)
            {
                UnityEditor.Selection.activeObject = shakePreset;
                UnityEditor.EditorGUIUtility.PingObject(shakePreset);
            }
        }

        // 编辑器调试方法
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        protected virtual void OnValidate()
        {
            // 如果有预设文件，确保时长不为负数
            if (shakePreset != null)
            {
                shakePreset.fadeInDuration = Mathf.Max(0, shakePreset.fadeInDuration);
                shakePreset.holdDuration = Mathf.Max(0, shakePreset.holdDuration);
                shakePreset.fadeOutDuration = Mathf.Max(0, shakePreset.fadeOutDuration);
            }
        }
#endif
    }
}
