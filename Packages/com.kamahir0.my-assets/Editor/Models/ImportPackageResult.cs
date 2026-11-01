#nullable enable
using System;
using UnityEngine;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Outcome of importing a downloaded Asset Store .unitypackage into the current project.
    /// </summary>
    [Serializable]
    public sealed class ImportPackageResult
    {
        [SerializeField] private bool success;
        [SerializeField] private long productId;
        [SerializeField] private string title = string.Empty;
        [SerializeField] private string versionString = string.Empty;
        [SerializeField] private string packagePath = string.Empty;
        [SerializeField] private bool interactive;
        [SerializeField] private string errorCode = string.Empty;
        [SerializeField] private string message = string.Empty;

        public ImportPackageResult(
            bool success,
            long productId,
            string? title,
            string? versionString,
            string? packagePath,
            bool interactive,
            string? errorCode = null,
            string? message = null)
        {
            this.success = success;
            this.productId = productId;
            this.title = title ?? string.Empty;
            this.versionString = versionString ?? string.Empty;
            this.packagePath = packagePath ?? string.Empty;
            this.interactive = interactive;
            this.errorCode = errorCode ?? string.Empty;
            this.message = message ?? string.Empty;
        }

        public bool Success => success;
        public long ProductId => productId;
        public string Title => title;
        public string VersionString => versionString;
        public string PackagePath => packagePath;
        public bool Interactive => interactive;
        public string ErrorCode => errorCode;
        public string Message => message;

        public static ImportPackageResult Succeeded(
            long productId,
            string title,
            string versionString,
            string packagePath,
            bool interactive)
        {
            return new ImportPackageResult(
                success: true,
                productId: productId,
                title: title,
                versionString: versionString,
                packagePath: packagePath,
                interactive: interactive,
                errorCode: string.Empty,
                message: $"Imported '{title}' ({packagePath}).");
        }

        public static ImportPackageResult Failed(
            long productId,
            string? packagePath,
            bool interactive,
            string errorCode,
            string message)
        {
            return new ImportPackageResult(
                success: false,
                productId: productId,
                title: string.Empty,
                versionString: string.Empty,
                packagePath: packagePath,
                interactive: interactive,
                errorCode: errorCode,
                message: message);
        }
    }
}
