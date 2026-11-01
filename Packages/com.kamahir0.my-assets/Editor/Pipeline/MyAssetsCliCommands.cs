#nullable enable
using System;
using System.Linq;
using Unity.Pipeline.Commands;

namespace Lilja.MyAssets.Editor.Pipeline
{
    /// <summary>
    /// Unity CLI (<c>com.unity.pipeline</c>) command adapters that expose <see cref="MyAssetsClient"/>
    /// operations to <c>unity cmd</c>.
    /// </summary>
    public static class MyAssetsCliCommands
    {
        [Serializable]
        public sealed class CachedPackageListResponse
        {
            public string query = string.Empty;
            public int count;
            public CachedPackageInfo[] items = Array.Empty<CachedPackageInfo>();
        }

        [Serializable]
        public sealed class AbortDownloadResponse
        {
            public long productId;
            public bool aborted;
        }

        [CliCommand(
            "myassets_diagnose",
            "Probe internal Package Manager UI Asset Store services and report compatibility, login state, and local cache status.",
            MainThreadRequired = true,
            Tags = new[] { "myassets", "assetstore" })]
        public static MyAssetsDiagnostics Diagnose()
        {
            return MyAssetsClient.Diagnose();
        }

        [CliCommand(
            "myassets_cache_list",
            "List locally cached Asset Store .unitypackage files discovered on disk (works offline).",
            MainThreadRequired = true,
            Tags = new[] { "myassets", "assetstore" })]
        public static CachedPackageListResponse ListCachedPackages(
            [CliArg("--query", "Optional case-insensitive filter matched against title, path, version, or product ID.")]
            string query = "",
            [CliArg("--refresh", "Rescan local Asset Store cache directories before listing.")]
            bool refresh = true)
        {
            var items = MyAssetsClient.ListCachedPackages(query, refresh);
            return new CachedPackageListResponse
            {
                query = query ?? string.Empty,
                count = items.Count,
                items = items.ToArray()
            };
        }

        [CliCommand(
            "myassets_search",
            "Search purchased Asset Store ('My Assets') packages and enrich results with local cache/import status.",
            MainThreadRequired = true,
            Tags = new[] { "myassets", "assetstore" })]
        public static MyAssetsSearchResult SearchPurchases(
            [CliArg("--query", "Search text filter.")]
            string query = "",
            [CliArg("--offset", "Zero-based pagination start index.")]
            int offset = 0,
            [CliArg("--limit", "Maximum number of results to return (1-500).")]
            int limit = 25)
        {
            var queryArgs = new MyAssetsQuery(query, offset, limit);
            return MyAssetsClient.StartPurchaseSearch(queryArgs);
        }

        [CliCommand(
            "myassets_search_status",
            "Get the latest status or completed results of the most recent 'myassets_search' operation.",
            MainThreadRequired = true,
            Tags = new[] { "myassets", "assetstore" })]
        public static MyAssetsSearchResult GetSearchStatus()
        {
            return MyAssetsClient.GetLastPurchaseSearchStatus();
        }

        [CliCommand(
            "myassets_download",
            "Start downloading a purchased Asset Store package by product ID into the local cache.",
            MainThreadRequired = true,
            Tags = new[] { "myassets", "assetstore" })]
        public static DownloadStatusInfo StartDownload(
            [CliArg("--id", "Asset Store product ID (positive integer).", Required = true)]
            long productId = 0)
        {
            return MyAssetsClient.StartDownload(productId);
        }

        [CliCommand(
            "myassets_status",
            "Get download progress and local cache status for an Asset Store product ID.",
            MainThreadRequired = true,
            Tags = new[] { "myassets", "assetstore" })]
        public static DownloadStatusInfo GetDownloadStatus(
            [CliArg("--id", "Asset Store product ID (positive integer).", Required = true)]
            long productId = 0)
        {
            return MyAssetsClient.GetDownloadStatus(productId);
        }

        [CliCommand(
            "myassets_abort",
            "Abort an active Asset Store package download by product ID.",
            MainThreadRequired = true,
            Tags = new[] { "myassets", "assetstore" })]
        public static AbortDownloadResponse AbortDownload(
            [CliArg("--id", "Asset Store product ID (positive integer).", Required = true)]
            long productId = 0)
        {
            var aborted = MyAssetsClient.AbortDownload(productId);
            return new AbortDownloadResponse
            {
                productId = productId,
                aborted = aborted
            };
        }

        [CliCommand(
            "myassets_import",
            "Import a downloaded Asset Store package into the project by product ID or .unitypackage file path.",
            MainThreadRequired = true,
            Tags = new[] { "myassets", "assetstore" })]
        public static ImportPackageResult ImportPackage(
            [CliArg("--id", "Asset Store product ID to import from the local cache.")]
            long productId = 0,
            [CliArg("--path", "Direct filesystem path to a .unitypackage file.")]
            string packagePath = "",
            [CliArg("--interactive", "Open Unity's interactive package import dialog instead of importing silently.")]
            bool interactive = false)
        {
            if (!string.IsNullOrWhiteSpace(packagePath))
            {
                return MyAssetsClient.ImportPackageFile(packagePath, interactive);
            }

            if (productId > 0)
            {
                return MyAssetsClient.ImportPackage(productId, interactive);
            }

            return ImportPackageResult.Failed(
                productId: 0L,
                packagePath: string.Empty,
                interactive: interactive,
                errorCode: "MISSING_ARGUMENTS",
                message: "Specify either --id <productId> or --path <file.unitypackage>.");
        }
    }
}
