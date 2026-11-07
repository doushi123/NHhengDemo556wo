using UnityEngine;
using UnityEngine.UI;

/// <summary>显示角色头顶的血量填充，不显示数字，并在镜头更新后朝向摄像机。</summary>
[DefaultExecutionOrder(100)]
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Camera viewCamera;

    private int displayedHp = -1;
    private int displayedMaxHp = -1;

    /// <summary>只在血量变化时刷新填充比例。</summary>
    public void Refresh(int hp, int maxHp)
    {
        if (hp == displayedHp && maxHp == displayedMaxHp)
        {
            return;
        }
        displayedHp = hp;
        displayedMaxHp = maxHp;
        fillImage.fillAmount = (float)hp / maxHp;
    }

    private void LateUpdate()
    {
        transform.rotation = viewCamera.transform.rotation;
    }
}
