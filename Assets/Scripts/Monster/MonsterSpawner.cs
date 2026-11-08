using System.Collections.Generic;
using System.Linq;
using FixMath.NET;
using UnityEngine;

/// <summary>在场景里配置中立刷怪；开战后用对象池显示模拟中的怪物，不用 Unity 时间决定生成。</summary>
public class MonsterSpawner : MonoBehaviour
{
    /// <summary>一种怪物的预制体、属性和攻击动画；导出时只保存战斗数值。</summary>
    [System.Serializable]
    public class MonsterType
    {
        public int typeID = 1;
        public string displayName = "普通怪物";
        public MonsterView prefab;
        [Tooltip("生成一只消耗的预算；普通/轻型/重型建议先设 1/3/6。")]
        public int spawnCost = 1;
        [Tooltip("参与随机配怪的相对权重，0 表示不生成。")]
        public int spawnWeight = 1;
        [Tooltip("从第几波开始出现，第一波为 1。")]
        public int unlockWave = 1;
        [Tooltip("每方每波的此类型数量上限，0 表示不单独限制。")]
        public int maxPerCampPerWave;
        public int maxHp = 150;
        public int damage = 20;
        public float moveSpeed = 2.4f;
        public float attackRange = 1.1f;
        [Tooltip("实际攻击动画，需要一个 AtkEvent，导出时读取命中时间。")]
        public AnimationClip attackAnimation;
        public float attackIntervalSeconds = 1.2f;
    }

    [Header("怪物类型与对象池")]
    [SerializeField] private MonsterType[] monsterTypes;
    [Tooltip("所有类型合计的预热数量，按类型均分。")]
    [SerializeField] private int prewarmCount = 24;

    [Header("出生表现（不参与战斗数值）")]
    [Tooltip("绑定 MonsterSpawnEffect 预制体；位置来自实际生成的怪物脚底，大小与偏移在预制体里调整。")]
    [SerializeField] private MonsterSpawnEffect spawnEffectPrefab;

    [Header("刷怪（修改后重新导出地图）")]
    [Tooltip("在这些标记附近的可通行地面生成，位置按脚底摆放。")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnRadius = 5f;
    [SerializeField] private float firstWaveSeconds = 5f;
    [SerializeField] private float waveIntervalSeconds = 20f;
    [Tooltip("双方合计的每波预算，怪物数量由随机组合决定。")]
    [SerializeField] private int waveBudget = 24;
    [Tooltip("每方存活与待生成怪物的强度上限，达到上限就削减本波，不补发。")]
    [SerializeField] private int maxCampStrength = 48;
    [Tooltip("每逻辑帧最多生成几只，整波分批落地，避免同帧大量创建物理身体。")]
    [SerializeField] private int spawnsPerFrame = 4;
    [Tooltip("存活怪物与待生成怪物的总上限，到上限后本波不再追加。")]
    [SerializeField] private int maxAlive = 120;
    [Tooltip("狂暴差达到下方设定值时，高狂暴方最多分到的预算百分比。")]
    [SerializeField] private int higherRagePercent = 60;
    [Tooltip("达到最大倾斜所需的狂暴差，差距更小时平滑地按比例分配。")]
    [SerializeField] private int rageDifferenceForMaxBias = 100;

    [Header("共同碰撞体与爬坡能力")]
    [SerializeField] private float collisionRadius = 0.35f;
    [SerializeField] private float collisionHeight = 1.5f;
    [SerializeField] private float stepHeight = 0.3f;
    [SerializeField] private float maxSlope = 45f;

    [Header("反击与寻路")]
    [SerializeField] private float retaliationSeconds = 5f;
    [Tooltip("玩家离受击地点超过这个距离后，怪物放弃追击，回去攻打原定基地。")]
    [SerializeField] private float chaseRadius = 10f;
    [SerializeField] private float corpseSeconds = 2f;
    [Tooltip("每个 XZ 格子只采样最上层地面；尺寸越小，寻路越细，入场构建越慢。")]
    [SerializeField] private float navigationCellSize = 1.25f;
    [SerializeField] private int decisionIntervalFrames = 5;

    private BattleSimulation simulation;
    private Dictionary<int, Stack<MonsterView>> pools = new Dictionary<int, Stack<MonsterView>>();
    private Dictionary<int, MonsterType> types = new Dictionary<int, MonsterType>();
    private Dictionary<int, MonsterView> views = new Dictionary<int, MonsterView>();
    private HashSet<int> visibleIDs = new HashSet<int>();
    private List<int> removedIDs = new List<int>();
    private List<MonsterSpawnEffect> spawnEffects = new List<MonsterSpawnEffect>();
    private int displayedFrame = -2;
    private float frameReceivedAt;

    /// <summary>把 Inspector 数值和出生标记写入正式地图，浮点参数在此转成定点数。</summary>
    public MapMonsterData BuildData()
    {
        MapMonsterTypeData[] data = new MapMonsterTypeData[monsterTypes.Length];
        for (int i = 0; i < data.Length; i++) data[i] = BuildType(monsterTypes[i]);
        MapVector[] spawns = new MapVector[spawnPoints.Length];
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Vector3 position = spawnPoints[i].position;
            spawns[i] = new MapVector { x = (Fix64)position.x, y = (Fix64)position.y, z = (Fix64)position.z };
        }
        return new MapMonsterData
        {
            spawns = spawns, spawnRadius = (Fix64)spawnRadius,
            firstWaveFrame = ToFrames(firstWaveSeconds), waveIntervalFrames = ToFrames(waveIntervalSeconds),
            waveBudget = waveBudget, maxCampStrength = maxCampStrength,
            spawnsPerFrame = spawnsPerFrame, maxAlive = maxAlive,
            higherRagePercent = higherRagePercent, rageDifferenceForMaxBias = rageDifferenceForMaxBias, types = data,
            radius = (Fix64)collisionRadius, height = (Fix64)collisionHeight,
            stepHeight = (Fix64)stepHeight, maxSlope = (Fix64)maxSlope,
            retaliationFrames = ToFrames(retaliationSeconds),
            chaseRadius = (Fix64)chaseRadius, corpseFrames = ToFrames(corpseSeconds),
            navigationCellSize = (Fix64)navigationCellSize, decisionIntervalFrames = decisionIntervalFrames,
        };
    }

    private static MapMonsterTypeData BuildType(MonsterType type)
    {
        float hitTime = type.attackAnimation.events.Single(item => item.functionName == "AtkEvent").time;
        return new MapMonsterTypeData
        {
            typeID = type.typeID, spawnCost = type.spawnCost, spawnWeight = type.spawnWeight,
            unlockWave = type.unlockWave, maxPerCampPerWave = type.maxPerCampPerWave,
            maxHp = type.maxHp, damage = type.damage,
            moveSpeed = (Fix64)type.moveSpeed, attackRange = (Fix64)type.attackRange,
            attackWindupFrames = ToFrames(hitTime), attackDurationFrames = ToFrames(type.attackAnimation.length),
            attackIntervalFrames = ToFrames(type.attackIntervalSeconds),
        };
    }

    /// <summary>正式模拟准备好后绑定本局怪物，并预热显示对象池。</summary>
    public void PrepareBattle(BattleSimulation battle)
    {
        ReleaseAll();
        simulation = battle;
        displayedFrame = -2;
        if (spawnEffectPrefab == null)
            Debug.LogWarning("MonsterSpawner 未绑定 Spawn Effect Prefab，怪物出生时不会播放魔能特效。", this);
        else
        {
            while (spawnEffects.Count < prewarmCount)
            {
                MonsterSpawnEffect effect = Instantiate(spawnEffectPrefab, transform);
                effect.gameObject.SetActive(false);
                spawnEffects.Add(effect);
            }
        }
        types.Clear();
        int index = 0;
        foreach (MonsterType type in monsterTypes)
        {
            types.Add(type.typeID, type);
            if (!pools.ContainsKey(type.typeID)) pools.Add(type.typeID, new Stack<MonsterView>());
            Stack<MonsterView> pool = pools[type.typeID];
            int count = prewarmCount / monsterTypes.Length + (index < prewarmCount % monsterTypes.Length ? 1 : 0);
            while (pool.Count < count) pool.Push(CreateView(type));
            index++;
        }
    }

    private MonsterView CreateView(MonsterType type)
    {
        MonsterView view = Instantiate(type.prefab, transform);
        view.gameObject.SetActive(false);
        return view;
    }

    private void Update()
    {
        // 地图场景先加载，随后网络入口才会创建本局模拟。
        if (simulation == null) return;
        MonsterSimulation monsters = simulation.Monsters;
        if (displayedFrame != simulation.Frame)
        {
            displayedFrame = simulation.Frame;
            frameReceivedAt = Time.unscaledTime;
            visibleIDs.Clear();
            for (int index = 0; index < monsters.Count; index++)
            {
                MonsterState state = monsters.GetState(index);
                visibleIDs.Add(state.id);
                if (!views.TryGetValue(state.id, out MonsterView view))
                {
                    Stack<MonsterView> pool = pools[state.typeID];
                    view = pool.Count > 0 ? pool.Pop() : CreateView(types[state.typeID]);
                    view.Show(state);
                    views.Add(state.id, view);
                    if (!state.IsDead && spawnEffectPrefab != null) PlaySpawnEffect(view.transform.position);
                }
                view.SetState(state, monsters.GetMaxHp(state.typeID));
            }
            removedIDs.Clear();
            foreach (KeyValuePair<int, MonsterView> pair in views)
            {
                if (!visibleIDs.Contains(pair.Key)) removedIDs.Add(pair.Key);
            }
            foreach (int id in removedIDs)
            {
                ReturnView(views[id]);
                views.Remove(id);
            }
        }
        float progress = Mathf.Clamp01((Time.unscaledTime - frameReceivedAt) * BattleConst.LogicFrameRate);
        foreach (MonsterView view in views.Values) view.Display(progress);
    }

    private void ReturnView(MonsterView view)
    {
        view.gameObject.SetActive(false);
        // 始终保留在刷怪对象下面，父对象停用时也能回收，不在 OnDisable 中修改层级。
        pools[view.TypeID].Push(view);
    }

    private void ReleaseAll()
    {
        foreach (MonsterView view in views.Values) ReturnView(view);
        views.Clear();
        foreach (MonsterSpawnEffect effect in spawnEffects) effect.gameObject.SetActive(false);
    }

    private void PlaySpawnEffect(Vector3 position)
    {
        foreach (MonsterSpawnEffect effect in spawnEffects)
        {
            if (!effect.gameObject.activeSelf)
            {
                effect.Play(position);
                return;
            }
        }
        // 特效属于刷怪器而非怪物，怪物移动/回收不会拖走出生光环。
        MonsterSpawnEffect created = Instantiate(spawnEffectPrefab, transform);
        spawnEffects.Add(created);
        created.Play(position);
    }

    private void OnDisable()
    {
        ReleaseAll();
        simulation = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.magenta;
        foreach (Transform point in spawnPoints)
        {
            if (point != null) Gizmos.DrawWireSphere(point.position, spawnRadius);
        }
    }

    private static int ToFrames(float seconds) =>
        (int)Fix64.Ceiling((Fix64)(decimal)seconds * BattleConst.LogicFrameRate);
}
