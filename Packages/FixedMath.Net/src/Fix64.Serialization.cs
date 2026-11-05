using System;

namespace FixMath.NET
{
    /// <summary>
    /// 允许 Unity 序列化 Fix64；只保存已有的 RawValue 字段，读写不经过浮点数转换。
    /// 单独补充序列化标记，不修改上游的定点运算实现。
    /// </summary>
    [Serializable]
    public partial struct Fix64
    {
    }
}
