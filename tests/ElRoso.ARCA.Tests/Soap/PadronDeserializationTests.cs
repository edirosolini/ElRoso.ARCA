// Deserializa respuestas SOAP del Padrón A5 con el proxy real, como lo hace el cliente WCF.
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Xml;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Soap;

public class PadronDeserializationTests
{
    private const long Cuit = 30712345678;

    // Respuesta con la estructura del ejemplo del manual v4.1 (impuesto con estadoImpuesto y motivo).
    private const string RegisteredLegalEntityEnvelope = """
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <ns2:getPersonaResponse xmlns:ns2="http://a5.soap.ws.server.puc.sr/">
              <personaReturn>
                <datosGenerales>
                  <caracterizacion>
                    <descripcionCaracterizacion>GANANCIAS SIMPLIFICADA LEY 27.779</descripcionCaracterizacion>
                    <fechaSolicitud>20260220</fechaSolicitud>
                    <idCaracterizacion>639</idCaracterizacion>
                    <periodo>20250101</periodo>
                  </caracterizacion>
                  <domicilioFiscal>
                    <codPostal>1000</codPostal>
                    <descripcionProvincia>CIUDAD AUTONOMA BUENOS AIRES</descripcionProvincia>
                    <direccion>CALLE FALSA 123</direccion>
                    <idProvincia>0</idProvincia>
                    <tipoDomicilio>FISCAL</tipoDomicilio>
                  </domicilioFiscal>
                  <estadoClave>ACTIVO</estadoClave>
                  <idPersona>30712345678</idPersona>
                  <mesCierre>12</mesCierre>
                  <razonSocial>EMPRESA DEMO SA</razonSocial>
                  <tipoClave>CUIT</tipoClave>
                  <tipoPersona>JURIDICA</tipoPersona>
                </datosGenerales>
                <datosRegimenGeneral>
                  <actividad>
                    <descripcionActividad>SERVICIOS DE CONSULTORES EN INFORMATICA</descripcionActividad>
                    <idActividad>620100</idActividad>
                    <nomenclador>883</nomenclador>
                    <orden>1</orden>
                    <periodo>201311</periodo>
                  </actividad>
                  <impuesto>
                    <descripcionImpuesto>GANANCIAS SOCIEDADES</descripcionImpuesto>
                    <estadoImpuesto>AC</estadoImpuesto>
                    <idImpuesto>10</idImpuesto>
                    <motivo>INSCRIPCIÓN TRAMITADA EN AGENCIA</motivo>
                    <periodo>201501</periodo>
                  </impuesto>
                  <impuesto>
                    <descripcionImpuesto>IVA</descripcionImpuesto>
                    <estadoImpuesto>AC</estadoImpuesto>
                    <idImpuesto>30</idImpuesto>
                    <motivo>INSCRIPCIÓN TRAMITADA EN AGENCIA</motivo>
                    <periodo>201501</periodo>
                  </impuesto>
                </datosRegimenGeneral>
                <metadata>
                  <fechaHora>2026-02-26T15:52:37.653-03:00</fechaHora>
                  <servidor>padron-test</servidor>
                </metadata>
              </personaReturn>
            </ns2:getPersonaResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private static Padron.personaReturn Deserialize(string envelope)
    {
        using var reader = XmlReader.Create(new StringReader(envelope));
        using var message = Message.CreateMessage(reader, int.MaxValue, MessageVersion.Soap11);
        var converter = TypedMessageConverter.Create(
            typeof(Padron.getPersonaResponse),
            "*",
            new XmlSerializerFormatAttribute { SupportFaults = true });

        return ((Padron.getPersonaResponse)converter.FromMessage(message)).personaReturn;
    }

    [Fact]
    public void Tax_with_estadoImpuesto_and_motivo_should_keep_its_id_and_new_fields()
    {
        var persona = Deserialize(RegisteredLegalEntityEnvelope);

        Assert.Equal(2, persona.datosRegimenGeneral.impuesto.Count());
        Assert.Equal(new[] { 10, 30 }, persona.datosRegimenGeneral.impuesto.Select(t => t.idImpuesto));
        Assert.All(persona.datosRegimenGeneral.impuesto, t => Assert.True(t.idImpuestoSpecified && t.periodoSpecified));
        Assert.Equal("AC", persona.datosRegimenGeneral.impuesto[1].estadoImpuesto);
        Assert.Equal("INSCRIPCIÓN TRAMITADA EN AGENCIA", persona.datosRegimenGeneral.impuesto[1].motivo);
    }

    [Fact]
    public void Characterization_with_fechaSolicitud_should_keep_its_id()
    {
        var persona = Deserialize(RegisteredLegalEntityEnvelope);

        var characterization = Assert.Single(persona.datosGenerales.caracterizacion);
        Assert.Equal(639, characterization.idCaracterizacion);
        Assert.True(characterization.idCaracterizacionSpecified);
        Assert.Equal(20250101, characterization.periodo);
        Assert.Equal(20260220, characterization.fechaSolicitud);
    }

    [Fact]
    public void Registered_legal_entity_response_should_map_to_RI()
    {
        var result = PadronOperations.MapPersona(Deserialize(RegisteredLegalEntityEnvelope), Cuit);

        Assert.Equal(new int?[] { 10, 30 }, result.Taxes.Select(t => t.Id));
        Assert.Equal(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO, result.VATCondition);
        Assert.Equal(1, result.ReceiverVATConditionId);
        Assert.Equal("EMPRESA DEMO SA", result.DisplayName);
    }
}
