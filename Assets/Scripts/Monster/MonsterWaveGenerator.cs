using System;
using System.Collections.Generic;

/// <summary>决定每波何时出、双方分多少预算、随机出哪些怪，并限制存活与待生成的积压。</summary>
public class MonsterWaveGenerator
{
    private MapMonsterData config;
    private MapMonsterTypeData[] types;
    private uint randomState;
    private int nextWaveFrame;
    private int population;
    // 按原定进攻阵营统计，怪物临时反击玩家时不改变归属；死亡才释放强度。
    private int[] campStrength = new int[2];

    public Queue<(BattleCamp camp, int typeID)> PendingSpawns { get; } = new Queue<(BattleCamp camp, int typeID)>();
    public int AliveCount => population - PendingSpawns.Count;
    public int WaveNumber { get; private set; }

    public MonsterWaveGenerator(MapMonsterData config, int randomSeed)
    {
        this.config = config;
        randomState = unchecked((uint)randomSeed);
        nextWaveFrame = config.firstWaveFrame;
        types = (MapMonsterTypeData[])config.types.Clone();
        // 按 ID 遍历，Inspector 中调整类型排列不会改变相同种子的抽取结果。
        Array.Sort(types, (a, b) => a.typeID.CompareTo(b.typeID));
    }

    public int GetCampStrength(BattleCamp camp) => campStrength[(int)camp - (int)BattleCamp.CampA];

    /// <summary>固定时间出下一波，不等待清场；被上限削减的预算当波作废。</summary>
    public void BeginFrame(int frame, int rageA, int rageB)
    {
        if (frame < nextWaveFrame) return;
        nextWaveFrame += config.waveIntervalFrames;
        WaveNumber++;
        long rageDifference = (long)rageA - rageB;
        long difference = Math.Min(Math.Abs(rageDifference), config.rageDifferenceForMaxBias);
        long tilt = difference * (config.higherRagePercent - 50) * Math.Sign(rageDifference);
        long denominator = 100L * config.rageDifferenceForMaxBias;
        long numerator = config.waveBudget * (50L * config.rageDifferenceForMaxBias + tilt);
        int budgetA = (int)(numerator / denominator);
        // 只在最后取整；恰好半点时交替分配，避免奇数预算长期偏向同一方。
        long remainder = numerator % denominator;
        if (remainder * 2 > denominator || remainder * 2 == denominator && WaveNumber % 2 == 1) budgetA++;
        int[] budgets =
        {
            Math.Min(budgetA, config.maxCampStrength - campStrength[0]),
            Math.Min(config.waveBudget - budgetA, config.maxCampStrength - campStrength[1]),
        };
        int[,] typeCounts = new int[2, types.Length];
        // 两方轮流配一只，数量上限不足以容纳整波时，交替优先方。
        bool added;
        do
        {
            added = false;
            for (int offset = 0; offset < 2 && population < config.maxAlive; offset++)
            {
                int camp = (WaveNumber - 1 + offset) % 2;
                int type = PickType(budgets[camp], camp, typeCounts);
                if (type < 0) continue;
                MapMonsterTypeData selected = types[type];
                PendingSpawns.Enqueue(((BattleCamp)((int)BattleCamp.CampA + camp), selected.typeID));
                budgets[camp] -= selected.spawnCost;
                campStrength[camp] += selected.spawnCost;
                typeCounts[camp, type]++;
                population++;
                added = true;
            }
        } while (added && population < config.maxAlive);
    }

    /// <summary>只在可负担、已解锁且未达本波上限的类型中按权重抽取。</summary>
    private int PickType(int budget, int camp, int[,] counts)
    {
        int weight = 0;
        for (int index = 0; index < types.Length; index++)
            if (CanPick(index, budget, camp, counts)) weight += types[index].spawnWeight;
        // 预算已用完，或剩余预算无法购买任何当前允许的类型。
        if (weight == 0) return -1;
        int choice = NextRandom(weight);
        int selected = -1;
        while (choice >= 0)
        {
            selected++;
            if (CanPick(selected, budget, camp, counts)) choice -= types[selected].spawnWeight;
        }
        return selected;
    }

    private bool CanPick(int index, int budget, int camp, int[,] counts)
    {
        MapMonsterTypeData type = types[index];
        return type.spawnWeight > 0 && type.spawnCost <= budget && type.unlockWave <= WaveNumber &&
            (type.maxPerCampPerWave == 0 || counts[camp, index] < type.maxPerCampPerWave);
    }

    /// <summary>只供配怪使用的整数随机序列，种子来自开战数据，不受画面帧率和其他随机调用影响。</summary>
    private int NextRandom(int count)
    {
        randomState = unchecked(randomState * 1664525u + 1013904223u);
        return (int)(((ulong)randomState * (uint)count) >> 32);
    }

    /// <summary>怪物死亡时释放原定进攻方的强度和全场数量名额，尸体不再占用。</summary>
    public void Release(BattleCamp camp, int cost)
    {
        campStrength[(int)camp - (int)BattleCamp.CampA] -= cost;
        population--;
    }

    /// <summary>战斗结束取消尚未落地的怪物；已存活怪物的名额保持到死亡。</summary>
    public void ClearPending()
    {
        while (PendingSpawns.Count > 0)
        {
            var spawn = PendingSpawns.Dequeue();
            MapMonsterTypeData type = Array.Find(types, item => item.typeID == spawn.typeID);
            Release(spawn.camp, type.spawnCost);
        }
    }
}
