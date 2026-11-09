# 秘核战争

基于 **帧同步的 Unity 3D 多人联机动作塔防 Demo**。四席位、双阵营，包含英雄对战、中立群怪与基地攻防，空席位由电脑补充。

**技术栈：** C# · Socket/TCP · KCP · Fix64 · BEPU · UGUI

## 核心技术

- **帧同步联机：** 实现 20Hz 逻辑帧、2 帧输入缓冲。服务端汇总广播操作，各客户端执行相同战斗规则；服务端不运行物理和群怪模拟，减少逐实体状态同步。
- **定点战斗模拟：** 针对 Unity PhysX 不保证跨平台确定性的问题，接入 Fix64 与 BEPU 定点物理，采用固定步长、单线程和稳定执行顺序；每逻辑帧推进 3 个物理子步。
- **逻辑与表现分离：** 将动画事件导出为攻击时序，由逻辑帧执行扇形普攻、飞行剑气、命中去重与受击打断；通过位置插值、动画进度映射表现模拟结果。
- **群怪寻路与电脑 AI：** 以反向多源 BFS 距离场共享同目标寻路结果，目标跨格才更新追击距离场；电脑英雄复用玩家输入结构，按守家、清怪、追敌的优先级决策。
- **混合刷怪与复用：** 按强度预算、类型权重和阵营狂暴差生成怪物，限制数量与积压强度；独立种子随机序列保持生成计划一致，对象池复用怪物模型与出生特效。
- **TCP 业务与战斗入场：** 独立收发线程、线程安全队列和主线程分发处理登录、好友、聊天与房间消息，按消息长度处理粘包半包；串联异步加载、KCP 认证与全员就绪开战。
- **编辑器工具与配置：** 将 Unity Collider 烘焙为定点地图，导出英雄、技能及动画时序配置；客户端与服务端共用协议数据源码，开局核对地图与配置 Hash。

## 主要源码

- [战斗模拟](Assets/Scripts/Battle/BattleSimulation.cs) · [定点物理](Assets/Scripts/Battle/BattlePhysics.cs)
- [KCP 客户端](Assets/Scripts/Network/BattleNetManager.cs) · [服务端操作帧中继](Server/SecretCore.Server/BattleKcpServer.cs)
- [共享寻路](Assets/Scripts/Monster/MonsterNavigation.cs) · [预算配怪](Assets/Scripts/Monster/MonsterWaveGenerator.cs) · [电脑决策](Assets/Scripts/Battle/BotBrain.cs)
