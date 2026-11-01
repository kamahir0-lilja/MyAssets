#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Lilja.MyAssets.Setup.Editor
{
    [InitializeOnLoad]
    internal static class SetupDependencies
    {
        private static AddRequest _activeRequest;
        private static string _activePackageName;

#if !LILJA_DEPENDENCY_PIPELINE_PRESENT
        [MenuItem("Lilja/Dependencies/MyAssets/Install Unity Pipeline")]
        private static void InstallUnityPipeline()
        {
            InstallPackage("com.unity.pipeline", "Unity Pipeline");
        }
#endif

        private static void InstallPackage(string packageIdentifier, string packageName)
        {
            if (_activeRequest != null && !_activeRequest.IsCompleted)
            {
                Debug.LogWarning($"[Lilja.MyAssets] Package installation ({_activePackageName}) is already in progress.");
                return;
            }

            _activePackageName = packageName;
            Debug.Log($"[Lilja.MyAssets] Installing {packageName} ({packageIdentifier})...");
            _activeRequest = Client.Add(packageIdentifier);
            EditorApplication.update += MonitorRequest;
        }

        private static void MonitorRequest()
        {
            if (_activeRequest == null || !_activeRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= MonitorRequest;

            if (_activeRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[Lilja.MyAssets] Successfully installed {_activePackageName} ({_activeRequest.Result.packageId}).");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            else if (_activeRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"[Lilja.MyAssets] Failed to install {_activePackageName}: {_activeRequest.Error.message}");
            }

            _activeRequest = null;
            _activePackageName = null;
        }
    }
}
#endif
