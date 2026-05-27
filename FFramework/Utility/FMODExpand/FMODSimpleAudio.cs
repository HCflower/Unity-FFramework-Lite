// =============================================================
// 描述：FMOD 简易音频播放工具 - 包装 PlayOneShot 支持音量设置
// 作者：HCFlower
// 创建时间：2026-05-27 17:43:00
// 版本：1.0.0
// =============================================================
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

namespace FFramework.Utility
{
    /// <summary>
    /// FMOD 简易音频播放工具
    /// 包装 RuntimeManager.PlayOneShot，补充音量设置能力
    /// 仅适用于一次性音效（非循环事件），循环事件请使用 FMODSoundEmitter
    /// </summary>
    public static class FMODSimpleAudio
    {
        /// <summary>
        /// 播放一次性音效，支持音量设置
        /// </summary>
        /// <param name="eventPath">FMOD 事件路径（如 "event:/Hit1"）</param>
        /// <param name="volume">音量 0.0 ~ 1.0，自动钳制</param>
        /// <example>
        /// FMODSimpleAudio.PlayOneShot("event:/Hit1", 0.5f);
        /// </example>
        public static void PlayOneShot(string eventPath, float volume = 1f)
        {
            volume = Mathf.Clamp01(volume);

            EventInstance instance;
            try
            {
                instance = RuntimeManager.CreateInstance(eventPath);
            }
            catch (EventNotFoundException)
            {
                Debug.LogError($"[FMODSimpleAudio] 找不到事件 \"{eventPath}\"");
                return;
            }

            instance.setVolume(volume);
            instance.start();
            // One-shot 事件：release() 不会立即停止播放，而是在音效自然结束后释放
            instance.release();
        }

        /// <summary>
        /// 播放一次性音效（使用 EventReference），支持音量设置
        /// </summary>
        /// <param name="eventRef">FMOD EventReference（Inspector 中拖拽赋值）</param>
        /// <param name="volume">音量 0.0 ~ 1.0，自动钳制</param>
        /// <example>
        /// FMODSimpleAudio.PlayOneShot(fmodEventRef, 0.8f);
        /// </example>
        public static void PlayOneShot(EventReference eventRef, float volume = 1f)
        {
            if (eventRef.IsNull)
            {
                Debug.LogError("[FMODSimpleAudio] EventReference 为空");
                return;
            }

            PlayOneShot(eventRef.ToString(), volume);
        }
    }
}
