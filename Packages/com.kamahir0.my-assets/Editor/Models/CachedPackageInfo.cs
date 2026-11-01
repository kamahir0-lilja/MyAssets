#nullable enable
using System;
using UnityEngine;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Metadata for a locally cached Asset Store .unitypackage on disk.
    /// </summary>
    [Serializable]
    public sealed class CachedPackageInfo
    {
        [SerializeField] private long productId;
        [SerializeField] private long uploadId;
        [SerializeField] private long versionId;
        [SerializeField] private string title = string.Empty;
        [SerializeField] private string versionString = string.Empty;
        [SerializeField] private string supportedVersion = string.Empty;
        [SerializeField] private string publishedDate = string.Empty;
        [SerializeField] private string packagePath = string.Empty;
        [SerializeField] private bool fileExists;
        [SerializeField] private bool isImported;

        public CachedPackageInfo(
            long productId,
            long uploadId,
            long versionId,
            string title,
            string versionString,
            string supportedVersion,
            string publishedDate,
            string packagePath,
            bool fileExists,
            bool isImported)
        {
            this.productId = productId;
            this.uploadId = uploadId;
            this.versionId = versionId;
            this.title = title ?? string.Empty;
            this.versionString = versionString ?? string.Empty;
            this.supportedVersion = supportedVersion ?? string.Empty;
            this.publishedDate = publishedDate ?? string.Empty;
            this.packagePath = packagePath ?? string.Empty;
            this.fileExists = fileExists;
            this.isImported = isImported;
        }

        public long ProductId => productId;
        public long UploadId => uploadId;
        public long VersionId => versionId;
        public string Title => title;
        public string VersionString => versionString;
        public string SupportedVersion => supportedVersion;
        public string PublishedDate => publishedDate;
        public string PackagePath => packagePath;
        public bool FileExists => fileExists;
        public bool IsImported => isImported;
    }
}
