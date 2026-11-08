using BEPUutilities;
using FixMath.NET;

/// <summary>根据当前确定性战斗状态，为一个电脑席位生成操作帧输入。</summary>
public class BotBrain
{
    /// <summary>目标优先级，数值越小越优先。</summary>
    private enum TargetPriority
    {
        /// <summary>优先处理我方基地附近的敌方威胁。</summary>
        DefendCore = 0,
        /// <summary>清理电脑自身附近的怪物。</summary>
        ClearNearbyMonster = 1,
        /// <summary>追击其他存活的敌方英雄。</summary>
        ChaseEnemyHero = 2,
        /// <summary>尚未选中目标。</summary>
        None = 3,
    }

    /// <summary>电脑行为配置，决定隔多少逻辑帧重新选择目标和动作。</summary>
    private BotConfigData config;
    /// <summary>普攻扇形半径，单位为米；加上目标身体半径后估算是否能攻击。</summary>
    private Fix64 attackRange;
    /// <summary>英雄身体半径，单位为米；用于判断敌方英雄是否进入攻击或守家范围。</summary>
    private Fix64 heroRadius;
    /// <summary>基地碰撞边缘向外的防守距离，单位为米；范围内的敌方威胁优先处理。</summary>
    private Fix64 coreDefenseRange;
    /// <summary>电脑自身周围主动清怪的距离，单位为米；没有基地威胁时优先处理范围内的怪物。</summary>
    private Fix64 monsterNoticeRange;
    /// <summary>下次重新选择目标和动作的逻辑帧号。</summary>
    private int nextDecisionFrame;
    /// <summary>最近一次决策生成的输入；两次决策之间沿用移动方向，清除攻击、技能和冲刺按钮。</summary>
    private BattleInput currentInput;

    public BotBrain(BotConfigData config, float attackRange, float targetRadius)
    {
        this.config = config;
        this.attackRange = (Fix64)attackRange;
        heroRadius = (Fix64)targetRadius;
        coreDefenseRange = (Fix64)config.coreDefenseRange;
        monsterNoticeRange = (Fix64)config.monsterNoticeRange;
    }

    /// <summary>
    /// 决定电脑玩家多久思考一次
    /// </summary>
    /// <param name="simulation">战斗模拟</param>
    /// <param name="seat">电脑席位</param>
    /// <param name="frame">当前逻辑帧</param>
    /// <returns></returns>
    public BattleInput GetInput(BattleSimulation simulation, int seat, int frame)
    {
        if (simulation.GetPlayerState(seat).IsDead)
        {
            currentInput = default;
            return currentInput;
        }
        //未到决策时间
        if (frame < nextDecisionFrame)
        {
            currentInput.buttons = BattleButtons.None;
            return currentInput;
        }

        nextDecisionFrame = frame + config.decisionIntervalFrames;
        currentInput = Decide(simulation, seat);
        return currentInput;
    }
    /// <summary>
    /// 选好目标后如何行动
    /// </summary>
    /// <param name="simulation"></param>
    /// <param name="seat"></param>
    /// <returns></returns>
    private BattleInput Decide(BattleSimulation simulation, int seat)
    {
        if (!TryFindTarget(simulation, seat, out int targetSeat, out Vector3 targetPosition, out Fix64 targetRadius))
        {
            return default;
        }
        //目标位置减去电脑自己的位置，得到"从自己指向目标"的向量
        Vector3 targetDirection = targetPosition - simulation.GetPosition(seat);
        targetDirection.Y = Fix64.Zero;
        Fix64 distance = targetDirection.Length();
        //转换成单位
        if (distance != Fix64.Zero)
        {
            targetDirection /= distance;
        }
        BattleButtons buttons = BattleButtons.None;
        Fix64 attackDistance = attackRange + targetRadius;
        //目标是英雄在攻击阶段时，且距离小于攻击距离，往反方向冲刺躲避 
        if (targetSeat >= 0 && simulation.GetAttack(targetSeat).phase == AttackPhase.Active && distance <= attackDistance)
        {
            targetDirection = -targetDirection;
            buttons = BattleButtons.Dodge;
        }
        //目标未攻击且在距离内时考虑发起技能攻击
        else if (simulation.GetAttack(seat).phase == AttackPhase.None)
        {
            SkillConfigData skill = simulation.Skill1Config;
            if (skill != null && simulation.GetSkill1CooldownFrames(seat) == 0 && distance <= (Fix64)skill.range)
            {
                buttons = BattleButtons.Skill1;
            }
            else if (distance <= attackDistance)
            {
                buttons = BattleButtons.Attack;
            }
        }

        return new BattleInput
        {
            moveX = (int)(targetDirection.X * 127),
            moveY = (int)(targetDirection.Z * 127),
            isRunning = buttons != BattleButtons.Dodge,
            buttons = buttons,
        };
    }

    /// <summary>先守家，再清附近怪物，最后追敌；守家选离基地最近的，其余选离自身最近的。</summary>
    private bool TryFindTarget(BattleSimulation simulation, int seat,
        out int targetSeat, out Vector3 targetPosition, out Fix64 targetRadius)
    {
        BattleCamp camp = simulation.GetPlayerState(seat).camp;
        Vector3 position = simulation.GetPosition(seat);
        CoreBaseState home = simulation.GetCoreState(camp);
        //计算从基地中心出发的防守距离
        Fix64 defenseDistance = home.radius + coreDefenseRange;
        TargetPriority bestPriority = TargetPriority.None;
        Fix64 bestDistance = Fix64.Zero;
        targetSeat = -1;
        targetPosition = default;
        targetRadius = Fix64.Zero;

        for (int candidateSeat = 0; candidateSeat < BattleConst.SeatCount; candidateSeat++)
        {
            PlayerBattleState candidate = simulation.GetPlayerState(candidateSeat);
            if (candidate.IsDead || candidate.camp == camp) continue;
            //读取候选敌人的位置
            Vector3 candidatePosition = simulation.GetPosition(candidateSeat);
            //计算敌人到我方基地中心的水平距离平方
            Fix64 homeDistance = HorizontalDistanceSquared(candidatePosition, home.center);
            //判断敌人的身体是否进入基地防守范围
            TargetPriority priority = homeDistance <= (defenseDistance + heroRadius) * (defenseDistance + heroRadius)? TargetPriority.DefendCore : TargetPriority.ChaseEnemyHero;
            Fix64 distance = priority == TargetPriority.DefendCore? homeDistance : HorizontalDistanceSquared(position, candidatePosition);
            //判断是否放弃当前候选，只有候选目标优先级更高才更新
            if ((int)priority > (int)bestPriority || (priority == bestPriority && distance >= bestDistance)) continue;
            bestPriority = priority;
            bestDistance = distance;
            targetSeat = candidateSeat;
            targetPosition = candidatePosition;
            targetRadius = heroRadius;
        }

        MonsterSimulation monsters = simulation.Monsters;
        Fix64 monsterRadius = monsters.Config.radius;
        //遍历当前怪物列表
        for (int index = 0; index < monsters.Count; index++)
        {
            MonsterState candidate = monsters.GetState(index);
            if (candidate.IsDead) continue;
            Fix64 homeDistance = HorizontalDistanceSquared(candidate.position, home.center);
            Fix64 selfDistance = HorizontalDistanceSquared(position, candidate.position);
            bool threatensHome = candidate.targetSeat >= 0? simulation.GetPlayerState(candidate.targetSeat).camp == camp: candidate.targetCamp == camp;
            TargetPriority priority = threatensHome && homeDistance <=
                (defenseDistance + monsterRadius) * (defenseDistance + monsterRadius)
                ? TargetPriority.DefendCore : TargetPriority.ClearNearbyMonster;
            if (priority == TargetPriority.ClearNearbyMonster && selfDistance > monsterNoticeRange * monsterNoticeRange) continue;
            Fix64 distance = priority == TargetPriority.DefendCore ? homeDistance : selfDistance;
            if ((int)priority > (int)bestPriority || (priority == bestPriority && distance >= bestDistance)) continue;
            bestPriority = priority;
            bestDistance = distance;
            targetSeat = -1;
            targetPosition = candidate.position;
            targetRadius = monsterRadius;
        }
        return bestPriority != TargetPriority.None;
    }
    /// <summary>
    /// 计算两个位置的水平距离的平方
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <returns></returns>
    private static Fix64 HorizontalDistanceSquared(Vector3 from, Vector3 to)
    {
        Vector3 difference = to - from;
        return difference.X * difference.X + difference.Z * difference.Z;
    }
}
