#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lilja.MyAssets.Editor.Internal;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Pipeline-independent Editor entry point for inspecting, searching, downloading,
    /// and importing purchased Asset Store ("My Assets") packages.
    /// </summary>
    public static class MyAssetsClient
    {
        private static readonly IMyAssetsBackend DefaultBackend = new UnityAssetStoreReflectionBackend();
        private static IMyAssetsBackend _currentBackend = DefaultBackend;

        private static Task<MyAssetsSearchResult>? _activeSearchTask;
        private static MyAssetsQuery? _activeSearchQuery;

        internal static IMyAssetsBackend Backend => _currentBackend;

        /// <summary>
        /// Temporarily swaps the underlying backend for deterministic EditMode testing.
        /// </summary>
        internal static IDisposable UseBackendForTests(IMyAssetsBackend backend)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            var previous = _currentBackend;
            _currentBackend = backend;
            _activeSearchTask = null;
            _activeSearchQuery = null;
            return new BackendScope(previous);
        }

        /// <summary>
        /// Probes internal Package Manager UI services and returns a compatibility and login report.
        /// </summary>
        public static MyAssetsDiagnostics Diagnose()
        {
            return _currentBackend.Diagnose();
        }

        /// <summary>
        /// Lists locally cached <c>.unitypackage</c> files discovered on disk. Works offline without Unity login.
        /// Results are sorted deterministically by <see cref="CachedPackageInfo.ProductId"/> ascending.
        /// </summary>
        public static IReadOnlyList<CachedPackageInfo> ListCachedPackages(string? query = null, bool refresh = true)
        {
            var all = _currentBackend.ListCachedPackages(refresh);
            var trimmed = query?.Trim() ?? string.Empty;

            IEnumerable<CachedPackageInfo> filtered = all;
            if (!string.IsNullOrEmpty(trimmed))
            {
                filtered = all.Where(p => MatchesCacheQuery(p, trimmed));
            }

            return filtered
                .OrderBy(p => p.ProductId)
                .ToArray();
        }

        /// <summary>
        /// Returns the locally cached package entry for <paramref name="productId"/>, or <c>null</c> if not downloaded.
        /// </summary>
        public static CachedPackageInfo? GetCachedPackage(long productId, bool refresh = true)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            return _currentBackend.GetCachedPackage(productId, refresh);
        }

        /// <summary>
        /// Asynchronously queries the user's purchased Asset Store packages and enriches results with local cache/import status.
        /// </summary>
        public static Task<MyAssetsSearchResult> SearchPurchasesAsync(
            MyAssetsQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return _currentBackend.SearchPurchasesAsync(query, cancellationToken);
        }

        /// <summary>
        /// Initiates a purchase search suitable for main-thread CLI commands.
        /// If the search completes synchronously (e.g., cached, mocked, or immediate error), returns the final result.
        /// If an asynchronous HTTP request is in flight, returns a status snapshot that can be polled via <see cref="GetLastPurchaseSearchStatus"/>.
        /// </summary>
        public static MyAssetsSearchResult StartPurchaseSearch(MyAssetsQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            _activeSearchQuery = query;
            _activeSearchTask = _currentBackend.SearchPurchasesAsync(query, CancellationToken.None);

            if (_activeSearchTask.IsCompleted)
            {
                return GetLastPurchaseSearchStatus();
            }

            return MyAssetsSearchResult.Failure(
                query,
                "SEARCH_IN_PROGRESS",
                "Purchase search is in progress on the Unity Editor main thread. Poll status with GetLastPurchaseSearchStatus or 'myassets_search_status'.",
                state: "in_progress");
        }

        /// <summary>
        /// Returns the current state or completed result of the most recent search started via <see cref="StartPurchaseSearch"/>.
        /// </summary>
        public static MyAssetsSearchResult GetLastPurchaseSearchStatus()
        {
            var query = _activeSearchQuery ?? new MyAssetsQuery();
            var task = _activeSearchTask;
            if (task == null)
            {
                return MyAssetsSearchResult.Failure(
                    query,
                    "NO_ACTIVE_SEARCH",
                    "No purchase search has been started in this session.",
                    state: "idle");
            }

            if (!task.IsCompleted)
            {
                return MyAssetsSearchResult.Failure(
                    query,
                    "SEARCH_IN_PROGRESS",
                    "Purchase search is still in progress.",
                    state: "in_progress");
            }

            if (task.IsCanceled)
            {
                return MyAssetsSearchResult.Failure(
                    query,
                    "SEARCH_CANCELED",
                    "Purchase search was canceled.",
                    state: "canceled");
            }

            if (task.IsFaulted)
            {
                var message = task.Exception?.InnerException?.Message ?? task.Exception?.Message ?? "Unknown search error.";
                return MyAssetsSearchResult.Failure(
                    query,
                    "SEARCH_FAULTED",
                    message,
                    state: "failed");
            }

            return task.Result;
        }

        /// <summary>
        /// Starts downloading a purchased Asset Store package by product ID into the local Asset Store cache.
        /// </summary>
        public static DownloadStatusInfo StartDownload(long productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            return _currentBackend.StartDownload(productId);
        }

        /// <summary>
        /// Starts downloading multiple purchased Asset Store packages by product ID.
        /// </summary>
        public static IReadOnlyList<DownloadStatusInfo> StartDownloads(IEnumerable<long> productIds)
        {
            if (productIds == null)
            {
                throw new ArgumentNullException(nameof(productIds));
            }

            var results = new List<DownloadStatusInfo>();
            foreach (var id in productIds.Distinct())
            {
                results.Add(StartDownload(id));
            }

            return results;
        }

        /// <summary>
        /// Queries the current download progress or local completion state for <paramref name="productId"/>.
        /// </summary>
        public static DownloadStatusInfo GetDownloadStatus(long productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            return _currentBackend.GetDownloadStatus(productId);
        }

        /// <summary>
        /// Aborts an in-progress download for <paramref name="productId"/>.
        /// </summary>
        public static bool AbortDownload(long productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            return _currentBackend.AbortDownload(productId);
        }

        /// <summary>
        /// Imports a downloaded Asset Store package into the project by its <paramref name="productId"/>.
        /// </summary>
        public static ImportPackageResult ImportPackage(long productId, bool interactive = false)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            return _currentBackend.ImportPackage(productId, interactive);
        }

        /// <summary>
        /// Imports a <c>.unitypackage</c> file directly from the given filesystem path.
        /// </summary>
        public static ImportPackageResult ImportPackageFile(string packagePath, bool interactive = false)
        {
            if (packagePath == null)
            {
                throw new ArgumentNullException(nameof(packagePath));
            }

            return _currentBackend.ImportPackageFile(packagePath, interactive);
        }

        private static bool MatchesCacheQuery(CachedPackageInfo info, string query)
        {
            if (info.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (info.PackagePath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (info.VersionString.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var idStr = info.ProductId.ToString(CultureInfo.InvariantCulture);
            return idStr.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class BackendScope : IDisposable
        {
            private readonly IMyAssetsBackend _previous;
            private bool _disposed;

            public BackendScope(IMyAssetsBackend previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _currentBackend = _previous;
                _activeSearchTask = null;
                _activeSearchQuery = null;
            }
        }
    }
}
