// <copyright file="ClientRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core
{
    using ElRoso.ARCA.Core;
    using ElRoso.ARCA.Billing;
    using ElRoso.ARCA.Read;

    public class ClientRequest
    {
        private VATConditionARCAEnum condition;
        private string clientName = string.Empty;

        public DocumentTypeARCAEnum DocumentType { get; set; }

        public long DocumentNumber { get; set; }

        public VATConditionARCAEnum Condition => condition;

        public string ClientName => clientName;

        public string Address { get; set; } = string.Empty;

        public short CountryId { get; set; }

        public string ClientLanguage { get; set; } = string.Empty;

        public void SetCondition(VATConditionARCAEnum value) => condition = value;

        public void SetClientName(string value) => clientName = value;
    }
}
