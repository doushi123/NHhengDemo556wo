using UnityEngine;

/// <summary>
/// 3D 客户端中的本地英雄控制器。
/// 采样本地按键，并用权威逻辑帧的模拟位置显示角色和动画。
/// </summary>
public sealed class PlayerController : MonoBehaviour
{
    [SerializeField] private HeroConfigSO heroConfig;

    [SerializeField] private Camera movementCamera;

    [SerializeField] private Animator animator;

    /// <summary>
    /// 死亡期间禁止移动。
    /// </summary>
    public bool IsDead { get; private set; }

  
    public HeroConfigData ConfigData => heroConfig.Data;

    // 当前模型正在显示的模拟实例；切换实例时重新初始化显示位置。
    private BattleSimulation displayedSimulation;

    // 最后已处理的逻辑帧编号；-1 表示尚未显示任何逻辑帧。
    private int displayedFrame = -1;

    // 位置插值的起点，使用上一次记录的逻辑位置。
    private Vector3 previousFramePosition;

    // 位置插值的终点，使用最新记录的逻辑位置。
    private Vector3 currentFramePosition;

    // 发现新逻辑帧时的本机时间，用于计算画面插值进度。
    private float frameReceivedAt;

    /// <summary>角色失活后提交空操作，避免网络继续发送失活前的移动输入。</summary>
    private void OnDisable()
    {
        // 测试入口可能在管理器加载前暂停角色，退出时管理器也可能已经销毁。
        if (BattleNetManager.Instance != null)
        {
            BattleNetManager.Instance.SetInput(default);
        }
    }

    private void Update()
    {
        if (!BattleNetManager.Instance.IsRunning)
        {
            BattleNetManager.Instance.SetInput(default);
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
        };
        BattleNetManager.Instance.SetInput(currentInput);
        ShowSimulation();
    }

    /// <summary>正式上报就绪前检查场景引用，并把本地角色显示到服务端指定的出生位置。</summary>
    public void PrepareBattle()
    {
        if (movementCamera == null || animator == null || heroConfig == null)
        {
            throw new System.InvalidOperationException("PlayerController 缺少摄像机、Animator 或英雄配置引用。");
        }
        ShowSimulation();
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

        if (isDead)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsRunning", false);
        }

        animator.SetBool("IsDead", isDead);
    }

    /// <summary>
    /// 在相邻逻辑帧位置之间插值；只在收到新逻辑帧时更新移动动画。
    /// </summary>
    private void ShowSimulation()
    {
        BattleNetManager manager = BattleNetManager.Instance;
        // 管理器由 Bootstrap 保证存在，但进入战斗前还没有模拟实例。
        if (manager.Simulation == null)
        {
            displayedSimulation = null;
            displayedFrame = -1;
            UpdateAnimator(false, false);
            return;
        }

        BattleSimulation simulation = manager.Simulation;
        BEPUutilities.Vector3 position = simulation.GetPosition(manager.SeatIndex);
        Vector3 logicPosition = new Vector3((float)position.X, (float)position.Y, (float)position.Z);
        //第一次拿到本局模拟时，初始化角色的显示位置
        if (displayedSimulation != simulation)
        {
            displayedSimulation = simulation;
            displayedFrame = simulation.Frame;
            previousFramePosition = logicPosition;
            currentFramePosition = logicPosition;
            transform.position = logicPosition;
            UpdateAnimator(false, false);
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
            if (isMoving)
            {
                transform.rotation = Quaternion.LookRotation(movement, Vector3.up);
            }

            BattleInput frameInput = simulation.GetLastInput(manager.SeatIndex);
            UpdateAnimator(isMoving, isMoving && frameInput.isRunning);
        }

        float progress = (Time.unscaledTime - frameReceivedAt) * BattleConst.LogicFrameRate;
        transform.position = Vector3.Lerp(previousFramePosition, currentFramePosition,
            Mathf.Clamp01(progress));
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
