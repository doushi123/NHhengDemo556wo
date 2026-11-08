using UnityEngine;

/// <summary>加在 Animator 的受击状态上，退出动作时清除 Hit，允许恢复待机或走跑。</summary>
public class PlayerHitState : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool("Hit", false);
    }
}
