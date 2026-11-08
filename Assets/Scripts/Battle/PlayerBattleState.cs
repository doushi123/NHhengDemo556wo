/// <summary>一个角色在本局中的生命和受击结果，由模拟更新，模型和 UI 只读取。</summary>
public struct PlayerBattleState
{
    /// <summary>所属阵营。</summary>
    public BattleCamp camp;
    /// <summary>本局使用的最大生命值，来自英雄配置。</summary>
    public int maxHp;
    /// <summary>当前生命值。</summary>
    public int hp;
    /// <summary>最近一次伤害结算实际扣除的血量；同一逻辑帧受到的伤害合并记录。</summary>
    public int lastDamage;
    /// <summary>最近受到伤害的逻辑帧；-1 表示本局还未受伤，供表现识别新的受击。</summary>
    public int lastHitFrame;

    /// <summary>死亡时为计划复活帧；复活后保留实际复活帧，初始存活状态为 -1。</summary>
    public int respawnFrame;

    public bool IsDead => hp == 0;
}
