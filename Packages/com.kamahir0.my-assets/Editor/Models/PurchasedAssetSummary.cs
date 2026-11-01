#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Summary of a purchased Asset Store product combined with its local cache and import status.
    /// </summary>
    [Serializable]
    public sealed class PurchasedAssetSummary
    {
        [SerializeField] private long productId;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string purchasedTime = string.Empty;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private bool isHidden;
        [SerializeField] private bool isDownloaded;
        [SerializeField] private string localVersion = string.Empty;
        [SerializeField] private string localPackagePath = string.Empty;
        [SerializeField] private bool isImported;

        public PurchasedAssetSummary(
            long productId,
            string displayName,
            string? purchasedTime = null,
            IReadOnlyList<string>? tags = null,
            bool isHidden = false,
            bool isDownloaded = false,
            string? localVersion = null,
            string? localPackagePath = null,
            bool isImported = false)
        {
            this.productId = productId;
            this.displayName = displayName ?? string.Empty;
            this.purchasedTime = purchasedTime ?? string.Empty;
            this.isHidden = isHidden;
            this.isDownloaded = isDownloaded;
            this.localVersion = localVersion ?? string.Empty;
            this.localPackagePath = localPackagePath ?? string.Empty;
            this.isImported = isImported;
            if (tags is null || tags.Count == 0)
            {
                this.tags = Array.Empty<string>();
            }
            else
            {
                var copy = new string[tags.Count];
                for (var i = 0; i < tags.Count; i++)
                {
                    copy[i] = tags[i] ?? string.Empty;
                }

                this.tags = copy;
            }
        }

        public long ProductId => productId;
        public string DisplayName => displayName;
        public string PurchasedTime => purchasedTime;
        public IReadOnlyList<string> Tags => tags;
        public bool IsHidden => isHidden;
        public bool IsDownloaded => isDownloaded;
        public string LocalVersion => localVersion;
        public string LocalPackagePath => localPackagePath;
        public bool IsImported => isImported;
    }
}
