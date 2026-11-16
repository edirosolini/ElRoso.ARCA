using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Soap;

public class WsfeAssociatedPeriodMappingTests
{
    [Fact]
    public void MapAssociatedPeriod_maps_dates_in_ARCA_format()
    {
        var result = WsfeOperations.MapAssociatedPeriod(new BillingDocumentNumberingAssociatedPeriodRequest
        {
            DateFrom = new DateTime(2026, 1, 2),
            DateTo = new DateTime(2026, 1, 31),
        });

        result.Should().NotBeNull();
        result!.FchDesde.Should().Be("20260102");
        result.FchHasta.Should().Be("20260131");
    }

    [Fact]
    public void MapAssociatedPeriod_returns_null_when_period_is_not_provided()
    {
        WsfeOperations.MapAssociatedPeriod(null).Should().BeNull();
    }

    [Fact]
    public void MapAssociatedDocuments_returns_null_when_documents_are_not_provided()
    {
        WsfeOperations.MapAssociatedDocuments(null).Should().BeNull();
    }

    [Fact]
    public void MapAssociatedDocuments_returns_null_when_documents_are_empty()
    {
        WsfeOperations.MapAssociatedDocuments([]).Should().BeNull();
    }

    [Fact]
    public void MapAssociatedDocuments_maps_non_empty_documents()
    {
        var result = WsfeOperations.MapAssociatedDocuments(
        [
            new BillingDocumentNumberingAssociatedRequest
            {
                BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
                BillingDocumentNumber = 42,
                BillingDocumentBookPrefix = 3,
                BillingDocumentDate = new DateTime(2026, 1, 10),
            },
        ]);

        result.Should().ContainSingle();
        result![0].Tipo.Should().Be((int)BillingDocumentTypeARCAEnum.FA);
        result[0].Nro.Should().Be(42);
        result[0].PtoVta.Should().Be(3);
        result[0].CbteFch.Should().Be("20260110");
    }
}
