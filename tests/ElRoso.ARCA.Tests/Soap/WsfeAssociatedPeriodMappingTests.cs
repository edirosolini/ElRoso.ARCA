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

        Assert.NotNull(result);
        Assert.Equal("20260102", result!.FchDesde);
        Assert.Equal("20260131", result.FchHasta);
    }

    [Fact]
    public void MapAssociatedPeriod_returns_null_when_period_is_not_provided()
    {
        Assert.Null(WsfeOperations.MapAssociatedPeriod(null));
    }

    [Fact]
    public void MapAssociatedDocuments_returns_null_when_documents_are_not_provided()
    {
        Assert.Null(WsfeOperations.MapAssociatedDocuments(null));
    }

    [Fact]
    public void MapAssociatedDocuments_returns_null_when_documents_are_empty()
    {
        Assert.Null(WsfeOperations.MapAssociatedDocuments([]));
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

        Assert.Single(result!);
        Assert.Equal((int)BillingDocumentTypeARCAEnum.FA, result![0].Tipo);
        Assert.Equal(42, result[0].Nro);
        Assert.Equal(3, result[0].PtoVta);
        Assert.Equal("20260110", result[0].CbteFch);
    }
}
