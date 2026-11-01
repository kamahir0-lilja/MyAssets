#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Lilja.MyAssets.Editor.Internal
{
    /// <summary>
    /// Reflection-backed implementation of <see cref="IMyAssetsBackend"/> that delegates to
    /// <c>UnityEditor.PackageManager.UI.Internal</c> services.
    /// </summary>
    internal sealed class UnityAssetStoreReflectionBackend : IMyAssetsBackend
    {
        private AssetStoreCompatibilityProbe? _cachedProbe;

        internal AssetStoreCompatibilityProbe GetProbe(bool forceRefresh = false)
        {
            if (_cachedProbe == null || forceRefresh)
            {
                _cachedProbe = AssetStoreCompatibilityProbe.Probe();
            }

            return _cachedProbe;
        }

        public MyAssetsDiagnostics Diagnose()
        {
            var probe = GetProbe(forceRefresh: true);
            var unityVersion = Application.unityVersion;
            var isUserInfoReady = probe.GetIsUserInfoReady();
            var isLoggedIn = probe.GetIsUserLoggedIn();
            var displayName = probe.GetUserDisplayName();
            var cacheRootPath = probe.GetCacheRootPath();
            var anyDownloadInProgress = probe.GetIsAnyDownloadInProgress();
            var importedCount = GetImportedProductIds(probe).Count;

            var localCount = 0;
            if (probe.IsCompatible)
            {
                try
                {
                    var cached = ListCachedPackages(refresh: true);
                    localCount = cached.Count;
                    if (string.IsNullOrEmpty(cacheRootPath) && cached.Count > 0 && !string.IsNullOrEmpty(cached[0].PackagePath))
                    {
                        cacheRootPath = Path.GetDirectoryName(cached[0].PackagePath) ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    return new MyAssetsDiagnostics(
                        isCompatible: false,
                        unityVersion: unityVersion,
                        isUserInfoReady: isUserInfoReady,
                        isUserLoggedIn: isLoggedIn,
                        userDisplayName: displayName,
                        cacheDirectory: cacheRootPath,
                        cachedPackageCount: 0,
                        importedPackageCount: importedCount,
                        anyDownloadInProgress: anyDownloadInProgress,
                        resolvedServices: probe.ResolvedServices.ToArray(),
                        missingMembers: probe.MissingMembers.ToArray(),
                        message: $"Reflection probe succeeded, but reading local cache failed: {ex.Message}");
                }
            }

            var message = probe.IsCompatible
                ? $"Internal Asset Store bridge is compatible on Unity {unityVersion}."
                : $"Missing {probe.MissingMembers.Count} internal member(s): {string.Join(", ", probe.MissingMembers)}";

            return new MyAssetsDiagnostics(
                isCompatible: probe.IsCompatible,
                unityVersion: unityVersion,
                isUserInfoReady: isUserInfoReady,
                isUserLoggedIn: isLoggedIn,
                userDisplayName: displayName,
                cacheDirectory: cacheRootPath,
                cachedPackageCount: localCount,
                importedPackageCount: importedCount,
                anyDownloadInProgress: anyDownloadInProgress,
                resolvedServices: probe.ResolvedServices.ToArray(),
                missingMembers: probe.MissingMembers.ToArray(),
                message: message);
        }

        public IReadOnlyList<CachedPackageInfo> ListCachedPackages(bool refresh)
        {
            var probe = EnsureCompatibleProbe();
            if (refresh)
            {
                RefreshLocalInfos(probe);
            }

            var importedIds = GetImportedProductIds(probe);
            var localInfosEnumerable = probe.LocalInfosProperty?.GetValue(probe.AssetStoreCacheInstance) as IEnumerable;
            if (localInfosEnumerable == null)
            {
                return Array.Empty<CachedPackageInfo>();
            }

            var list = new List<CachedPackageInfo>();
            foreach (var item in localInfosEnumerable)
            {
                if (item == null)
                {
                    continue;
                }

                var info = MapLocalInfo(item, importedIds);
                if (info != null)
                {
                    list.Add(info);
                }
            }

            list.Sort((a, b) => a.ProductId.CompareTo(b.ProductId));
            return list;
        }

        public CachedPackageInfo? GetCachedPackage(long productId, bool refresh)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            var probe = EnsureCompatibleProbe();
            if (refresh)
            {
                RefreshLocalInfos(probe);
            }

            var importedIds = GetImportedProductIds(probe);
            if (probe.GetLocalInfoMethod != null && probe.AssetStoreCacheInstance != null)
            {
                var rawLocalInfo = InvokeWithNullableLong(probe.GetLocalInfoMethod, probe.AssetStoreCacheInstance, productId);
                if (rawLocalInfo != null)
                {
                    return MapLocalInfo(rawLocalInfo, importedIds);
                }
            }

            var all = ListCachedPackages(refresh: false);
            return all.FirstOrDefault(p => p.ProductId == productId);
        }

        public async Task<MyAssetsSearchResult> SearchPurchasesAsync(MyAssetsQuery query, CancellationToken cancellationToken)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var probe = GetProbe();
            if (!probe.IsCompatible || probe.AssetStoreClientInstance == null || probe.ListPurchasesMethod == null || probe.OnProductListFetchedEvent == null)
            {
                return MyAssetsSearchResult.Failure(
                    query,
                    "INCOMPATIBLE_EDITOR_API",
                    $"Internal Asset Store API is incompatible on Unity {Application.unityVersion}: {string.Join(", ", probe.MissingMembers)}");
            }

            if (!probe.GetIsUserLoggedIn())
            {
                return MyAssetsSearchResult.Failure(
                    query,
                    "NOT_LOGGED_IN",
                    "Unity Editor is not logged into a Unity Account (UnityConnectProxy.isUserLoggedIn is false). Sign in via Unity Hub or Package Manager to search online purchases, or use ListCachedPackages for offline local packages.");
            }

            RefreshLocalInfos(probe);
            var localByProductId = ListCachedPackages(refresh: false).ToDictionary(p => p.ProductId, p => p);
            var importedIds = GetImportedProductIds(probe);

            var tcs = new TaskCompletionSource<MyAssetsSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Delegate? productFetchedDelegate = null;
            Delegate? listOperationDelegate = null;
            object? capturedOperation = null;
            EventInfo? opErrorEvent = null;
            EventInfo? opFinalizedEvent = null;
            Delegate? opErrorDelegate = null;
            Delegate? opFinalizedDelegate = null;

            void AttachOperationListeners(object? operation)
            {
                if (operation == null || ReferenceEquals(capturedOperation, operation))
                {
                    return;
                }

                capturedOperation = operation;
                var opType = operation.GetType();
                opErrorEvent = AssetStoreCompatibilityProbe.FindEvent(opType, "onOperationError");
                if (opErrorEvent?.EventHandlerType != null)
                {
                    Action<object?, object?> onError = (_, uiError) =>
                    {
                        var errorMsg = ExtractUIErrorMessage(uiError) ?? "Asset Store purchase list operation failed.";
                        tcs.TrySetResult(MyAssetsSearchResult.Failure(query, "OPERATION_ERROR", errorMsg));
                    };
                    opErrorDelegate = Delegate.CreateDelegate(opErrorEvent.EventHandlerType, onError.Target, onError.Method, throwOnBindFailure: false);
                    if (opErrorDelegate != null)
                    {
                        opErrorEvent.AddEventHandler(operation, opErrorDelegate);
                    }
                }

                opFinalizedEvent = AssetStoreCompatibilityProbe.FindEvent(opType, "onOperationFinalized");
                if (opFinalizedEvent?.EventHandlerType != null)
                {
                    Action<object?> onFinalized = _ =>
                    {
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.TrySetResult(MyAssetsSearchResult.Failure(
                                query,
                                "OPERATION_FINALIZED_WITHOUT_RESULT",
                                "Asset Store purchase list operation finalized without returning results."));
                        }
                    };
                    opFinalizedDelegate = Delegate.CreateDelegate(opFinalizedEvent.EventHandlerType, onFinalized.Target, onFinalized.Method, throwOnBindFailure: false);
                    if (opFinalizedDelegate != null)
                    {
                        opFinalizedEvent.AddEventHandler(operation, opFinalizedDelegate);
                    }
                }
            }

            Action<object?> onFetched = purchasesObj =>
            {
                try
                {
                    var result = MapPurchasesResult(query, purchasesObj, localByProductId, importedIds);
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(MyAssetsSearchResult.Failure(query, "MAPPING_ERROR", $"Failed to map AssetStorePurchases: {ex.Message}"));
                }
            };

            try
            {
                var fetchedHandlerType = probe.OnProductListFetchedEvent.EventHandlerType;
                if (fetchedHandlerType == null)
                {
                    return MyAssetsSearchResult.Failure(query, "EVENT_BIND_ERROR", "onProductListFetched event handler type could not be resolved.");
                }

                productFetchedDelegate = Delegate.CreateDelegate(fetchedHandlerType, onFetched.Target, onFetched.Method, throwOnBindFailure: false);
                if (productFetchedDelegate == null)
                {
                    return MyAssetsSearchResult.Failure(query, "EVENT_BIND_ERROR", "Failed to bind delegate to onProductListFetched.");
                }

                probe.OnProductListFetchedEvent.AddEventHandler(probe.AssetStoreClientInstance, productFetchedDelegate);

                if (probe.OnListOperationEvent?.EventHandlerType != null)
                {
                    Action<object?> onListOp = AttachOperationListeners;
                    listOperationDelegate = Delegate.CreateDelegate(probe.OnListOperationEvent.EventHandlerType, onListOp.Target, onListOp.Method, throwOnBindFailure: false);
                    if (listOperationDelegate != null)
                    {
                        probe.OnListOperationEvent.AddEventHandler(probe.AssetStoreClientInstance, listOperationDelegate);
                    }
                }

                using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

                var queryArgsInstance = CreatePurchasesQueryArgs(probe, query);
                probe.ListPurchasesMethod.Invoke(probe.AssetStoreClientInstance, new[] { queryArgsInstance });

                if (capturedOperation == null)
                {
                    var opField = AssetStoreCompatibilityProbe.FindField(probe.AssetStoreClientInstance.GetType(), "m_ListOperation");
                    AttachOperationListeners(opField?.GetValue(probe.AssetStoreClientInstance));
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                if (productFetchedDelegate != null)
                {
                    probe.OnProductListFetchedEvent?.RemoveEventHandler(probe.AssetStoreClientInstance, productFetchedDelegate);
                }

                if (listOperationDelegate != null)
                {
                    probe.OnListOperationEvent?.RemoveEventHandler(probe.AssetStoreClientInstance, listOperationDelegate);
                }

                if (capturedOperation != null && opErrorDelegate != null)
                {
                    opErrorEvent?.RemoveEventHandler(capturedOperation, opErrorDelegate);
                }

                if (capturedOperation != null && opFinalizedDelegate != null)
                {
                    opFinalizedEvent?.RemoveEventHandler(capturedOperation, opFinalizedDelegate);
                }
            }
        }

        public DownloadStatusInfo StartDownload(long productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            var probe = EnsureCompatibleProbe();
            var manager = probe.AssetStoreDownloadManagerInstance!;

            try
            {
                if (probe.DownloadManyMethod != null)
                {
                    probe.DownloadManyMethod.Invoke(manager, new object[] { new[] { productId } });
                }
                else if (probe.DownloadSingleMethod != null)
                {
                    probe.DownloadSingleMethod.Invoke(manager, new object[] { productId });
                }
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                return new DownloadStatusInfo(
                    productId: productId,
                    requestAccepted: false,
                    state: "Error",
                    isInProgress: false,
                    isPaused: false,
                    progress: 0f,
                    downloadedBytes: 0UL,
                    totalBytes: 0UL,
                    isCachedOnDisk: false,
                    cachedPackagePath: string.Empty,
                    cachedVersion: string.Empty,
                    errorCode: "DOWNLOAD_START_ERROR",
                    errorMessage: inner.Message);
            }

            return GetDownloadStatus(productId);
        }

        public DownloadStatusInfo GetDownloadStatus(long productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            var probe = EnsureCompatibleProbe();
            var cached = GetCachedPackage(productId, refresh: false);
            var cachedPath = cached?.PackagePath ?? string.Empty;
            var cachedVersion = cached?.VersionString ?? string.Empty;

            if (probe.GetDownloadOperationMethod != null && probe.AssetStoreDownloadManagerInstance != null)
            {
                var operation = InvokeWithNullableLong(probe.GetDownloadOperationMethod, probe.AssetStoreDownloadManagerInstance, productId);
                if (operation != null)
                {
                    var stateObj = AssetStoreCompatibilityProbe.GetMemberValue(operation, "state");
                    var stateStr = stateObj?.ToString() ?? "Unknown";
                    var progressObj = AssetStoreCompatibilityProbe.GetMemberValue(operation, "progressPercentage");
                    var progress = progressObj is float f ? Mathf.Clamp01(f) : 0f;
                    var isInProgress = AssetStoreCompatibilityProbe.GetMemberValue(operation, "isInProgress") is bool ip && ip;
                    var isPaused = AssetStoreCompatibilityProbe.GetMemberValue(operation, "isInPause") is bool pause && pause;
                    var errorMsg = AssetStoreCompatibilityProbe.GetMemberValue(operation, "errorMessage") as string ?? string.Empty;
                    var downloadedBytes = ConvertToUInt64(AssetStoreCompatibilityProbe.GetMemberValue(operation, "m_DownloadedBytes"));
                    var totalBytes = ConvertToUInt64(AssetStoreCompatibilityProbe.GetMemberValue(operation, "m_TotalBytes"));

                    if (!isInProgress && string.IsNullOrEmpty(cachedPath))
                    {
                        cached = GetCachedPackage(productId, refresh: true);
                        cachedPath = cached?.PackagePath ?? string.Empty;
                        cachedVersion = cached?.VersionString ?? string.Empty;
                    }

                    var isCached = cached != null && cached.FileExists;
                    return new DownloadStatusInfo(
                        productId: productId,
                        requestAccepted: true,
                        state: stateStr,
                        isInProgress: isInProgress,
                        isPaused: isPaused,
                        progress: progress,
                        downloadedBytes: downloadedBytes,
                        totalBytes: totalBytes,
                        isCachedOnDisk: isCached,
                        cachedPackagePath: cachedPath,
                        cachedVersion: cachedVersion,
                        errorCode: string.IsNullOrEmpty(errorMsg) ? string.Empty : "DOWNLOAD_ERROR",
                        errorMessage: errorMsg);
                }
            }

            if (cached == null)
            {
                cached = GetCachedPackage(productId, refresh: true);
                cachedPath = cached?.PackagePath ?? string.Empty;
                cachedVersion = cached?.VersionString ?? string.Empty;
            }

            if (cached != null && cached.FileExists)
            {
                var fileSize = File.Exists(cachedPath) ? (ulong)Math.Max(0L, new FileInfo(cachedPath).Length) : 0UL;
                return new DownloadStatusInfo(
                    productId: productId,
                    requestAccepted: true,
                    state: "Completed",
                    isInProgress: false,
                    isPaused: false,
                    progress: 1f,
                    downloadedBytes: fileSize,
                    totalBytes: fileSize,
                    isCachedOnDisk: true,
                    cachedPackagePath: cachedPath,
                    cachedVersion: cachedVersion,
                    errorCode: string.Empty,
                    errorMessage: string.Empty);
            }

            return new DownloadStatusInfo(
                productId: productId,
                requestAccepted: false,
                state: "None",
                isInProgress: false,
                isPaused: false,
                progress: 0f,
                downloadedBytes: 0UL,
                totalBytes: 0UL,
                isCachedOnDisk: false,
                cachedPackagePath: string.Empty,
                cachedVersion: string.Empty,
                errorCode: string.Empty,
                errorMessage: string.Empty);
        }

        public bool AbortDownload(long productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            var probe = EnsureCompatibleProbe();
            if (probe.AbortDownloadMethod == null || probe.AssetStoreDownloadManagerInstance == null)
            {
                return false;
            }

            InvokeWithNullableLong(probe.AbortDownloadMethod, probe.AssetStoreDownloadManagerInstance, productId);
            return true;
        }

        public ImportPackageResult ImportPackage(long productId, bool interactive)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be a positive integer.");
            }

            var probe = EnsureCompatibleProbe();
            var cached = GetCachedPackage(productId, refresh: true);
            if (cached == null || string.IsNullOrWhiteSpace(cached.PackagePath) || !File.Exists(cached.PackagePath))
            {
                return ImportPackageResult.Failed(
                    productId: productId,
                    packagePath: cached?.PackagePath ?? string.Empty,
                    interactive: interactive,
                    errorCode: "PACKAGE_NOT_DOWNLOADED",
                    message: $"Package {productId} is not present in the local Asset Store cache. Download it first before importing.");
            }

            try
            {
                if (probe.InstallMethod != null && probe.AssetStorePackageInstallerInstance != null)
                {
                    probe.InstallMethod.Invoke(probe.AssetStorePackageInstallerInstance, new object[] { productId, interactive });
                }
                else
                {
                    AssetDatabase.ImportPackage(cached.PackagePath, interactive);
                }

                return ImportPackageResult.Succeeded(
                    productId: productId,
                    title: cached.Title,
                    versionString: cached.VersionString,
                    packagePath: cached.PackagePath,
                    interactive: interactive);
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                return ImportPackageResult.Failed(
                    productId: productId,
                    packagePath: cached.PackagePath,
                    interactive: interactive,
                    errorCode: "IMPORT_FAILED",
                    message: $"Failed to import package {productId}: {inner.Message}");
            }
        }

        public ImportPackageResult ImportPackageFile(string packagePath, bool interactive)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return ImportPackageResult.Failed(
                    productId: 0L,
                    packagePath: string.Empty,
                    interactive: interactive,
                    errorCode: "EMPTY_PATH",
                    message: "Package file path must not be empty.");
            }

            var fullPath = Path.GetFullPath(packagePath);
            if (!fullPath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                return ImportPackageResult.Failed(
                    productId: 0L,
                    packagePath: fullPath,
                    interactive: interactive,
                    errorCode: "INVALID_EXTENSION",
                    message: $"File '{fullPath}' does not have a '.unitypackage' extension.");
            }

            if (!File.Exists(fullPath))
            {
                return ImportPackageResult.Failed(
                    productId: 0L,
                    packagePath: fullPath,
                    interactive: interactive,
                    errorCode: "FILE_NOT_FOUND",
                    message: $"Package file does not exist at '{fullPath}'.");
            }

            try
            {
                AssetDatabase.ImportPackage(fullPath, interactive);
                return ImportPackageResult.Succeeded(
                    productId: 0L,
                    title: Path.GetFileNameWithoutExtension(fullPath),
                    versionString: string.Empty,
                    packagePath: fullPath,
                    interactive: interactive);
            }
            catch (Exception ex)
            {
                return ImportPackageResult.Failed(
                    productId: 0L,
                    packagePath: fullPath,
                    interactive: interactive,
                    errorCode: "IMPORT_FAILED",
                    message: $"Failed to import '{fullPath}': {ex.Message}");
            }
        }

        private AssetStoreCompatibilityProbe EnsureCompatibleProbe()
        {
            var probe = GetProbe();
            if (!probe.IsCompatible)
            {
                throw new InvalidOperationException(
                    $"Unity internal Asset Store API is not compatible on Unity {Application.unityVersion}. Missing: {string.Join(", ", probe.MissingMembers)}");
            }

            return probe;
        }

        private static void RefreshLocalInfos(AssetStoreCompatibilityProbe probe)
        {
            if (probe.FullScanLocalInfosMethod != null && probe.AssetStoreClientInstance != null)
            {
                try
                {
                    probe.FullScanLocalInfosMethod.Invoke(probe.AssetStoreClientInstance, Array.Empty<object>());
                }
                catch
                {
                    // Ignore scan errors and continue with existing cache state.
                }
            }
        }

        private static HashSet<long> GetImportedProductIds(AssetStoreCompatibilityProbe probe)
        {
            var set = new HashSet<long>();
            if (probe.ImportedPackagesProperty == null || probe.AssetStoreCacheInstance == null)
            {
                return set;
            }

            try
            {
                if (probe.ImportedPackagesProperty.GetValue(probe.AssetStoreCacheInstance) is IEnumerable imported)
                {
                    foreach (var item in imported)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        var id = ConvertToInt64(AssetStoreCompatibilityProbe.GetMemberValue(item, "productId"));
                        if (id > 0)
                        {
                            set.Add(id);
                        }
                    }
                }
            }
            catch
            {
                // Ignore optional imported package inspection errors.
            }

            return set;
        }

        private static CachedPackageInfo? MapLocalInfo(object rawLocalInfo, HashSet<long> importedIds)
        {
            var productId = ConvertToInt64(AssetStoreCompatibilityProbe.GetMemberValue(rawLocalInfo, "productId"));
            if (productId <= 0)
            {
                return null;
            }

            var uploadId = ConvertToInt64(AssetStoreCompatibilityProbe.GetMemberValue(rawLocalInfo, "uploadId"));
            var versionId = ConvertToInt64(AssetStoreCompatibilityProbe.GetMemberValue(rawLocalInfo, "versionId"));
            var title = AssetStoreCompatibilityProbe.GetMemberValue(rawLocalInfo, "title") as string ?? string.Empty;
            var versionString = AssetStoreCompatibilityProbe.GetMemberValue(rawLocalInfo, "versionString") as string ?? string.Empty;
            var supportedVersion = AssetStoreCompatibilityProbe.GetMemberValue(rawLocalInfo, "supportedVersion") as string ?? string.Empty;
            var publishedDate = AssetStoreCompatibilityProbe.GetMemberValue(rawLocalInfo, "publishedDate") as string ?? string.Empty;
            var packagePath = AssetStoreCompatibilityProbe.GetMemberValue(rawLocalInfo, "packagePath") as string ?? string.Empty;
            var fileExists = !string.IsNullOrEmpty(packagePath) && File.Exists(packagePath);

            return new CachedPackageInfo(
                productId: productId,
                uploadId: uploadId,
                versionId: versionId,
                title: title,
                versionString: versionString,
                supportedVersion: supportedVersion,
                publishedDate: publishedDate,
                packagePath: packagePath,
                fileExists: fileExists,
                isImported: importedIds.Contains(productId));
        }

        private static object CreatePurchasesQueryArgs(AssetStoreCompatibilityProbe probe, MyAssetsQuery query)
        {
            var type = probe.PurchasesQueryArgsType!;
            object? instance = null;

            try
            {
                var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(c => c.GetParameters().Length == 0);
                if (ctor != null)
                {
                    instance = ctor.Invoke(Array.Empty<object>());
                }
                else
                {
                    var fourArgCtor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(c => c.GetParameters().Length == 4);
                    if (fourArgCtor != null)
                    {
                        instance = fourArgCtor.Invoke(new object?[] { query.StartIndex, query.Limit, query.SearchText, null });
                    }
                }
            }
            catch
            {
                instance = null;
            }

            instance ??= FormatterServices.GetUninitializedObject(type);

            AssetStoreCompatibilityProbe.SetMemberValue(instance, "startIndex", query.StartIndex);
            var limitField = AssetStoreCompatibilityProbe.FindField(type, "limit");
            if (limitField != null && limitField.FieldType == typeof(long))
            {
                limitField.SetValue(instance, (long)query.Limit);
            }
            else
            {
                AssetStoreCompatibilityProbe.SetMemberValue(instance, "limit", query.Limit);
            }

            AssetStoreCompatibilityProbe.SetMemberValue(instance, "searchText", query.SearchText);

            if (query.ProductIds.Count > 0)
            {
                var idsField = AssetStoreCompatibilityProbe.FindField(type, "productIds");
                if (idsField != null)
                {
                    if (idsField.FieldType == typeof(long[]))
                    {
                        idsField.SetValue(instance, query.ProductIds.ToArray());
                    }
                    else if (idsField.FieldType.IsAssignableFrom(typeof(List<long>)))
                    {
                        idsField.SetValue(instance, query.ProductIds.ToList());
                    }
                }
            }

            return instance;
        }

        private static MyAssetsSearchResult MapPurchasesResult(
            MyAssetsQuery query,
            object? purchasesObj,
            Dictionary<long, CachedPackageInfo> localByProductId,
            HashSet<long> importedIds)
        {
            if (purchasesObj == null)
            {
                return MyAssetsSearchResult.Failure(query, "NULL_RESPONSE", "AssetStorePurchases callback returned null.");
            }

            var totalCount = ConvertToInt64(AssetStoreCompatibilityProbe.GetMemberValue(purchasesObj, "total"));
            var listEnumerable = AssetStoreCompatibilityProbe.GetMemberValue(purchasesObj, "list") as IEnumerable;
            var items = new List<PurchasedAssetSummary>();

            if (listEnumerable != null)
            {
                foreach (var entry in listEnumerable)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    var productId = ConvertToInt64(AssetStoreCompatibilityProbe.GetMemberValue(entry, "productId"));
                    var displayName = AssetStoreCompatibilityProbe.GetMemberValue(entry, "displayName") as string ?? string.Empty;
                    var purchasedTime = AssetStoreCompatibilityProbe.GetMemberValue(entry, "purchasedTime") as string ?? string.Empty;
                    var isHidden = AssetStoreCompatibilityProbe.GetMemberValue(entry, "isHidden") is bool hidden && hidden;

                    var tagsObj = AssetStoreCompatibilityProbe.GetMemberValue(entry, "tags");
                    var tags = ExtractStringArray(tagsObj);

                    localByProductId.TryGetValue(productId, out var localInfo);
                    var isDownloaded = localInfo != null && localInfo.FileExists;
                    var isImported = importedIds.Contains(productId) || (localInfo?.IsImported ?? false);

                    items.Add(new PurchasedAssetSummary(
                        productId: productId,
                        displayName: displayName,
                        purchasedTime: purchasedTime,
                        tags: tags,
                        isHidden: isHidden,
                        isDownloaded: isDownloaded,
                        localVersion: localInfo?.VersionString ?? string.Empty,
                        localPackagePath: localInfo?.PackagePath ?? string.Empty,
                        isImported: isImported));
                }
            }

            return MyAssetsSearchResult.Success(query, totalCount, items);
        }

        private static string[] ExtractStringArray(object? raw)
        {
            if (raw is string[] strArray)
            {
                return strArray;
            }

            if (raw is IEnumerable enumerable)
            {
                var result = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item is string s)
                    {
                        result.Add(s);
                    }
                }

                return result.ToArray();
            }

            return Array.Empty<string>();
        }

        private static string? ExtractUIErrorMessage(object? uiError)
        {
            if (uiError == null)
            {
                return null;
            }

            var message = AssetStoreCompatibilityProbe.GetMemberValue(uiError, "message") as string;
            var statusCode = AssetStoreCompatibilityProbe.GetMemberValue(uiError, "operationErrorCode");
            if (!string.IsNullOrWhiteSpace(message))
            {
                return statusCode != null ? $"{message} (code: {statusCode})" : message;
            }

            return uiError.ToString();
        }

        private static object? InvokeWithNullableLong(MethodInfo method, object instance, long value)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                return null;
            }

            var paramType = parameters[0].ParameterType;
            object arg = paramType == typeof(long?) ? (long?)value : value;
            return method.Invoke(instance, new[] { arg });
        }

        private static long ConvertToInt64(object? value)
        {
            return value switch
            {
                long l => l,
                int i => i,
                ulong ul => (long)ul,
                string s when long.TryParse(s, out var parsed) => parsed,
                _ => 0L
            };
        }

        private static ulong ConvertToUInt64(object? value)
        {
            return value switch
            {
                ulong ul => ul,
                long l when l > 0 => (ulong)l,
                int i when i > 0 => (ulong)i,
                _ => 0UL
            };
        }
    }
}
