#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lilja.MyAssets.Editor
{
    /// <summary>
    /// Parameters for querying purchased Asset Store packages.
    /// </summary>
    [Serializable]
    public sealed class MyAssetsQuery
    {
        [SerializeField] private string searchText = string.Empty;
        [SerializeField] private int startIndex;
        [SerializeField] private int limit = 25;
        [SerializeField] private long[] productIds = Array.Empty<long>();

        public MyAssetsQuery(
            string? searchText = null,
            int startIndex = 0,
            int limit = 25,
            IReadOnlyList<long>? productIds = null)
        {
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex), "startIndex must be greater than or equal to 0.");
            }

            if (limit <= 0 || limit > 500)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "limit must be between 1 and 500.");
            }

            this.searchText = searchText?.Trim() ?? string.Empty;
            this.startIndex = startIndex;
            this.limit = limit;
            if (productIds is null || productIds.Count == 0)
            {
                this.productIds = Array.Empty<long>();
            }
            else
            {
                var ids = new long[productIds.Count];
                for (var i = 0; i < productIds.Count; i++)
                {
                    if (productIds[i] <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(productIds), "All productIds must be positive.");
                    }

                    ids[i] = productIds[i];
                }

                this.productIds = ids;
            }
        }

        public string SearchText => searchText;
        public int StartIndex => startIndex;
        public int Limit => limit;
        public IReadOnlyList<long> ProductIds => productIds;
    }
}
