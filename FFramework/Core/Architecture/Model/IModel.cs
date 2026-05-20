// =============================================================
// 描述：数据模型接口
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：2.0.0
// =============================================================
namespace FFramework.Core
{
    public interface IModel : ICommandSender
    {
        /// <summary>
        /// 初始化模型
        /// </summary>
        void Initialize();

        /// <summary>
        /// 销毁模型
        /// </summary>
        void Dispose();

        /// <summary>
        /// 保存模型数据到指定存档
        /// </summary>
        /// <param name="slotName">存档文件夹名称，由调用方自定义</param>
        void SaveData(string slotName);

        /// <summary>
        /// 从指定存档加载模型数据
        /// </summary>
        /// <param name="slotName">存档文件夹名称，与保存时传入的名称一致</param>
        void LoadData(string slotName);
    }
}
