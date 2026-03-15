using UnityEngine;
using System.Collections.Generic;

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance;

    [Header("场景中所有角色")]
    public List<Character> characters = new List<Character>();

    void Awake()
    {
        // 单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 可选：自动收集场景中Character组件
        Character[] chars = FindObjectsOfType<Character>();
        foreach (var c in chars)
        {
            if (!characters.Contains(c))
                characters.Add(c);
        }
    }

    /// <summary>
    /// 根据名字查找角色
    /// </summary>
    public Character GetCharacter(string name)
    {
        return characters.Find(c => c.characterName == name);
    }

    /// <summary>
    /// 注册角色（动态生成NPC可用）
    /// </summary>
    public void RegisterCharacter(Character character)
    {
        if (!characters.Contains(character))
            characters.Add(character);
    }
}