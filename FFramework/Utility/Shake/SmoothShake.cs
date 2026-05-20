// =============================================================
// 描述：平滑震动效果
// 作者：HCFlower
// 创建时间：2025-11-16 00:44:00
// 版本：1.0.1
// 更新记录：移除与基类 ShakeBase 重复的协程逻辑，复用基类 PlayShake/StopShake
// =============================================================
using UnityEngine;

namespace FFramework.Utility
{
    /// <summary>
    /// 通用平滑震动组件
    /// 基于SmoothShakeFree的实现原理，专为技能编辑器设计
    /// 可用于任何GameObject，包括摄像机、UI元素、特效等
    /// </summary>
    public class SmoothShake : ShakeBase
    {
        [Header("目标设置")]
        [Tooltip("指定震动目标,如果为空则使用当前GameObject")]
        public Transform shakeTarget;

        /// <summary>
        /// 获取实际的震动目标
        /// </summary>
        private Transform ShakeTransform => shakeTarget != null ? shakeTarget : transform;

        /// <summary>
        /// 保存原始变换状态
        /// </summary>
        public override void SaveOriginalTransform()
        {
            var target = ShakeTransform;
            originalPosition = target.localPosition;
            originalRotation = target.localEulerAngles;
        }

        /// <summary>
        /// 应用震动变换
        /// </summary>
        /// <param name="positionOffset">位置偏移</param>
        /// <param name="rotationOffset">旋转偏移</param>
        protected override void ApplyShake(Vector3 positionOffset, Vector3 rotationOffset)
        {
            var target = ShakeTransform;
            target.localPosition = originalPosition + positionOffset;
            target.localEulerAngles = originalRotation + rotationOffset;
        }

        /// <summary>
        /// 重置变换到原始状态
        /// </summary>
        public override void ResetTransform()
        {
            var target = ShakeTransform;
            target.localPosition = originalPosition;
            target.localEulerAngles = originalRotation;
        }

        /// <summary>
        /// 设置震动目标
        /// </summary>
        /// <param name="target">目标Transform</param>
        public void SetShakeTarget(Transform target)
        {
            shakeTarget = target;
            SaveOriginalTransform();
        }

        /// <summary>
        /// 快速开始震动（使用简单参数）
        /// </summary>
        /// <param name="positionIntensity">位置震动强度</param>
        /// <param name="rotationIntensity">旋转震动强度</param>
        /// <param name="duration">持续时间</param>
        public void StartQuickShake(float positionIntensity = 0.2f, float rotationIntensity = 2.0f, float duration = 0.5f)
        {
            // 创建临时预设
            var tempPreset = ScriptableObject.CreateInstance<ShakePreset>();
            tempPreset.positionShake = new ShakePreset.ShakeSettings
            {
                noiseType = ShakePreset.NoiseType.PerlinNoise,
                amplitude = Vector3.one * positionIntensity,
                frequency = Vector3.one * 2.0f
            };
            tempPreset.rotationShake = new ShakePreset.ShakeSettings
            {
                noiseType = ShakePreset.NoiseType.PerlinNoise,
                amplitude = Vector3.one * rotationIntensity,
                frequency = Vector3.one * 1.8f
            };
            tempPreset.holdDuration = duration;

            PlayShake(tempPreset);
        }
    }
}