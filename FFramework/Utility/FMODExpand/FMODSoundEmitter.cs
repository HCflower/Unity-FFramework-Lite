// =============================================================
// 描述：FMOD 音频发射器 - 支持 3D 空间音频和 2D 全局音频
// 作者：HCFlower
// 创建时间：2026-05-26 23:16:00
// 修改时间：2026-05-26 23:50:00
// 版本：1.2.0
// =============================================================
using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;
using FMOD;

namespace FFramework.Utility
{
    /// <summary>
    /// 音频播放触发模式
    /// </summary>
    public enum AudioPlayTriggerMode
    {
        /// <summary>按进度触发 (0.0 ~ 1.0)</summary>
        Progress,
        /// <summary>按时间触发（秒）</summary>
        Time,
    }

    /// <summary>
    /// 触发点数据
    /// </summary>
    public struct TriggerPoint
    {
        public AudioPlayTriggerMode mode;
        public float value;
        public System.Action callback;

        public TriggerPoint(AudioPlayTriggerMode mode, float value, System.Action callback = null)
        {
            this.mode = mode;
            this.value = value;
            this.callback = callback;
        }
    }

    /// <summary>
    /// 通用 FMOD 音频发射器，支持 3D 空间音频和 2D 全局音频
    /// 支持链式 API、播放触发点、进度查询
    /// </summary>
    public class FMODSoundEmitter : MonoBehaviour
    {
        /// <summary>
        /// FMOD Studio 事件引用（在 Inspector 中拖拽赋值）
        /// </summary>
        public EventReference fmodEvent;

        /// <summary>启用 3D 空间音频（关闭则为 2D 全局音频）</summary>
        [Tooltip("只能3D->2D,不能2D->3D")]
        public bool is3D = true;

        [Range(0f, 1f)]
        /// <summary>默认音量 (0.0 ~ 1.0)</summary>
        public float volume = 1f;

        /// <summary>启动时自动播放</summary>
        public bool playOnAwake = true;

        /// <summary>是否自动循环播放</summary>
        public bool loop = false;

        // 内部触发点列表
        private List<TriggerPoint> triggerPoints = new List<TriggerPoint>();
        // 已触发的标记（按 index 对应）
        private List<bool> triggerFlags = new List<bool>();

        private EventInstance eventInstance;
        private Rigidbody rb;
        private Rigidbody2D rb2d;

        // 缓存的事件总时长（毫秒），避免每帧查询
        private int eventLengthMs = -1;

        #region 状态查询属性

        /// <summary>事件总时长（毫秒），-1 表示尚未获取</summary>
        public int EventLengthMs => eventLengthMs;

        /// <summary>当前播放进度 (0.0 ~ 1.0)，可读写用于拖拽进度</summary>
        public float PlaybackProgress
        {
            get
            {
                if (!eventInstance.isValid() || eventLengthMs <= 0) return 0f;
                eventInstance.getTimelinePosition(out int posMs);
                return Mathf.Clamp01((float)posMs / eventLengthMs);
            }
            set
            {
                if (!eventInstance.isValid() || eventLengthMs <= 0) return;
                int posMs = Mathf.RoundToInt(Mathf.Clamp01(value) * eventLengthMs);
                eventInstance.setTimelinePosition(posMs);
            }
        }

        /// <summary>当前播放位置（毫秒），只读</summary>
        public int TimelinePositionMs
        {
            get
            {
                if (!eventInstance.isValid()) return 0;
                eventInstance.getTimelinePosition(out int posMs);
                return posMs;
            }
        }

        /// <summary>是否正在播放中</summary>
        public bool IsPlaying
        {
            get
            {
                if (!eventInstance.isValid()) return false;
                eventInstance.getPlaybackState(out PLAYBACK_STATE state);
                return state == PLAYBACK_STATE.PLAYING;
            }
        }

        /// <summary>是否已暂停</summary>
        public bool IsPaused
        {
            get
            {
                if (!eventInstance.isValid()) return false;
                eventInstance.getPaused(out bool paused);
                return paused;
            }
        }

        /// <summary>EventInstance 是否有效（已创建且未释放）</summary>
        public bool IsValid => eventInstance.isValid();

        /// <summary>当前正在播放的音频事件路径（动态路径或 fmodEvent 的路径）</summary>
        public string CurrentEventPath { get; private set; } = "";

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            // 缓存物理组件引用，避免每帧重复 GetComponent 查询
            rb = GetComponent<Rigidbody>();
            rb2d = GetComponent<Rigidbody2D>();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Inspector 参数变化时实时同步到音频
            volume = Mathf.Clamp01(volume);

            if (Application.isPlaying && eventInstance.isValid())
            {
                eventInstance.setVolume(volume);
            }
        }
#endif

        void Start()
        {
            if (fmodEvent.IsNull)
            {
                UnityEngine.Debug.LogWarning($"[{name}] FMODSoundEmitter: fmodEvent 未赋值，跳过创建", this);
                return;
            }

            if (playOnAwake)
            {
                Play();
            }
        }

        void Update()
        {
            if (!eventInstance.isValid()) return;

            // 3D 音频：每帧同步位置；2D 音频跳过
            if (is3D) Set3DAttributes();

            // 循环检测：如果事件自然结束且 loop 开启，自动重新播放
            if (loop)
            {
                eventInstance.getPlaybackState(out PLAYBACK_STATE state);
                if (state == PLAYBACK_STATE.STOPPED)
                {
                    ResetTriggers();
                    eventInstance.start();
                }
            }

            // 每帧仅调用一次 getTimelinePosition + PlaybackProgress，避免多次 FMOD 跨域调用
            eventInstance.getTimelinePosition(out int posMs);
            float progress = eventLengthMs > 0 ? Mathf.Clamp01((float)posMs / eventLengthMs) : 0f;

            CheckTriggers(progress, posMs);
        }

        void OnDestroy()
        {
            ReleaseInstance();
        }

        #endregion

        #region 外部控制 API

        /// <summary>播放 / 重新播放</summary>
        /// <param name="eventPath">动态指定事件路径（如 "event:/BGM"），为 null 时使用 Inspector 中的 fmodEvent</param>
        /// <returns>返回自身，支持链式调用</returns>
        public FMODSoundEmitter Play(string eventPath = null)
        {
            // 确定使用哪个事件源
            bool useDynamicPath = !string.IsNullOrEmpty(eventPath);
            bool useInspectorEvent = !useDynamicPath && !fmodEvent.IsNull;

            if (!useDynamicPath && !useInspectorEvent)
            {
                UnityEngine.Debug.LogWarning($"[{name}] FMODSoundEmitter: 未指定播放事件", this);
                return this;
            }

            // 动态路径：每次调用都创建新实例（不同音频不同事件）
            if (useDynamicPath)
            {
                ReleaseInstance();
                ClearTriggers();
                CurrentEventPath = eventPath;

                try
                {
                    eventInstance = RuntimeManager.CreateInstance(eventPath);
                }
                catch (EventNotFoundException)
                {
                    UnityEngine.Debug.LogError($"[{name}] FMODSoundEmitter: 找不到事件 \"{eventPath}\"", this);
                    return this;
                }
            }
            else
            {
                // Inspector 事件：复用已有实例
                if (eventInstance.isValid())
                {
                    eventInstance.getPlaybackState(out PLAYBACK_STATE state);
                    if (state == PLAYBACK_STATE.PLAYING)
                        return this;

                    if (state == PLAYBACK_STATE.STOPPED)
                    {
                        ResetTriggers();
                        Set3DAttributes();
                        eventInstance.setVolume(volume);
                        eventInstance.start();
                        return this;
                    }
                }

                ReleaseInstance();
                ResetTriggers();
                CurrentEventPath = fmodEvent.ToString();
                eventInstance = RuntimeManager.CreateInstance(fmodEvent);
            }

            // 获取事件总时长用于进度显示
            eventInstance.getDescription(out EventDescription desc);
            desc.getLength(out eventLengthMs);
            Set3DAttributes();
            eventInstance.setVolume(volume);
            eventInstance.start();

            return this;
        }

        /// <summary>停止播放</summary>
        /// <param name="mode">停止模式（默认淡出）</param>
        public void Stop(FMOD.Studio.STOP_MODE mode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
        {
            if (!eventInstance.isValid()) return;
            eventInstance.stop(mode);
        }

        /// <summary>暂停播放</summary>
        public void Pause()
        {
            if (!eventInstance.isValid()) return;
            eventInstance.setPaused(true);
        }

        /// <summary>恢复播放</summary>
        public void Resume()
        {
            if (!eventInstance.isValid()) return;
            eventInstance.setPaused(false);
        }

        /// <summary>切换 暂停/恢复</summary>
        public void TogglePause()
        {
            if (!eventInstance.isValid()) return;
            eventInstance.getPaused(out bool paused);
            eventInstance.setPaused(!paused);
        }

        /// <summary>设置音量 (0.0 ~ 1.0)</summary>
        /// <param name="newVolume">音量值，自动钳制到 0~1</param>
        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (eventInstance.isValid())
            {
                eventInstance.setVolume(volume);
            }
        }

        /// <summary>设置是否循环播放</summary>
        public void SetLoop(bool loopEnabled)
        {
            loop = loopEnabled;
        }

        #region 播放触发事件 API

        /// <summary>到达触发点时发出的事件</summary>
        public event System.Action<TriggerPoint> OnReached;

        /// <summary>当前注册的触发点列表（Editor 绘制用）</summary>
        public IReadOnlyList<TriggerPoint> TriggerPoints => triggerPoints;

        /// <summary>添加一个触发点，到达时自动执行 callback</summary>
        /// <returns>返回自身，支持链式调用</returns>
        public FMODSoundEmitter AddTrigger(AudioPlayTriggerMode mode, float value, System.Action callback = null)
        {
            var point = new TriggerPoint(mode, value, callback);
            triggerPoints.Add(point);
            triggerFlags.Add(false);
            return this;
        }

        /// <summary>移除所有触发点</summary>
        /// <returns>返回自身，支持链式调用</returns>
        public FMODSoundEmitter ClearTriggers()
        {
            triggerPoints.Clear();
            triggerFlags.Clear();
            return this;
        }

        /// <summary>重置所有触发状态（每次播放或循环时自动调用）</summary>
        /// <returns>返回自身，支持链式调用</returns>
        public FMODSoundEmitter ResetTriggers()
        {
            for (int i = 0; i < triggerFlags.Count; i++)
                triggerFlags[i] = false;
            return this;
        }

        #endregion

        #endregion

        #region 触发点检测

        private void CheckTriggers(float progress, int positionMs)
        {
            if (triggerPoints.Count == 0) return;

            for (int i = 0; i < triggerPoints.Count; i++)
            {
                if (triggerFlags[i]) continue;

                float current = triggerPoints[i].mode == AudioPlayTriggerMode.Progress
                    ? progress
                    : positionMs / 1000f;

                if (current >= triggerPoints[i].value)
                {
                    triggerFlags[i] = true;
                    var point = triggerPoints[i];
                    point.callback?.Invoke();
                    OnReached?.Invoke(point);
                }
            }
        }

        #endregion

        #region 内部方法

        private void Set3DAttributes()
        {
            // 使用 FMOD 内置方法转换物体的位置、朝向、速度到 3D 属性
            // 优先级：3D Rigidbody > 2D Rigidbody2D > 仅 Transform
            ATTRIBUTES_3D attributes;
            if (rb != null)
                attributes = RuntimeUtils.To3DAttributes(gameObject, rb);
            else if (rb2d != null)
                attributes = RuntimeUtils.To3DAttributes(gameObject, rb2d);
            else
                attributes = RuntimeUtils.To3DAttributes(gameObject);

            eventInstance.set3DAttributes(attributes);
        }

        private void ReleaseInstance()
        {
            if (eventInstance.isValid())
            {
                eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                eventInstance.release();
            }
        }

        #endregion
    }
}