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

        persona.datosRegimenGeneral.impuesto.Should().HaveCount(2);
        persona.datosRegimenGeneral.impuesto.Select(t => t.idImpuesto).Should().Equal(10, 30);
        persona.datosRegimenGeneral.impuesto.Should().OnlyContain(t => t.idImpuestoSpecified && t.periodoSpecified);
        persona.datosRegimenGeneral.impuesto[1].estadoImpuesto.Should().Be("AC");
        persona.datosRegimenGeneral.impuesto[1].motivo.Should().Be("INSCRIPCIÓN TRAMITADA EN AGENCIA");
    }

    [Fact]
    public void Characterization_with_fechaSolicitud_should_keep_its_id()
    {
        var persona = Deserialize(RegisteredLegalEntityEnvelope);

        var characterization = persona.datosGenerales.caracterizacion.Should().ContainSingle().Subject;
        characterization.idCaracterizacion.Should().Be(639);
        characterization.idCaracterizacionSpecified.Should().BeTrue();
        characterization.periodo.Should().Be(20250101);
        characterization.fechaSolicitud.Should().Be(20260220);
    }

    [Fact]
    public void Registered_legal_entity_response_should_map_to_RI()
    {
        var result = PadronOperations.MapPersona(Deserialize(RegisteredLegalEntityEnvelope), Cuit);

        result.Taxes.Select(t => t.Id).Should().Equal(10, 30);
        result.VATCondition.Should().Be(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO);
        result.ReceiverVATConditionId.Should().Be(1);
        result.DisplayName.Should().Be("EMPRESA DEMO SA");
    }
}
