// Cada valor de VATConditionARCAEnum es el código de condición IVA del receptor de ARCA.
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Domains.Enums;

public class VATConditionARCAEnumTests
{
    [Theory]
    [InlineData(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO, 1)]
    [InlineData(VATConditionARCAEnum.IVA_SUJETO_EXENTO, 4)]
    [InlineData(VATConditionARCAEnum.CONSUMIDOR_FINAL, 5)]
    [InlineData(VATConditionARCAEnum.MONOTRIBUTO, 6)]
    [InlineData(VATConditionARCAEnum.SUJETO_NO_CATEGORIZADO, 7)]
    [InlineData(VATConditionARCAEnum.PROVEEDOR_DEL_EXTERIOR, 8)]
    [InlineData(VATConditionARCAEnum.CLIENTE_DEL_EXTERIOR, 9)]
    [InlineData(VATConditionARCAEnum.IVA_LIBERADO_LEY_19640, 10)]
    [InlineData(VATConditionARCAEnum.MONOTRIBUTISTA_SOCIAL, 13)]
    [InlineData(VATConditionARCAEnum.IVA_NO_ALCANZADO, 15)]
    [InlineData(VATConditionARCAEnum.MONOTRIBUTO_TRABAJADOR_INDEPENDIENTE_PROMOVIDO, 16)]
    public void Members_should_match_the_ARCA_receiver_code(VATConditionARCAEnum condition, int expected)
    {
        ((int)condition).Should().Be(expected);
    }

    [Fact]
    public void Enum_should_have_exactly_the_eleven_ARCA_conditions()
    {
        Enum.GetValues<VATConditionARCAEnum>().Should().HaveCount(11);
    }

    [Fact]
    public void Values_should_be_unique()
    {
        var values = Enum.GetValues<VATConditionARCAEnum>().Select(v => (int)v);

        values.Should().OnlyHaveUniqueItems();
    }
}
