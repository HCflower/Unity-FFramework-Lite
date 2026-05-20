// =============================================================
// 描述：视图控制器接口
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.0
// =============================================================
using UnityEngine;

namespace FFramework.Core
{
    public interface IViewController : ICommandSender
    {
        /// <summary>
        /// 初始化视图控制器
        /// </summary>
        void Initialize();

        /// <summary>
        /// 销毁视图控制器
        /// </summary>
        void Dispose();

        /// <summary>
        /// 绑定的GameObject
        /// </summary>
        GameObject GameObject { get; }
    }
}