// Mapeo de la respuesta WCF de FEParamGetPtosVenta a puntos de venta.
using ElRoso.ARCA.Billing;

namespace ElRoso.ARCA.Tests.Soap;

public class WsfePointsOfSaleMappingTests
{
    [Fact]
    public void Should_map_number_emission_type_block_and_deactivation_date()
    {
        var response = new WSFEv1.FEPtoVentaResponse
        {
            ResultGet =
            [
                new WSFEv1.PtoVenta { Nro = 3, EmisionTipo = "CAE - RECE", Bloqueado = "N", FchBaja = "NULL" },
                new WSFEv1.PtoVenta { Nro = 5, EmisionTipo = "CAEA - RECE", Bloqueado = "S", FchBaja = "20260131" },
            ],
        };

        var result = WsfeOperations.MapPointsOfSale(response);

        result.Errors.Should().BeEmpty();
        result.PointsOfSale.Should().BeEquivalentTo(
        [
            new PointOfSaleResponse { Number = 3, EmissionType = "CAE - RECE", IsBlocked = false, DeactivationDate = null },
            new PointOfSaleResponse { Number = 5, EmissionType = "CAEA - RECE", IsBlocked = true, DeactivationDate = new DateTime(2026, 1, 31) },
        ]);
    }

    [Fact]
    public void Should_surface_ARCA_errors_with_their_code()
    {
        var response = new WSFEv1.FEPtoVentaResponse
        {
            Errors = [new WSFEv1.Err { Code = 600, Msg = "ValidacionDeToken: No apareció CUIT en lista de relaciones" }],
        };

        var result = WsfeOperations.MapPointsOfSale(response);

        result.PointsOfSale.Should().BeEmpty();
        result.Errors.Should().ContainSingle().Which.Should().Be(
            new WsfeError { Code = 600, Message = "ValidacionDeToken: No apareció CUIT en lista de relaciones" });
    }
}
