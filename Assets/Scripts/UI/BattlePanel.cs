using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>战斗场景的 UI 面板，负责战斗信息显示和界面操作。</summary>
public class BattlePanel : BasePanel
{
    [SerializeField] private HealthBar healthBar;
    [Tooltip("死亡时显示复活倒计时，复活保护期间显示保护提示。")]
    [SerializeField] private TMP_Text respawnText;
    [Tooltip("技能一图标上的冷却数字，可与冷却遮罩独立使用。")]
    [SerializeField] private TMP_Text skill1CooldownText;
    [Tooltip("Filled 类型的技能冷却遮罩，冷却结束时填充为 0。")]
    [SerializeField] private Image skill1CooldownMask;

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
        if (respawnText != null)
        {
            bool protectedPlayer = simulation.IsProtected(seatIndex);
            respawnText.gameObject.SetActive(state.IsDead || protectedPlayer);
            if (state.IsDead)
            {
                int seconds = (simulation.GetRespawnRemainingFrames(seatIndex) + BattleConst.LogicFrameRate - 1) /
                    BattleConst.LogicFrameRate;
                respawnText.SetText("{0} 秒后重返战场", seconds);
            }
            else if (protectedPlayer)
            {
                respawnText.text = "复活保护 · 攻击后结束";
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
