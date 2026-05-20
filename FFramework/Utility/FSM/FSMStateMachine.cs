// =============================================================
// 描述：状态机类
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.0
// =============================================================
using System.Collections.Generic;
using System;

namespace FFramework.Utility
{
    public class FSMStateMachine
    {
        // 当前状态
        private IFSMState currentState;
        // 状态机的拥有者
        private object owner;
        // 存储状态实例的字典
        private Dictionary<Type, IFSMState> stateCache = new Dictionary<Type, IFSMState>();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">状态机持有者</param>
        public FSMStateMachine(object owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// 获取持有者
        /// </summary>
        /// <typeparam name="T">持有者类型</typeparam>
        /// <returns>强类型持有者</returns>
        public T GetOwner<T>() where T : class
        {
            return owner as T;
        }

        /// <summary>
        /// 设置默认状态
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        public void SetDefault<TState>() where TState : IFSMState, new()
        {
            var stateType = typeof(TState);
            if (!stateCache.TryGetValue(stateType, out var defaultState))
            {
                defaultState = new TState();
                InitializeState(defaultState);
                stateCache[stateType] = defaultState;
            }
            currentState = defaultState;
            currentState?.OnEnter(this);
        }

        /// <summary>
        /// 设置默认状态
        /// </summary>
        /// <param name="defaultState">默认状态实例</param>
        public void SetDefault(IFSMState defaultState)
        {
            if (defaultState == null) return;

            InitializeState(defaultState);
            var stateType = defaultState.GetType();
            stateCache[stateType] = defaultState;
            currentState = defaultState;
            currentState.OnEnter(this);
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        public void ChangeState<TState>() where TState : IFSMState, new()
        {
            var stateType = typeof(TState);
            if (currentState != null && currentState.GetType() == stateType) return;

            if (!stateCache.TryGetValue(stateType, out var newState))
            {
                newState = new TState();
                InitializeState(newState);
                stateCache[stateType] = newState;
            }

            currentState?.OnExit(this);
            currentState = newState;
            currentState.OnEnter(this);
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        /// <param name="newState">新状态实例</param>
        public void ChangeState(IFSMState newState)
        {
            if (currentState == newState || newState == null) return;

            InitializeState(newState);
            var stateType = newState.GetType();
            stateCache[stateType] = newState;

            currentState?.OnExit(this);
            currentState = newState;
            currentState.OnEnter(this);
        }

        /// <summary>
        /// 初始化状态（设置持有者）
        /// </summary>
        /// <param name="state">状态实例</param>
        private void InitializeState(IFSMState state)
        {
            if (state is FSMStateBase stateBase)
            {
                stateBase.InternalInit(owner);
            }
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <returns>当前状态（如果类型匹配）</returns>
        public TState GetCurrentState<TState>() where TState : class, IFSMState
        {
            return currentState as TState;
        }

        /// <summary>
        /// 检查当前是否为指定状态
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <returns>是否为指定状态</returns>
        public bool IsCurrentState<TState>() where TState : IFSMState
        {
            return currentState != null && currentState.GetType() == typeof(TState);
        }

        // 手动调用更新
        public void Update() => currentState?.OnUpdate(this);
        // 固定更新
        public void FixedUpdate() => currentState?.OnFixedUpdate(this);
        // 延迟更新
        public void LateUpdate() => currentState?.OnLateUpdate(this);
        // 获取当前状态类型（用于调试）
        public Type GetCurrentStateType() => currentState?.GetType();
        // 获取当前状态名称
        public string GetCurrentStateName() => currentState?.GetType().Name ?? "None";
    }
}