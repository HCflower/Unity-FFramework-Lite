// =============================================================
// 描述：Animancer动画播放类
// 作者：HCFlower
// 创建时间：2026-02-02 18:33:35
// 版本：1.0.1
// 更新记录：缓存 AnimancerComponent 引用，避免反复 GetComponent
// =============================================================
using System.Collections.Generic;
using UnityEngine;
using Animancer;
using System;

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

    public AnimaArgs(AnimationClip clip, float transitionTime = 0.15f, FadeMode fadeMode = FadeMode.FixedDuration, float startTime = 0.0f, float speed = 1.0f, Action onEnd = null)
    {
        Clip = clip;
        TransitionTime = transitionTime;
        FadeMode = fadeMode;
        StartTime = startTime;
        Speed = speed;
        OnEnd = onEnd;
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

    private void Awake()
    {
        animancer = GetComponent<AnimancerComponent>();
    }

    /// <summary>
    /// 核心播放逻辑，处理通用参数应用
    /// </summary>
    private AnimancerState InternalPlay(AnimaArgs args)
    {
        if (args.Clip == null) return null;

        // 播放动画
        AnimancerState state = animancer.Play(args.Clip, args.TransitionTime, args.FadeMode);
        state.Time = args.StartTime;
        state.Speed = args.Speed;

        // 获取并清理旧事件（防止旧状态的事件残留）
        var events = state.Events(this);
        events.Clear();
        events.OnEnd = args.OnEnd;

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