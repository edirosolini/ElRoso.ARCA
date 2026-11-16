// EN: Tests for ClientRequest - read-only Condition/ClientName mutated via internal setters.
// ES: Tests para ClientRequest - Condition/ClientName son read-only y mutan via setters internos.
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

namespace ElRoso.ARCA.Tests.Domains.Requests;

public class ClientRequestTests
{
    [Fact]
    public void Defaults_should_have_empty_strings_and_zero_values()
    {
        var client = new ClientRequest();

        Assert.Empty(client.ClientName);
        Assert.Empty(client.Address);
        Assert.Empty(client.ClientLanguage);
        Assert.Equal(0, client.DocumentNumber);
        Assert.Equal(0, client.CountryId);
    }

    [Fact]
    public void SetCondition_should_update_readonly_property()
    {
        var client = new ClientRequest();

        client.SetCondition(VATConditionARCAEnum.CONSUMIDOR_FINAL);

        Assert.Equal(VATConditionARCAEnum.CONSUMIDOR_FINAL, client.Condition);
    }

    [Fact]
    public void SetClientName_should_update_readonly_property()
    {
        var client = new ClientRequest();

        client.SetClientName("Juan Pérez SA");

        Assert.Equal("Juan Pérez SA", client.ClientName);
    }
}