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
        Assert.Contains(key, DictionariesCommon.Currencies);
        Assert.Equal(expectedCode, DictionariesCommon.Currencies[key]);
    }

    [Fact]
    public void Currencies_should_have_three_entries()
    {
        Assert.Equal(3, DictionariesCommon.Currencies.Count);
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
        Assert.Contains(key, DictionariesCommon.Tax);
        Assert.Equal(expectedCode, DictionariesCommon.Tax[key]);
    }

    [Theory]
    [InlineData("Impuestos nacionales", 1)]
    [InlineData("Impuestos provinciales", 2)]
    [InlineData("Impuestos municipales", 3)]
    [InlineData("Impuestos internos", 4)]
    [InlineData("Otros", 99)]
    public void OtherTax_should_contain_expected_arca_codes(string key, short expectedCode)
    {
        Assert.Contains(key, DictionariesCommon.OtherTax);
        Assert.Equal(expectedCode, DictionariesCommon.OtherTax[key]);
    }

    [Theory]
    [InlineData("Español", (short)1)]
    [InlineData("Inglés", (short)2)]
    [InlineData("Portugués", (short)3)]
    public void Language_should_contain_expected_arca_codes(string key, short expectedCode)
    {
        Assert.Contains(key, DictionariesCommon.Language);
        Assert.Equal(expectedCode, DictionariesCommon.Language[key]);
    }

    [Fact]
    public void Dictionaries_should_be_readonly()
    {
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(DictionariesCommon.Currencies);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(DictionariesCommon.Tax);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, short>>(DictionariesCommon.OtherTax);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, short>>(DictionariesCommon.Language);
    }
}
