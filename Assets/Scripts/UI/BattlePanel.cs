using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>战斗场景的 UI 面板，负责战斗信息显示和界面操作。</summary>
public class BattlePanel : BasePanel
{
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private TMP_Text respawnText;
    [SerializeField] private TMP_Text skill1CooldownText;
    [SerializeField] private Image skill1CooldownMask;
    [SerializeField] private TMP_Text ownEnergyText;
    [SerializeField] private Image ownEnergyFill;
    [SerializeField] private TMP_Text ownRageText;
    [SerializeField] private Image ownRageFill;
    [SerializeField] private TMP_Text enemyEnergyText;
    [SerializeField] private Image enemyEnergyFill;
    [SerializeField] private TMP_Text enemyRageText;
    [SerializeField] private Image enemyRageFill;

    private BattleSimulation simulation;
    private int seatIndex;

    /// <summary>统一开战通知打开面板时，绑定本局模拟和本机席位，立即显示满血或当前血量。</summary>
    public override void Open(object data = null)
    {
        simulation = BattleNetManager.Instance.Simulation;
        seatIndex = BattleNetManager.Instance.SeatIndex;
        base.Open(data);
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        PlayerBattleState state = simulation.GetPlayerState(seatIndex);
        healthBar.Refresh(state.hp, state.maxHp);
        CoreWarCampState ownCamp = simulation.GetCampState(state.camp);
        BattleCamp enemyCampID = state.camp == BattleCamp.CampA ? BattleCamp.CampB : BattleCamp.CampA;
        CoreWarCampState enemyCamp = simulation.GetCampState(enemyCampID);
        CoreBaseState ownCore = simulation.GetCoreState(state.camp);
        CoreBaseState enemyCore = simulation.GetCoreState(enemyCampID);
        float ownEnergyPercent = Mathf.Clamp01((float)ownCore.hp / ownCore.maxHp);
        ownEnergyText.SetText("{0}%", Mathf.RoundToInt(ownEnergyPercent * 100f));
        ownEnergyFill.fillAmount = ownEnergyPercent;
        float ownRagePercent = Mathf.Clamp01((float)ownCamp.coreRage / BattleConst.StartingCoreRage);
        ownRageText.SetText("{0}%", Mathf.RoundToInt(ownRagePercent * 100f));
        ownRageFill.fillAmount = ownRagePercent;
        float enemyEnergyPercent = Mathf.Clamp01((float)enemyCore.hp / enemyCore.maxHp);
        enemyEnergyText.SetText("{0}%", Mathf.RoundToInt(enemyEnergyPercent * 100f));
        enemyEnergyFill.fillAmount = enemyEnergyPercent;
        float enemyRagePercent = Mathf.Clamp01((float)enemyCamp.coreRage / BattleConst.StartingCoreRage);
        enemyRageText.SetText("{0}%", Mathf.RoundToInt(enemyRagePercent * 100f));
        enemyRageFill.fillAmount = enemyRagePercent;
        if (respawnText != null)
        {
            respawnText.gameObject.SetActive(state.IsDead);
            if (state.IsDead)
            {
                int seconds = (simulation.GetRespawnRemainingFrames(seatIndex) + BattleConst.LogicFrameRate - 1) /
                    BattleConst.LogicFrameRate;
                respawnText.SetText("{0} 秒后重返战场", seconds);
            }
        }
        SkillConfigData skill = simulation.Skill1Config;
        int remaining = simulation.GetSkill1CooldownFrames(seatIndex);
        if (skill1CooldownText != null)
        {
            skill1CooldownText.gameObject.SetActive(skill != null);
            if (remaining > 0)
            {
                skill1CooldownText.SetText("{0:1}", (float)remaining / BattleConst.LogicFrameRate);
            }
            else
            {
                skill1CooldownText.text = "E";
            }
        }
        if (skill1CooldownMask != null)
        {
            skill1CooldownMask.gameObject.SetActive(skill != null && remaining > 0);
            if (skill != null && skill.cooldownSeconds > 0f)
            {
                skill1CooldownMask.fillAmount = Mathf.Clamp01(remaining /
                    Mathf.Ceil(skill.cooldownSeconds * BattleConst.LogicFrameRate));
            }
        }
    }

    public override void Close()
    {
        base.Close();
        simulation = null;
    }
}
