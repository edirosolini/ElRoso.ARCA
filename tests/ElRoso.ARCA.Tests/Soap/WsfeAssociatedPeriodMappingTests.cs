using ElRoso.ARCA.Billing;

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
}
