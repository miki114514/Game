#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成 JRPG 初始装备模板资产：
/// - 武器：Sword / Lance / Dagger / Axe / Bow / Staff
/// - 防具：Head / Body 基础套
/// - 饰品：基础数值与规则扩展样例
///
/// 使用方法：
/// 1. Unity 菜单点击 Tools/Battle/Create Starter Equipment Templates
/// 2. 资产会生成到 Assets/Scrpits/Battle/Data/Equipment/Starter/
/// </summary>
public static class StarterEquipmentTemplateGenerator
{
    private const string RootPath = "Assets/Scrpits/Equip/Starter";

    private sealed class WeaponTemplate
    {
        public string Name;
        public WeaponType Type;
        public int PAtk;
        public int Accuracy;
        public int Speed;
        public int Crit;
    }

    private sealed class ArmorTemplate
    {
        public string Name;
        public ArmorSlot Slot;
        public int PDef;
        public int EDef;
        public int Speed;
    }

    private static readonly WeaponTemplate[] WeaponTemplates =
    {
        new WeaponTemplate { Name = "WPN_Starter_BronzeSword",  Type = WeaponType.Sword,  PAtk = 12, Accuracy = 4, Speed = 0, Crit = 1 },
        new WeaponTemplate { Name = "WPN_Starter_IronSpear",   Type = WeaponType.Lance,  PAtk = 13, Accuracy = 2, Speed = -1, Crit = 0 },
        new WeaponTemplate { Name = "WPN_Starter_QuickDagger", Type = WeaponType.Dagger, PAtk = 9,  Accuracy = 3, Speed = 3, Crit = 3 },
        new WeaponTemplate { Name = "WPN_Starter_WoodAxe",     Type = WeaponType.Axe,    PAtk = 15, Accuracy = -1, Speed = -2, Crit = 0 },
        new WeaponTemplate { Name = "WPN_Starter_HunterBow",   Type = WeaponType.Bow,    PAtk = 10, Accuracy = 6, Speed = 1, Crit = 2 },
        new WeaponTemplate { Name = "WPN_Starter_AshStaff",    Type = WeaponType.Staff,  PAtk = 8,  Accuracy = 5, Speed = 0, Crit = 1 },
    };

    private static readonly ArmorTemplate[] ArmorTemplates =
    {
        new ArmorTemplate { Name = "ARM_Starter_ClothCap",      Slot = ArmorSlot.Head, PDef = 3, EDef = 4, Speed = 0 },
        new ArmorTemplate { Name = "ARM_Starter_LeatherHood",   Slot = ArmorSlot.Head, PDef = 4, EDef = 3, Speed = 1 },
        new ArmorTemplate { Name = "ARM_Starter_TravelerCoat",  Slot = ArmorSlot.Body, PDef = 7, EDef = 6, Speed = 0 },
        new ArmorTemplate { Name = "ARM_Starter_LeatherArmor",  Slot = ArmorSlot.Body, PDef = 8, EDef = 5, Speed = -1 },
        new ArmorTemplate { Name = "ARM_Starter_AdeptRobe",     Slot = ArmorSlot.Body, PDef = 5, EDef = 9, Speed = 0 },
    };

    [MenuItem("Tools/Battle/Create Starter Equipment Templates")]
    public static void CreateStarterEquipmentTemplates()
    {
        EnsureFolders();

        int created = 0;
        int skipped = 0;

        foreach (var tpl in WeaponTemplates)
        {
            bool createdNow = CreateWeaponAsset(tpl);
            if (createdNow) created++;
            else skipped++;
        }

        foreach (var tpl in ArmorTemplates)
        {
            bool createdNow = CreateArmorAsset(tpl);
            if (createdNow) created++;
            else skipped++;
        }

        if (CreateAccessoryAsset_StarterPowerBand()) created++; else skipped++;
        if (CreateAccessoryAsset_StarterFocusCharm()) created++; else skipped++;
        if (CreateAccessoryAsset_StarterCleanseRing()) created++; else skipped++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StarterEquipmentTemplateGenerator] 完成。新增 {created} 个模板，跳过 {skipped} 个已存在模板。路径: {RootPath}");
    }

    [MenuItem("Tools/Battle/Create Starter Equipment Templates", true)]
    private static bool ValidateCreateStarterEquipmentTemplates()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Scrpits/Equip");
        EnsureFolder("Assets/Scrpits/Equip/Starter");
        EnsureFolder(RootPath + "/Weapons");
        EnsureFolder(RootPath + "/Armors");
        EnsureFolder(RootPath + "/Accessories");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
            string name = folderPath.Substring(folderPath.LastIndexOf('/') + 1);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static bool CreateWeaponAsset(WeaponTemplate tpl)
    {
        string path = $"{RootPath}/Weapons/{tpl.Name}.asset";
        if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null)
            return false;

        var asset = ScriptableObject.CreateInstance<WeaponData>();
        asset.equipmentName = tpl.Name;
        asset.description = $"初始武器模板：{tpl.Type}";
        asset.weaponType = tpl.Type;

        asset.pAtk = tpl.PAtk;
        asset.eAtk = 0;
        asset.pDef = 0;
        asset.eDef = 0;
        asset.speed = tpl.Speed;
        asset.accuracy = tpl.Accuracy;
        asset.crit = tpl.Crit;
        asset.hp = 0;
        asset.sp = 0;

        AssetDatabase.CreateAsset(asset, path);
        return true;
    }

    private static bool CreateArmorAsset(ArmorTemplate tpl)
    {
        string path = $"{RootPath}/Armors/{tpl.Name}.asset";
        if (AssetDatabase.LoadAssetAtPath<ArmorData>(path) != null)
            return false;

        var asset = ScriptableObject.CreateInstance<ArmorData>();
        asset.equipmentName = tpl.Name;
        asset.description = $"基础防具模板：{tpl.Slot}";
        asset.slot = tpl.Slot;

        asset.pAtk = 0;
        asset.eAtk = 0;
        asset.pDef = tpl.PDef;
        asset.eDef = tpl.EDef;
        asset.speed = tpl.Speed;
        asset.accuracy = 0;
        asset.crit = 0;
        asset.hp = 0;
        asset.sp = 0;

        AssetDatabase.CreateAsset(asset, path);
        return true;
    }

    private static bool CreateAccessoryAsset_StarterPowerBand()
    {
        const string path = RootPath + "/Accessories/ACC_Starter_PowerBand.asset";
        if (AssetDatabase.LoadAssetAtPath<AccessoryData>(path) != null)
            return false;

        var asset = ScriptableObject.CreateInstance<AccessoryData>();
        asset.equipmentName = "ACC_Starter_PowerBand";
        asset.description = "基础攻击饰品：少量 P.Atk 与最大 HP";
        asset.pAtk = 3;
        asset.eAtk = 0;
        asset.pDef = 0;
        asset.eDef = 0;
        asset.speed = 0;
        asset.accuracy = 0;
        asset.crit = 1;
        asset.hp = 20;
        asset.sp = 0;

        AssetDatabase.CreateAsset(asset, path);
        return true;
    }

    private static bool CreateAccessoryAsset_StarterFocusCharm()
    {
        const string path = RootPath + "/Accessories/ACC_Starter_FocusCharm.asset";
        if (AssetDatabase.LoadAssetAtPath<AccessoryData>(path) != null)
            return false;

        var asset = ScriptableObject.CreateInstance<AccessoryData>();
        asset.equipmentName = "ACC_Starter_FocusCharm";
        asset.description = "开局 BP+1，适合 BP 驱动角色";
        asset.pAtk = 0;
        asset.eAtk = 2;
        asset.pDef = 0;
        asset.eDef = 1;
        asset.speed = 1;
        asset.accuracy = 0;
        asset.crit = 0;
        asset.hp = 0;
        asset.sp = 10;

        asset.specialEffects = new List<AccessorySpecialEffect>
        {
            new AccessorySpecialEffect
            {
                effectType = AccessoryEffectType.StartBPBonus,
                value = 1f,
            }
        };

        AssetDatabase.CreateAsset(asset, path);
        return true;
    }

    private static bool CreateAccessoryAsset_StarterCleanseRing()
    {
        const string path = RootPath + "/Accessories/ACC_Starter_CleanseRing.asset";
        if (AssetDatabase.LoadAssetAtPath<AccessoryData>(path) != null)
            return false;

        var asset = ScriptableObject.CreateInstance<AccessoryData>();
        asset.equipmentName = "ACC_Starter_CleanseRing";
        asset.description = "基础抗性饰品：Poison/Sleep 抗性";
        asset.pAtk = 0;
        asset.eAtk = 0;
        asset.pDef = 1;
        asset.eDef = 1;
        asset.speed = 0;
        asset.accuracy = 0;
        asset.crit = 0;
        asset.hp = 15;
        asset.sp = 5;

        asset.resistances = new List<StatusResistanceEntry>
        {
            new StatusResistanceEntry { statusType = StatusEffectType.Poison, resistancePercent = 0.4f },
            new StatusResistanceEntry { statusType = StatusEffectType.Sleep,  resistancePercent = 0.3f },
        };

        AssetDatabase.CreateAsset(asset, path);
        return true;
    }
}
#endif
