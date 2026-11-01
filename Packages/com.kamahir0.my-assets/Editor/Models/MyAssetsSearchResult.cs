#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Result of a purchased assets search operation.
    /// </summary>
    [Serializable]
    public sealed class MyAssetsSearchResult
    {
        [SerializeField] private bool isSuccess;
        [SerializeField] private string state = string.Empty;
        [SerializeField] private string searchText = string.Empty;
        [SerializeField] private int startIndex;
        [SerializeField] private int limit;
        [SerializeField] private long total;
        [SerializeField] private PurchasedAssetSummary[] items = Array.Empty<PurchasedAssetSummary>();
        [SerializeField] private string errorCode = string.Empty;
        [SerializeField] private string errorMessage = string.Empty;

        public MyAssetsSearchResult(
            bool isSuccess,
            string state,
            string searchText,
            int startIndex,
            int limit,
            long total,
            IReadOnlyList<PurchasedAssetSummary>? items,
            string? errorCode = null,
            string? errorMessage = null)
        {
            this.isSuccess = isSuccess;
            this.state = state ?? string.Empty;
            this.searchText = searchText ?? string.Empty;
            this.startIndex = startIndex;
            this.limit = limit;
            this.total = total;
            this.errorCode = errorCode ?? string.Empty;
            this.errorMessage = errorMessage ?? string.Empty;
            if (items is null || items.Count == 0)
            {
                this.items = Array.Empty<PurchasedAssetSummary>();
            }
            else
            {
                var copy = new PurchasedAssetSummary[items.Count];
                for (var i = 0; i < items.Count; i++)
                {
                    copy[i] = items[i];
                }

                this.items = copy;
            }
        }

        public bool IsSuccess => isSuccess;
        public string State => state;
        public string SearchText => searchText;
        public int StartIndex => startIndex;
        public int Limit => limit;
        public long Total => total;
        public IReadOnlyList<PurchasedAssetSummary> Items => items;
        public string ErrorCode => errorCode;
        public string ErrorMessage => errorMessage;

        public static MyAssetsSearchResult Success(
            MyAssetsQuery query,
            long total,
            IReadOnlyList<PurchasedAssetSummary> items)
        {
            return new MyAssetsSearchResult(
                isSuccess: true,
                state: "completed",
                searchText: query.SearchText,
                startIndex: query.StartIndex,
                limit: query.Limit,
                total: total,
                items: items);
        }

        public static MyAssetsSearchResult Failure(
            MyAssetsQuery query,
            string errorCode,
            string errorMessage,
            string state = "failed")
        {
            return new MyAssetsSearchResult(
                isSuccess: false,
                state: state,
                searchText: query.SearchText,
                startIndex: query.StartIndex,
                limit: query.Limit,
                total: 0,
                items: Array.Empty<PurchasedAssetSummary>(),
                errorCode: errorCode,
                errorMessage: errorMessage);
        }
    }
}
