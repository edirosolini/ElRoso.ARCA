// EN: Tests for DictionariesCommon - maps business strings to ARCA codes.
// ES: Tests para DictionariesCommon - mapea strings de negocio a códigos ARCA.
using ElRoso.ARCA.Billing;

namespace ElRoso.ARCA.Tests.Commons;

public class DictionariesCommonTests
{
    [Theory]
    [InlineData("Pesos", "PES")]
    [InlineData("Dolares", "DOL")]
    [InlineData("Euro", "060")]
    public void Currencies_should_contain_expected_arca_codes(string key, string expectedCode)
    {
        DictionariesCommon.Currencies.Should().ContainKey(key);
        DictionariesCommon.Currencies[key].Should().Be(expectedCode);
    }

    [Fact]
    public void Currencies_should_have_three_entries()
    {
        DictionariesCommon.Currencies.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("No Gravado", 3)]
    [InlineData("Exento", 2)]
    [InlineData("0.00", 3)]
    [InlineData("10.50", 4)]
    [InlineData("21.00", 5)]
    [InlineData("27.00", 6)]
    [InlineData("5.00", 8)]
    [InlineData("2.50", 9)]
    public void Tax_should_contain_expected_arca_codes(string key, int expectedCode)
    {
        DictionariesCommon.Tax.Should().ContainKey(key);
        DictionariesCommon.Tax[key].Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("Impuestos nacionales", 1)]
    [InlineData("Impuestos provinciales", 2)]
    [InlineData("Impuestos municipales", 3)]
    [InlineData("Impuestos internos", 4)]
    [InlineData("Otros", 99)]
    public void OtherTax_should_contain_expected_arca_codes(string key, short expectedCode)
    {
        DictionariesCommon.OtherTax.Should().ContainKey(key);
        DictionariesCommon.OtherTax[key].Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("Español", (short)1)]
    [InlineData("Inglés", (short)2)]
    [InlineData("Portugués", (short)3)]
    public void Language_should_contain_expected_arca_codes(string key, short expectedCode)
    {
        DictionariesCommon.Language.Should().ContainKey(key);
        DictionariesCommon.Language[key].Should().Be(expectedCode);
    }

    [Fact]
    public void Dictionaries_should_be_readonly()
    {
        DictionariesCommon.Currencies.Should().BeAssignableTo<IReadOnlyDictionary<string, string>>();
        DictionariesCommon.Tax.Should().BeAssignableTo<IReadOnlyDictionary<string, int>>();
        DictionariesCommon.OtherTax.Should().BeAssignableTo<IReadOnlyDictionary<string, short>>();
        DictionariesCommon.Language.Should().BeAssignableTo<IReadOnlyDictionary<string, short>>();
    }
}
