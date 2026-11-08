using System;
using System.Collections.Generic;
using BEPUphysics.Character;
using BEPUutilities;
using FixMath.NET;

/// <summary>按逻辑帧刷中立怪物、寻路、反击和结算伤害；不依赖 Unity 或网络请求。</summary>
public class MonsterSimulation
{
    private class Monster
    {
        public MonsterState state;
        public MapMonsterTypeData type;
        public CharacterController body;
        public Vector3 stepPosition;
        public Vector3 chaseOrigin;
        public int retaliationEndFrame;
        public int nextDecisionFrame;
        public int[] lastSwingFrames = new int[BattleConst.SeatCount];
        public long[] damageBySeat = new long[BattleConst.SeatCount];
    }

    private BattleSimulation battle;
    private BattlePhysics physics;
    private HeroConfigData hero;
    private MonsterNavigation navigation;
    private List<Monster> monsters = new List<Monster>();
    private Stack<Monster> recycled = new Stack<Monster>();
    private MonsterWaveGenerator waves;
    private Dictionary<int, MapMonsterTypeData> types = new Dictionary<int, MapMonsterTypeData>();
    private Vector3[] spawnPositions;
    private int spawnCursor;
    private int nextID;

    public MapMonsterData Config { get; }
    /// <summary>包含仍在显示死亡动作的怪物；死亡动作结束后从列表移除。</summary>
    public int Count => monsters.Count;
    public int AliveCount => waves.AliveCount;
    public int WaveNumber => waves.WaveNumber;
    public int PendingSpawnCount => waves.PendingSpawns.Count;
    public int GetCampStrength(BattleCamp camp) => waves.GetCampStrength(camp);
    public MonsterState GetState(int index) => monsters[index].state;
    /// <summary>从本局导出的类型配置读取最大生命，供头顶血条显示。</summary>
    public int GetMaxHp(int typeID) => types[typeID].maxHp;

    public MonsterSimulation(BattleSimulation battle, BattlePhysics physics, HeroConfigData hero, int randomSeed)
    {
        this.battle = battle;
        this.physics = physics;
        this.hero = hero;
        Config = physics.Monsters;
        foreach (MapMonsterTypeData type in Config.types) types.Add(type.typeID, type);
        navigation = new MonsterNavigation(physics, Config);
        spawnPositions = navigation.GetSpawnPositions();
        foreach (MapMonsterTypeData type in Config.types)
        for (int camp = 0; camp < 2; camp++)
        {
            if (!Array.Exists(spawnPositions, position => navigation.CanReach(position, navigation.CoreField(type.typeID, camp))))
            {
                throw new InvalidOperationException("怪物类型 " + type.typeID + " 的出生区域没有通往阵营 " + (camp + 1) + " 基地的路径。");
            }
        }
        waves = new MonsterWaveGenerator(Config, randomSeed);
    }

    /// <summary>每帧先回收尸体并处理刷怪，再按稳定的怪物 ID 顺序决策。</summary>
    public void BeginFrame(int frame)
    {
        for (int index = monsters.Count - 1; index >= 0; index--)
        {
            Monster monster = monsters[index];
            if (monster.state.IsDead && frame >= monster.state.deathFrame + Config.corpseFrames)
            {
                monsters.RemoveAt(index);
                recycled.Push(monster);
            }
        }
        waves.BeginFrame(frame, battle.GetCampState(BattleCamp.CampA).coreRage,
            battle.GetCampState(BattleCamp.CampB).coreRage);
        for (int spawned = 0; spawned < Config.spawnsPerFrame && waves.PendingSpawns.Count > 0; spawned++)
        {
            if (!TrySpawn(waves.PendingSpawns.Peek(), frame)) break;
            waves.PendingSpawns.Dequeue();
        }
        if (frame % Config.decisionIntervalFrames == 0)
        {
            for (int seat = 0; seat < BattleConst.SeatCount; seat++)
            {
                if (!battle.GetPlayerState(seat).IsDead) navigation.SetTarget(navigation.PlayerField(seat), battle.GetPosition(seat));
            }
        }
        foreach (Monster monster in monsters)
        {
            monster.state.previousPosition = monster.state.position;
            if (!monster.state.IsDead) AdvanceAI(monster, frame);
        }
    }

    private bool TrySpawn((BattleCamp camp, int typeID) spawn, int frame)
    {
        for (int attempt = 0; attempt < spawnPositions.Length; attempt++)
        {
            Vector3 feet = spawnPositions[spawnCursor];
            spawnCursor = (spawnCursor + 1) % spawnPositions.Length;
            if (!navigation.CanReach(feet, navigation.CoreField(spawn.typeID, (int)spawn.camp - (int)BattleCamp.CampA)) || !IsSpawnFree(feet)) continue;
            Monster monster = recycled.Count > 0 ? recycled.Pop() : new Monster();
            monster.type = types[spawn.typeID];
            monster.body = physics.CreateMonster(feet, Config, monster.type);
            monster.state = new MonsterState
            {
                id = ++nextID, typeID = spawn.typeID, previousPosition = feet, position = feet,
                facing = new Vector3(0, 0, 1), hp = monster.type.maxHp,
                targetCamp = spawn.camp, targetSeat = -1, attackStartFrame = -1, deathFrame = -1,
            };
            monster.retaliationEndFrame = -1;
            monster.nextDecisionFrame = frame;
            Array.Clear(monster.damageBySeat, 0, monster.damageBySeat.Length);
            for (int seat = 0; seat < BattleConst.SeatCount; seat++) monster.lastSwingFrames[seat] = -1;
            monsters.Add(monster);
            return true;
        }
        // 出生区被身体占满时保留队列，后续帧腾出空间后再生成，不把怪物叠在一起。
        return false;
    }

    private bool IsSpawnFree(Vector3 feet)
    {
        foreach (Monster other in monsters)
        {
            if (!other.state.IsDead && OverlapsSpawn(feet, other.state.position, Config.radius, Config.height)) return false;
        }
        for (int seat = 0; seat < BattleConst.SeatCount; seat++)
        {
            if (!battle.GetPlayerState(seat).IsDead &&
                OverlapsSpawn(feet, battle.GetPosition(seat), (Fix64)hero.collisionRadius, (Fix64)hero.collisionHeight)) return false;
        }
        return true;
    }

    private bool OverlapsSpawn(Vector3 feet, Vector3 otherFeet, Fix64 radius, Fix64 height)
    {
        if (feet.Y >= otherFeet.Y + height || feet.Y + Config.height <= otherFeet.Y) return false;
        Vector3 delta = otherFeet - feet;
        Fix64 combinedRadius = Config.radius + radius + (Fix64)0.05m;
        return delta.X * delta.X + delta.Z * delta.Z < combinedRadius * combinedRadius;
    }

    private void AdvanceAI(Monster monster, int frame)
    {
        ref MonsterState state = ref monster.state;
        if (state.targetSeat >= 0 && (frame >= monster.retaliationEndFrame ||
            battle.GetPlayerState(state.targetSeat).IsDead ||
            (battle.GetPosition(state.targetSeat) - monster.chaseOrigin).LengthSquared() > Config.chaseRadius * Config.chaseRadius))
        {
            state.targetSeat = -1;
            state.isAttacking = false;
            monster.nextDecisionFrame = frame;
        }
        Vector3 target = TargetCenter(monster, out Fix64 radius, out Fix64 height);
        if (state.isAttacking)
        {
            int elapsed = frame - state.attackStartFrame;
            if (elapsed == monster.type.attackWindupFrames &&
                CanAttack(monster, target, radius, height))
            {
                if (state.targetSeat >= 0) battle.RecordMonsterDamage(state.targetSeat, monster.type.damage);
                else battle.RecordCoreDamage((int)state.targetCamp - (int)BattleCamp.CampA, monster.type.damage);
            }
            if (elapsed < monster.type.attackDurationFrames) return;
            state.isAttacking = false;
        }
        if (frame < monster.nextDecisionFrame) return;
        monster.nextDecisionFrame = frame + Config.decisionIntervalFrames;
        bool inRange = CanAttack(monster, target, radius, height);
        if (inRange)
        {
            monster.body.HorizontalMotionConstraint.MovementDirection = Vector2.Zero;
            Vector3 facing = target - monster.body.Body.Position;
            facing.Y = Fix64.Zero;
            if (facing.LengthSquared() > Toolbox.Epsilon) { facing.Normalize(); state.facing = facing; }
            if (state.attackStartFrame < 0 || frame - state.attackStartFrame >= monster.type.attackIntervalFrames)
            {
                state.isAttacking = true;
                state.attackStartFrame = frame;
                // 前摇为零的配置当帧命中，仍不依赖动画事件。
                if (monster.type.attackWindupFrames == 0)
                {
                    if (state.targetSeat >= 0) battle.RecordMonsterDamage(state.targetSeat, monster.type.damage);
                    else battle.RecordCoreDamage((int)state.targetCamp - (int)BattleCamp.CampA, monster.type.damage);
                }
            }
            return;
        }
        int field = state.targetSeat >= 0 ? navigation.PlayerField(state.targetSeat) :
            navigation.CoreField(state.typeID, (int)state.targetCamp - (int)BattleCamp.CampA);
        Vector3 feet = state.targetSeat >= 0 ? battle.GetPosition(state.targetSeat) : target - new Vector3(0, radius, 0);
        Vector3 direction = navigation.GetDirection(state.position, feet, field);
        monster.body.HorizontalMotionConstraint.MovementDirection = new Vector2(direction.X, -direction.Z);
        if (direction.LengthSquared() > Toolbox.Epsilon) state.facing = direction;
    }

    private Vector3 TargetCenter(Monster monster, out Fix64 radius, out Fix64 height)
    {
        if (monster.state.targetSeat >= 0)
        {
            radius = (Fix64)hero.collisionRadius;
            height = (Fix64)hero.collisionHeight;
            return battle.GetPosition(monster.state.targetSeat) + new Vector3(0, height / 2, 0);
        }
        CoreBaseState core = battle.GetCoreState(monster.state.targetCamp);
        radius = core.radius;
        height = core.radius * 2;
        return core.center;
    }

    private bool CanAttack(Monster monster, Vector3 target, Fix64 radius, Fix64 height)
    {
        return physics.CanMonsterAttack(monster.state.position, target, radius, height, Config, monster.type,
            monster.state.targetSeat < 0 ? (int)monster.state.targetCamp - (int)BattleCamp.CampA : -1);
    }

    public void BeforePhysicsStep()
    {
        foreach (Monster monster in monsters)
        {
            if (!monster.state.IsDead) monster.stepPosition = monster.body.Body.Position;
        }
    }

    public void AfterPhysicsStep()
    {
        foreach (Monster monster in monsters)
        {
            if (monster.state.IsDead) continue;
            physics.KeepInBounds(monster.body);
            monster.state.position = monster.body.Body.Position - new Vector3(0, Config.height / 2, 0);
        }
    }

    /// <summary>每只怪物分别记录四名英雄的挥刀帧，数量超过 32 也不受位掩码限制。</summary>
    public void DetectMeleeHits(int seat, CharacterController attacker, Vector3 facing, int swingFrame, int damage)
    {
        foreach (Monster monster in monsters)
        {
            if (monster.state.IsDead || monster.lastSwingFrames[seat] == swingFrame) continue;
            if (battle.IsInAttackSector(attacker, facing, monster.body.Body.Position, Config.radius, Config.height) &&
                !physics.IsBlocked(attacker.Body.Position, monster.body.Body.Position))
            {
                monster.lastSwingFrames[seat] = swingFrame;
                monster.damageBySeat[seat] += damage;
            }
        }
    }

    public int CastSwordWave(Vector3 from, Vector3 sweep, Fix64 radius, ref Fix64 hitTime, bool collided)
    {
        int target = -1;
        for (int index = 0; index < monsters.Count; index++)
        {
            Monster monster = monsters[index];
            if (!monster.state.IsDead && physics.CastSwordWaveOnPlayer(from, sweep, radius, monster.body,
                monster.stepPosition, monster.body.Body.Position, out Fix64 time) && (!collided || time < hitTime))
            {
                collided = true;
                hitTime = time;
                target = index;
            }
        }
        return target;
    }

    public void RecordDamage(int index, int attacker, int damage) => monsters[index].damageBySeat[attacker] += damage;

    /// <summary>合并玩家伤害；活着则反击本帧伤害最多的攻击者，死亡则移除身体、保留短暂死亡表现。</summary>
    public void ResolveDamage(int frame)
    {
        foreach (Monster monster in monsters)
        {
            long damage = 0;
            long highest = 0;
            int attacker = -1;
            for (int seat = 0; seat < BattleConst.SeatCount; seat++)
            {
                long amount = monster.damageBySeat[seat];
                damage += amount;
                if (amount > highest) { highest = amount; attacker = seat; }
                monster.damageBySeat[seat] = 0;
            }
            if (damage == 0 || monster.state.IsDead) continue;
            monster.state.hp -= (int)Math.Min(monster.state.hp, damage);
            if (monster.state.IsDead)
            {
                monster.state.deathFrame = frame;
                monster.state.isAttacking = false;
                physics.Space.Remove(monster.body);
                waves.Release(monster.state.targetCamp, monster.type.spawnCost);
            }
            else
            {
                monster.state.targetSeat = attacker;
                monster.chaseOrigin = monster.state.position;
                monster.retaliationEndFrame = frame + Config.retaliationFrames;
                monster.nextDecisionFrame = frame + 1;
                monster.state.isAttacking = false;
                navigation.SetTarget(navigation.PlayerField(attacker), battle.GetPosition(attacker));
            }
        }
    }

    public void Stop()
    {
        waves.ClearPending();
        foreach (Monster monster in monsters)
        {
            monster.state.isAttacking = false;
            monster.state.previousPosition = monster.state.position;
            monster.body.HorizontalMotionConstraint.MovementDirection = Vector2.Zero;
        }
    }
}
