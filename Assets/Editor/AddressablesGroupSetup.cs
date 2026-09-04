using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// Addressables 分组一键配置工具 —— CCD 热更新专用
/// 菜单：Tools/Addressables/一键配置Remote分组(CCD热更新)
/// 
/// 【功能】
///   1. 创建 Config_Remote 和 GamePrefab_Remote 两个 Remote 分组
///   2. 把 Config_Local / GamePrefab_Local 中的资源迁移到对应 Remote 分组
///   3. Remote 分组的 BuildPath/LoadPath 自动绑定 Remote 变量（对接 CCD）
///   4. Local 分组保留为空，用于存放不需要热更新的资源
/// 
/// 【使用前提】
///   - 已在 Addressables Settings → Profiles 中配置好 Remote.LoadPath 为 CCD 地址
///   - 运行后需要：Addressables Groups 窗口 → Build → New Build → Default Build Script
///   - 然后把 ServerData/[BuildTarget] 下的文件上传到 CCD bucket
/// 
/// 【为什么需要 Remote 分组】
///   Local 分组的资源会打进 APK/IPA，无法热更新。
///   Remote 分组的资源从 CCD 下载，CheckForCatalogUpdates + UpdateCatalogs 才能生效。
/// </summary>
public static class AddressablesGroupSetup
{
    [MenuItem("Tools/Addressables/一键配置Remote分组(CCD热更新)")]
    public static void SetupRemoteGroups()
    {
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            EditorUtility.DisplayDialog("错误", "未找到 AddressableAssetSettings，请先在 Window/Asset Management/Addressables/Groups 中初始化", "OK");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "一键配置 Remote 分组",
            "将执行以下操作：\n" +
            "1. 创建 Config_Remote 和 GamePrefab_Remote 分组\n" +
            "2. 把 Config_Local / GamePrefab_Local 的资源迁移到 Remote 分组\n" +
            "3. Remote 分组绑定 CCD 远程路径\n\n" +
            "确定继续吗？",
            "确定", "取消");
        if (!confirm) return;

        // 1. 创建/获取 Remote 分组
        var configRemote = CreateOrGetRemoteGroup(settings, "Config_Remote");
        var prefabRemote = CreateOrGetRemoteGroup(settings, "GamePrefab_Remote");

        // 2. 迁移资源从 Local 到 Remote
        int configMoved = MoveEntriesBetweenGroups(settings, "Config_Local", configRemote);
        int prefabMoved = MoveEntriesBetweenGroups(settings, "GamePrefab_Local", prefabRemote);

        // 3. 保存（m_BuildRemoteCatalog 已在 asset 文件中开启为 1）
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ Remote分组配置完成！\n" +
                  $"  - Config_Remote: 迁移 {configMoved} 个资源\n" +
                  $"  - GamePrefab_Remote: 迁移 {prefabMoved} 个资源\n\n" +
                  $"【下一步】\n" +
                  $"1. 打开 Addressables Groups 窗口，确认资源已在 Remote 分组\n" +
                  $"2. Build → New Build → Default Build Script\n" +
                  $"3. 把 ServerData/Android 下的所有文件上传到 CCD bucket\n" +
                  $"4. 打安卓包测试热更新");
    }

    /// <summary>
    /// 创建或获取 Remote 分组，并配置 BundledAssetGroupSchema 指向 Remote 路径
    /// </summary>
    private static AddressableAssetGroup CreateOrGetRemoteGroup(AddressableAssetSettings settings, string groupName)
    {
        var group = settings.FindGroup(groupName);
        if (group != null)
        {
            Debug.Log($"[AddressablesGroupSetup] 分组已存在，直接复用: {groupName}");
        }
        else
        {
            // 创建分组，自动添加 BundledAssetGroupSchema 和 ContentUpdateGroupSchema
            group = settings.CreateGroup(
                groupName,
                false,  // readOnly
                false,  // postEvent
                false,  // readOnly (legacy param)
                null,   // defaults
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
            Debug.Log($"[AddressablesGroupSetup] 创建分组: {groupName}");
        }

        // 配置 schema
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null)
        {
            Debug.LogError($"[AddressablesGroupSetup] {groupName} 缺少 BundledAssetGroupSchema，尝试添加");
            schema = group.AddSchema<BundledAssetGroupSchema>();
        }

        // 绑定 Remote 路径变量
        schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
        schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");

        // 远程包用 LZ4 压缩：加载速度快，适合热更资源
        schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;

        // 确保包含在构建中
        schema.IncludeInBuild = true;

        // 内容更新 schema：静态内容（可以热更）
        var updateSchema = group.GetSchema<ContentUpdateGroupSchema>();
        if (updateSchema != null)
        {
            updateSchema.StaticContent = true;
        }

        EditorUtility.SetDirty(group);
        Debug.Log($"[AddressablesGroupSetup] {groupName} 已绑定 Remote.BuildPath / Remote.LoadPath");
        return group;
    }

    /// <summary>
    /// 把源分组中的所有 entry 迁移到目标分组
    /// </summary>
    private static int MoveEntriesBetweenGroups(AddressableAssetSettings settings, string fromGroupName, AddressableAssetGroup toGroup)
    {
        var fromGroup = settings.FindGroup(fromGroupName);
        if (fromGroup == null)
        {
            Debug.LogWarning($"[AddressablesGroupSetup] 未找到源分组: {fromGroupName}，跳过迁移");
            return 0;
        }

        var entries = fromGroup.entries.ToList();
        if (entries.Count == 0)
        {
            Debug.Log($"[AddressablesGroupSetup] {fromGroupName} 为空，无需迁移");
            return 0;
        }

        int count = 0;
        foreach (var entry in entries)
        {
            // Addressables 1.22.3：直接设置 parentGroup 即可移动 entry
            entry.parentGroup = toGroup;
            count++;
        }

        EditorUtility.SetDirty(fromGroup);
        EditorUtility.SetDirty(toGroup);
        Debug.Log($"[AddressablesGroupSetup] 从 {fromGroupName} 迁移 {count} 个资源 → {toGroup.Name}");
        return count;
    }

    /// <summary>
    /// 菜单项：查看当前分组状态
    /// </summary>
    [MenuItem("Tools/Addressables/查看当前分组状态")]
    public static void ShowGroupStatus()
    {
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("未找到 AddressableAssetSettings");
            return;
        }

        Debug.Log("===== Addressables 分组状态 =====");
        foreach (var group in settings.groups)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            string buildPath = schema != null ? schema.BuildPath.ToString() : "N/A";
            string loadPath = schema != null ? schema.LoadPath.ToString() : "N/A";
            bool isRemote = schema != null &&
                            (buildPath.Contains("Remote") || loadPath.Contains("Remote") ||
                             buildPath.Contains("remote") || loadPath.Contains("remote"));

            Debug.Log($"  [{(isRemote ? "Remote" : "Local")}] {group.Name}: {group.entries.Count} 个资源" +
                      $"\n      BuildPath: {buildPath}" +
                      $"\n      LoadPath: {loadPath}");
        }
        Debug.Log("================================");
    }
}
