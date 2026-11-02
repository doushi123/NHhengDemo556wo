using System;
using BEPUphysics.Character;
using BEPUutilities;
using FixMath.NET;

/// <summary>
/// 按服务端确认的操作帧推动角色移动
/// </summary>
public sealed class BattleSimulation
{
    private readonly BattlePhysics physics;
    private readonly Fix64 walkSpeed;
    private readonly Fix64 runSpeed;
    // 数组下标就是席位编号；四个角色在同一个物理世界中。
    private readonly CharacterController[] players = new CharacterController[BattleConst.SeatCount];
    private readonly BattleInput[] lastInputs = new BattleInput[BattleConst.SeatCount];

    /// <summary>已经执行的最后一帧；开局前为 -1。</summary>
    public int Frame { get; private set; } = -1;

    /// <summary>按固定席位顺序建立角色；初始化位置统一表示角色脚底，单位为米。</summary>
    public BattleSimulation(BattleInitData init, BattlePhysics physics, BattleConfigData config)
    {
        if (physics.mapID != init.mapID || init.seats == null ||init.seats.Length != BattleConst.SeatCount || config.heroes.Length == 0)
        {
            throw new InvalidOperationException("战斗地图、席位或英雄配置未准备好。");
        }

        this.physics = physics;
        // 所有席位暂时使用同一个英雄配置。
        HeroConfigData hero = config.heroes[0];
        walkSpeed = (Fix64)hero.moveSpeed;
        runSpeed = (Fix64)hero.runSpeed;
        for (int seat = 0; seat < players.Length; seat++)
        {
            BattleSeatData data = init.seats[seat];
            Vector3 feet = new Vector3((Fix64)data.spawnX, (Fix64)data.spawnY, (Fix64)data.spawnZ);
            players[seat] = physics.CreatePlayer(feet, hero);
        }
    }

    /// <summary>
    /// 收到一帧操作后
    /// 1.检查帧号是否连续
    /// 2.给四个角色分别设置移动方向、走路或移动速度
    /// 3.推进这一帧的物理计算
    /// 4.限制地图边界
    /// </summary>
    public void Step(AuthorityFramePacket packet)
    {
        if (packet.frame != Frame + 1 || packet.inputs == null || packet.inputs.Length != players.Length)
        {
            throw new InvalidOperationException("权威操作帧不连续或席位数量错误：" + packet.frame);
        }

        for (int seat = 0; seat < players.Length; seat++)
        {
            BattleInput input = packet.inputs[seat];
            lastInputs[seat] = input;
            CharacterController player = players[seat];
            Fix64 speed = input.isRunning ? runSpeed : walkSpeed;
            player.StandingSpeed = speed;
            player.SlidingSpeed = speed;
            player.AirSpeed = speed;
            // BEPU 默认前方是 -Z，右方是 +X；输入已是世界方向，因此翻转第二个分量。
            player.HorizontalMotionConstraint.MovementDirection =new Vector2((Fix64)input.moveX, -(Fix64)input.moveY);
        }

        for (int step = 0; step < PhysicsConst.PhysicsSubsteps; step++)
        {
            physics.Space.Update();
            foreach (CharacterController player in players)
            {
                physics.KeepInBounds(player);
            }
        }
        Frame = packet.frame;
    }

    /// <summary>取得模拟中的脚底 XYZ；Unity 只将该结果转换为显示位置。</summary>
    public Vector3 GetPosition(int seat)
    {
        CharacterController player = players[seat];
        return player.Body.Position - new Vector3(0, player.Body.Height / 2, 0);
    }

    /// <summary>取得最后一帧该席位的操作，供动画表现使用。</summary>
    public BattleInput GetLastInput(int seat)
    {
        return lastInputs[seat];
    }
}
