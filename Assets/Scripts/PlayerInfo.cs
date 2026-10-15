using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class PlayerData
{
    public int level;
    public int[] friends;   //拥有的好友ID
    
}


public class PlayerInfo 
{
    public int id;  //玩家账号对应的唯一ID
    public string name; //玩家的账户名
    public string passWord; //密码

    public PlayerData playerData;   //该账户的对应数据
}
