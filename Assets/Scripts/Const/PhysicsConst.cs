/// <summary>
/// 确定性物理模拟使用的常量。
/// </summary>
public static class PhysicsConst
{
    /// <summary>每个逻辑帧执行的物理子步数；逻辑 20 Hz 时，三个子步对应物理 60 Hz。</summary>
    public const int PhysicsSubsteps = 3;

    /// <summary>竖直重力加速度，单位为米/秒²，负值表示向下。</summary>
    public const float Gravity = -9.81f;

    /// <summary>允许提供滑动支撑的最大坡度，单位为度；小于 90 度以满足物理库约束。</summary>
    public const float SupportSlope = 89f;
}
