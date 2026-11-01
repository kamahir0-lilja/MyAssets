#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Diagnostic snapshot of Unity's internal Asset Store services and current Editor session state.
    /// </summary>
    [Serializable]
    public sealed class MyAssetsDiagnostics
    {
        [SerializeField] private bool isCompatible;
        [SerializeField] private string unityVersion = string.Empty;
        [SerializeField] private bool isUserInfoReady;
        [SerializeField] private bool isUserLoggedIn;
        [SerializeField] private string userDisplayName = string.Empty;
        [SerializeField] private string cacheDirectory = string.Empty;
        [SerializeField] private int cachedPackageCount;
        [SerializeField] private int importedPackageCount;
        [SerializeField] private bool anyDownloadInProgress;
        [SerializeField] private string[] resolvedServices = Array.Empty<string>();
        [SerializeField] private string[] missingMembers = Array.Empty<string>();
        [SerializeField] private string message = string.Empty;

        public MyAssetsDiagnostics(
            bool isCompatible,
            string unityVersion,
            bool isUserInfoReady,
            bool isUserLoggedIn,
            string userDisplayName,
            string cacheDirectory,
            int cachedPackageCount,
            int importedPackageCount,
            bool anyDownloadInProgress,
            IReadOnlyList<string> resolvedServices,
            IReadOnlyList<string> missingMembers,
            string message)
        {
            this.isCompatible = isCompatible;
            this.unityVersion = unityVersion ?? string.Empty;
            this.isUserInfoReady = isUserInfoReady;
            this.isUserLoggedIn = isUserLoggedIn;
            this.userDisplayName = userDisplayName ?? string.Empty;
            this.cacheDirectory = cacheDirectory ?? string.Empty;
            this.cachedPackageCount = cachedPackageCount;
            this.importedPackageCount = importedPackageCount;
            this.anyDownloadInProgress = anyDownloadInProgress;
            this.resolvedServices = resolvedServices is null ? Array.Empty<string>() : CopyToArray(resolvedServices);
            this.missingMembers = missingMembers is null ? Array.Empty<string>() : CopyToArray(missingMembers);
            this.message = message ?? string.Empty;
        }

        public bool IsCompatible => isCompatible;
        public string UnityVersion => unityVersion;
        public bool IsUserInfoReady => isUserInfoReady;
        public bool IsUserLoggedIn => isUserLoggedIn;
        public string UserDisplayName => userDisplayName;
        public string CacheDirectory => cacheDirectory;
        public int CachedPackageCount => cachedPackageCount;
        public int ImportedPackageCount => importedPackageCount;
        public bool AnyDownloadInProgress => anyDownloadInProgress;
        public IReadOnlyList<string> ResolvedServices => resolvedServices;
        public IReadOnlyList<string> MissingMembers => missingMembers;
        public string Message => message;

        private static string[] CopyToArray(IReadOnlyList<string> source)
        {
            if (source.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = source[i] ?? string.Empty;
            }

            return result;
        }
    }
}
