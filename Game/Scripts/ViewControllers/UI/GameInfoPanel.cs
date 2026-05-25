using FFramework.Core;
using FFramework.Utility;
using TMPro;

public class GameInfoPanel : UIPanel
{
    // 信息显示
    public TextMeshProUGUI hpTMP;           // 血量
    public TextMeshProUGUI attackPowerTMP;  // 攻击力
    public TextMeshProUGUI moveSpeedTMP;    // 移动速度
    public TextMeshProUGUI levelTMP;        // 等级
    public TextMeshProUGUI expTMP;          // 经验
    public TextMeshProUGUI coinTMP;         // 金币

    [Inject] private GameDataModel gameDataModel;

    protected override void OnInitialize()
    {
        gameDataModel.hp.Register((value) =>
        {
            hpTMP.gameObject.GetComponent<LocalizationComponent>().SetFormatArg(0, $"{value.ToString()}/{gameDataModel.maxHp.Value.ToString()}");
        }, this);

        // 注册攻击力变化事件
        gameDataModel.attackPower.Register((value) =>
        {
            attackPowerTMP.gameObject.GetComponent<LocalizationComponent>().SetFormatArg(0, value.ToString());
        }, this);

        // 注册移动速度变化事件
        gameDataModel.moveSpeed.Register((value) =>
        {
            moveSpeedTMP.gameObject.GetComponent<LocalizationComponent>().SetFormatArg(0, value.ToString());
        }, this);

        // 注册等级变化事件
        gameDataModel.level.Register((value) =>
        {
            levelTMP.gameObject.GetComponent<LocalizationComponent>().SetFormatArg(0, value.ToString());
        }, this);

        // 注册经验变化事件
        gameDataModel.exp.Register((value) =>
        {
            expTMP.gameObject.GetComponent<LocalizationComponent>().SetFormatArg(0, $"{value.ToString()}/{(gameDataModel.level.Value * 10).ToString()}");
        }, this);

        // 注册金币变化事件
        gameDataModel.coin.Register((value) =>
        {
            coinTMP.gameObject.GetComponent<LocalizationComponent>().SetFormatArg(0, value.ToString());
        }, this);
    }
}
