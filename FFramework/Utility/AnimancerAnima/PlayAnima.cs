// =============================================================
// 描述：Animancer动画播放类
// 作者：HCFlower
// 创建时间：2026-02-02 18:33:35
// 版本：2.0.0
// 功能描述：提供Animancer动画播放功能
// =============================================================
using System.Collections.Generic;
using UnityEngine;
using Animancer;
using System;

namespace FFramework.Utility
{
    /// <summary>
    /// 动画播放配置参数
    /// </summary>
    public class AnimaArgs
    {
        /// <summary>
        /// 动画片段
        /// </summary>
        public AnimationClip Clip;

        /// <summary>
        /// 动画过渡时间
        /// </summary>
        public float TransitionTime = 0.15f;

        /// <summary>
        /// 动画过渡时间的计算方式
        /// </summary>
        /// <remarks>
        /// <para>FixedDuration - 固定过渡时间</para>
        /// <para>NormalizedDuration - 归一化过渡时间</para>
        /// <para>FixedSpeed - 固定过渡速度</para>
        /// <para>NormalizedSpeed - 归一化过渡速度</para>
        /// <para>FromStart - 从开始播放</para>
        /// <para>NormalizedFromStart - 归一化从开始播放</para>
        /// </remarks>
        public FadeMode FadeMode = FadeMode.FixedDuration;

        /// <summary>
        /// 动画开始播放的时间
        /// </summary>
        public float StartTime = 0.0f;

        /// <summary>
        /// 动画播放速度
        /// </summary>
        public float Speed = 1.0f;

        /// <summary>
        /// 动画播放结束回调
        /// </summary>
        public Action OnEnd = null;

        /// <summary>
        /// 定时事件列表（归一化时间 + 回调），存储在 AnimaArgs 上可复用
        /// </summary>
        public List<(float normalizedTime, Action callback)> TimedEvents { get; private set; } = new List<(float, Action)>();

        public AnimaArgs(AnimationClip clip, float transitionTime = 0.15f, FadeMode fadeMode = FadeMode.FixedDuration, float startTime = 0.0f, float speed = 1.0f, Action onEnd = null)
        {
            Clip = clip;
            TransitionTime = transitionTime;
            FadeMode = fadeMode;
            StartTime = startTime;
            Speed = speed;
            OnEnd = onEnd;
        }

        /// <summary>
        /// 添加一个定时事件（归一化时间 0.0 ~ 1.0）
        /// </summary>
        /// <returns>返回自身，支持链式调用</returns>
        public AnimaArgs AddEvent(float normalizedTime, Action callback)
        {
            if (normalizedTime >= 0f && callback != null)
                TimedEvents.Add((normalizedTime, callback));
            return this;
        }

        /// <summary>
        /// 清除所有定时事件
        /// </summary>
        public AnimaArgs ClearTimedEvents()
        {
            TimedEvents.Clear();
            return this;
        }
    }

    /// <summary>
    /// 播放动画组件
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(AnimancerComponent))]
    public class PlayAnima : MonoBehaviour
    {
        // Animancer动画控制组件（已缓存）
        private AnimancerComponent animancer;

        // 当前播放状态缓存（Editor 查询用）
        private AnimancerState currentState;
        // 暂停前速度（恢复时使用）
        private float pauseSpeed;
        // 组件级循环标记
        private bool loopRequested = false;
        // 上次播放的参数（循环重播时使用）
        private AnimaArgs lastArgs;
        // 缓存的 EventPoints 数组，避免每帧 GC 分配
        private float[] cachedEventPoints;
        private int cachedEventPointsHash;

        private void Awake()
        {
            animancer = GetComponent<AnimancerComponent>();
        }

        #region 状态查询属性

        /// <summary>是否正在播放中</summary>
        public bool IsPlaying => currentState != null && currentState.IsValid() && currentState.IsPlaying;

        /// <summary>是否已暂停（Speed == 0 且权重 > 0）</summary>
        public bool IsPaused => currentState != null && currentState.IsValid() && currentState.Weight > 0f && Mathf.Approximately(currentState.Speed, 0f);

        /// <summary>当前是否有有效的动画状态</summary>
        public bool IsValid => currentState != null && currentState.IsValid() && currentState.IsPlaying;

        /// <summary>是否循环播放</summary>
        public bool IsLooping => currentState != null && currentState.IsValid() && currentState.IsLooping;

        /// <summary>当前播放的定时事件进度点（Editor 锚点绘制用），缓存避免 GC 分配</summary>
        public IReadOnlyList<float> EventPoints
        {
            get
            {
                if (lastArgs == null || lastArgs.TimedEvents.Count == 0)
                    return System.Array.Empty<float>();

                int hash = lastArgs.GetHashCode();
                if (cachedEventPoints != null && cachedEventPoints.Length == lastArgs.TimedEvents.Count && cachedEventPointsHash == hash)
                    return cachedEventPoints;

                cachedEventPoints = new float[lastArgs.TimedEvents.Count];
                for (int i = 0; i < cachedEventPoints.Length; i++)
                    cachedEventPoints[i] = lastArgs.TimedEvents[i].normalizedTime;
                cachedEventPointsHash = hash;
                return cachedEventPoints;
            }
        }

        /// <summary>当前播放进度 (0.0 ~ 1.0)，可读写用于拖拽进度。循环动画自动取余，始终反映当前循环周期内的进度</summary>
        public float PlaybackProgress
        {
            get
            {
                if (currentState == null || !currentState.IsValid()) return 0f;
                float len = currentState.Length;
                if (len <= 0f) return 0f;
                float t = currentState.Time;
                if (currentState.IsLooping)
                    t = t % len;
                return Mathf.Clamp01(t / len);
            }
            set
            {
                if (currentState == null || !currentState.IsValid()) return;
                currentState.Time = Mathf.Clamp01(value) * currentState.Length;
            }
        }

        /// <summary>当前播放时间（秒）。循环动画返回当前周期内的时间，非循环动画钳制到总时长</summary>
        public float CurrentTime
        {
            get
            {
                if (currentState == null || !currentState.IsValid()) return 0f;
                float len = currentState.Length;
                if (len <= 0f) return 0f;
                float t = currentState.Time;
                if (currentState.IsLooping)
                    t = t % len;
                else
                    t = Mathf.Min(t, len);
                return t;
            }
        }

        /// <summary>动画总时长（秒）</summary>
        public float TotalDuration => (currentState != null && currentState.IsValid()) ? currentState.Length : 0f;

        /// <summary>当前动画片段名称</summary>
        public string CurrentClipName => currentState?.Clip?.name ?? "";

        #endregion

        #region 播放控制 API

        /// <summary>设置播放速度，对当前播放中的动画立即生效。播放前调用则影响下一次播放</summary>
        /// <returns>返回自身，支持链式调用</returns>
        public PlayAnima SetSpeed(float speed)
        {
            if (currentState != null && currentState.IsValid())
            {
                currentState.Speed = speed;
                pauseSpeed = speed;
            }
            return this;
        }

        /// <summary>设置是否循环播放（组件级循环检测）</summary>
        /// <returns>返回自身，支持链式调用</returns>
        public PlayAnima SetLoop(bool loop)
        {
            loopRequested = loop;
            return this;
        }

        /// <summary>暂停播放</summary>
        public void Pause()
        {
            if (currentState == null || currentState.Speed <= 0f) return;
            pauseSpeed = currentState.Speed;
            currentState.Speed = 0f;
        }

        /// <summary>恢复播放</summary>
        public void Resume()
        {
            if (currentState == null || currentState.Speed > 0f) return;
            currentState.Speed = pauseSpeed > 0f ? pauseSpeed : 1f;
        }

        /// <summary>停止播放并清理状态</summary>
        public void Stop()
        {
            if (currentState != null)
            {
                currentState.Stop();
                currentState = null;
            }
        }

        #endregion

        void Update()
        {
            if (currentState == null || !currentState.IsValid()) { currentState = null; return; }

            // 组件级循环检测：动画自然结束后自动重播
            if (loopRequested && !currentState.IsPlaying && lastArgs != null)
            {
                InternalPlay(lastArgs);
                return;
            }
        }

        /// <summary>
        /// 核心播放逻辑，处理通用参数应用
        /// </summary>
        private AnimancerState InternalPlay(AnimaArgs args)
        {
            if (args.Clip == null) return null;

            // 播放动画并缓存状态引用
            AnimancerState state = animancer.Play(args.Clip, args.TransitionTime, args.FadeMode);
            state.Time = args.StartTime;
            state.Speed = args.Speed;

            // 获取并清理旧事件（防止旧状态的事件残留）
            var events = state.Events(this);
            events.Clear();
            events.OnEnd = args.OnEnd;

            // 自动绑定 AnimaArgs 上的定时事件（一次配置，多次复用）
            if (args.TimedEvents.Count > 0)
            {
                foreach (var (time, callback) in args.TimedEvents)
                {
                    state.Events(this).Add(time, callback);
                }
            }

            // 缓存当前状态供 Editor 查询
            currentState = state;
            pauseSpeed = args.Speed;
            lastArgs = args; // 缓存参数供循环重播

            return state;
        }

        /// <summary>
        /// 播放动画片段
        /// </summary>
        public void PlayAnimaClip(AnimaArgs args)
        {
            InternalPlay(args);
        }

        /// <summary>
        /// 播放动画片段 - 带单个动画事件触发
        /// </summary>
        public void PlayAnimaWithEvent(AnimaArgs args, float triggerTime, Action onTrigger)
        {
            var state = InternalPlay(args);
            if (state == null) return;

            if (triggerTime >= 0 && onTrigger != null)
            {
                state.Events(this).Add(triggerTime, onTrigger);
            }
        }

        /// <summary>
        /// 播放动画片段 - 带多个动画事件触发
        /// </summary>
        public void PlayAnimaWithEvents(AnimaArgs args, IEnumerable<(float normalizedTime, Action callback)> triggerEvents)
        {
            var state = InternalPlay(args);
            if (state == null) return;

            if (triggerEvents != null)
            {
                var events = state.Events(this);
                foreach (var (time, callback) in triggerEvents)
                {
                    if (time >= 0 && callback != null)
                    {
                        events.Add(time, callback);
                    }
                }
            }
        }

        #region 为了兼容旧代码的重载（可选）

        public void PlayAnimaClip(AnimationClip animaClip, float transitionTime = 0.15f, FadeMode fadeMode = FadeMode.FixedDuration, float startTime = 0.0f, float animaPlaySpeed = 1.0f, Action onEnd = null)
        {
            PlayAnimaClip(new AnimaArgs(animaClip, transitionTime, fadeMode, startTime, animaPlaySpeed, onEnd));
        }

        #endregion
    }
}