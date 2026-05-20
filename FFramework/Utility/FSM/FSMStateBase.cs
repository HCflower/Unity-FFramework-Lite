// =============================================================
// 描述：状态基类
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.0
// =============================================================
namespace FFramework.Utility
{
    public abstract class FSMStateBase : IFSMState
    {
        /// <summary>
        /// 状态持有者（可以是任何类型）
        /// </summary>
        protected object owner;

        /// <summary>
        /// 获取强类型的持有者
        /// </summary>
        /// <typeparam name="T">持有者类型</typeparam>
        /// <returns>强类型持有者</returns>
        protected T GetOwner<T>() where T : class
        {
            return owner as T;
        }

        /// <summary>
        /// 内部初始化方法（由状态机调用）
        /// </summary>
        internal void InternalInit(object owner)
        {
            this.owner = owner;
            OnInit();
        }

        /// <summary>
        /// 状态初始化（子类可重写）
        /// </summary>
        protected virtual void OnInit() { }

        // 状态机事件
        public abstract void OnEnter(FSMStateMachine machine);
        public abstract void OnUpdate(FSMStateMachine machine);
        public abstract void OnExit(FSMStateMachine machine);

        // 虚方法
        public virtual void OnFixedUpdate(FSMStateMachine machine) { }
        public virtual void OnLateUpdate(FSMStateMachine machine) { }
    }
}