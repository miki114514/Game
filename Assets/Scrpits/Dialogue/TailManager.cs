using UnityEngine;
using System;
using System.Collections.Generic;

public enum TailDirection
{
    Up, Down, Left, Right,
    UpLeft, UpRight, DownLeft, DownRight
}

[Serializable]
public struct TailSpriteInfo
{
    public BubbleType type;          // 使用全局 BubbleType
    public TailDirection direction;
    public Sprite sprite;
}

public class TailManager : MonoBehaviour
{
    public List<TailSpriteInfo> tailSprites = new List<TailSpriteInfo>();
}