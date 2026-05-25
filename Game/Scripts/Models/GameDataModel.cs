using FFramework.Core;

/// <summary>
/// 游戏数据模型
/// </summary>
public class GameDataModel : ArchitectureModel
{
    // 玩家可升级的数据
    public BindableProperty<int> maxHp = new BindableProperty<int>(20);
    // 玩家游戏中的数据
    public BindableProperty<int> hp = new BindableProperty<int>(20);
    public BindableProperty<int> attackPower = new BindableProperty<int>(1);
    public BindableProperty<int> moveSpeed = new BindableProperty<int>(1);
    // 资源
    public BindableProperty<int> coin = new BindableProperty<int>(0);
    // Game数据
    public BindableProperty<int> level = new BindableProperty<int>(1);
    public BindableProperty<int> exp = new BindableProperty<int>(0);
}
