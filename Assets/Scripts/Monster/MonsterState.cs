using BEPUutilities;

/// <summary>一只中立怪物的逻辑状态，模型只能读取它，不能反过来决定移动或伤害。</summary>
public struct MonsterState
{
    public int id;
    /// <summary>配置与表现预制体使用的怪物类型 ID。</summary>
    public int typeID;
    /// <summary>上帧与当前帧的脚底位置，供模型插值显示。</summary>
    public Vector3 previousPosition;
    public Vector3 position;
    public Vector3 facing;
    public int hp;
    /// <summary>本波出生时分配的基地目标；临时反击结束后继续进攻它。</summary>
    public BattleCamp targetCamp;
    /// <summary>正在反击的英雄席位；-1 表示进攻原定基地。</summary>
    public int targetSeat;
    public int attackStartFrame;
    public int deathFrame;
    public bool isAttacking;
    public bool IsDead => hp == 0;
}
