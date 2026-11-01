#nullable enable
using UnityEditor;
using UnityEngine;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Unity Editor menu shortcuts for inspecting MyAssets status without the CLI.
    /// </summary>
    internal static class MyAssetsMenuItems
    {
        [MenuItem("Lilja/MyAssets/Diagnose Internal API", priority = 100)]
        private static void DiagnoseInternalApi()
        {
            var diagnostics = MyAssetsClient.Diagnose();
            var json = JsonUtility.ToJson(diagnostics, prettyPrint: true);
            Debug.Log($"[Lilja.MyAssets] Diagnostics:\n{json}");
        }

        [MenuItem("Lilja/MyAssets/Log Cached Packages", priority = 101)]
        private static void LogCachedPackages()
        {
            var packages = MyAssetsClient.ListCachedPackages(refresh: true);
            Debug.Log($"[Lilja.MyAssets] Found {packages.Count} locally cached package(s).");
            foreach (var pkg in packages)
            {
                Debug.Log($"[Lilja.MyAssets] [{pkg.ProductId}] {pkg.Title} v{pkg.VersionString} -> {pkg.PackagePath}");
            }
        }
    }
}
