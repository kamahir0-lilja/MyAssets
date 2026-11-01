#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lilja.MyAssets.Editor.Internal
{
    /// <summary>
    /// Internal abstraction over Unity's Asset Store Editor services.
    /// Allows deterministic testing of <see cref="MyAssetsClient"/> without requiring live network or login state.
    /// </summary>
    internal interface IMyAssetsBackend
    {
        MyAssetsDiagnostics Diagnose();

        IReadOnlyList<CachedPackageInfo> ListCachedPackages(bool refresh);

        CachedPackageInfo? GetCachedPackage(long productId, bool refresh);

        Task<MyAssetsSearchResult> SearchPurchasesAsync(MyAssetsQuery query, CancellationToken cancellationToken);

        DownloadStatusInfo StartDownload(long productId);

        DownloadStatusInfo GetDownloadStatus(long productId);

        bool AbortDownload(long productId);

        ImportPackageResult ImportPackage(long productId, bool interactive);

        ImportPackageResult ImportPackageFile(string packagePath, bool interactive);
    }
}
