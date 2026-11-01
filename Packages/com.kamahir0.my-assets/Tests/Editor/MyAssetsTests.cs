#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lilja.MyAssets.Editor;
using Lilja.MyAssets.Editor.Internal;
#if LILJA_DEPENDENCY_PIPELINE_PRESENT
using Lilja.MyAssets.Editor.Pipeline;
using Unity.Pipeline.Commands;
#endif
using NUnit.Framework;

namespace Lilja.MyAssets.Tests.Editor
{
    [TestFixture]
    public sealed class MyAssetsTests
    {
        [Test]
        public void RealUnityReflectionProbeResolvesCoreServices()
        {
            var probe = AssetStoreCompatibilityProbe.Probe();

            Assert.That(probe.ServicesContainerType, Is.Not.Null, "ServicesContainer type should be found in Unity Editor.");
            Assert.That(probe.ServicesContainerInstance, Is.Not.Null, "ServicesContainer singleton instance should be available.");
            Assert.That(probe.AssetStoreClientInstance, Is.Not.Null, "IAssetStoreClient should resolve.");
            Assert.That(probe.AssetStoreDownloadManagerInstance, Is.Not.Null, "IAssetStoreDownloadManager should resolve.");
            Assert.That(probe.AssetStorePackageInstallerInstance, Is.Not.Null, "IAssetStorePackageInstaller should resolve.");
            Assert.That(probe.AssetStoreCacheInstance, Is.Not.Null, "IAssetStoreCache should resolve.");
            Assert.That(probe.MissingMembers, Is.Empty, () => $"Unexpected missing internal members: {string.Join(", ", probe.MissingMembers)}");
            Assert.That(probe.IsCompatible, Is.True);

            var diagnostics = MyAssetsClient.Diagnose();
            Assert.That(diagnostics.IsCompatible, Is.True, diagnostics.Message);
            Assert.That(diagnostics.UnityVersion, Is.Not.Empty);
            Assert.That(diagnostics.MissingMembers, Is.Empty);
            Assert.That(diagnostics.ResolvedServices.Count, Is.GreaterThanOrEqualTo(5));

            var cached = MyAssetsClient.ListCachedPackages(refresh: true);
            Assert.That(cached, Is.Not.Null);
        }

        [Test]
        public void DiagnoseReportsBackendState()
        {
            var fake = new FakeMyAssetsBackend
            {
                DiagnosticsToReturn = new MyAssetsDiagnostics(
                    isCompatible: false,
                    unityVersion: "6000.6.6f1",
                    isUserInfoReady: true,
                    isUserLoggedIn: false,
                    userDisplayName: string.Empty,
                    cacheDirectory: "/tmp/Asset Store-5.x",
                    cachedPackageCount: 2,
                    importedPackageCount: 1,
                    anyDownloadInProgress: false,
                    resolvedServices: new[] { "ServicesContainer" },
                    missingMembers: new[] { "Service:IAssetStoreClient" },
                    message: "Simulated missing member")
            };

            using (MyAssetsClient.UseBackendForTests(fake))
            {
                var diag = MyAssetsClient.Diagnose();
                Assert.That(diag.IsCompatible, Is.False);
                Assert.That(diag.CachedPackageCount, Is.EqualTo(2));
                Assert.That(diag.ImportedPackageCount, Is.EqualTo(1));
                Assert.That(diag.CacheDirectory, Is.EqualTo("/tmp/Asset Store-5.x"));
                Assert.That(diag.MissingMembers, Contains.Item("Service:IAssetStoreClient"));
                Assert.That(diag.Message, Is.EqualTo("Simulated missing member"));
            }
        }

        [Test]
        public void ListCachedPackagesFiltersAndSortsDeterministically()
        {
            var fake = new FakeMyAssetsBackend();
            fake.CachedPackages.Add(new CachedPackageInfo(
                productId: 300,
                uploadId: 30,
                versionId: 3,
                title: "Zenject Dependency Injection",
                versionString: "9.2.0",
                supportedVersion: "2021.3",
                publishedDate: "2024-01-01",
                packagePath: "/cache/Extenject/Zenject.unitypackage",
                fileExists: true,
                isImported: false));
            fake.CachedPackages.Add(new CachedPackageInfo(
                productId: 100,
                uploadId: 10,
                versionId: 1,
                title: "DOTween Pro",
                versionString: "1.0.380",
                supportedVersion: "2022.3",
                publishedDate: "2024-05-01",
                packagePath: "/cache/Demigiant/DOTweenPro.unitypackage",
                fileExists: true,
                isImported: true));
            fake.CachedPackages.Add(new CachedPackageInfo(
                productId: 200,
                uploadId: 20,
                versionId: 2,
                title: "Odin Inspector and Serializer",
                versionString: "3.3.1",
                supportedVersion: "6000.0",
                publishedDate: "2025-01-01",
                packagePath: "/cache/Sirenix/Odin.unitypackage",
                fileExists: true,
                isImported: false));

            using (MyAssetsClient.UseBackendForTests(fake))
            {
                var all = MyAssetsClient.ListCachedPackages();
                Assert.That(all.Select(p => p.ProductId).ToArray(), Is.EqualTo(new long[] { 100, 200, 300 }));

                var byTitle = MyAssetsClient.ListCachedPackages("odin");
                Assert.That(byTitle.Count, Is.EqualTo(1));
                Assert.That(byTitle[0].ProductId, Is.EqualTo(200));

                var byId = MyAssetsClient.ListCachedPackages("100");
                Assert.That(byId.Count, Is.EqualTo(1));
                Assert.That(byId[0].Title, Is.EqualTo("DOTween Pro"));
                Assert.That(byId[0].IsImported, Is.True);
            }
        }

        [Test]
        public void GetCachedPackageReturnsMatchingEntryOrNull()
        {
            var fake = new FakeMyAssetsBackend();
            fake.CachedPackages.Add(new CachedPackageInfo(
                productId: 555,
                uploadId: 55,
                versionId: 5,
                title: "UniTask Plus",
                versionString: "2.5.0",
                supportedVersion: "6000.0",
                publishedDate: "2025-02-01",
                packagePath: "/cache/Cysharp/UniTaskPlus.unitypackage",
                fileExists: true,
                isImported: false));

            using (MyAssetsClient.UseBackendForTests(fake))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => MyAssetsClient.GetCachedPackage(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => MyAssetsClient.GetCachedPackage(-10));

                var hit = MyAssetsClient.GetCachedPackage(555);
                Assert.That(hit, Is.Not.Null);
                Assert.That(hit!.Title, Is.EqualTo("UniTask Plus"));

                var miss = MyAssetsClient.GetCachedPackage(999);
                Assert.That(miss, Is.Null);
            }
        }

        [Test]
        public async Task SearchPurchasesValidatesArgumentsAndCombinesLocalStatus()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MyAssetsQuery("test", startIndex: -1, limit: 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MyAssetsQuery("test", startIndex: 0, limit: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MyAssetsQuery("test", startIndex: 0, limit: 501));

            var fake = new FakeMyAssetsBackend();
            fake.SearchHandler = (query, _) =>
            {
                var items = new[]
                {
                    new PurchasedAssetSummary(
                        productId: 101,
                        displayName: "Bakery GPU Lightmapper",
                        purchasedTime: "2025-03-10T00:00:00Z",
                        tags: new[] { "Lightmaps", "Rendering" },
                        isHidden: false,
                        isDownloaded: true,
                        localVersion: "1.95",
                        localPackagePath: "/cache/Bakery.unitypackage",
                        isImported: false)
                };
                return Task.FromResult(MyAssetsSearchResult.Success(query, total: 1, items: items));
            };

            using (MyAssetsClient.UseBackendForTests(fake))
            {
                var query = new MyAssetsQuery("Bakery", startIndex: 0, limit: 10);
                var result = await MyAssetsClient.SearchPurchasesAsync(query);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Items.Count, Is.EqualTo(1));
                Assert.That(result.Items[0].ProductId, Is.EqualTo(101));
                Assert.That(result.Items[0].IsDownloaded, Is.True);
                Assert.That(result.Items[0].LocalVersion, Is.EqualTo("1.95"));
            }
        }

        [Test]
        public async Task SearchPurchasesReturnsStructuredErrorWhenNotLoggedIn()
        {
            var fake = new FakeMyAssetsBackend
            {
                SearchHandler = (query, _) => Task.FromResult(
                    MyAssetsSearchResult.Failure(query, "NOT_LOGGED_IN", "Unity Editor is not logged into a Unity Account."))
            };

            using (MyAssetsClient.UseBackendForTests(fake))
            {
                var result = await MyAssetsClient.SearchPurchasesAsync(new MyAssetsQuery("ProBuilder", 0, 25));
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Items, Is.Empty);
                Assert.That(result.ErrorCode, Is.EqualTo("NOT_LOGGED_IN"));
                Assert.That(result.ErrorMessage, Does.Contain("not logged in"));
            }
        }

        [Test]
        public void SearchPurchasesCancellationStopsOperation()
        {
            var fake = new FakeMyAssetsBackend
            {
                SearchHandler = async (_, token) =>
                {
                    await Task.Delay(5000, token);
                    return MyAssetsSearchResult.Success(new MyAssetsQuery(), 0, Array.Empty<PurchasedAssetSummary>());
                }
            };

            using (MyAssetsClient.UseBackendForTests(fake))
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.CatchAsync<OperationCanceledException>(async () =>
                    await MyAssetsClient.SearchPurchasesAsync(new MyAssetsQuery("anything"), cts.Token));
            }
        }

        [Test]
        public void StartDownloadAndStatusTrackProgressAndCompletion()
        {
            var fake = new FakeMyAssetsBackend();

            using (MyAssetsClient.UseBackendForTests(fake))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => MyAssetsClient.StartDownload(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => MyAssetsClient.GetDownloadStatus(-5));

                var initial = MyAssetsClient.GetDownloadStatus(777);
                Assert.That(initial.State, Is.EqualTo("None"));
                Assert.That(initial.IsInProgress, Is.False);

                var started = MyAssetsClient.StartDownload(777);
                Assert.That(started.ProductId, Is.EqualTo(777));
                Assert.That(started.State, Is.EqualTo("Downloading"));
                Assert.That(started.IsInProgress, Is.True);
                Assert.That(started.Progress, Is.EqualTo(0.45f).Within(0.001f));

                fake.DownloadStatuses[777] = new DownloadStatusInfo(
                    productId: 777,
                    requestAccepted: true,
                    state: "Completed",
                    isInProgress: false,
                    isPaused: false,
                    progress: 1.0f,
                    downloadedBytes: 8192,
                    totalBytes: 8192,
                    isCachedOnDisk: true,
                    cachedPackagePath: "/cache/Asset777.unitypackage",
                    cachedVersion: "1.0.0");

                var completed = MyAssetsClient.GetDownloadStatus(777);
                Assert.That(completed.State, Is.EqualTo("Completed"));
                Assert.That(completed.Progress, Is.EqualTo(1.0f).Within(0.001f));
                Assert.That(completed.IsInProgress, Is.False);
                Assert.That(completed.CachedPackagePath, Is.EqualTo("/cache/Asset777.unitypackage"));
            }
        }

        [Test]
        public void AbortDownloadCancelsActiveOperation()
        {
            var fake = new FakeMyAssetsBackend();

            using (MyAssetsClient.UseBackendForTests(fake))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => MyAssetsClient.AbortDownload(0));

                MyAssetsClient.StartDownload(888);
                var aborted = MyAssetsClient.AbortDownload(888);
                Assert.That(aborted, Is.True);

                var status = MyAssetsClient.GetDownloadStatus(888);
                Assert.That(status.State, Is.EqualTo("Aborted"));
                Assert.That(status.IsInProgress, Is.False);
            }
        }

        [Test]
        public void ImportPackageRequiresDownloadedFileAndInstallsWhenPresent()
        {
            var fake = new FakeMyAssetsBackend();
            var tempFile = Path.Combine(Path.GetTempPath(), $"lilja_myassets_test_{Guid.NewGuid():N}.unitypackage");
            File.WriteAllBytes(tempFile, new byte[] { 0x1F, 0x8B, 0x08, 0x00 });

            try
            {
                using (MyAssetsClient.UseBackendForTests(fake))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => MyAssetsClient.ImportPackage(0));

                    var missingResult = MyAssetsClient.ImportPackage(404, interactive: false);
                    Assert.That(missingResult.Success, Is.False);
                    Assert.That(missingResult.Message, Does.Contain("not present"));

                    fake.CachedPackages.Add(new CachedPackageInfo(
                        productId: 909,
                        uploadId: 90,
                        versionId: 9,
                        title: "Sample Asset",
                        versionString: "1.0.0",
                        supportedVersion: "6000.0",
                        publishedDate: "2025-01-01",
                        packagePath: tempFile,
                        fileExists: true,
                        isImported: false));

                    var okResult = MyAssetsClient.ImportPackage(909, interactive: false);
                    Assert.That(okResult.Success, Is.True);
                    Assert.That(okResult.ProductId, Is.EqualTo(909));
                    Assert.That(okResult.PackagePath, Is.EqualTo(tempFile));
                    Assert.That(fake.ImportedProductIds, Contains.Item(909L));
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Test]
        public void ImportPackageFileValidatesExtensionAndExistence()
        {
            var realBackend = new UnityAssetStoreReflectionBackend();

            var emptyResult = realBackend.ImportPackageFile("", interactive: false);
            Assert.That(emptyResult.Success, Is.False);
            Assert.That(emptyResult.Message, Does.Contain("must not be empty"));

            var badExtResult = realBackend.ImportPackageFile("/tmp/not_a_package.zip", interactive: false);
            Assert.That(badExtResult.Success, Is.False);
            Assert.That(badExtResult.Message, Does.Contain(".unitypackage"));

            var nonExistentResult = realBackend.ImportPackageFile("/tmp/definitely_missing_123456789.unitypackage", interactive: false);
            Assert.That(nonExistentResult.Success, Is.False);
            Assert.That(nonExistentResult.Message, Does.Contain("does not exist"));
        }

        [Test]
        public void CoreAssemblyDoesNotReferencePipeline()
        {
            var coreAssembly = typeof(MyAssetsClient).Assembly;
            var referencedNames = coreAssembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.That(coreAssembly.GetName().Name, Is.EqualTo("Lilja.MyAssets.Editor"));
            foreach (var name in referencedNames)
            {
                Assert.That(name, Does.Not.StartWith("Unity.Pipeline"),
                    $"Core assembly Lilja.MyAssets.Editor must not reference com.unity.pipeline assembly '{name}'.");
                Assert.That(name, Is.Not.EqualTo("Lilja.MyAssets.Pipeline.Editor"));
            }
        }

        [Test]
        public void CliCommandsAreRegisteredAndDelegateToCoreClient()
        {
#if LILJA_DEPENDENCY_PIPELINE_PRESENT
            var cliType = typeof(MyAssetsCliCommands);
            var methods = cliType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var commandNames = new HashSet<string>();

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<CliCommandAttribute>();
                if (attr != null)
                {
                    commandNames.Add(attr.Name);
                }
            }

            var expectedCommands = new[]
            {
                "myassets_diagnose",
                "myassets_cache_list",
                "myassets_search",
                "myassets_search_status",
                "myassets_download",
                "myassets_status",
                "myassets_abort",
                "myassets_import"
            };

            foreach (var expected in expectedCommands)
            {
                Assert.That(commandNames, Contains.Item(expected), $"Missing CLI command '{expected}'.");
            }

            var fake = new FakeMyAssetsBackend();
            fake.CachedPackages.Add(new CachedPackageInfo(
                productId: 42,
                uploadId: 4,
                versionId: 1,
                title: "CLI Test Package",
                versionString: "1.2.3",
                supportedVersion: "6000.0",
                publishedDate: "2025-04-01",
                packagePath: "/cache/CliTest.unitypackage",
                fileExists: true,
                isImported: false));

            using (MyAssetsClient.UseBackendForTests(fake))
            {
                var diag = MyAssetsCliCommands.Diagnose();
                Assert.That(diag.IsCompatible, Is.True);

                var cacheResponse = MyAssetsCliCommands.ListCachedPackages("CLI", refresh: false);
                Assert.That(cacheResponse.count, Is.EqualTo(1));
                Assert.That(cacheResponse.items[0].ProductId, Is.EqualTo(42));

                var searchResponse = MyAssetsCliCommands.SearchPurchases("CLI", offset: 0, limit: 10);
                Assert.That(searchResponse.IsSuccess, Is.True);

                var searchStatus = MyAssetsCliCommands.GetSearchStatus();
                Assert.That(searchStatus.IsSuccess, Is.True);

                var dl = MyAssetsCliCommands.StartDownload(42);
                Assert.That(dl.ProductId, Is.EqualTo(42));
                Assert.That(dl.IsInProgress, Is.True);

                var status = MyAssetsCliCommands.GetDownloadStatus(42);
                Assert.That(status.ProductId, Is.EqualTo(42));

                var abort = MyAssetsCliCommands.AbortDownload(42);
                Assert.That(abort.aborted, Is.True);

                var importMissingArgs = MyAssetsCliCommands.ImportPackage(productId: 0, packagePath: "");
                Assert.That(importMissingArgs.Success, Is.False);
            }
#else
            Assert.Fail("LILJA_DEPENDENCY_PIPELINE_PRESENT should be defined in the development project.");
#endif
        }

        private sealed class FakeMyAssetsBackend : IMyAssetsBackend
        {
            public MyAssetsDiagnostics DiagnosticsToReturn { get; set; } = new MyAssetsDiagnostics(
                isCompatible: true,
                unityVersion: "6000.6.6f1",
                isUserInfoReady: true,
                isUserLoggedIn: true,
                userDisplayName: "Test User",
                cacheDirectory: "/cache",
                cachedPackageCount: 0,
                importedPackageCount: 0,
                anyDownloadInProgress: false,
                resolvedServices: new[] { "ServicesContainer", "IAssetStoreClient" },
                missingMembers: Array.Empty<string>(),
                message: "OK");

            public List<CachedPackageInfo> CachedPackages { get; } = new List<CachedPackageInfo>();

            public Dictionary<long, DownloadStatusInfo> DownloadStatuses { get; } = new Dictionary<long, DownloadStatusInfo>();

            public List<long> ImportedProductIds { get; } = new List<long>();

            public Func<MyAssetsQuery, CancellationToken, Task<MyAssetsSearchResult>>? SearchHandler { get; set; }

            public MyAssetsDiagnostics Diagnose() => DiagnosticsToReturn;

            public IReadOnlyList<CachedPackageInfo> ListCachedPackages(bool refresh) => CachedPackages.ToArray();

            public CachedPackageInfo? GetCachedPackage(long productId, bool refresh)
            {
                return CachedPackages.FirstOrDefault(p => p.ProductId == productId);
            }

            public Task<MyAssetsSearchResult> SearchPurchasesAsync(MyAssetsQuery query, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (SearchHandler != null)
                {
                    return SearchHandler(query, cancellationToken);
                }

                return Task.FromResult(MyAssetsSearchResult.Success(query, 0, Array.Empty<PurchasedAssetSummary>()));
            }

            public DownloadStatusInfo StartDownload(long productId)
            {
                var status = new DownloadStatusInfo(
                    productId: productId,
                    requestAccepted: true,
                    state: "Downloading",
                    isInProgress: true,
                    isPaused: false,
                    progress: 0.45f,
                    downloadedBytes: 450,
                    totalBytes: 1000,
                    isCachedOnDisk: false);
                DownloadStatuses[productId] = status;
                return status;
            }

            public DownloadStatusInfo GetDownloadStatus(long productId)
            {
                if (DownloadStatuses.TryGetValue(productId, out var status))
                {
                    return status;
                }

                return new DownloadStatusInfo(
                    productId: productId,
                    requestAccepted: false,
                    state: "None",
                    isInProgress: false,
                    isPaused: false,
                    progress: 0f,
                    downloadedBytes: 0,
                    totalBytes: 0,
                    isCachedOnDisk: false);
            }

            public bool AbortDownload(long productId)
            {
                if (!DownloadStatuses.ContainsKey(productId))
                {
                    return false;
                }

                DownloadStatuses[productId] = new DownloadStatusInfo(
                    productId: productId,
                    requestAccepted: true,
                    state: "Aborted",
                    isInProgress: false,
                    isPaused: false,
                    progress: 0f,
                    downloadedBytes: 0,
                    totalBytes: 0,
                    isCachedOnDisk: false);
                return true;
            }

            public ImportPackageResult ImportPackage(long productId, bool interactive)
            {
                var cached = GetCachedPackage(productId, refresh: true);
                if (cached == null || !File.Exists(cached.PackagePath))
                {
                    return ImportPackageResult.Failed(
                        productId: productId,
                        packagePath: cached?.PackagePath ?? string.Empty,
                        interactive: interactive,
                        errorCode: "PACKAGE_NOT_DOWNLOADED",
                        message: $"Package {productId} is not present in the local Asset Store cache.");
                }

                ImportedProductIds.Add(productId);
                return ImportPackageResult.Succeeded(
                    productId: productId,
                    title: cached.Title,
                    versionString: cached.VersionString,
                    packagePath: cached.PackagePath,
                    interactive: interactive);
            }

            public ImportPackageResult ImportPackageFile(string packagePath, bool interactive)
            {
                return ImportPackageResult.Succeeded(
                    productId: 0,
                    title: "DirectPackage",
                    versionString: string.Empty,
                    packagePath: packagePath,
                    interactive: interactive);
            }
        }
    }
}
