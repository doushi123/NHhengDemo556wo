# WorkDemo 中的 BEPUphysicsint

来源：https://github.com/sam-vdp/bepuphysics1int 。代码来自用户下载的 `bepuphysics1int-master`；包版本 `1.0.0` 是本地接入版本，不代表上游发布版本。上游许可证保留在 `LICENSE.md`。

## 编译关系

```text
FixMath.NET → BEPUutilities → BEPUphysics
```

- Unity：三个目录通过 `Packages/manifest.json` 注册为本地包，各自的 `.asmdef` 编译独立程序集；它们都不依赖 UnityEngine。
- `Server/ThirdParty` 下三个 SDK 工程保留此前的 .NET 编译配置，目标为 `netstandard2.1`。当前 `SecretCore.Server` 已移除物理项目引用，不再构建或加载这三个库；这些目录和上游资源未删除。
- 原有 `.csproj` 留作上游资料，服务端不构建这些旧工程。
- `FixedMath.Net/tests` 已保留为 `tests~`，Unity 忽略该目录，服务端也只编译 `src`；本次没有启动测试或示例。
- 两端都不额外定义上游 `WINDOWS`、`ALLOWUNSAFE`、`CHECKMATH` 等宏，使用普通的托管代码分支。以后调整这些条件时必须检查双端是否一致。

## 本地兼容修正

1. `BEPUutilities/DataStructures/HashSet.cs`：旧平台的自制 `HashSet` 改为仅在显式定义 `BEPU_LEGACY_HASHSET` 时启用。Unity 2022 和 .NET 服务端已有标准库实现，原来的 `!WINDOWS` 条件会造成类型重名。
2. `CollisionTests/CollisionAlgorithms/BoxBoxCollider.cs`：普通托管分支的八处浮点常量表达式改为已有 `F64.C0p5` / `F64.C0p01`，与上方 unsafe 分支一致，修复 `Fix64` 和 `float` 不能直接运算的编译错误。
3. `FixedMath.Net/src/Fix64.Serialization.cs`：通过 partial 声明给 Fix64 补充 `[Serializable]`，让 Unity JsonUtility 能保存已有的公开 RawValue 字段。服务端用 IncludeFields 读写相同结构，不经过浮点转换，不修改上游运算实现。地图几何字段直接使用 Fix64；旧 long 格式地图需由用户在编辑器重新导出。

## 当前使用（2026-11-01）

各客户端的 `BattlePhysics` 加载相同地图形状与世界坐标三角网格，`BattleSimulation` 使用 BEPU 的圆柱角色控制器处理三维移动、重力、地面支撑、坡面、台阶和碰撞。战斗统一使用 `FixMath.NET.Fix64`，服务端已移除旧 FixedMathSharp NuGet 引用；Unity 旧包暂留用户目录，没有参与模拟。

服务端采用轻量输入中继，不编译 BattleSimulation/BattlePhysics，不引用 BEPU/BEPUutilities/FixMath.NET，也不执行出生点查询。原自动搜索、圆柱扫掠及出生点分散已删除；建房仅计算配置与地图 Hash，原样使用调用方给出的初始化数据。

用户确定后续手动摆放四个固定出生点、按席位分配，当前只在 AI 交接文档记录方案，不实现制作、导出或开局分配逻辑。

客户端世界不传线程池，按固定实体顺序建立；20 Hz 逻辑帧内固定调用三次 `Space.Update()`，每次步长 1/60 秒。不调用按机器耗时累积的 Update(dt)，不使用 Unity PhysX 结果决定战斗状态。

BEPU 角色位置为身体中心，本项目共享的出生点/显示位置为脚底，边界处由 BattlePhysics 完成半身高转换。角色高度、半径、台阶高度、可走坡度来自 HeroConfigData；可滑动支撑坡度为 89 度，不能直接填 90 度，因为库要求支撑法线阈值大于零。

此前已检查 Unity / .NET 编译、用户地图两实例 1200 帧及 Unity Mono / .NET 相同输入的逐帧 XYZ 原始整数一致性（轨迹摘要 C677266A761A8F91）。这记录的是历史纯模拟验证，不表示当前服务端继续运行整场模拟。基础纯模拟涵盖落地、墙体、台阶、坡面、活动边界与角色碰撞；这不代表 IL2CPP、其他 CPU 或全部物理情形已经验证。没有启动服务端、进入 Play Mode 或修改场景资产。更详细的设置与待办记录在 AI交接区/战斗帧同步技术方案.md。
