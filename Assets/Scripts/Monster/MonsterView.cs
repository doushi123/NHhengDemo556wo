using UnityEngine;

/// <summary>显示中立怪物的位置、朝向和动画，由 MonsterSpawner 的对象池复用。</summary>
public class MonsterView : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private WorldHealthBar healthBar;
    private MonsterState state;

    public int TypeID => state.typeID;

    /// <summary>从对象池取出时重置上一只怪物的动画和位置。</summary>
    public void Show(MonsterState initial)
    {
        state = initial;
        healthBar.SetCamera(Camera.main);
        transform.position = ToUnity(initial.position);
        transform.rotation = Quaternion.LookRotation(ToUnity(initial.facing));
        gameObject.SetActive(true);
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    public void SetState(MonsterState value, int maxHp)
    {
        state = value;
        healthBar.Refresh(state.hp, maxHp);
        healthBar.gameObject.SetActive(!state.IsDead);
        if (animator != null)
        {
            Vector3 delta = ToUnity(state.position - state.previousPosition);
            delta.y = 0f;
            animator.SetBool("IsMoving", !state.IsDead && delta.sqrMagnitude > 0.0001f);
            animator.SetBool("IsAttacking", !state.IsDead && state.isAttacking);
            animator.SetBool("IsDead", state.IsDead);
        }
    }

    /// <summary>只插值模拟位置；Root Motion、动画事件和 Unity 碰撞不参与怪物伤害。</summary>
    public void Display(float progress)
    {
        transform.position = Vector3.Lerp(ToUnity(state.previousPosition), ToUnity(state.position), progress);
        transform.rotation = Quaternion.LookRotation(ToUnity(state.facing));
    }

    /// <summary>接收攻击动画的挥击标记；伤害在导出的命中帧结算，音效和特效暂未接入。</summary>
    public void AtkEvent()
    {
        // 不在动画回调中扣血，避免帧率、动画混合或剔除影响各端战斗结果。
    }

    /// <summary>接收死亡动画的 DeadEvent；死亡状态和尸体回收由逻辑帧控制。</summary>
    public void DeadEvent()
    {
        // 暂无死亡音效或特效；这里不销毁或回收对象，以免与 MonsterSpawner 的对象池重复处理。
    }

    private static Vector3 ToUnity(BEPUutilities.Vector3 value) =>new Vector3((float)value.X, (float)value.Y, (float)value.Z);
}
