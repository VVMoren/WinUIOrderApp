using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WinUIOrderApp.Models
{
    public class NationalCatalogProductListResponse
    {
        [JsonPropertyName("apiversion")]
        public int ApiVersion
        {
            get; set;
        }

        [JsonPropertyName("result")]
        public NationalCatalogProductListResult? Result
        {
            get; set;
        }
    }

    public class NationalCatalogProductListResult
    {
        [JsonPropertyName("goods")]
        public List<NationalCatalogGood> Goods
        {
            get;
            set;
        } = new();

        [JsonPropertyName("offset")]
        public int Offset
        {
            get; set;
        }

        [JsonPropertyName("limit")]
        public int Limit
        {
            get; set;
        }

        [JsonPropertyName("total")]
        public int Total
        {
            get; set;
        }
    }

    public class NationalCatalogGood
    {
        [JsonPropertyName("good_id")]
        public long GoodId
        {
            get; set;
        }

        [JsonPropertyName("gtin")]
        public string? Gtin
        {
            get; set;
        }

        [JsonPropertyName("good_name")]
        public string? GoodName
        {
            get; set;
        }

        [JsonPropertyName("tnved")]
        public string? Tnved
        {
            get; set;
        }

        [JsonPropertyName("brand_name")]
        public string? BrandName
        {
            get; set;
        }

        [JsonPropertyName("good_status")]
        public string? GoodStatus
        {
            get; set;
        }

        [JsonPropertyName("good_detailed_status")]
        public List<string>? GoodDetailedStatus
        {
            get; set;
        }

        [JsonPropertyName("updated_date")]
        public string? UpdatedDate
        {
            get; set;
        }
    }

    public class NationalCatalogProductInfoResponse
    {
        [JsonPropertyName("results")]
        public List<NationalCatalogProductInfo> Results
        {
            get;
            set;
        } = new();

        [JsonPropertyName("errorCode")]
        public string? ErrorCode
        {
            get; set;
        }

        [JsonPropertyName("total")]
        public int Total
        {
            get; set;
        }
    }

    public class NationalCatalogProductInfo
    {
        [JsonPropertyName("name")]
        public string? Name
        {
            get; set;
        }

        [JsonPropertyName("gtin")]
        public string? Gtin
        {
            get; set;
        }

        [JsonPropertyName("brand")]
        public string? Brand
        {
            get; set;
        }

        [JsonPropertyName("productGroupId")]
        public int? ProductGroupId
        {
            get; set;
        }

        [JsonPropertyName("productGroup")]
        public string? ProductGroupCode
        {
            get; set;
        }

        [JsonPropertyName("productKind")]
        public string? ProductKind
        {
            get; set;
        }

        [JsonPropertyName("tnVedCode")]
        public string? TnVedCode
        {
            get; set;
        }

        [JsonPropertyName("tnVedCode10")]
        public string? TnVedCode10
        {
            get; set;
        }

        [JsonPropertyName("goodStatus")]
        public string? GoodStatus
        {
            get; set;
        }

        [JsonPropertyName("packageType")]
        public string? PackageType
        {
            get; set;
        }

        [JsonPropertyName("nicotineConcentration")]
        public string? NicotineConcentration
        {
            get; set;
        }

        [JsonPropertyName("volumeLiquid")]
        public string? VolumeLiquid
        {
            get; set;
        }
    }
}
