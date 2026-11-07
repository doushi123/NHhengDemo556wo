using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>显示 HUD 中的血量比例和数值，不负责扣血。</summary>
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text hpText;

    private int displayedHp = -1;
    private int displayedMaxHp = -1;


    /// <summary>按模拟确认的血量更新填充和文字。</summary>
    public void Refresh(int hp, int maxHp)
    {
        if (hp == displayedHp && maxHp == displayedMaxHp)
        {
            return;
        }
        displayedHp = hp;
        displayedMaxHp = maxHp;
        fillImage.fillAmount = (float)hp / maxHp;
        if (hpText != null)
        {
            hpText.text = hp + " / " + maxHp;
        }
    }
}
