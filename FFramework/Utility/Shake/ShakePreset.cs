// =============================================================
// 描述：震动配置
// 作者：HCFlower
// 创建时间：2025-11-16 00:44:00
// 版本：1.0.0
// =============================================================
using UnityEngine;

namespace FFramework.Utility
{
    /// <summary>
    /// 震动设置ScriptableObject
    /// 可以创建可复用的震动配置预设
    /// </summary>
    [CreateAssetMenu(fileName = "ShakePreset", menuName = "FFramework/Shake/Shake Preset", order = 1)]
    public class ShakePreset : ScriptableObject
    {
        [Header("基本信息")]
        [Tooltip("震动预设名称")]
        public string presetName = "新震动预设";

        [Tooltip("震动预设描述")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("震动设置")]
        [Tooltip("位置震动设置")]
        public ShakeSettings positionShake = new ShakeSettings();

        [Tooltip("旋转震动设置")]
        public ShakeSettings rotationShake = new ShakeSettings();

        [Header("时间设置")]
        [Tooltip("淡入时长")]
        [Range(0f, 2f)]
        public float fadeInDuration = 0.1f;

        [Tooltip("保持时长")]
        [Range(0f, 5f)]
        public float holdDuration = 0.5f;

        [Tooltip("淡出时长")]
        [Range(0f, 2f)]
        public float fadeOutDuration = 0.2f;

        [Header("时间曲线")]
        [Tooltip("淡入曲线")]
        public AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("淡出曲线")]
        public AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        /// <summary>
        /// 震动设置数据
        /// </summary>
        [System.Serializable]
        public class ShakeSettings
        {
            [Tooltip("震动类型")]
            public NoiseType noiseType = NoiseType.SineWave;

            [Tooltip("震动强度")]
            public Vector3 amplitude = Vector3.one;

            [Tooltip("震动频率")]
            public Vector3 frequency = Vector3.one;

            public ShakeSettings()
            {
                amplitude = Vector3.one;
                frequency = Vector3.one;
            }

            public ShakeSettings(Vector3 amp, Vector3 freq)
            {
                amplitude = amp;
                frequency = freq;
            }

            /// <summary>
            /// 计算震动值
            /// </summary>
            public Vector3 Evaluate(float time)
            {
                Vector3 result;
                result.x = EvaluateAxis(time, amplitude.x, frequency.x);
                result.y = EvaluateAxis(time, amplitude.y, frequency.y);
                result.z = EvaluateAxis(time, amplitude.z, frequency.z);
                return result;
            }

            private float EvaluateAxis(float time, float amp, float freq)
            {
                return noiseType switch
                {
                    NoiseType.SineWave => amp * Mathf.Sin(2 * Mathf.PI * freq * time),
                    NoiseType.WhiteNoise => amp * UnityEngine.Random.Range(-1f, 1f),
                    NoiseType.PerlinNoise => amp * (Mathf.PerlinNoise(freq * time, 0) * 2 - 1),
                    NoiseType.Cosine => amp * Mathf.Cos(2 * Mathf.PI * freq * time),
                    _ => 0f
                };
            }
        }

        /// <summary>
        /// 噪声类型
        /// </summary>
        public enum NoiseType
        {
            SineWave,       // 正弦波
            WhiteNoise,     // 白噪声
            PerlinNoise,    // 柏林噪声
            Cosine          // 余弦波
        }

        /// <summary>
        /// 获取总震动时长
        /// </summary>
        public float TotalDuration => fadeInDuration + holdDuration + fadeOutDuration;

        /// <summary>
        /// 应用设置到震动组件
        /// </summary>
        /// <param name="shakeBase">目标震动组件</param>
        public void ApplyToShakeComponent(ShakeBase shakeBase)
        {
            if (shakeBase == null) return;

            // 直接设置预设引用，让组件使用这个SO文件的数据
            shakeBase.shakePreset = this;
        }

        /// <summary>
        /// 从震动组件复制设置
        /// </summary>
        /// <param name="shakeBase">源震动组件</param>
        public void CopyFromShakeComponent(ShakeBase shakeBase)
        {
            if (shakeBase == null || shakeBase.shakePreset == null) return;

            var sourcePreset = shakeBase.shakePreset;

            positionShake.noiseType = sourcePreset.positionShake.noiseType;
            positionShake.amplitude = sourcePreset.positionShake.amplitude;
            positionShake.frequency = sourcePreset.positionShake.frequency;

            rotationShake.noiseType = sourcePreset.rotationShake.noiseType;
            rotationShake.amplitude = sourcePreset.rotationShake.amplitude;
            rotationShake.frequency = sourcePreset.rotationShake.frequency;

            fadeInDuration = sourcePreset.fadeInDuration;
            holdDuration = sourcePreset.holdDuration;
            fadeOutDuration = sourcePreset.fadeOutDuration;
            fadeInCurve = sourcePreset.fadeInCurve;
            fadeOutCurve = sourcePreset.fadeOutCurve;
        }

        /// <summary>
        /// 创建预设震动设置的静态方法
        /// </summary>
        public static class Presets
        {
            /// <summary>
            /// 轻微震动预设
            /// </summary>
            public static ShakePreset CreateLightShake()
            {
                var preset = CreateInstance<ShakePreset>();
                preset.presetName = "轻微震动";
                preset.description = "适用于轻微碰撞或小型特效";

                preset.positionShake.amplitude = new Vector3(0.05f, 0.05f, 0f);
                preset.positionShake.frequency = Vector3.one * 2f;
                preset.rotationShake.amplitude = new Vector3(0.5f, 0.5f, 0.2f);
                preset.rotationShake.frequency = Vector3.one * 3f;

                preset.holdDuration = 0.2f;

                return preset;
            }

            /// <summary>
            /// 中等震动预设
            /// </summary>
            public static ShakePreset CreateMediumShake()
            {
                var preset = CreateInstance<ShakePreset>();
                preset.presetName = "中等震动";
                preset.description = "适用于普通攻击或中型爆炸特效";

                preset.positionShake.amplitude = new Vector3(0.1f, 0.1f, 0f);
                preset.positionShake.frequency = Vector3.one * 2f;
                preset.rotationShake.amplitude = new Vector3(1f, 1f, 0.5f);
                preset.rotationShake.frequency = Vector3.one * 3f;

                preset.holdDuration = 0.3f;

                return preset;
            }

            /// <summary>
            /// 强烈震动预设
            /// </summary>
            public static ShakePreset CreateHeavyShake()
            {
                var preset = CreateInstance<ShakePreset>();
                preset.presetName = "强烈震动";
                preset.description = "适用于大型爆炸或重击特效";

                preset.positionShake.amplitude = new Vector3(0.2f, 0.2f, 0.05f);
                preset.positionShake.frequency = Vector3.one * 2f;
                preset.rotationShake.amplitude = new Vector3(2f, 2f, 1f);
                preset.rotationShake.frequency = Vector3.one * 3f;

                preset.holdDuration = 0.5f;

                return preset;
            }

            /// <summary>
            /// 持续震动预设（如地震效果）
            /// </summary>
            public static ShakePreset CreateContinuousShake()
            {
                var preset = CreateInstance<ShakePreset>();
                preset.presetName = "持续震动";
                preset.description = "适用于地震或持续性环境效果";

                preset.positionShake.amplitude = new Vector3(0.08f, 0.08f, 0.02f);
                preset.positionShake.frequency = Vector3.one * 1f;
                preset.positionShake.noiseType = NoiseType.PerlinNoise;

                preset.rotationShake.amplitude = new Vector3(0.8f, 0.8f, 0.4f);
                preset.rotationShake.frequency = Vector3.one * 1.5f;
                preset.rotationShake.noiseType = NoiseType.PerlinNoise;

                preset.fadeInDuration = 0.5f;
                preset.holdDuration = 2f;
                preset.fadeOutDuration = 0.5f;

                return preset;
            }
        }

        private void OnValidate()
        {
            // 确保时长不为负数
            fadeInDuration = Mathf.Max(0, fadeInDuration);
            holdDuration = Mathf.Max(0, holdDuration);
            fadeOutDuration = Mathf.Max(0, fadeOutDuration);
        }
    }
}
