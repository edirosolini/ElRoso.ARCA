# Roadmap

> 📌 Este documento lista features futuros y pendientes de `ElRoso.ARCA`. **Para reportar bugs o sugerir un feature concreto**, abrí un [Issue](https://github.com/edirosolini/ElRoso.ARCA/issues/new/choose) o una [Discussion](https://github.com/edirosolini/ElRoso.ARCA/discussions).

---

## ✅ Ya entregado

| Versión | Highlights |
|---------|------------|
| **v1.0.0** | WSAA + WSFEv1 + WSFEXv1 + Padrón A5 — emisión completa |
| **v1.1.0** | Read-side: WSCDC (validación) + WSCComu (e-Ventanilla / DFE) |
| **v2.0.0** | Refactor modular: sub-namespaces `Core` / `Billing` / `Read` |
| **v2.1.x** | Coverage + polish · fix WSAA para WSCComu (`veconsumerws`) · soporte MTOM |
| **v2.2.0** | Consulta de comprobantes emitidos: `GetLastAuthorizedNumberAsync` + `GetAuthorizedAsync` |
| **v2.3.0** | Cache de tokens cifrado con `IDataProtection` en todas las plataformas |
| **v3.0.0** | `IPadronService` público (Padrón A5 completo) · `VATConditionARCAEnum` con los códigos de condición IVA del receptor · la emisión usa la condición real del Padrón |

---

## ✅ `v2.1.0` — Coverage + polish (entregado)

Sin features nuevos. Foco en pulir la lib post-refactor. Los ítems que quedaron abiertos siguen en **Deuda técnica conocida**, más abajo.

| Item | Detalle | Estimación |
|------|---------|------------|
| Subir cobertura a 75%+ | Cubrir los SOAP-touching paths con mocks de los proxies WCF (no se mockean fácil, requiere `IServiceSoap` factory). El gap está en `LoginTicketService` (~94 LoC), `WsfeOperations.SolicitarCaeAsync` (~84 LoC), `ElectronicMailboxOperations.ListAsync/ConsumeAsync` (~120 LoC) | 12 – 20 hs |
| Limpiar warnings StyleCop preexistentes (`SA1101`, etc.) | Heredado del código pre-refactor. ~400 warnings que no fallan el build pero ensucian el output | 4 – 6 hs |
| Renamespacing de Connected Services (WSAA, WSFEv1, WSFEXv1, Padron, WSCDC, WSCComu) | Hoy quedaron con namespace plano (ej. `WSCDC.ServiceSoapClient`). Re-generar con `--namespace "*,ElRoso.ARCA.Core.Wsaa"` etc. | 3 – 5 hs |
| **Confirmar URL de producción de WSCComu** | Hoy solo tenemos la de homologación (`stable-middleware-tecno-ext.afip.gob.ar`). Pedir a `webservices-desa@arca.gob.ar` cuando un usuario lo necesite | 0 hs (gestión externa) |
| Codecov badge en color verde | Una vez arriba de 75%, el badge pasa de amarillo a verde | 0 hs (automático con la cobertura) |
| **Total `v2.1.0`** | | **19 – 31 hs** |

---

## Próximo — Antifraude + sincronización

| Feature | WS | Por qué |
|---------|----|---------|
| **Padrón de Apócrifos** | `WSAPOC` | Verificar si un emisor de factura recibida está en lista de apócrifos. ORO antifraude para compras |
| **Tablas paramétricas dinámicas** | `ws_sr_padron_a100` | Hidratar dinámicamente `DictionariesCommon` (monedas, alícuotas IVA, otros impuestos) desde ARCA. Sin más releases por cambios de tabla |
| **Códigos de actividad económica** | `ws_sr_padron_a13` | Tabla maestra de actividades AFIP — útil para autocomplete al dar de alta clientes/proveedores |

Estimación: 25 – 40 hs total.

---

## Siguientes — Por demanda comunitaria

Implementación según pedido vía Issues / Discussions:

| Feature | WS | Cuándo |
|---------|----|--------|
| Comprobantes T (turismo / tax-free) | `WSCT` | Si la comunidad lo pide o el Facturador agrega vertical hotelero |
| CAE Anticipado | `WSCAEA` | Para emisores de alto volumen — feature pro |
| Factura con detalle de ítems | `WSMTXCA` | Si hay demanda real. Hoy `WSFEv1` cubre el 99% de casos |
| Padrón A4 detallado | `ws_sr_padron_a4` | Si el `ws_sr_constancia_inscripcion` (A5) actual queda corto |
| F.931 (sueldos) | `TRABAJO_F931` | Cuando alguien quiera levantar un Facturador-RRHH |
| SIRE retenciones | `SIRE` | Para agentes de retención / contadores |

---

## 🔴 Bloqueado por falta de WS oficial

Estos features tienen demanda pero ARCA no expone API SOAP/REST oficial. Si aparece, se reactivan.

| Feature | Razón del bloqueo |
|---------|-------------------|
| **Mis Comprobantes Recibidos (listado completo)** | ARCA no expone WS para **listar** compras de un CUIT. Las soluciones comerciales (AfipSDK, TusFacturasApp) scrapean el portal — fuera del scope de esta lib (rompería la filosofía "SOAP oficial only"). El WSCDC actual valida UN comprobante a la vez |
| **Libro IVA Digital — presentación automática** | RG 4597. Hoy solo se opera vía portal manualmente. Si ARCA expone WS para presentación, sumarlo |
| **Consulta de notificaciones de Cuentas Tributarias** | Sin WS oficial conocido |

Si encontrás un WS oficial que cubra alguno de estos, **abrí un [Issue](https://github.com/edirosolini/ElRoso.ARCA/issues/new/choose)** con la URL del WSDL y los reactivamos.

---

## Futuro lejano (posible breaking)

- **Paquete `ElRoso.ARCA.Pro` privado** — modelo Open Core: features avanzados pagos (ej. integración con padrones IIBB provinciales — ARBA, AGIP, etc., que no son ARCA pero son la misma audiencia).
- **Soporte multi-cert por proceso** — hoy el cert se setea en `ARCAOptions` global. Para SaaS multi-tenant donde cada empresa tiene su cert, refactorear `ICertificateService` para resolverlo por contexto.
- **Renombre interno de Connected Services** — `WSCDC` → `ElRoso.ARCA.Read.Wscdc.Soap`, etc. (cosmetic).

---

## ❌ Servicios ARCA que NO se implementarán

Por estar fuera del scope (sector específico, aduanas, beneficios sectoriales):

`WSLPG`, `WSLSP`, `WSLCA`, `WSLTV`, `WSLUM`, `WSREMHARINA`, `WSREMAZUCAR`, `WSREMCARNE`, `WSBFE`, `WSCTA`, `WSCREATEVEP`, `WSSEG`, `WSCPE`, `WSCES`, `WSSV`, `WdiaUtiDEs`, `WGESINV`, `wgestabref`, `wConsDepFiel`, `wgestiendaslibres`, `DigDepFiel`, `WutiGOPDeclaraciones`, `wdepmovimientos`, `wEnysa`, `sud_restricciones`, `sud_contrataciones`, `wscec`, `JAZA`, `Régimen Percepción IVA`, `WSPresentaciondeDDJJ`, `AGR`, `WSTABACO`, `WSICDB`.

Si tu caso de uso requiere alguno de estos, **abrí una [Discussion](https://github.com/edirosolini/ElRoso.ARCA/discussions/new?category=ideas)** explicando el contexto. Lo revaluamos.

---

## 🛠 Deuda técnica conocida

- **`FECompConsultar` no devuelve importes** — el proxy generado expone `FECompConsResponse` con `Resultado`, `CodAutorizacion`, `FchVto`, `FchProceso`, `PtoVta`, `CbteTipo` y `Observaciones`, **sin `ImpTotal` ni `CbteFch`**. Para reconciliar un CAE alcanza (el importe lo tiene el propio consumidor), pero no permite validarlo contra ARCA. Regenerar el Connected Service y verificar si el WSDL real los trae.
- **Wrappers SOAP sin cobertura unitaria** — `WsfeOperations`, `WsfexOperations` y `PadronOperations` no se testean sin extraer un factory para los clientes WCF. Afecta por igual a `SolicitarCaeAsync`, `GetLastNumberAsync`, `ConsultarComprobanteAsync` y `PadronOperations.GetPersonaAsync` (el mapeo a `PadronPersonaResponse` sí está cubierto). (El puente de cancelación y el cierre del canal sí están cubiertos, vía `SoapInvoker`.)
- **400+ warnings de StyleCop** (`SA1101`, `SA1503`, `SA1407`) heredados. No fallan el build (`<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`) pero ensucian el output.
- **`tests` con duplicate `using` warnings (CS0105)** post-refactor v2.0.0. El bulk replace generó múltiples `using` por archivo y el dedup no fue 100% perfecto.
- **`FileTokenCacheTests.Dispose()` warning CA1816** — agregar `GC.SuppressFinalize(this)`.
- **`InvoiceVerificationOperations` constructor sin tests directos** — cubierto indirectamente por `OperationsMappingTests` pero no por test específico de la clase.
- **Connected Services regenerables** — documentar en CLAUDE.md del repo cómo regenerar cada uno con `dotnet-svcutil`. Comandos:
  ```bash
  dotnet dotnet-svcutil "https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL" --outputDir "ARCA/Billing/Connected Services/WSFEv1" --outputFile "Reference.cs" --namespace "*,WSFEv1" --targetFramework "net9.0" --internal
  dotnet dotnet-svcutil "https://wswhomo.afip.gov.ar/WSCDC/service.asmx?WSDL" --outputDir "ARCA/Read/Connected Services/WSCDC" --outputFile "Reference.cs" --namespace "*,WSCDC" --targetFramework "net9.0" --internal
  ```

---

## Cómo proponer features nuevos

1. Verificar primero que el WS de ARCA existe en https://www.afip.gob.ar/ws/documentacion/catalogo.asp
2. Abrir una [Discussion](https://github.com/edirosolini/ElRoso.ARCA/discussions/new?category=ideas) describiendo el caso de uso real
3. Si hay alineación → abrir un Issue para tracking

**Lo que NO se va a implementar nunca:**

- ❌ Scraping del portal ARCA (rompe la filosofía "SOAP oficial only" y depende del frontend ajeno)
- ❌ Bypass de WSAA o usos no autorizados de los WS
- ❌ Features que requieran credenciales fuera del estándar (cert X.509 + delegación)
