#nullable enable
using System;
using UnityEngine;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Current download and local cache status for a specific Asset Store product ID.
    /// </summary>
    [Serializable]
    public sealed class DownloadStatusInfo
    {
        [SerializeField] private long productId;
        [SerializeField] private bool requestAccepted;
        [SerializeField] private string state = string.Empty;
        [SerializeField] private bool isInProgress;
        [SerializeField] private bool isPaused;
        [SerializeField] private float progress;
        [SerializeField] private ulong downloadedBytes;
        [SerializeField] private ulong totalBytes;
        [SerializeField] private bool isCachedOnDisk;
        [SerializeField] private string cachedPackagePath = string.Empty;
        [SerializeField] private string cachedVersion = string.Empty;
        [SerializeField] private string errorCode = string.Empty;
        [SerializeField] private string errorMessage = string.Empty;

        public DownloadStatusInfo(
            long productId,
            bool requestAccepted,
            string state,
            bool isInProgress,
            bool isPaused,
            float progress,
            ulong downloadedBytes,
            ulong totalBytes,
            bool isCachedOnDisk,
            string? cachedPackagePath = null,
            string? cachedVersion = null,
            string? errorCode = null,
            string? errorMessage = null)
        {
            this.productId = productId;
            this.requestAccepted = requestAccepted;
            this.state = state ?? string.Empty;
            this.isInProgress = isInProgress;
            this.isPaused = isPaused;
            this.progress = Mathf.Clamp01(progress);
            this.downloadedBytes = downloadedBytes;
            this.totalBytes = totalBytes;
            this.isCachedOnDisk = isCachedOnDisk;
            this.cachedPackagePath = cachedPackagePath ?? string.Empty;
            this.cachedVersion = cachedVersion ?? string.Empty;
            this.errorCode = errorCode ?? string.Empty;
            this.errorMessage = errorMessage ?? string.Empty;
        }

        public long ProductId => productId;
        public bool RequestAccepted => requestAccepted;
        public string State => state;
        public bool IsInProgress => isInProgress;
        public bool IsPaused => isPaused;
        public float Progress => progress;
        public ulong DownloadedBytes => downloadedBytes;
        public ulong TotalBytes => totalBytes;
        public bool IsCachedOnDisk => isCachedOnDisk;
        public string CachedPackagePath => cachedPackagePath;
        public string CachedVersion => cachedVersion;
        public string ErrorCode => errorCode;
        public string ErrorMessage => errorMessage;
    }
}
