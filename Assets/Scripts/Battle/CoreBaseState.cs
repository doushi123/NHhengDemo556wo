using BEPUutilities;
using FixMath.NET;

/// <summary>一座秘核基地的逻辑状态，受击和摧毁由确定性模拟更新。</summary>
public struct CoreBaseState
{
    public BattleCamp camp;
    public Vector3 center;
    public Fix64 radius;
    /// <summary>初始秘核能量，也是基地满血时的生命值。</summary>
    public int maxHp;
    /// <summary>当前秘核能量，即基地当前生命；攻击、击杀和死亡都改变这一份数值。</summary>
    public int hp;
    /// <summary>最近一帧实际受到的总伤害，不包含溢出伤害。</summary>
    public int lastDamage;
    public int lastHitFrame;
    /// <summary>基地被摧毁的逻辑帧，未摧毁时为 -1。</summary>
    public int destroyedFrame;
    /// <summary>
    /// 是否已经被销毁掉
    /// </summary>
    public bool IsDestroyed => hp == 0;
}
