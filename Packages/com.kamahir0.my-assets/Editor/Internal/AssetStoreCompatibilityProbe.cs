#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Lilja.MyAssets.Editor.Internal
{
    /// <summary>
    /// Reflection probe that resolves and validates internal Package Manager UI Asset Store services
    /// across Unity Editor versions without taking a compile-time dependency on internal modules.
    /// </summary>
    internal sealed class AssetStoreCompatibilityProbe
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public Type? ServicesContainerType { get; private set; }
        public object? ServicesContainerInstance { get; private set; }

        public Type? AssetStoreClientType { get; private set; }
        public object? AssetStoreClientInstance { get; private set; }

        public Type? AssetStoreDownloadManagerType { get; private set; }
        public object? AssetStoreDownloadManagerInstance { get; private set; }

        public Type? AssetStorePackageInstallerType { get; private set; }
        public object? AssetStorePackageInstallerInstance { get; private set; }

        public Type? AssetStoreCacheType { get; private set; }
        public object? AssetStoreCacheInstance { get; private set; }

        public Type? UnityConnectProxyType { get; private set; }
        public object? UnityConnectProxyInstance { get; private set; }

        public Type? AssetStoreCachePathProxyType { get; private set; }
        public object? AssetStoreCachePathProxyInstance { get; private set; }

        public Type? PurchasesQueryArgsType { get; private set; }
        public Type? AssetStorePurchasesType { get; private set; }
        public Type? AssetStorePurchaseInfoType { get; private set; }
        public Type? AssetStoreLocalInfoType { get; private set; }
        public Type? AssetStoreDownloadOperationType { get; private set; }
        public Type? UIErrorType { get; private set; }

        public MethodInfo? ListPurchasesMethod { get; private set; }
        public MethodInfo? FullScanLocalInfosMethod { get; private set; }
        public EventInfo? OnProductListFetchedEvent { get; private set; }
        public EventInfo? OnListOperationEvent { get; private set; }

        public MethodInfo? DownloadManyMethod { get; private set; }
        public MethodInfo? DownloadSingleMethod { get; private set; }
        public MethodInfo? GetDownloadOperationMethod { get; private set; }
        public MethodInfo? AbortDownloadMethod { get; private set; }
        public MethodInfo? IsAnyDownloadInProgressMethod { get; private set; }

        public MethodInfo? InstallMethod { get; private set; }

        public PropertyInfo? LocalInfosProperty { get; private set; }
        public PropertyInfo? ImportedPackagesProperty { get; private set; }
        public MethodInfo? GetLocalInfoMethod { get; private set; }
        public MethodInfo? GetImportedPackageMethod { get; private set; }

        public PropertyInfo? IsUserLoggedInProperty { get; private set; }
        public PropertyInfo? IsUserInfoReadyProperty { get; private set; }
        public PropertyInfo? UserDisplayNameProperty { get; private set; }
        public MethodInfo? GetCachePathConfigMethod { get; private set; }

        public List<string> ResolvedServices { get; } = new List<string>();
        public List<string> MissingMembers { get; } = new List<string>();

        public bool IsCompatible => MissingMembers.Count == 0
                                    && ServicesContainerInstance != null
                                    && AssetStoreClientInstance != null
                                    && AssetStoreDownloadManagerInstance != null
                                    && AssetStorePackageInstallerInstance != null
                                    && AssetStoreCacheInstance != null;

        public static AssetStoreCompatibilityProbe Probe()
        {
            var probe = new AssetStoreCompatibilityProbe();
            probe.Initialize();
            return probe;
        }

        private void Initialize()
        {
            var editorAssembly = typeof(EditorApplication).Assembly;

            ServicesContainerType = FindInternalType(editorAssembly, "UnityEditor.PackageManager.UI.Internal.ServicesContainer");
            if (ServicesContainerType == null)
            {
                MissingMembers.Add("Type:UnityEditor.PackageManager.UI.Internal.ServicesContainer");
                return;
            }

            ServicesContainerInstance = GetSingletonInstance(ServicesContainerType);
            if (ServicesContainerInstance == null)
            {
                MissingMembers.Add("ServicesContainer.instance");
                return;
            }

            ResolvedServices.Add("ServicesContainer");

            AssetStoreClientInstance = ResolveService(
                editorAssembly,
                "UnityEditor.PackageManager.UI.Internal.IAssetStoreClient",
                "UnityEditor.PackageManager.UI.Internal.AssetStoreClientV2",
                "UnityEditor.PackageManager.UI.Internal.AssetStoreClient");
            if (AssetStoreClientInstance != null)
            {
                AssetStoreClientType = AssetStoreClientInstance.GetType();
                ResolvedServices.Add($"IAssetStoreClient ({AssetStoreClientType.Name})");
            }
            else
            {
                MissingMembers.Add("Service:IAssetStoreClient");
            }

            AssetStoreDownloadManagerInstance = ResolveService(
                editorAssembly,
                "UnityEditor.PackageManager.UI.Internal.IAssetStoreDownloadManager",
                "UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager");
            if (AssetStoreDownloadManagerInstance != null)
            {
                AssetStoreDownloadManagerType = AssetStoreDownloadManagerInstance.GetType();
                ResolvedServices.Add($"IAssetStoreDownloadManager ({AssetStoreDownloadManagerType.Name})");
            }
            else
            {
                MissingMembers.Add("Service:IAssetStoreDownloadManager");
            }

            AssetStorePackageInstallerInstance = ResolveService(
                editorAssembly,
                "UnityEditor.PackageManager.UI.Internal.IAssetStorePackageInstaller",
                "UnityEditor.PackageManager.UI.Internal.AssetStorePackageInstaller");
            if (AssetStorePackageInstallerInstance != null)
            {
                AssetStorePackageInstallerType = AssetStorePackageInstallerInstance.GetType();
                ResolvedServices.Add($"IAssetStorePackageInstaller ({AssetStorePackageInstallerType.Name})");
            }
            else
            {
                MissingMembers.Add("Service:IAssetStorePackageInstaller");
            }

            AssetStoreCacheInstance = ResolveService(
                editorAssembly,
                "UnityEditor.PackageManager.UI.Internal.IAssetStoreCache",
                "UnityEditor.PackageManager.UI.Internal.AssetStoreCache");
            if (AssetStoreCacheInstance != null)
            {
                AssetStoreCacheType = AssetStoreCacheInstance.GetType();
                ResolvedServices.Add($"IAssetStoreCache ({AssetStoreCacheType.Name})");
            }
            else
            {
                MissingMembers.Add("Service:IAssetStoreCache");
            }

            UnityConnectProxyInstance = ResolveService(
                editorAssembly,
                "UnityEditor.PackageManager.UI.Internal.IUnityConnectProxy",
                "UnityEditor.PackageManager.UI.Internal.UnityConnectProxy");
            if (UnityConnectProxyInstance != null)
            {
                UnityConnectProxyType = UnityConnectProxyInstance.GetType();
                ResolvedServices.Add($"IUnityConnectProxy ({UnityConnectProxyType.Name})");
            }

            AssetStoreCachePathProxyInstance = ResolveService(
                editorAssembly,
                "UnityEditor.PackageManager.UI.Internal.IAssetStoreCachePathProxy",
                "UnityEditor.PackageManager.UI.Internal.AssetStoreCachePathProxy");
            if (AssetStoreCachePathProxyInstance != null)
            {
                AssetStoreCachePathProxyType = AssetStoreCachePathProxyInstance.GetType();
                ResolvedServices.Add($"IAssetStoreCachePathProxy ({AssetStoreCachePathProxyType.Name})");
            }

            PurchasesQueryArgsType = FindInternalType(editorAssembly, "UnityEditor.PackageManager.UI.Internal.PurchasesQueryArgs");
            if (PurchasesQueryArgsType == null)
            {
                MissingMembers.Add("Type:PurchasesQueryArgs");
            }

            AssetStorePurchasesType = FindInternalType(editorAssembly, "UnityEditor.PackageManager.UI.Internal.AssetStorePurchases");
            if (AssetStorePurchasesType == null)
            {
                MissingMembers.Add("Type:AssetStorePurchases");
            }

            AssetStorePurchaseInfoType = FindInternalType(editorAssembly, "UnityEditor.PackageManager.UI.Internal.AssetStorePurchaseInfo");
            if (AssetStorePurchaseInfoType == null)
            {
                MissingMembers.Add("Type:AssetStorePurchaseInfo");
            }

            AssetStoreLocalInfoType = FindInternalType(editorAssembly, "UnityEditor.PackageManager.UI.Internal.AssetStoreLocalInfo");
            if (AssetStoreLocalInfoType == null)
            {
                MissingMembers.Add("Type:AssetStoreLocalInfo");
            }

            AssetStoreDownloadOperationType = FindInternalType(editorAssembly, "UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadOperation");
            if (AssetStoreDownloadOperationType == null)
            {
                MissingMembers.Add("Type:AssetStoreDownloadOperation");
            }

            UIErrorType = FindInternalType(editorAssembly, "UnityEditor.PackageManager.UI.Internal.UIError");

            BindClientMembers();
            BindDownloadManagerMembers();
            BindInstallerMembers();
            BindCacheMembers();
            BindOptionalMembers();
        }

        private void BindClientMembers()
        {
            if (AssetStoreClientType == null)
            {
                return;
            }

            ListPurchasesMethod = FindMethod(AssetStoreClientType, "ListPurchases", 1);
            if (ListPurchasesMethod == null)
            {
                MissingMembers.Add("IAssetStoreClient.ListPurchases(PurchasesQueryArgs)");
            }

            FullScanLocalInfosMethod = FindMethod(AssetStoreClientType, "FullScanLocalInfos", 0);
            if (FullScanLocalInfosMethod == null)
            {
                MissingMembers.Add("IAssetStoreClient.FullScanLocalInfos()");
            }

            OnProductListFetchedEvent = FindEvent(AssetStoreClientType, "onProductListFetched");
            if (OnProductListFetchedEvent == null)
            {
                MissingMembers.Add("IAssetStoreClient.onProductListFetched");
            }

            OnListOperationEvent = FindEvent(AssetStoreClientType, "onListOperation");
        }

        private void BindDownloadManagerMembers()
        {
            if (AssetStoreDownloadManagerType == null)
            {
                return;
            }

            DownloadManyMethod = FindMethod(AssetStoreDownloadManagerType, "Download", typeof(IEnumerable<long>));
            DownloadSingleMethod = FindMethod(AssetStoreDownloadManagerType, "Download", typeof(long));
            if (DownloadManyMethod == null && DownloadSingleMethod == null)
            {
                MissingMembers.Add("IAssetStoreDownloadManager.Download");
            }

            GetDownloadOperationMethod = FindMethod(AssetStoreDownloadManagerType, "GetDownloadOperation", 1);
            if (GetDownloadOperationMethod == null)
            {
                MissingMembers.Add("IAssetStoreDownloadManager.GetDownloadOperation");
            }

            AbortDownloadMethod = FindMethod(AssetStoreDownloadManagerType, "AbortDownload", 1);
            if (AbortDownloadMethod == null)
            {
                MissingMembers.Add("IAssetStoreDownloadManager.AbortDownload");
            }

            IsAnyDownloadInProgressMethod = FindMethod(AssetStoreDownloadManagerType, "IsAnyDownloadInProgress", 0);
        }

        private void BindInstallerMembers()
        {
            if (AssetStorePackageInstallerType == null)
            {
                return;
            }

            InstallMethod = FindMethod(AssetStorePackageInstallerType, "Install", typeof(long), typeof(bool));
            if (InstallMethod == null)
            {
                MissingMembers.Add("IAssetStorePackageInstaller.Install(long, bool)");
            }
        }

        private void BindCacheMembers()
        {
            if (AssetStoreCacheType == null)
            {
                return;
            }

            LocalInfosProperty = FindProperty(AssetStoreCacheType, "localInfos");
            if (LocalInfosProperty == null)
            {
                MissingMembers.Add("IAssetStoreCache.localInfos");
            }

            ImportedPackagesProperty = FindProperty(AssetStoreCacheType, "importedPackages");
            GetLocalInfoMethod = FindMethod(AssetStoreCacheType, "GetLocalInfo", 1);
            if (GetLocalInfoMethod == null)
            {
                MissingMembers.Add("IAssetStoreCache.GetLocalInfo(long?)");
            }

            GetImportedPackageMethod = FindMethod(AssetStoreCacheType, "GetImportedPackage", 1);
        }

        private void BindOptionalMembers()
        {
            if (UnityConnectProxyType != null)
            {
                IsUserLoggedInProperty = FindProperty(UnityConnectProxyType, "isUserLoggedIn");
                IsUserInfoReadyProperty = FindProperty(UnityConnectProxyType, "isUserInfoReady");
                UserDisplayNameProperty = FindProperty(UnityConnectProxyType, "displayName");
            }

            if (AssetStoreCachePathProxyType != null)
            {
                GetCachePathConfigMethod = FindMethod(AssetStoreCachePathProxyType, "GetConfig", 0);
            }
        }

        public bool GetIsUserLoggedIn()
        {
            if (UnityConnectProxyInstance == null || IsUserLoggedInProperty == null)
            {
                return false;
            }

            try
            {
                var value = IsUserLoggedInProperty.GetValue(UnityConnectProxyInstance);
                return value is bool loggedIn && loggedIn;
            }
            catch
            {
                return false;
            }
        }

        public bool GetIsUserInfoReady()
        {
            if (UnityConnectProxyInstance == null || IsUserInfoReadyProperty == null)
            {
                return false;
            }

            try
            {
                var value = IsUserInfoReadyProperty.GetValue(UnityConnectProxyInstance);
                return value is bool ready && ready;
            }
            catch
            {
                return false;
            }
        }

        public string GetUserDisplayName()
        {
            if (UnityConnectProxyInstance == null || UserDisplayNameProperty == null)
            {
                return string.Empty;
            }

            try
            {
                return UserDisplayNameProperty.GetValue(UnityConnectProxyInstance) as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool GetIsAnyDownloadInProgress()
        {
            if (AssetStoreDownloadManagerInstance == null || IsAnyDownloadInProgressMethod == null)
            {
                return false;
            }

            try
            {
                var value = IsAnyDownloadInProgressMethod.Invoke(AssetStoreDownloadManagerInstance, Array.Empty<object>());
                return value is bool inProgress && inProgress;
            }
            catch
            {
                return false;
            }
        }

        public string GetCacheRootPath()
        {
            if (AssetStoreCachePathProxyInstance != null && GetCachePathConfigMethod != null)
            {
                try
                {
                    var config = GetCachePathConfigMethod.Invoke(AssetStoreCachePathProxyInstance, Array.Empty<object>());
                    if (config != null)
                    {
                        var pathProp = FindProperty(config.GetType(), "path");
                        if (pathProp?.GetValue(config) is string path && !string.IsNullOrWhiteSpace(path))
                        {
                            return path;
                        }
                    }
                }
                catch
                {
                    // Ignore and fall back to Empty.
                }
            }

            return string.Empty;
        }

        private object? ResolveService(Assembly editorAssembly, params string[] candidateTypeNames)
        {
            if (ServicesContainerType == null || ServicesContainerInstance == null)
            {
                return null;
            }

            var genericResolve = FindGenericResolveMethod(ServicesContainerType);
            foreach (var typeName in candidateTypeNames)
            {
                var candidateType = FindInternalType(editorAssembly, typeName);
                if (candidateType == null)
                {
                    continue;
                }

                if (genericResolve != null)
                {
                    try
                    {
                        var typedResolve = genericResolve.MakeGenericMethod(candidateType);
                        var instance = typedResolve.Invoke(ServicesContainerInstance, Array.Empty<object>());
                        if (instance != null)
                        {
                            return instance;
                        }
                    }
                    catch
                    {
                        // Fall through to dictionary scan.
                    }
                }

                var fromDictionary = FindInRegisteredServicesDictionary(ServicesContainerType, ServicesContainerInstance, candidateType);
                if (fromDictionary != null)
                {
                    return fromDictionary;
                }
            }

            return null;
        }

        private static object? FindInRegisteredServicesDictionary(Type containerType, object containerInstance, Type targetType)
        {
            var dictField = containerType.GetField("m_RegisteredServices", AnyInstance);
            if (dictField?.GetValue(containerInstance) is IDictionary dictionary)
            {
                if (dictionary.Contains(targetType))
                {
                    return dictionary[targetType];
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value != null && targetType.IsInstanceOfType(entry.Value))
                    {
                        return entry.Value;
                    }
                }
            }

            return null;
        }

        private static MethodInfo? FindGenericResolveMethod(Type containerType)
        {
            var methods = containerType.GetMethods(AnyInstance);
            foreach (var method in methods)
            {
                if (method.Name == "Resolve" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0)
                {
                    return method;
                }
            }

            return null;
        }

        private static object? GetSingletonInstance(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var prop = current.GetProperty("instance", AnyStatic);
                if (prop != null)
                {
                    return prop.GetValue(null);
                }

                var field = current.GetField("s_Instance", AnyStatic) ?? current.GetField("instance", AnyStatic);
                if (field != null)
                {
                    return field.GetValue(null);
                }
            }

            return null;
        }

        private static Type? FindInternalType(Assembly primaryAssembly, string fullName)
        {
            var found = primaryAssembly.GetType(fullName, throwOnError: false);
            if (found != null)
            {
                return found;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    found = assembly.GetType(fullName, throwOnError: false);
                    if (found != null)
                    {
                        return found;
                    }
                }
                catch
                {
                    // Ignore dynamic assembly lookup errors.
                }
            }

            return null;
        }

        internal static MethodInfo? FindMethod(Type type, string name, int parameterCount)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var method in current.GetMethods(AnyInstance))
                {
                    if (method.Name == name && method.GetParameters().Length == parameterCount)
                    {
                        return method;
                    }
                }
            }

            foreach (var iface in type.GetInterfaces())
            {
                foreach (var method in iface.GetMethods(AnyInstance))
                {
                    if (method.Name == name && method.GetParameters().Length == parameterCount)
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        internal static MethodInfo? FindMethod(Type type, string name, params Type[] parameterTypes)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var method = current.GetMethod(name, AnyInstance, binder: null, types: parameterTypes, modifiers: null);
                if (method != null)
                {
                    return method;
                }
            }

            foreach (var iface in type.GetInterfaces())
            {
                var method = iface.GetMethod(name, AnyInstance, binder: null, types: parameterTypes, modifiers: null);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        internal static PropertyInfo? FindProperty(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var prop = current.GetProperty(name, AnyInstance);
                if (prop != null)
                {
                    return prop;
                }
            }

            foreach (var iface in type.GetInterfaces())
            {
                var prop = iface.GetProperty(name, AnyInstance);
                if (prop != null)
                {
                    return prop;
                }
            }

            return null;
        }

        internal static FieldInfo? FindField(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, AnyInstance);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        internal static EventInfo? FindEvent(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var evt = current.GetEvent(name, AnyInstance);
                if (evt != null)
                {
                    return evt;
                }
            }

            foreach (var iface in type.GetInterfaces())
            {
                var evt = iface.GetEvent(name, AnyInstance);
                if (evt != null)
                {
                    return evt;
                }
            }

            return null;
        }

        internal static object? GetMemberValue(object instance, string memberName)
        {
            var type = instance.GetType();
            var prop = FindProperty(type, memberName);
            if (prop != null)
            {
                return prop.GetValue(instance);
            }

            var field = FindField(type, memberName);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            return null;
        }

        internal static void SetMemberValue(object instance, string memberName, object? value)
        {
            var type = instance.GetType();
            var prop = FindProperty(type, memberName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(instance, value);
                return;
            }

            var field = FindField(type, memberName);
            if (field != null)
            {
                field.SetValue(instance, value);
            }
        }
    }
}
