using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Lilja.MyAssets.Development
{
    public static class ProjectBootstrap
    {
        private const string ActiveKey = "Lilja.MyAssets.Bootstrap";
        private static Request _request;
        private static double _deadline;

        public static void Install()
        {
            var additions = new System.Collections.Generic.List<string>
            {
                "com.unity.pipeline@0.8.0-exp.1",
                "com.unity.test-framework@1.4.6",
                "com.unity.modules.jsonserialize@1.0.0"
            };
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-myAssetsPackage");
            if (index >= 0 && index + 1 < args.Length) additions.Add(args[index + 1]);
            SessionState.SetBool(ActiveKey, true);
            _request = Client.AddAndRemove(additions.ToArray(), Array.Empty<string>());
            StartPolling();
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            _request = Client.List(true, true);
            StartPolling();
        }

        private static void StartPolling()
        {
            _deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup > _deadline) { Finish(2, "UPM timeout"); return; }
            if (_request == null || !_request.IsCompleted) return;
            if (_request.Status == StatusCode.Failure) { Finish(1, _request.Error?.message); return; }
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            if (!packages.Any(p => p.name == "com.unity.test-framework")) return;
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Finish(0, "MyAssets development dependencies resolved.");
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Poll;
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }
    }
}
