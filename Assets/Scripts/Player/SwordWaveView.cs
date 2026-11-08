using UnityEngine;

/// <summary>从施法者摆好的特效位置发射粒子，之后按逻辑剑气的位移飞行，不执行伤害判定。</summary>
public class SwordWaveView
{
    private ParticleSystem effect;
    private float displayedAge;
    // 发射时记录场景模板的位置，离手后不再跟随角色或骨骼。
    private Vector3 spawnPosition;
    // 记录首次显示的逻辑轨迹起点，之后只读取轨迹相对它的位移。
    private Vector3 logicStartPosition;

    public SwordWaveView(ParticleSystem template, SwordWaveState wave)
    {
        Transform source = template.transform;
        spawnPosition = source.position;
        logicStartPosition = ToUnity(wave.previousPosition);
        effect = Object.Instantiate(template, spawnPosition, source.rotation);
        effect.name = "SwordWave_" + wave.id + "_" + template.name;
        effect.transform.localScale = source.lossyScale;
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // 只修改运行时副本，让斩击整体随逻辑剑气移动；不改原 Prefab。
        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            particles[i].useAutoRandomSeed = false;
            particles[i].randomSeed = (uint)(wave.id * 31 + i + 1);
        }
        // 模板在战斗中隐藏；副本启用并从零初始化，后续由模拟进度推进粒子。
        effect.gameObject.SetActive(true);
        effect.Simulate(0f, true, true, false);
    }

    /// <summary>用相邻逻辑位置插值；网络不推进时，粒子也不会继续飞行或播放。</summary>
    public void Refresh(SwordWaveState wave, float age, float progress)
    {
        Vector3 travel = Vector3.Lerp(ToUnity(wave.previousPosition), ToUnity(wave.position), progress) - logicStartPosition;
        effect.transform.position = spawnPosition + travel;
        effect.Simulate(Mathf.Max(0f, age - displayedAge), true, false, false);
        displayedAge = age;
    }

    /// <summary>逻辑剑气结束或退出战斗时移除对应粒子。</summary>
    public void Clear()
    {
        // 场景卸载可能已经先销毁粒子。
        if (effect != null)
        {
            Object.Destroy(effect.gameObject);
        }
    }

    private static Vector3 ToUnity(BEPUutilities.Vector3 value) => new Vector3((float)value.X, (float)value.Y, (float)value.Z);
}
