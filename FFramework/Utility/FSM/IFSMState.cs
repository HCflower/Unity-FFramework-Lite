// =============================================================
// 描述：状态接口类
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.0
// =============================================================
namespace FFramework.Utility
{
    public interface IFSMState
    {
        void OnEnter(FSMStateMachine machine);         //当状态进入时调用
        void OnUpdate(FSMStateMachine machine);        //当状态更新时调用
        void OnFixedUpdate(FSMStateMachine machine);   //当状态固定更新时调用
        void OnLateUpdate(FSMStateMachine machine);    //当状态延迟更新时调用
        void OnExit(FSMStateMachine machine);          //当状态退出时调用
    }
}