using FFramework.Core;

[ConsoleCommand("addHp", "血量增加10")]
public class AddHpCommand : AbstractCommand
{
    [Inject] private GameDataModel gameDataModel;
    protected override void OnExecute()
    {
        gameDataModel.hp.Value += 10;
    }
}

[ConsoleCommand("addAttackPower", "攻击力增加1")]
public class AddAttackPowerCommand : AbstractCommand
{
    [Inject] private GameDataModel gameDataModel;
    protected override void OnExecute()
    {
        gameDataModel.attackPower.Value += 1;
    }
}

[ConsoleCommand("addMoveSpeed", "移动速度增加1")]
public class AddMoveSpeedCommand : AbstractCommand
{
    [Inject] private GameDataModel gameDataModel;
    protected override void OnExecute()
    {
        gameDataModel.moveSpeed.Value += 1;
    }
}

[ConsoleCommand("addLevel", "等级增加1")]
public class AddLevelCommand : AbstractCommand
{
    [Inject] private GameDataModel gameDataModel;
    protected override void OnExecute()
    {
        gameDataModel.level.Value += 1;
    }
}

[ConsoleCommand("addExp", "经验增加100")]
public class AddExpCommand : AbstractCommand
{
    [Inject] private GameDataModel gameDataModel;
    protected override void OnExecute()
    {
        gameDataModel.exp.Value += 100;
    }
}

[ConsoleCommand("addCoin", "金币增加100")]
public class AddCoinCommand : AbstractCommand
{
    [Inject] private GameDataModel gameDataModel;
    protected override void OnExecute()
    {
        gameDataModel.coin.Value += 100;
    }
}






