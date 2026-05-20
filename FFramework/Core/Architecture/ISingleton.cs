// =============================================================
// 描述：单例生命周期接口 - 提供统一的初始化/销毁契约
// 作者：HCFlower
// 创建时间：2026-05-12
// 版本：2.0.0
// 修改：
//   v2.0.0 - 移除 Priority 属性（所有单例使用懒加载，不再需要按优先级有序初始化）
// =============================================================

namespace FFramework.Core
{
    /// <summary>
    /// 单例生命周期接口。
    /// 实现此接口的类型可通过 Architecture 注册，获得依赖注入和生命周期管理能力。
    /// 单例采用懒加载模式，首次访问 Instance 时自行初始化，不再需要 Priority 排序。
    /// </summary>
    public interface ISingleton
    {
        /// <summary>
        /// 单例初始化时调用。
        /// 此时 [Inject] 依赖已注入完成。
        /// </summary>
        void OnSingletonInit();

        /// <summary>
        /// 单例销毁时调用（由 Architecture.UnloadAll 触发）。
        /// </summary>
        void OnSingletonDispose();
    }
}
