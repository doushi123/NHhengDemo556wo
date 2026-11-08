using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 显示指定席位的英雄位置和动画；只有本地英雄采样并提交按键。
/// </summary>
public sealed class PlayerController : MonoBehaviour
{
    [SerializeField] private HeroConfigSO heroConfig;

    [SerializeField] private Camera movementCamera;

    [SerializeField] private Animator animator;

    [SerializeField] private WorldHealthBar healthBar;

    [SerializeField] private SkillConfigSO skill1Config;

    [SerializeField] private ParticleSystem skill1Effect;

    /// <summary>
    /// 死亡期间禁止移动。
    /// </summary>
    public bool IsDead { get; private set; }

  
    public HeroConfigData ConfigData => heroConfig.Data;

    // 场景中原有的角色由本机操作；复制出来的其他席位只显示模拟结果。
    private bool isLocalPlayer = true;
    // 这个模型对应的战斗席位，位置和动画输入都按它查询。
    private int seatIndex;

    // 本地角色创建的其他席位模型，结束战斗或离开场景时一并清理。
    private PlayerController[] otherPlayers;

    // 当前模型正在显示的模拟实例；切换实例时重新初始化显示位置。
    private BattleSimulation displayedSimulation;

    // 最后已处理的逻辑帧编号；-1 表示尚未显示任何逻辑帧。
    private int displayedFrame = -1;

    // 最近已经显示过的受击逻辑帧，防止同一次伤害每个画面帧都重播动画。
    private int displayedHitFrame = -1;
    // 复活时瞬间回到出生点，避免把死亡位置到基地的距离做成移动插值。
    private int displayedRespawnFrame = -1;
    // 仅由本机角色统一显示本局剑气，其他席位模型不重复创建粒子。
    private Dictionary<int, SwordWaveView> swordWaveViews = new Dictionary<int, SwordWaveView>();
    private HashSet<int> visibleSwordWaves = new HashSet<int>();
    private List<int> endedSwordWaves = new List<int>();

    // 位置插值的起点，使用上一次记录的逻辑位置。
    private Vector3 previousFramePosition;

    // 位置插值的终点，使用最新记录的逻辑位置。
    private Vector3 currentFramePosition;

    // 发现新逻辑帧时的本机时间，用于计算画面插值进度。
    private float frameReceivedAt;

    /// <summary>角色失活后提交空操作，避免网络继续发送失活前的移动输入。</summary>
    private void OnDisable()
    {
        if (!isLocalPlayer)
        {
            return;
        }
        ClearOtherPlayers();
        ClearSwordWaveViews();
        // 测试入口可能在管理器加载前暂停角色，退出时管理器也可能已经销毁。
        if (BattleNetManager.Instance != null)
        {
            BattleNetManager.Instance.ClearInput();
        }
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            ShowSimulation();
            return;
        }
        if (!BattleNetManager.Instance.IsRunning)
        {
            BattleNetManager.Instance.ClearInput();
            ShowSimulation();
            return;
        }
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
        Vector3 direction = IsDead ? Vector3.zero : GetMoveDirection(input).normalized;
        BattleInput currentInput = new BattleInput
        {
            moveX = Mathf.RoundToInt(direction.x * 127f),
            moveY = Mathf.RoundToInt(direction.z * 127f),
            isRunning = !IsDead && IsRunKeyHeld(),
            buttons = !IsDead && Input.GetMouseButtonDown(0) ? BattleButtons.Attack : BattleButtons.None,
        };
        if (!IsDead && Input.GetKeyDown(KeyCode.E))
        {
            currentInput.buttons |= BattleButtons.Skill1;
        }
        if (!IsDead && Input.GetMouseButtonDown(1))
        {
            currentInput.buttons |= BattleButtons.Dodge;
        }
        BattleNetManager.Instance.SetInput(currentInput);
        ShowSimulation();
    }

    /// <summary>等所有角色的位置与动画更新后，再读取施法者剑旁的特效位置。</summary>
    private void LateUpdate()
    {
        if (!isLocalPlayer || displayedSimulation == null)
        {
            return;
        }
        float progress = Mathf.Clamp01((Time.unscaledTime - frameReceivedAt) * BattleConst.LogicFrameRate);
        ShowSwordWaves(displayedSimulation, progress);
    }

    /// <summary>上报就绪前检查引用，按席位创建其余角色，并把所有模型放到各自的模拟位置。</summary>
    public void PrepareBattle()
    {
        if (movementCamera == null || animator == null || heroConfig == null || healthBar == null)
        {
            throw new System.InvalidOperationException("PlayerController 缺少摄像机、Animator、英雄配置或头顶血条引用。");
        }
        SkillConfigData skill = BattleNetManager.Instance.Simulation.Skill1Config;
        if (skill != null && (skill1Config == null || skill1Config.Data.skillID != skill.skillID ||
            skill1Config.AnimationClip == null || skill1Effect == null ||
            skill1Effect.transform == transform || !skill1Effect.transform.IsChildOf(transform)))
        {
            throw new System.InvalidOperationException("PlayerController 需要绑定对应的技能一动画配置，以及放在本角色子层级中的 Skill1 Effect 特效模板。");
        }
        if (skill != null)
        {
            // 场景中这份用于编辑和复制，不在角色身上常驻播放。
            skill1Effect.gameObject.SetActive(false);
        }
        ClearOtherPlayers();
        ClearSwordWaveViews();
        seatIndex = BattleNetManager.Instance.SeatIndex;
        displayedHitFrame = -1;
        displayedRespawnFrame = -1;
        SetDead(false);
        otherPlayers = new PlayerController[BattleConst.SeatCount];
        for (int seat = 0; seat < otherPlayers.Length; seat++)
        {
            if (seat == seatIndex)
            {
                continue;
            }

            // 复用场景中已调好的模型、Animator 和武器，复制对象不采集本机输入。
            PlayerController player = Instantiate(this,
                transform.position, transform.rotation, transform.parent);
            player.isLocalPlayer = false;
            player.seatIndex = seat;
            player.name = "Player_" + seat;
            otherPlayers[seat] = player;
            player.ShowSimulation();
        }
        ShowSimulation();
    }

    /// <summary>移除本地角色创建的其他席位显示对象，不修改模拟中的角色。</summary>
    private void ClearOtherPlayers()
    {
        if (otherPlayers == null)
        {
            return;
        }
        foreach (PlayerController player in otherPlayers)
        {
            if (player != null)
            {
                player.gameObject.SetActive(false);
                Destroy(player.gameObject);
            }
        }
        otherPlayers = null;
    }

    /// <summary>
    /// 战斗逻辑确认死亡或复活后调用，更新移动许可和动画状态。
    /// </summary>
    public void SetDead(bool isDead)
    {
        IsDead = isDead;

        if (animator == null)
        {
            return;
        }

        animator.SetBool("Hit", false);
        animator.SetBool("IsDodging", false);

        if (isDead)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsAttacking", false);
            if (isLocalPlayer)
            {
                BattleNetManager.Instance.ClearInput();
            }
        }

        animator.SetBool("IsDead", isDead);
    }

    /// <summary>
    /// 在相邻逻辑帧位置之间插值；只在收到新逻辑帧时更新移动动画。
    /// 更新本机角色的位置和动画
    /// </summary>
    private void ShowSimulation()
    {
        BattleNetManager manager = BattleNetManager.Instance;
        // 管理器由 Bootstrap 保证存在，但进入战斗前还没有模拟实例。
        if (manager.Simulation == null)
        {
            if (isLocalPlayer)
            {
                ClearOtherPlayers();
                ClearSwordWaveViews();
            }
            displayedSimulation = null;
            displayedFrame = -1;
            displayedHitFrame = -1;
            displayedRespawnFrame = -1;
            // 战斗场景可以先于模拟准备好，未入场时隐藏头顶 UI。
            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(false);
            }
            UpdateAnimator(false, false);
            if (animator != null)
            {
                animator.SetBool("IsAttacking", false);
                animator.SetBool("IsDodging", false);
            }
            return;
        }

        BattleSimulation simulation = manager.Simulation;
        PlayerBattleState state = simulation.GetPlayerState(seatIndex);
        healthBar.Refresh(state.hp, state.maxHp);
        // 本机血量由 HUD 显示，头顶血条只显示其他存活角色。
        healthBar.gameObject.SetActive(!isLocalPlayer && !state.IsDead);
        if (IsDead != state.IsDead)
        {
            SetDead(state.IsDead);
        }
        BEPUutilities.Vector3 facing = simulation.GetFacing(seatIndex);
        transform.rotation = Quaternion.LookRotation(new Vector3((float)facing.X, 0f, (float)facing.Z), Vector3.up);
        BEPUutilities.Vector3 position = simulation.GetPosition(seatIndex);
        Vector3 logicPosition = new Vector3((float)position.X, (float)position.Y, (float)position.Z);
        if (!state.IsDead && displayedRespawnFrame != state.respawnFrame)
        {
            displayedRespawnFrame = state.respawnFrame;
            displayedHitFrame = state.lastHitFrame;
            displayedFrame = simulation.Frame;
            previousFramePosition = logicPosition;
            currentFramePosition = logicPosition;
            frameReceivedAt = Time.unscaledTime;
            transform.position = logicPosition;
            UpdateAnimator(false, false);
        }
        //第一次拿到本局模拟时，初始化角色的显示位置
        if (displayedSimulation != simulation)
        {
            displayedSimulation = simulation;
            displayedFrame = simulation.Frame;
            displayedHitFrame = state.lastHitFrame;
            displayedRespawnFrame = state.respawnFrame;
            previousFramePosition = logicPosition;
            currentFramePosition = logicPosition;
            frameReceivedAt = Time.unscaledTime;
            transform.position = logicPosition;
            UpdateAnimator(false, false);
            UpdateAttackAnimation(simulation, 0f);
            return;
        }

        if (displayedFrame != simulation.Frame)
        {
            previousFramePosition = currentFramePosition;
            currentFramePosition = logicPosition;
            displayedFrame = simulation.Frame;
            frameReceivedAt = Time.unscaledTime;

            Vector3 movement = currentFramePosition - previousFramePosition;
            // 落地与坡面高度变化参与位置插值。
            movement.y = 0f;
            bool isMoving = movement.sqrMagnitude > 0.000001f;

            BattleInput frameInput = simulation.GetLastInput(seatIndex);
            bool dodging = simulation.IsDodging(seatIndex);
            UpdateAnimator(!IsDead && !dodging && isMoving, !IsDead && !dodging && isMoving && frameInput.isRunning);
        }

        float progress = (Time.unscaledTime - frameReceivedAt) * BattleConst.LogicFrameRate;
        transform.position = Vector3.Lerp(previousFramePosition, currentFramePosition,
            Mathf.Clamp01(progress));
        UpdateAttackAnimation(simulation, Mathf.Clamp01(progress));
        if (displayedHitFrame != state.lastHitFrame)
        {
            displayedHitFrame = state.lastHitFrame;
            // 致死一击只播死亡；补帧后已经重新开始攻击时也不回放旧受击动作。
            if (!state.IsDead && simulation.GetAttack(seatIndex).phase == AttackPhase.None)
            {
                animator.SetBool("Hit", true);
            }
        }
    }

    /// <summary>接收攻击动画的判定开始事件，当前按约定留空。</summary>
    public void HitStart()
    {
    }

    /// <summary>接收攻击动画的判定结束事件，当前按约定留空。</summary>
    public void HitEnd()
    {
    }

    /// <summary>用模拟进度驱动攻击姿势；没有新操作帧时最多插值一帧。</summary>
    private void UpdateAttackAnimation(BattleSimulation simulation, float progress)
    {
        AttackState attack = simulation.GetAttack(seatIndex);
        bool isAttacking = !IsDead && attack.phase != AttackPhase.None;
        animator.SetBool("IsAttacking", isAttacking);
        animator.SetBool("IsDodging", !IsDead && simulation.IsDodging(seatIndex));
        if (simulation.Skill1Config != null)
        {
            // IsAttacking 表示动作是否进行，ActionID 只负责选择普攻或技能动画。
            animator.SetInteger("ActionID", attack.skillID);
        }
        if (isAttacking || simulation.IsDodging(seatIndex))
        {
            // 开始有效攻击或冲刺时结束旧受击表现。
            animator.SetBool("Hit", false);
        }
        if (isAttacking)
        {
            animator.SetFloat("AttackTime", simulation.GetAttackAnimationTime(seatIndex, progress));
        }
    }

    /// <summary>本机统一显示所有席位的剑气，剑气结束后销毁对应特效。</summary>
    private void ShowSwordWaves(BattleSimulation simulation, float progress)
    {
        visibleSwordWaves.Clear();
        for (int i = 0; i < simulation.SwordWaveCount; i++)
        {
            SwordWaveState wave = simulation.GetSwordWave(i);
            visibleSwordWaves.Add(wave.id);
            if (!swordWaveViews.TryGetValue(wave.id, out SwordWaveView view))
            {
                PlayerController caster = wave.ownerSeat == seatIndex ? this : otherPlayers[wave.ownerSeat];
                view = new SwordWaveView(caster.skill1Effect, wave);
                swordWaveViews.Add(wave.id, view);
            }
            float age = (simulation.Frame - wave.spawnFrame + progress) / BattleConst.LogicFrameRate;
            view.Refresh(wave, age, progress);
        }
        endedSwordWaves.Clear();
        foreach (KeyValuePair<int, SwordWaveView> pair in swordWaveViews)
        {
            if (!visibleSwordWaves.Contains(pair.Key))
            {
                pair.Value.Clear();
                endedSwordWaves.Add(pair.Key);
            }
        }
        foreach (int id in endedSwordWaves)
        {
            swordWaveViews.Remove(id);
        }
    }

    private void ClearSwordWaveViews()
    {
        foreach (SwordWaveView view in swordWaveViews.Values)
        {
            view.Clear();
        }
        swordWaveViews.Clear();
    }

    /// <summary>
    /// 判断玩家是否按住任意一个 Shift 键。
    /// </summary>
    private bool IsRunKeyHeld()
    {
        return Input.GetKey(KeyCode.LeftShift)|| Input.GetKey(KeyCode.RightShift);
    }

    /// <summary>
    /// 将当前移动状态同步给 Animator。
    /// Animator 需要准备 IsMoving、IsRunning 和 IsDead 三个 Bool 参数。
    /// </summary>
    private void UpdateAnimator(bool isMoving, bool isRunning)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsRunning", isRunning);
    }

    /// <summary>
    /// 将输入转换成相对摄像机的水平移动方向。
    /// </summary>
    private Vector3 GetMoveDirection(Vector2 input)
    {
        Vector3 inputDirection = new Vector3(input.x, 0f, input.y);
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        Vector3 cameraForward = movementCamera.transform.forward;
        Vector3 cameraRight = movementCamera.transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        return cameraRight * inputDirection.x + cameraForward * inputDirection.z;
    }
}
