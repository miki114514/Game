#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成初始职业模板：
/// - 剑斗士（Sword）
/// - 疾风士（Dagger）
/// - 重装卫士（Axe）
/// - 医护兵（Staff）
/// </summary>
public static class StarterClassTemplateGenerator
{
    private const string RootPath = "Assets/Scrpits/Battle/Data/Class";

    [MenuItem("Tools/Battle/Create Starter Class Templates")]
    public static void CreateStarterClassTemplates()
    {
        EnsureFolders();

        int created = 0;
        int skipped = 0;

        if (CreateClassAsset("CLS_SwordFighter", "class_swordfighter", "剑斗士", "擅长近战压制，稳定输出。", WeaponType.Sword)) created++; else skipped++;
        if (CreateClassAsset("CLS_GaleStrider", "class_galestrider", "疾风士", "高速近身切割，节奏灵活。", WeaponType.Dagger)) created++; else skipped++;
        if (CreateClassAsset("CLS_HeavyGuard", "class_heavyguard", "重装卫士", "高压破阵，强调生存与破防。", WeaponType.Axe)) created++; else skipped++;
        if (CreateClassAsset("CLS_Medic", "class_medic", "医护兵", "恢复与支援并重。", WeaponType.Staff)) created++; else skipped++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StarterClassTemplateGenerator] 完成。新增 {created} 个模板，跳过 {skipped} 个已存在模板。路径: {RootPath}");
    }

    [MenuItem("Tools/Battle/Create Starter Class Templates", true)]
    private static bool ValidateCreateStarterClassTemplates()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Scrpits/Battle");
        EnsureFolder("Assets/Scrpits/Battle/Data");
        EnsureFolder(RootPath);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
        string name = folderPath.Substring(folderPath.LastIndexOf('/') + 1);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static bool CreateClassAsset(string assetName, string classId, string displayName, string description, WeaponType weaponType)
    {
        string path = $"{RootPath}/{assetName}.asset";
        if (AssetDatabase.LoadAssetAtPath<CharacterClassDefinition>(path) != null)
            return false;

        var asset = ScriptableObject.CreateInstance<CharacterClassDefinition>();
        asset.classId = classId;
        asset.displayName = displayName;
        asset.description = description;
        asset.allowedWeaponTypes.Add(weaponType);

        AssetDatabase.CreateAsset(asset, path);
        return true;
    }
}
#endif