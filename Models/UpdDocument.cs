using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WinUIOrderApp.Models
{
    public class UpdDocument
    {
        public string Id { get; set; } = string.Empty;
        public UpdHeader Header { get; set; } = new UpdHeader();
        public UpdBody Body { get; set; } = new UpdBody();
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentStatus { get; set; } = string.Empty;
    }

    public class UpdHeader
    {
        [JsonPropertyName("invoiceId")]
        public string InvoiceId { get; set; } = string.Empty;

        [JsonPropertyName("invoiceDate")]
        public string InvoiceDate { get; set; } = string.Empty;
    }

    public class UpdBody
    {
        [JsonPropertyName("cisesInfo")]
        public List<CisInfo> CisesInfo { get; set; } = new List<CisInfo>();

        [JsonPropertyName("signerBuyer")]
        public UpdParticipant SignerBuyer { get; set; } = new UpdParticipant();

        [JsonPropertyName("signerSeller")]
        public UpdParticipant SignerSeller { get; set; } = new UpdParticipant();
    }

    public class CisInfo
    {
        public string Cis { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Gtin { get; set; } = string.Empty;
    }

    public class UpdParticipant
    {
        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("inn")]
        public string Inn { get; set; } = string.Empty;
    }

    public class UpdSearchRequest
    {
        public Dictionary<string, object> Filter { get; set; } = new Dictionary<string, object>();
        public string DocumentType { get; set; } = "UNIVERSAL_TRANSFER_DOCUMENT";
        public string DocumentStatus { get; set; } = "ALL";
        public UpdPagination Pagination { get; set; } = new UpdPagination();
    }

    public class UpdPagination
    {
        public int Limit { get; set; } = 10000;
        public int Offset { get; set; } = 0;
        public string Order { get; set; } = "DESC";
    }

    public class UpdRecord
    {
        public string Cis { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string UpdNumber { get; set; } = string.Empty;
        public string DocumentDate { get; set; } = string.Empty;
    }
}