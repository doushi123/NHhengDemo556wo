using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 显示一条世界聊天消息。自己的消息和其他玩家的消息可以使用不同 Prefab，
/// 但两个 Prefab 都挂这个脚本并绑定各自的文本组件。
/// </summary>
public sealed class WorldChatItem : MonoBehaviour
{
    [SerializeField] private TMP_Text senderText;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private TMP_Text timeText;

    public void SetData(WorldChatData data)
    {
        senderText.text = data.senderAccount;
        contentText.text = data.content;
        timeText.text = DateTimeOffset.FromUnixTimeSeconds(data.sendTime).ToLocalTime().ToString("HH:mm");
    }
}
