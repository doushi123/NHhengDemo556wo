using System;
using System.Collections.Generic;
using BEPUphysics.Character;
using BEPUutilities;
using FixMath.NET;

/// <summary>保存阵营战绩与秘核狂暴；秘核能量统一使用基地生命。</summary>
public struct CoreWarCampState
{
    public BattleCamp camp;
    public int kills;
    public int deaths;
    public int coreRage;
}

/// <summary>
/// 按服务端确认的操作帧推进角色移动、物理、攻击命中和生命值。
/// </summary>
public sealed class BattleSimulation
{
    private BattlePhysics physics;
    private Fix64 walkSpeed;
    private Fix64 runSpeed;
    // 数组下标就是席位编号；四个角色在同一个物理世界中。
    private CharacterController[] players = new CharacterController[BattleConst.SeatCount];
    private BattleInput[] lastInputs = new BattleInput[BattleConst.SeatCount];
    private BattleInput[] frameInputs = new BattleInput[BattleConst.SeatCount];
    private BotBrain[] botBrains = new BotBrain[BattleConst.SeatCount];
    // 保存四个席位各自的攻击阶段和冷却，数组下标就是席位编号。
    private AttackState[] attacks = new AttackState[BattleConst.SeatCount];
    private AttackTiming attackTiming;
    private Fix64 attackRange;
    private bool attackFullCircle;
    private Fix64 attackHalfAngleCos;
    private Fix64 attackHalfAngleSin;
    private int attackDamage;
    private HeroConfigData hero;
    private SkillConfigData skill1;
    private AttackTiming skill1Timing;
    // 普攻和技能各自计冷却，受击中断动作不会重置冷却。
    private int[] attackReadyFrames = new int[BattleConst.SeatCount];
    private int[] skill1ReadyFrames = new int[BattleConst.SeatCount];
    private DodgeState[] dodges = new DodgeState[BattleConst.SeatCount];
    private int dodgeDurationFrames;
    private Fix64 dodgeSpeed;
    private Vector3[] spawnPositions = new Vector3[BattleConst.SeatCount];
    private List<SwordWaveState> swordWaves = new List<SwordWaveState>();
    private int nextSwordWaveID;
    private Fix64 waveSpeed;
    private Fix64 waveRadius;
    private Fix64 waveRange;
    // 朝向、阵营和生命值都是逻辑数据，不能从模型 Transform 或动画回调读取。
    private Vector3[] facings = new Vector3[BattleConst.SeatCount];
    private PlayerBattleState[] playerStates = new PlayerBattleState[BattleConst.SeatCount];
    // 保存物理子步前的位置，用于扫掠时计入攻击者和受击者的移动。
    private Vector3[] stepPositions = new Vector3[BattleConst.SeatCount];
    // 按攻击者和受击者分别记录同帧伤害，用于确定击杀归属。
    private long[,] pendingDamage = new long[BattleConst.SeatCount, BattleConst.SeatCount];
    // 阵营下标 0、1 分别对应 CampA、CampB。
    private CoreWarCampState[] campStates = new CoreWarCampState[2];
    private CoreBaseState[] coreStates = new CoreBaseState[2];
    // 合并物理子步内对基地的伤害，在逻辑帧末统一扣血。
    private long[] pendingCoreDamage = new long[2];
    // 击杀和死亡引起的能量变化，与攻塔伤害在帧末合并到同一份基地生命。
    private long[] pendingCoreEnergyChange = new long[2];
    // 只累计基地实际受到的攻击伤害，恢复能量不会重复触发整百分比狂暴奖励。
    private long[] coreDamageTaken = new long[2];
    private Vector3[] baseVertices = new Vector3[8];
    private Vector3[] baseContacts = new Vector3[32];
    private long[] pendingMonsterDamage = new long[BattleConst.SeatCount];

    /// <summary>地图启用中立怪物时存在，怪物与英雄共同使用本局定点物理世界。</summary>
    public MonsterSimulation Monsters { get; }

    /// <summary>已经执行的最后一帧；开局前为 -1。</summary>
    public int Frame { get; private set; } = -1;

    /// <summary>基地被摧毁、胜负确定的逻辑帧；未结束时为 -1。</summary>
    public int EndFrame { get; private set; } = -1;
    public bool IsFinished => EndFrame >= 0;
    /// <summary>获胜阵营；未结束或同帧双方基地都被摧毁时为 null。</summary>
    public BattleCamp? Winner { get; private set; }

    /// <summary>本英雄配置的技能一；未配置时为 null，不允许释放。</summary>
    public SkillConfigData Skill1Config => skill1;
    public int SwordWaveCount => swordWaves.Count;

    /// <summary>读取阵营战绩和基地、怪物算法使用的数值。</summary>
    public CoreWarCampState GetCampState(BattleCamp camp) => campStates[(int)camp - (int)BattleCamp.CampA];

    /// <summary>读取基地位置、受击范围、生命值和摧毁状态。</summary>
    public CoreBaseState GetCoreState(BattleCamp camp) => coreStates[(int)camp - (int)BattleCamp.CampA];

    /// <summary>读取一条剑气的状态副本，表现层不能修改模拟中的剑气。</summary>
    public SwordWaveState GetSwordWave(int index) => swordWaves[index];

    /// <summary>技能一还要等待的逻辑帧数；0 表示冷却已结束。</summary>
    public int GetSkill1CooldownFrames(int seat) => Math.Max(0, skill1ReadyFrames[seat] - Frame);

    /// <summary>当前是否在冲刺；受击、死亡或持续时间结束时恢复普通移动。</summary>
    public bool IsDodging(int seat) => Frame < dodges[seat].endFrame;

    /// <summary>读取该席位的冲刺状态副本，用于检查时序和方向。</summary>
    public DodgeState GetDodge(int seat) => dodges[seat];

    /// <summary>距复活还要等待的逻辑帧数，存活时为 0。</summary>
    public int GetRespawnRemainingFrames(int seat) => playerStates[seat].IsDead
        ? Math.Max(0, playerStates[seat].respawnFrame - Frame) : 0;

    /// <summary>按固定席位顺序建立角色；初始化位置统一表示角色脚底，单位为米。</summary>
    public BattleSimulation(BattleInitData init, BattlePhysics physics, BattleConfigData config)
    {
        if (physics.mapID != init.mapID || init.seats == null ||init.seats.Length != BattleConst.SeatCount || config.heroes.Length == 0)
        {
            throw new InvalidOperationException("战斗地图、席位或英雄配置未准备好。");
        }

        this.physics = physics;
        foreach (MapCoreData data in physics.Cores)
        {
            coreStates[(int)data.camp - (int)BattleCamp.CampA] = new CoreBaseState
            {
                camp = data.camp,
                center = new Vector3(data.center.x, data.center.y, data.center.z),
                radius = data.radius,
                maxHp = data.maxHp,
                hp = data.maxHp,
                lastHitFrame = -1,
                destroyedFrame = -1,
            };
        }
        campStates[0] = new CoreWarCampState
        {
            camp = BattleCamp.CampA,
            coreRage = BattleConst.StartingCoreRage,
        };
        campStates[1] = new CoreWarCampState
        {
            camp = BattleCamp.CampB,
            coreRage = BattleConst.StartingCoreRage,
        };
        // 所有席位暂时使用同一个英雄配置。
        hero = config.heroes[0];
        walkSpeed = (Fix64)hero.moveSpeed;
        runSpeed = (Fix64)hero.runSpeed;
        SkillConfigData attack = Array.Find(config.skills,skill => skill.skillID == BattleConst.BasicAttackSkillID);
        attackTiming = new AttackTiming(attack);
        attackRange = (Fix64)attack.range;
        attackFullCircle = attack.angle == 360;
        Fix64 halfAngle = (Fix64)attack.angle * Fix64.Pi / 360;
        attackHalfAngleCos = Fix64.Cos(halfAngle);
        attackHalfAngleSin = Fix64.Sin(halfAngle);
        attackDamage = attack.damage;
        SkillConfigData dodge = Array.Find(config.skills, skill => skill.skillID == BattleConst.DodgeSkillID);
        dodgeDurationFrames = SecondsToFrames(dodge.activeSeconds);
        dodgeSpeed = (Fix64)(decimal)dodge.moveDistance * BattleConst.LogicFrameRate / dodgeDurationFrames;
        if (hero.skill1ID != 0)
        {
            skill1 = Array.Find(config.skills, skill => skill.skillID == hero.skill1ID);
            skill1Timing = new AttackTiming(skill1);
            waveSpeed = (Fix64)skill1.projectileSpeed;
            waveRadius = (Fix64)skill1.projectileRadius;
            waveRange = (Fix64)skill1.range;
        }
        for (int seat = 0; seat < players.Length; seat++)
        {
            BattleSeatData data = init.seats[seat];
            //发现该席位没真人，则创建电脑玩家
            if (data.playerID == 0)
            {
                botBrains[seat] = new BotBrain(hero.bot, attack.range, hero.collisionRadius);
            }
            Vector3 feet = new Vector3((Fix64)data.spawnX, (Fix64)data.spawnY, (Fix64)data.spawnZ);
            spawnPositions[seat] = feet;
            attackReadyFrames[seat] = -1;
            skill1ReadyFrames[seat] = -1;
            dodges[seat] = new DodgeState { startFrame = -1, endFrame = -1 };
            players[seat] = physics.CreatePlayer(feet, hero);
            playerStates[seat] = new PlayerBattleState
            {
                camp = data.camp,
                maxHp = hero.maxHp,
                hp = hero.maxHp,
                lastHitFrame = -1,
                respawnFrame = -1,
            };
            facings[seat] = new Vector3(0, 0, 1);
        }
        if (physics.Monsters != null)
        {
            Monsters = new MonsterSimulation(this, physics, hero, init.randomSeed);
        }
    }

    /// <summary>
    /// 收到一帧操作后
    /// 1.检查帧号是否连续
    /// 2.处理到时复活，推进冲刺、招式和移动；冲刺期间固定方向并禁止攻击
    /// 3.推进物理，检查普攻扇形与剑气命中
    /// 4.统一扣血，处理招式中断与死亡
    /// </summary>
    public void Step(AuthorityFramePacket packet)
    {
        if (packet.frame != Frame + 1 || packet.inputs == null || packet.inputs.Length != players.Length)
        {
            throw new InvalidOperationException("权威操作帧不连续或席位数量错误：" + packet.frame);
        }

        // 结局确定后继续接收中继帧，但不再改变战斗结果。
        if (IsFinished)
        {
            Frame = packet.frame;
            return;
        }

        // 上帧结束的剑气已经留给表现读取；不再参与本帧碰撞。
        for (int i = swordWaves.Count - 1; i >= 0; i--)
        {
            SwordWaveState wave = swordWaves[i];
            if (wave.finished)
            {
                swordWaves.RemoveAt(i);
            }
            else
            {
                wave.previousPosition = wave.position;
                swordWaves[i] = wave;
            }
        }
        for (int seat = 0; seat < players.Length; seat++)
        {
            AdvanceRespawn(seat, packet.frame);
        }
        DecayCoreRage(packet.frame);
        Monsters?.BeginFrame(packet.frame);
        for (int seat = 0; seat < players.Length; seat++)
        {
            frameInputs[seat] = default;
            if (playerStates[seat].IsDead || playerStates[seat].respawnFrame == packet.frame)
            {
                continue;
            }
            frameInputs[seat] = botBrains[seat] == null
                ? packet.inputs[seat]
                : botBrains[seat].GetInput(this, seat, packet.frame);
        }
        for (int seat = 0; seat < players.Length; seat++)
        {
            // 复活当帧不执行死亡期间已在网络中传输的输入。
            BattleInput input = frameInputs[seat];
            lastInputs[seat] = input;
            if (dodges[seat].endFrame == packet.frame)
            {
                StopDodge(seat, packet.frame);
            }
            if (packet.frame >= dodges[seat].endFrame && (input.moveX != 0 || input.moveY != 0))
            {
                facings[seat] = new Vector3(input.moveX, 0, input.moveY);
                facings[seat].Normalize();
            }
            if (!playerStates[seat].IsDead)
            {
                TryStartDodge(seat, input.buttons, packet.frame);
                if (packet.frame >= dodges[seat].endFrame)
                {
                    AdvanceAttack(seat, input.buttons, packet.frame);
                }
            }
            CharacterController player = players[seat];
            bool dodging = packet.frame < dodges[seat].endFrame;
            Fix64 speed = playerStates[seat].IsDead ? Fix64.Zero : dodging ? dodgeSpeed : input.isRunning ? runSpeed : walkSpeed;
            player.StandingSpeed = speed;
            player.SlidingSpeed = speed;
            player.AirSpeed = speed;
            // BEPU 默认前方是 -Z，右方是 +X；输入已是世界方向，因此翻转第二个分量。
            player.HorizontalMotionConstraint.MovementDirection = dodging
                ? new Vector2(dodges[seat].direction.X, -dodges[seat].direction.Z)
                : new Vector2((Fix64)input.moveX, -(Fix64)input.moveY);
        }

        for (int step = 0; step < PhysicsConst.PhysicsSubsteps; step++)
        {
            for (int seat = 0; seat < players.Length; seat++)
            {
                stepPositions[seat] = players[seat].Body.Position;
            }
            Monsters?.BeforePhysicsStep();
            physics.Space.Update();
            Monsters?.AfterPhysicsStep();
            foreach (CharacterController player in players)
            {
                physics.KeepInBounds(player);
            }
            DetectAttackHits(packet.frame);
            AdvanceSwordWaves(packet.frame);
        }
        ResolveDamage(packet.frame);
        Monsters?.ResolveDamage(packet.frame);
        ResolveCoreDamage(packet.frame);
        Frame = packet.frame;
    }

    /// <summary>统一结算攻塔、击杀和死亡对秘核能量（基地生命）的影响，归零摧毁；同帧双方归零为平局。</summary>
    private void ResolveCoreDamage(int frame)
    {
        for (int index = 0; index < coreStates.Length; index++)
        {
            ref CoreBaseState state = ref coreStates[index];
            long energy = Math.Max(0, state.hp + pendingCoreEnergyChange[index]);
            int damage = (int)Math.Min(energy, pendingCoreDamage[index]);
            pendingCoreDamage[index] = 0;
            pendingCoreEnergyChange[index] = 0;
            state.hp = (int)(energy - damage);
            if (damage > 0)
            {
                long previousLostPercent = coreDamageTaken[index] * 100 / state.maxHp;
                coreDamageTaken[index] += damage;
                long lostPercent = coreDamageTaken[index] * 100 / state.maxHp;
                int percentChange = (int)(lostPercent - previousLostPercent);
                campStates[index].coreRage += percentChange * BattleConst.CoreDamageRageGainPerPercent;
                state.lastDamage = damage;
                state.lastHitFrame = frame;
            }
            if (state.IsDestroyed)
            {
                state.destroyedFrame = frame;
                physics.RemoveCore(index);
            }
        }
        if (coreStates[0].IsDestroyed || coreStates[1].IsDestroyed)
        {
            EndFrame = frame;
            Winner = coreStates[0].IsDestroyed && coreStates[1].IsDestroyed
                ? (BattleCamp?)null : coreStates[0].IsDestroyed ? BattleCamp.CampB : BattleCamp.CampA;
            for (int seat = 0; seat < players.Length; seat++)
            {
                lastInputs[seat] = default;
                attacks[seat].phase = AttackPhase.None;
                dodges[seat].endFrame = frame;
            }
            swordWaves.Clear();
            Monsters?.Stop();
        }
    }

    /// <summary>合并本帧伤害、确定击杀归属并更新阵营战绩；死亡移除身体并安排复活。</summary>
    private void ResolveDamage(int frame)
    {
        for (int seat = 0; seat < playerStates.Length; seat++)
        {
            ref PlayerBattleState state = ref playerStates[seat];
            long totalDamage = pendingMonsterDamage[seat];
            long highestDamage = totalDamage;
            pendingMonsterDamage[seat] = 0;
            int killerSeat = -1;
            for (int attacker = 0; attacker < playerStates.Length; attacker++)
            {
                long damage = pendingDamage[attacker, seat];
                pendingDamage[attacker, seat] = 0;
                totalDamage += damage;
                if (damage > highestDamage)
                {
                    highestDamage = damage;
                    killerSeat = attacker;
                }
            }
            int damageApplied = (int)Math.Min(state.hp, totalDamage);
            if (damageApplied == 0)
            {
                continue;
            }

            state.hp -= damageApplied;
            state.lastDamage = damageApplied;
            state.lastHitFrame = frame;
            // 只中断这次动作，已经开始的冷却保留；移动不因受击中断。
            attacks[seat].phase = AttackPhase.None;
            if (frame < dodges[seat].endFrame)
            {
                StopDodge(seat, frame);
            }
            if (state.IsDead)
            {
                ref CoreWarCampState defeatedCamp = ref campStates[(int)state.camp - (int)BattleCamp.CampA];
                defeatedCamp.deaths++;
                pendingCoreEnergyChange[(int)state.camp - (int)BattleCamp.CampA] -= BattleConst.DeathCoreEnergy;
                defeatedCamp.coreRage = Math.Max(0,
                    defeatedCamp.coreRage - BattleConst.DeathCoreRage);
                if (killerSeat >= 0)
                {
                    ref CoreWarCampState winningCamp = ref campStates[
                        (int)playerStates[killerSeat].camp - (int)BattleCamp.CampA];
                    winningCamp.kills++;
                    pendingCoreEnergyChange[(int)playerStates[killerSeat].camp - (int)BattleCamp.CampA] += BattleConst.KillCoreEnergy;
                    winningCamp.coreRage += BattleConst.KillCoreRage;
                }
                state.respawnFrame = frame + SecondsToFrames(BattleConst.RespawnSeconds);
                lastInputs[seat] = default;
                CharacterController player = players[seat];
                player.HorizontalMotionConstraint.MovementDirection = Vector2.Zero;
                Vector3 velocity = player.Body.LinearVelocity;
                velocity.X = 0;
                velocity.Z = 0;
                player.Body.LinearVelocity = velocity;
                player.HorizontalMotionConstraint.ResetPositionAnchor();
                // 死亡模型留在画面中，身体移出物理世界，避免尸体继续堵路。
                physics.Space.Remove(player);
            }
        }
    }

    /// <summary>秘核狂暴每秒自然回落；不改变秘核能量或英雄战绩。</summary>
    private void DecayCoreRage(int frame)
    {
        if (frame == 0 || frame % BattleConst.LogicFrameRate != 0)
        {
            return;
        }
        for (int camp = 0; camp < campStates.Length; camp++)
        {
            campStates[camp].coreRage = Math.Max(0,
                campStates[camp].coreRage - BattleConst.CoreRageDecayPerSecond);
        }
    }

    /// <summary>记录一次攻击者对受击者造成的确定性伤害。</summary>
    private void RecordDamage(int attackerSeat, int targetSeat, int damage)
    {
        pendingDamage[attackerSeat, targetSeat] += damage;
    }

    /// <summary>怪物伤害加入同帧结算；英雄因此死亡计死亡，怪物不会给任何玩家阵营记击杀。</summary>
    internal void RecordMonsterDamage(int targetSeat, int damage) => pendingMonsterDamage[targetSeat] += damage;

    internal void RecordCoreDamage(int core, int damage) => pendingCoreDamage[core] += damage;

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

    /// <summary>取得指定席位的攻击阶段和进度，表现层不能修改模拟中的状态。</summary>
    public AttackState GetAttack(int seat)
    {
        return attacks[seat];
    }

    /// <summary>取得逻辑朝向；不移动时保留最后一次朝向，开局统一朝世界 +Z。</summary>
    public Vector3 GetFacing(int seat) => facings[seat];

    /// <summary>取得当前生命值，最大生命值仍来自英雄配置。</summary>
    public int GetHp(int seat) => playerStates[seat].hp;

    /// <summary>取得角色生命值和最近受击结果的副本，供血条、死亡与受击动画显示。</summary>
    public PlayerBattleState GetPlayerState(int seat) => playerStates[seat];

    /// <summary>按攻击阶段显示动画，让 HitStart、HitEnd 与逻辑判定时间对齐。</summary>
    public float GetAttackAnimationTime(int seat, float progress)
    {
        AttackTiming timing = attacks[seat].skillID == BattleConst.BasicAttackSkillID ? attackTiming : skill1Timing;
        return (float)timing.GetNormalizedTime(attacks[seat].elapsedFrames + (Fix64)progress);
    }

    /// <summary>判定期内检查角色前方的扇形；一刀可命中多人，同一目标只扣一次血。</summary>
    private void DetectAttackHits(int frame)
    {
        for (int seat = 0; seat < players.Length; seat++)
        {
            ref AttackState attack = ref attacks[seat];
            if (playerStates[seat].IsDead || attack.phase != AttackPhase.Active ||
                attack.skillID != BattleConst.BasicAttackSkillID)
            {
                continue;
            }
            Vector3 origin = players[seat].Body.Position;
            Monsters?.DetectMeleeHits(seat, players[seat], facings[seat], attack.startFrame, attackDamage);
            for (int target = 0; target < players.Length; target++)
            {
                if (playerStates[target].IsDead ||
                    playerStates[target].camp == playerStates[seat].camp ||
                    (attack.hitMask & (1 << target)) != 0)
                {
                    continue;
                }
                if (IsInAttackSector(players[seat], facings[seat], players[target].Body.Position,
                    players[target].Body.Radius, players[target].Body.Height) &&
                    !physics.IsBlocked(origin, players[target].Body.Position))
                {
                    attack.hitMask |= 1 << target;
                    RecordDamage(seat, target, attackDamage);
                }
            }
            for (int target = 0; target < coreStates.Length; target++)
            {
                CoreBaseState core = coreStates[target];
                int mask = 1 << (BattleConst.SeatCount + target);
                if (core.IsDestroyed || core.camp == playerStates[seat].camp || (attack.hitMask & mask) != 0)
                {
                    continue;
                }
                bool hit = IsInAttackSector(players[seat], facings[seat], core.center, core.radius, core.radius * 2) &&
                    !physics.IsBlocked(origin, core.center, target);
                foreach (BEPUphysics.Entities.Prefabs.Box basis in physics.GetCoreBases(target))
                {
                    if (!hit) hit = IsBaseInAttackSector(players[seat], facings[seat], basis, target);
                }
                if (hit)
                {
                    attack.hitMask |= mask;
                    pendingCoreDamage[target] += attackDamage;
                }
            }
        }
    }

    /// <summary>先按攻击高度裁剪底座，再检查水平扇形；不把旋转盒体扩大成圆形受击范围。</summary>
    private bool IsBaseInAttackSector(CharacterController attacker, Vector3 forward,
        BEPUphysics.Entities.Prefabs.Box basis, int core)
    {
        Vector3 origin = attacker.Body.Position;
        Vector3 nearest = BattlePhysics.ClosestPoint(basis, origin);
        if (IsBaseContact(attacker, forward, nearest, core)) return true;
        Fix64 minY = origin.Y - attacker.Body.Height / 2;
        Fix64 maxY = origin.Y + attacker.Body.Height / 2;
        Ray vertical = new Ray(new Vector3(origin.X, minY, origin.Z), Vector3.Up);
        if (basis.CollisionInformation.RayCast(vertical, attacker.Body.Height, out RayHit verticalHit) &&
            IsBaseContact(attacker, forward, verticalHit.Location, core)) return true;
        int count = 0;
        for (int vertex = 0; vertex < 8; vertex++)
        {
            Vector3 local = new Vector3((vertex & 1) == 0 ? -basis.Width / 2 : basis.Width / 2,
                (vertex & 2) == 0 ? -basis.Height / 2 : basis.Height / 2,
                (vertex & 4) == 0 ? -basis.Length / 2 : basis.Length / 2);
            Vector3 point = basis.Position + Quaternion.Transform(local, basis.Orientation);
            baseVertices[vertex] = point;
            if (point.Y >= minY && point.Y <= maxY) baseContacts[count++] = point;
        }
        for (int vertex = 0; vertex < 8; vertex++)
        for (int axis = 1; axis <= 4; axis *= 2)
        {
            if ((vertex & axis) != 0) continue;
            Vector3 from = baseVertices[vertex];
            Vector3 delta = baseVertices[vertex | axis] - from;
            if (delta.Y == Fix64.Zero) continue;
            for (int plane = 0; plane < 2; plane++)
            {
                Fix64 time = ((plane == 0 ? minY : maxY) - from.Y) / delta.Y;
                if (time >= Fix64.Zero && time <= Fix64.One) baseContacts[count++] = from + delta * time;
            }
        }
        Vector3 right = new Vector3(forward.Z, 0, -forward.X);
        for (int a = 0; a < count; a++)
        {
            if (IsBaseContact(attacker, forward, baseContacts[a], core)) return true;
            for (int b = a + 1; b < count; b++)
            {
                Vector3 segment = baseContacts[b] - baseContacts[a];
                Fix64 lengthSquared = segment.X * segment.X + segment.Z * segment.Z;
                if (lengthSquared <= Toolbox.Epsilon) continue;
                Vector3 offset = origin - baseContacts[a];
                Fix64 time = MathHelper.Clamp((offset.X * segment.X + offset.Z * segment.Z) / lengthSquared,
                    Fix64.Zero, Fix64.One);
                if (IsBaseContact(attacker, forward, baseContacts[a] + segment * time, core)) return true;
                if (attackFullCircle) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 edge = (forward * attackHalfAngleCos + right * (attackHalfAngleSin * side)) * attackRange;
                    Fix64 cross = edge.X * segment.Z - edge.Z * segment.X;
                    if (Fix64.Abs(cross) <= Toolbox.Epsilon) continue;
                    Vector3 start = baseContacts[a] - origin;
                    Fix64 alongEdge = (start.X * segment.Z - start.Z * segment.X) / cross;
                    Fix64 alongSegment = (start.X * edge.Z - start.Z * edge.X) / cross;
                    if (alongEdge >= Fix64.Zero && alongEdge <= Fix64.One &&
                        alongSegment >= Fix64.Zero && alongSegment <= Fix64.One &&
                        IsBaseContact(attacker, forward, baseContacts[a] + segment * alongSegment, core)) return true;
                }
            }
        }
        return false;
    }

    private bool IsBaseContact(CharacterController attacker, Vector3 forward, Vector3 point, int core)
    {
        return IsInAttackSector(attacker, forward, point, Fix64.Zero, Fix64.Zero) &&
            !physics.IsBlocked(attacker.Body.Position, point, core);
    }

    /// <summary>扇形与敌人的水平圆形占地相交就可命中；双方身体高度必须重叠。</summary>
    internal bool IsInAttackSector(CharacterController attacker, Vector3 forward, Vector3 center, Fix64 radius, Fix64 height)
    {
        Vector3 offset = center - attacker.Body.Position;
        if (Fix64.Abs(offset.Y) > (attacker.Body.Height + height) / 2)
        {
            return false;
        }
        offset.Y = Fix64.Zero;
        Fix64 distanceSquared = offset.LengthSquared();
        Fix64 reach = attackRange + radius;
        if (distanceSquared > reach * reach)
        {
            return false;
        }
        if (attackFullCircle || distanceSquared <= radius * radius ||
            Vector3.Dot(forward, offset) >= Fix64.Sqrt(distanceSquared) * attackHalfAngleCos)
        {
            return true;
        }

        // 中心在夹角外时，身体仍可能碰到扇形的两条边，不能只按中心点排除。
        Vector3 right = new Vector3(forward.Z, 0, -forward.X);
        Vector3 edge = forward * attackHalfAngleCos + right * attackHalfAngleSin;
        if (TouchesSectorEdge(offset, edge, radius))
        {
            return true;
        }
        edge = forward * attackHalfAngleCos - right * attackHalfAngleSin;
        return TouchesSectorEdge(offset, edge, radius);
    }

    /// <summary>检查敌人的圆形占地是否接触扇形的一条边，边长就是配置半径。</summary>
    private bool TouchesSectorEdge(Vector3 offset, Vector3 direction, Fix64 radius)
    {
        Fix64 along = MathHelper.Clamp(Vector3.Dot(offset, direction), Fix64.Zero, attackRange);
        return (offset - direction * along).LengthSquared() <= radius * radius;
    }

    /// <summary>右键开始冲刺，可打断普攻；技能动作中和冲刺进行中的请求忽略，不缓存。</summary>
    private void TryStartDodge(int seat, BattleButtons buttons, int frame)
    {
        ref DodgeState dodge = ref dodges[seat];
        AttackState attack = attacks[seat];
        bool skillInProgress = attack.phase != AttackPhase.None && attack.skillID != BattleConst.BasicAttackSkillID &&
            frame - attack.startFrame < skill1Timing.durationFrames;
        if ((buttons & BattleButtons.Dodge) == 0 || frame < dodge.endFrame || skillInProgress)
        {
            return;
        }
        dodge = new DodgeState
        {
            startFrame = frame, endFrame = frame + dodgeDurationFrames, direction = facings[seat],
        };
        attacks[seat].phase = AttackPhase.None;
        // 立即达到冲刺速度，保留重力产生的竖直速度；后续碰撞、坡面和台阶仍交给 BEPU。
        CharacterController player = players[seat];
        Vector3 velocity = player.Body.LinearVelocity;
        velocity.X = dodge.direction.X * dodgeSpeed;
        velocity.Z = dodge.direction.Z * dodgeSpeed;
        player.Body.LinearVelocity = velocity;
        player.HorizontalMotionConstraint.ResetPositionAnchor();
    }

    /// <summary>结束冲刺并清掉水平惯性，防止空中或收招后继续按冲刺速度滑行。</summary>
    private void StopDodge(int seat, int frame)
    {
        dodges[seat].endFrame = frame;
        CharacterController player = players[seat];
        Vector3 velocity = player.Body.LinearVelocity;
        velocity.X = 0;
        velocity.Z = 0;
        player.Body.LinearVelocity = velocity;
        player.HorizontalMotionConstraint.ResetPositionAnchor();
    }

    /// <summary>执行当前帧的点击并推进前摇、判定和后摇；动作中与冷却中的点击直接忽略，不缓存连击。</summary>
    private void AdvanceAttack(int seat, BattleButtons buttons, int frame)
    {
        ref AttackState attack = ref attacks[seat];
        AttackTiming timing = attack.skillID == BattleConst.BasicAttackSkillID ? attackTiming : skill1Timing;
        if (attack.phase != AttackPhase.None)
        {
            attack.elapsedFrames = frame - attack.startFrame;
            if (attack.elapsedFrames >= timing.durationFrames)
            {
                attack.phase = AttackPhase.None;
            }
        }

        if (attack.phase == AttackPhase.None)
        {
            // 同帧两个按键都可用时先释放技能一；招式进行时不接受其他招式，也不缓存点击。
            if ((buttons & BattleButtons.Skill1) != 0 && skill1 != null && frame >= skill1ReadyFrames[seat])
            {
                timing = skill1Timing;
                skill1ReadyFrames[seat] = frame + timing.cooldownFrames;
                attack = new AttackState { skillID = skill1.skillID, startFrame = frame, phase = AttackPhase.Startup };
            }
            else if ((buttons & BattleButtons.Attack) != 0 && frame >= attackReadyFrames[seat])
            {
                timing = attackTiming;
                attackReadyFrames[seat] = frame + timing.cooldownFrames;
                attack = new AttackState { skillID = BattleConst.BasicAttackSkillID, startFrame = frame, phase = AttackPhase.Startup };
            }
        }

        if (attack.phase != AttackPhase.None)
        {
            attack.phase = attack.elapsedFrames < timing.hitStartFrame ? AttackPhase.Startup :
                attack.elapsedFrames < timing.hitEndFrame ? AttackPhase.Active : AttackPhase.Recovery;
            if (skill1 != null && attack.skillID == skill1.skillID && attack.elapsedFrames == timing.hitStartFrame)
            {
                Vector3 position = players[seat].Body.Position;
                swordWaves.Add(new SwordWaveState
                {
                    id = ++nextSwordWaveID,
                    ownerSeat = seat,
                    spawnFrame = frame,
                    position = position,
                    previousPosition = position,
                    direction = facings[seat],
                    remainingDistance = waveRange,
                });
            }
        }
    }

    /// <summary>按物理子步推进剑气；先遇到的墙或敌人结束飞行，同距离时地图优先。</summary>
    private void AdvanceSwordWaves(int frame)
    {
        Fix64 distancePerStep = waveSpeed / (BattleConst.LogicFrameRate * PhysicsConst.PhysicsSubsteps);
        for (int i = 0; i < swordWaves.Count; i++)
        {
            SwordWaveState wave = swordWaves[i];
            if (wave.finished)
            {
                continue;
            }
            Fix64 distance = MathHelper.Min(wave.remainingDistance, distancePerStep);
            Vector3 sweep = wave.direction * distance;
            bool collided = physics.CastSwordWaveOnMap(wave.position, sweep, waveRadius, out Fix64 hitTime);
            int hitSeat = -1;
            int hitCore = -1;
            for (int target = 0; target < players.Length; target++)
            {
                if (playerStates[target].IsDead ||
                    playerStates[target].camp == playerStates[wave.ownerSeat].camp)
                {
                    continue;
                }
                if (physics.CastSwordWaveOnPlayer(wave.position, sweep, waveRadius, players[target],
                    stepPositions[target], players[target].Body.Position, out Fix64 time) && (!collided || time < hitTime))
                {
                    collided = true;
                    hitTime = time;
                    hitSeat = target;
                }
            }
            for (int target = 0; target < coreStates.Length; target++)
            {
                if (coreStates[target].IsDestroyed)
                {
                    continue;
                }
                if (physics.CastSwordWaveOnCore(wave.position, sweep, waveRadius, target, out Fix64 time) &&
                    (!collided || time < hitTime))
                {
                    collided = true;
                    hitTime = time;
                    hitSeat = -1;
                    hitCore = target;
                }
            }
            int hitMonster = Monsters == null ? -1 : Monsters.CastSwordWave(wave.position, sweep, waveRadius, ref hitTime, collided);
            if (hitMonster >= 0)
            {
                collided = true;
                hitSeat = -1;
                hitCore = -1;
                Monsters.RecordDamage(hitMonster, wave.ownerSeat, skill1.damage);
            }
            wave.position += sweep * hitTime;
            wave.remainingDistance -= distance * hitTime;
            wave.finished = collided || wave.remainingDistance <= Fix64.Zero;
            if (hitSeat >= 0)
            {
                RecordDamage(wave.ownerSeat, hitSeat, skill1.damage);
            }
            if (hitCore >= 0 && coreStates[hitCore].camp != playerStates[wave.ownerSeat].camp)
            {
                pendingCoreDamage[hitCore] += skill1.damage;
            }
            swordWaves[i] = wave;
        }
    }

    /// <summary>到时在本席位原出生点满血复活，重建身体以清空旧接触、支撑和速度。</summary>
    private void AdvanceRespawn(int seat, int frame)
    {
        ref PlayerBattleState state = ref playerStates[seat];
        if (!state.IsDead || frame < state.respawnFrame)
        {
            return;
        }
        players[seat] = physics.CreatePlayer(spawnPositions[seat], hero);
        state.hp = state.maxHp;
        state.lastDamage = 0;
        state.lastHitFrame = -1;
        state.respawnFrame = frame;
        attacks[seat] = default;
        dodges[seat].endFrame = frame;
        lastInputs[seat] = default;
        facings[seat] = new Vector3(0, 0, 1);
    }

    private static int SecondsToFrames(float seconds) =>
        (int)Fix64.Ceiling((Fix64)(decimal)seconds * BattleConst.LogicFrameRate);
}
