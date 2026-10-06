# Changelog

Todos los cambios notables a este proyecto se documentan acá.

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y este proyecto adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [Unreleased]

## [3.0.1] — 2026-10-06

### Fixed

- **El Padrón ya no deduce Sujeto No Categorizado.** Una persona activa sin monotributo ni impuestos 20, 30 o 32 salía como `SUJETO_NO_CATEGORIZADO`, y la emisión mandaba condición 7 aunque el consumidor hubiera puesto otra. Ahora, si el Padrón no determina la condición, `VATCondition` y `ReceiverVATConditionId` quedan en `null` y la emisión conserva la que puso el consumidor con `ClientRequest.SetCondition(...)`. `SUJETO_NO_CATEGORIZADO` sigue en el enum para setearlo a mano.
- **Los impuestos y caracterizaciones del Padrón llegaban sin id.** ARCA agregó `estadoImpuesto` y `motivo` al impuesto y `fechaSolicitud` a la caracterización; el proxy no los tenía y el deserializador descartaba en silencio todo lo que venía después (`idImpuesto`, `idCaracterizacion`, `periodo`). Un Responsable Inscripto se leía sin el impuesto 30. El proxy se actualizó con el WSDL vigente.

## [3.0.0] — 2026-10-06

✨ **Consulta pública al Padrón A5** y **la emisión usa la condición frente al IVA real del receptor**.

### Breaking changes

- **`VATConditionARCAEnum` pasa a valer el código de condición IVA del receptor de ARCA** (`FEParamGetCondicionIvaReceptor`):

  | Miembro | 2.x | 3.0.0 |
  |---|---|---|
  | `RESPONSABLE_INSCRIPTO` | 7 | **1** |
  | `IVA_SUJETO_EXENTO` | — | 4 |
  | `CONSUMIDOR_FINAL` | 5 | 5 |
  | `MONOTRIBUTO` | 6 | 6 |
  | `SUJETO_NO_CATEGORIZADO` | — | **7** |
  | `PROVEEDOR_DEL_EXTERIOR` | — | 8 |
  | `CLIENTE_DEL_EXTERIOR` | — | 9 |
  | `IVA_LIBERADO_LEY_19640` | — | 10 |
  | `MONOTRIBUTISTA_SOCIAL` | — | 13 |
  | `IVA_NO_ALCANZADO` | — | 15 |
  | `MONOTRIBUTO_TRABAJADOR_INDEPENDIENTE_PROMOVIDO` | — | 16 |

  ⚠️ **Si persistís el enum como entero, migrá tus datos `7 → 1` ANTES de leerlos con 3.0.0.** Sin la migración, un Responsable Inscripto guardado se lee como `SUJETO_NO_CATEGORIZADO`. Si lo persistís por nombre (incluido el JSON con el `StringEnumConverter` del enum) no cambia nada. Afecta también a `IssuingCompanyRequest.VATCondition`, que usa el mismo enum.

- **La emisión a un CUIT ya no asume Responsable Inscripto.** `AuthorizeAsync` resolvía la condición del receptor como `Monotributo` o `Responsable Inscripto`, así que un exento o un no categorizado salía como RI. Ahora usa la condición que deriva el Padrón (ver abajo). Si el Padrón no permite determinarla, **se conserva la que puso el consumidor** con `ClientRequest.SetCondition(...)`.

- **`CondicionIVAReceptorId` en WSFEv1 es el valor del enum.** Una condición no definida en el enum (por ejemplo, un `ClientRequest` a CUIT sin condición y con un Padrón que no la determina) tira `ARCAValidationException` antes de pedir el CAE, en vez de mandar Consumidor Final en silencio.

### Added

- `IPadronService.GetPersonaAsync(representedCuit, cuit, ct)` → `PadronPersonaResponse`. Pide el ticket de `ws_sr_constancia_inscripcion` del CUIT representado con la misma caché que el resto de la lib. Registrado como Singleton en `AddARCAClient`.
- `PadronPersonaResponse`: identidad (tipo de persona, clave y su estado, apellido, nombre, razón social, `DisplayName`), sucesión, fechas de contrato social y fallecimiento, mes de cierre, domicilio fiscal completo, dependencia de ARCA, caracterizaciones, impuestos, actividades (con `MainActivity` = orden 1), regímenes, categoría de autónomos y todo el bloque de monotributo (categoría, actividad, actividades, impuestos e integrantes de la sociedad). Los campos que el proxy marca con `*Specified` salen como `null` cuando ARCA no los informa.
- `Found = false` cuando ARCA no tiene una persona con ese CUIT: no tira excepción. `Errors` trae `errorConstancia` (o el mensaje del fault de ARCA para el CUIT inexistente) y `PartialErrors` los errores del régimen general y del monotributo con su mensaje. Cualquier otra falla del servicio sale como `ARCAServiceException`.
- `VATCondition` y `ReceiverVATConditionId`: monotributo (bloque de monotributo o impuesto 20) → `MONOTRIBUTO`; impuesto 30 → `RESPONSABLE_INSCRIPTO`; impuesto 32 → `IVA_SUJETO_EXENTO`; persona activa, sin errores parciales y sin ninguno de esos impuestos → `SUJETO_NO_CATEGORIZADO`. Si aparece el impuesto 34 (en régimen general o monotributo) queda en `null`, igual que cualquier otro caso no cubierto; la lista cruda de impuestos viene siempre. La emisión usa esta misma derivación.

### Notes

- La letra del comprobante (A/B/C) la sigue eligiendo el consumidor en `BillingDocumentType`.
- A un receptor sin CUIT la emisión le sigue fijando `CONSUMIDOR_FINAL`.

### Migration guide (2.3.0 → 3.0.0)

1. **Antes de desplegar 3.0.0**, si guardás `VATConditionARCAEnum` como entero (columna, caché, cola), pasá los `7` a `1`. Ejemplo en SQL:

   ```sql
   UPDATE Clients SET VATCondition = 1 WHERE VATCondition = 7;
   ```

   Hacelo una sola vez: después del upgrade, `7` es `SUJETO_NO_CATEGORIZADO` y es un valor válido.
2. Reemplazá cualquier comparación contra literales (`(int)condition == 7`) por el miembro del enum.
3. Revisá los `switch` sobre `VATConditionARCAEnum`: ahora hay ocho miembros más y un `default` que asumía RI o Consumidor Final puede dejar de ser cierto.
4. Si emitís a un CUIT, poné una condición de respaldo con `request.Client.SetCondition(...)`: se usa cuando el Padrón no la determina (por ejemplo, con el impuesto 34). Sin ella, `AuthorizeAsync` tira `ARCAValidationException`.
5. Atrapá `ARCAValidationException` en la emisión si todavía no lo hacías.
6. Para consultar un CUIT sin emitir, pedí `IPadronService` al contenedor; `AddARCAClient` ya lo registra.

## [2.3.0] — 2026-09-07

### Security

- **El login ticket de WSAA ahora se cifra en reposo en todas las plataformas.** `FileTokenCache` cifraba con **DPAPI**, que solo existe en Windows: en Linux y macOS el archivo `.bin` era JSON legible con el `Token` y el `Sign` de ARCA en claro — la credencial que autoriza a facturar durante 12 h. El payload pasa a protegerse con `IDataProtector` (purpose `ElRoso.ARCA.TokenCache`), que es multiplataforma.

  - `AddARCAClient` registra `services.AddDataProtection()`. Todo lo que registra ese método es `TryAdd`, así que **si tu app ya configuró su propio key ring, ese gana** y no hace falta tocar nada.
  - ⚠️ Si persistís `TokenCacheDirectory` en un volumen, **persistí también el key ring de DataProtection**. Sin él los archivos no se pueden descifrar y cada arranque pide un TA nuevo.
  - **No hace falta migración:** un archivo del formato anterior no se puede desproteger, se descarta como cache miss y se pide un ticket nuevo.
  - Se saca la dependencia `System.Security.Cryptography.ProtectedData` (ya no se usa) y entra `Microsoft.AspNetCore.DataProtection`.

### Fixed

- **Las operaciones SOAP respetan el `CancellationToken` que reciben y ya no filtran el canal WCF.** El token viajaba por toda la superficie asíncrona del paquete y no se usaba en ninguna llamada; además, cada operación construía un `ClientBase<T>` por llamada y nunca lo cerraba.

  - Nuevo `SoapInvoker` interno: puentea el token hacia los proxies generados (que no lo reciben), y cierra el canal siempre — `Close()` en el camino feliz, `Abort()` ante error, cancelación o `Close()` fallido. Aplicado a WSAA, WSFEv1, WSFEXv1, Padrón A5, WSCDC y WSCComu.
  - Cancelar antes de empezar ni siquiera abre el socket.
  - La cancelación sale como `OperationCanceledException`; antes quedaba envuelta en `ARCAServiceException` / `ARCAAuthException` y era indistinguible de una falla de ARCA.
  - ⚠️ **Cancelar un pedido de CAE no cancela lo que ARCA ya hizo.** Abortar el canal corta la espera local, no la autorización remota. Por eso `SolicitarCaeAsync` y `FEXAuthorize` loguean un warning con CUIT, tipo, punto de venta y número del comprobante en vuelo: es lo que necesita la reconciliación para saber dónde mirar antes de re-emitir.

- **La validez del login ticket ya no depende del `TZ` del proceso.** El vencimiento que devuelve WSAA se parseaba con `DateTime.Parse` sin `DateTimeStyles`, que pliega el offset a la hora local de la máquina y marca `Kind = Local`; del otro lado, `FileTokenCache` lo comparaba contra un `DateTime.UtcNow.AddHours(-3)` escrito a mano. Las dos mitades coincidían solo mientras el contenedor corriera en hora argentina.

  - `LoginTicketResponse.ExpirationTime` es ahora siempre un instante **UTC** (`Kind = Utc`), venga el XML con offset, con `Z` o sin nada (sin offset se interpreta como hora argentina, que es lo que manda WSAA).
  - `FileTokenCache` compara contra `DateTime.UtcNow`. No queda ningún `-3` en el camino de expiración.
  - El `generationTime` / `expirationTime` del `loginTicketRequest` se arma con la zona resuelta por nombre (`America/Argentina/Buenos_Aires`) en vez de un `-3` fijo, así un cambio de horario de verano no lo rompe. Si el host no tiene base de zonas horarias, cae a un UTC-3 fijo.
- **El `uniqueId` del `loginTicketRequest` ya no reinicia con el proceso.** Salía de un `Interlocked.Increment` sobre un `static int` que arrancaba en 0, así que después de cada deploy el primer pedido a WSAA volvía a mandar `uniqueId=1` — WSAA lo espera monótono creciente por (CUIT, servicio) y eso puede activar su control anti-replay. Ahora se deriva de los segundos unix, con un piso que garantiza que no se repita dentro del mismo segundo ni retroceda si el reloj del host salta hacia atrás. No persiste estado.

  - ℹ️ Lo que **sí** conviene persistir es `TokenCacheDirectory`: el TA vive 12 h y WSAA no emite un segundo TA mientras el primero siga vigente. Perder la caché en un redeploy no es gratis. Ver [Cookbook → cache de tokens](./docs/COOKBOOK.md#cache-de-tokens).

  - ⚠️ **Los archivos de caché cambian de nombre a `ARCA_token_v2_*.bin`.** Los del formato anterior guardan el vencimiento en hora local: se ignoran en vez de leerse mal. Se pueden borrar.

## [2.2.0] — 2026-07-23

✨ **Consulta de comprobantes ya autorizados** — `IBillingDocumentNumberingService` suma dos operaciones de lectura sobre WSFEv1 para poder **reconciliar** un comprobante que ARCA autorizó pero el consumidor nunca llegó a persistir.

### Added

- `IBillingDocumentNumberingService.GetLastAuthorizedNumberAsync(...)` — último número autorizado para un tipo + punto de venta. Envuelve `FECompUltimoAutorizado`, que ya se usaba internamente para numerar y hasta ahora no era accesible desde afuera.
- `IBillingDocumentNumberingService.GetAuthorizedAsync(...)` — trae un comprobante autorizado con su **CAE**, vencimiento y fecha de proceso. Envuelve `FECompConsultar`, que estaba en el proxy generado pero sin usar.
- `AuthorizedBillingDocumentResponse` — POCO de respuesta, sin tipos WCF a la vista.

### Why

ARCA otorga el CAE **antes** de que el consumidor pueda persistirlo. Si el guardado local falla en esa ventana, el comprobante queda autorizado en ARCA y ausente en la base; el reintento natural —volver a autorizar— emite un **segundo CAE para la misma venta**, que es un duplicado fiscal real y hay que anularlo con nota de crédito.

Sin estas operaciones no había forma de distinguir "esto nunca se emitió" de "esto se emitió y no lo guardé". Ahora el flujo correcto ante un fallo de persistencia es:

1. `GetLastAuthorizedNumberAsync` → ¿ARCA está más adelante que mi último número guardado?
2. Si lo está, `GetAuthorizedAsync` → recuperás el CAE y lo reconciliás localmente, sin re-emitir.

### Notes

- **No es breaking.** Solo agrega miembros a la interfaz; los implementadores propios de `IBillingDocumentNumberingService` (poco probables, la implementación es `internal`) tendrían que agregarlos.
- ⚠️ **`FECompConsultar` no devuelve importes en el proxy actual.** El WSDL generado expone `Resultado`, `CodAutorizacion`, `FchVto`, `FchProceso`, `PtoVta`, `CbteTipo` y `Observaciones` — sin `ImpTotal`. Para reconciliar alcanza (el importe ya lo tenés en tu propio comprobante), pero si necesitás validarlo contra ARCA hay que regenerar el Connected Service. Anotado como deuda en el ROADMAP.
- **Cobertura:** el wrapper SOAP nuevo queda sin tests unitarios, igual que `SolicitarCaeAsync` y `GetLastNumberAsync` — los clientes WCF no se mockean sin extraer un factory (deuda ya listada en el ROADMAP). Sí están cubiertos el service, el mapeo de la respuesta y el parser de fechas.

## [2.1.2] — 2026-05-14

🐛 **Fix: WSCComu cliente SOAP ahora soporta MTOM** — el endpoint de producción siempre responde con `multipart/related; type="application/xop+xml"` (MTOM), incluso cuando no hay adjuntos. El binding generado por defecto solo aceptaba `application/soap+xml` plano y explotaba con `ProtocolException` al parsear cualquier respuesta exitosa.

### Fixed

- `ElectronicMailboxOperations.CreateClient()` ahora crea el `VEConsumerClient` con un `BasicHttpBinding` custom:
  - `MessageEncoding = WSMessageEncoding.Mtom`
  - `MaxReceivedMessageSize = int.MaxValue` (para no romper con adjuntos grandes en `ConsumeAsync`)
  - `Security.Mode = Transport` (HTTPS)
  - Timeouts derivados de `ARCAOptions.SoapTimeoutSeconds`.

### Notes

Bug puro, sin migración necesaria. Si los calls a `ListAsync` o `ConsumeAsync` te devolvían:

```
System.ServiceModel.ProtocolException: The content type multipart/related; type="application/xop+xml"
  of the response message does not match the content type of the binding (application/soap+xml; charset=utf-8)
```

…upgradeá a 2.1.2 directo y desaparece. WSCDC, WSFEv1, WSFEXv1, Padron no usan MTOM, así que no necesitan cambios.

## [2.1.1] — 2026-05-14

🐛 **Fix: WSAA service identifier para WSCComu** — la lib estaba mandando `"wsccomu"` y WSAA respondía `"Servicio informado inexistente"`. El identificador correcto es `"veconsumerws"` (el namespace del Connected Service, no la abreviación marketinera). Sin este fix, **ningún call a WSCComu funcionaba en producción**.

### Fixed

- `ElectronicMailboxService.WsccomuServiceName`: `"wsccomu"` → `"veconsumerws"`.

### Notes

Sin migración necesaria — bug puro. Cualquiera que tuviera la integración rota con `ARCAAuthException → FaultException: Servicio informado inexistente` debería upgradear directo.

## [2.1.0] — 2026-05-14

✨ **`WsccomuUrl` ahora se resuelve por `IsProduction`** — el último WS que requería configuración manual ahora sigue la misma convención que el resto.

### Changed

- `ARCAOptions.WsccomuUrl` se calcula automáticamente desde `IsProduction`:
  - `IsProduction=false` → `https://stable-middleware-tecno-ext.afip.gob.ar/ve-ws/services/veconsumer`
  - `IsProduction=true`  → `https://infraestructura.afip.gob.ar/ve-ws/services/veconsumer`
- La property sigue siendo `public settable` — si la seteás manualmente tu override gana (útil si ARCA migra la URL o si proxyás a través de tu propio middleware). Setearla a `null`/string vacío vuelve al default por ambiente.

### Why

ARCA mantiene la URL de producción de WSCComu fuera de la documentación pública (a diferencia de los demás WS). En `v2.0.0` la dejamos como property settable con default a homologación, lo cual rompía la simetría: cuando el consumidor seteaba `IsProduction=true`, **todos** los WS se iban a producción excepto WSCComu, que quedaba apuntando a homologación → mismatch de cert → `ARCAAuthException` engañoso que parecía falta de delegación pero era URL incorrecta.

### Migration

Sin cambios para la mayoría — la behavior change resuelve un bug, no introduce uno. Si tu código tenía `options.WsccomuUrl = "<prod-url>"` para producción, **podés eliminar esa línea** — la lib la resuelve sola. Si lo dejás, sigue funcionando como override.

### Added

- 3 tests nuevos para `WsccomuUrl`: switch por ambiente, override respetado, fallback al default cuando el override se limpia.

## [2.0.0] — 2026-05-14

🧱 **Modular refactor** — la API pública se separa en tres sub-namespaces por área funcional. **Breaking change** en el namespace de cada tipo. La funcionalidad es idéntica.

### Breaking changes

Los tipos públicos se reorganizaron en tres sub-namespaces. **El comportamiento es idéntico** — solo cambia el `using`.

| Antes | Ahora |
|-------|-------|
| `ElRoso.ARCA.Caching.*` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Commons.*` | `ElRoso.ARCA.Billing` |
| `ElRoso.ARCA.Exceptions.*` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Options.*` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Domains.Enums.DocumentTypeARCAEnum` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Domains.Enums.VATConditionARCAEnum` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Domains.Enums.BillingDocumentTypeARCAEnum` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Domains.Enums.ConceptTypeARCAEnum` | `ElRoso.ARCA.Billing` |
| `ElRoso.ARCA.Domains.Enums.AuthorizationModeARCAEnum` | `ElRoso.ARCA.Read` |
| `ElRoso.ARCA.Domains.Requests.{Issuing,Client}Request` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Domains.Requests.BillingDocument*` | `ElRoso.ARCA.Billing` |
| `ElRoso.ARCA.Domains.Requests.ItemRequest` | `ElRoso.ARCA.Billing` |
| `ElRoso.ARCA.Domains.Requests.InvoiceVerification*` | `ElRoso.ARCA.Read` |
| `ElRoso.ARCA.Domains.Requests.Mailbox*` | `ElRoso.ARCA.Read` |
| `ElRoso.ARCA.Domains.Responses.LoginTicketResponse` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Domains.Responses.BillingDocumentNumberingResponse` | `ElRoso.ARCA.Billing` |
| `ElRoso.ARCA.Domains.Responses.InvoiceVerificationResponse` | `ElRoso.ARCA.Read` |
| `ElRoso.ARCA.Domains.Responses.Mailbox*` | `ElRoso.ARCA.Read` |
| `ElRoso.ARCA.Domains.Services.I{Certificate,LoginTicket}Service` | `ElRoso.ARCA.Core` |
| `ElRoso.ARCA.Domains.Services.IBillingDocumentNumberingService` | `ElRoso.ARCA.Billing` |
| `ElRoso.ARCA.Domains.Services.I{InvoiceVerification,ElectronicMailbox}Service` | `ElRoso.ARCA.Read` |
| `ElRoso.ARCA.DependencyInjection.AddARCAClient()` | `ElRoso.ARCA.AddARCAClient()` |

### Migration guide

Reemplazá los `using` viejos con los tres nuevos buckets:

```diff
-using ElRoso.ARCA.DependencyInjection;
-using ElRoso.ARCA.Domains.Enums;
-using ElRoso.ARCA.Domains.Requests;
-using ElRoso.ARCA.Domains.Responses;
-using ElRoso.ARCA.Domains.Services;
-using ElRoso.ARCA.Exceptions;
-using ElRoso.ARCA.Options;
+using ElRoso.ARCA;          // AddARCAClient
+using ElRoso.ARCA.Core;     // ARCAOptions, exceptions, IssuingCompany, Client, document types
+using ElRoso.ARCA.Billing;  // IBillingDocumentNumberingService, BillingDocumentNumberingRequest, etc.
+using ElRoso.ARCA.Read;     // IInvoiceVerificationService, IElectronicMailboxService, etc.
```

Find/replace cheatsheet (regex):

```
\busing ElRoso\.ARCA\.(Caching|Exceptions|Options|Domains\.(Enums|Requests|Responses|Services))\b
→  using ElRoso.ARCA.Core;
   using ElRoso.ARCA.Billing;
   using ElRoso.ARCA.Read;
   (luego borrar los duplicados)
```

### Added

- Sub-namespaces `ElRoso.ARCA.Core`, `ElRoso.ARCA.Billing`, `ElRoso.ARCA.Read` con todos los tipos públicos reorganizados por área funcional.
- 21 tests nuevos para las mapping helpers internas (`BuildFilter`, `MapSummary`, `MapMessage`, `BuildCmpDatos`, etc.).
- Estructura de carpetas alineada con los namespaces: `ARCA/{Core,Billing,Read}/...`.

### Changed

- Cobertura subió **54.91% → 64.08%** (197/197 tests pasando).
- `MIN_COVERAGE` del CI sigue en 55 con margen amplio.
- Helpers de mapping en `Soap` operations cambiaron de `private static` a `internal static` para habilitar unit testing directo.

### Internal

- Connected Services namespaces (WSAA, WSFEv1, WSFEXv1, Padron, WSCDC, WSCComu) sin cambios — son tipos internos generados que no afectan la API pública.
- Las interfaces `IPadronOperations`, `IWsfeOperations`, `IWsfexOperations` (Billing) y `IInvoiceVerificationOperations`, `IElectronicMailboxOperations` (Read) siguen siendo internal — el consumidor no las ve.

## [1.1.0] — 2026-05-14

🎉 **Read-side de ARCA** — features que la comunidad pidió desde el día uno.

### Added

- **WSCDC (Constatación de Comprobantes)** — `IInvoiceVerificationService.VerifyAsync()` valida que un comprobante recibido (CAE/CAI/CAEA) es auténtico contra ARCA. Caso de uso clave: cargar facturas de proveedores con confianza.
- **WSCComu (e-Ventanilla / DFE)** — `IElectronicMailboxService.ListAsync()` y `ConsumeAsync()`. Leé el Domicilio Fiscal Electrónico desde código en lugar de loguearte al portal. Evitá notificaciones tácitas a los 5 días hábiles.
- Nuevo enum `AuthorizationModeARCAEnum` (CAE / CAI / CAEA) para WSCDC.
- Nuevos DTOs públicos: `InvoiceVerificationRequest`, `InvoiceVerificationResponse`, `MailboxQueryRequest`, `MailboxQueryResponse`, `MailboxMessageSummary`, `MailboxConsumeRequest`, `MailboxConsumeResponse`, `MailboxMessage`, `MailboxAttachment`.
- Connected Services nuevos (`WSCDC`, `WSCComu`) bajo `ARCA/Connected Services/`. Generados con `dotnet-svcutil` — regeneración documentada en CLAUDE.md del repo.
- Nuevos validators FluentValidation: `InvoiceVerificationValidator`.
- Nuevos wrappers SOAP internos: `IInvoiceVerificationOperations`, `IElectronicMailboxOperations`.
- Samples runnable: `samples/InvoiceVerification/`, `samples/ElectronicMailbox/`.
- Secciones nuevas en README + Cookbook con pitfalls específicos de cada WS.
- 25 tests nuevos (total: 176/176 pasando).

### Changed

- `ARCAOptions.WsccomuUrl` es propiedad `public` settable (a diferencia de `WsfeUrl`/`WsfexUrl` que son internal computed). Razón: ARCA no publica URL de producción del WSCComu — el usuario debe configurarla.
- `dotnet-svcutil` agregado como local tool (`dotnet-tools.json`) para regenerar proxies SOAP.

### Notas

- **Mis Comprobantes Recibidos (listado)** — sigue sin WS oficial de ARCA. Documentado en [ROADMAP](./docs/ROADMAP.md).
- **Libro IVA Digital** — sin WS oficial actualmente (solo portal). Documentado en ROADMAP.

## [1.0.0] — 2026-05-14

Primer release estable (GA). API pública estable bajo SemVer — cambios breaking solo en `v2.x`.

### Cambios desde `1.0.0-preview.1`

#### Added

- **Cobertura de tests subió de 25% → 60%+** (151 tests, +56 desde preview.1)
- Nueva capa de wrappers SOAP (`IPadronOperations`, `IWsfeOperations`, `IWsfexOperations`) en `Services.Soap` — separa la lógica de orquestación del transporte SOAP, habilita unit-testing del service principal con mocks
- Tests del happy path doméstico (con CUIT/Padrón y SIN_IDENTIFICAR), happy path exportación, ramas de error de WSFEv1/WSFEXv1, errores de Padrón, token cache miss → WSAA refresh
- Tests de `FileTokenCache` (roundtrip, expiración, isolation por servicio/empresa, concurrencia)
- Tests de `CertificateService` (load PFX, cache, firma PKCS#7, errores)
- Tests de `BillingDocumentNumberingResponse.QRCode()` (decode base64 + verificación del schema AFIP completo)
- Tests del registro de DI vía `AddARCAClient()` (lifetimes, opciones, fluent chaining)
- Integración con Codecov: badge en el README, gate en CI (mínimo 55%), comentarios automáticos en PRs

#### Changed

- **Refactor interno de `BillingDocumentNumberingService`** — ahora delega a los tres wrappers SOAP en vez de construir clientes WCF inline. **API pública sin cambios.**
- `MIN_COVERAGE` del CI subido de 25 a 55 (margen de 5 pp bajo el real)
- `dependabot.yml` agrupa Microsoft.Extensions.*, System.ServiceModel.*, xunit y test-tooling — minor/patch agrupados, major requiere review individual
- `dependabot.yml` bloquea bumps que rompen: FluentAssertions v8+ (licencia comercial Xceed), System.ServiceModel.* major (versiones inconsistentes 6.x/8.x), StyleCop.Analyzers v2.x

## [1.0.0-preview.1] — 2026-05-13

Primer release público en NuGet.org. ¡Bienvenidos! 🇦🇷

### Funcionalidad de la librería

- Cliente .NET 9 para facturación electrónica con ARCA (ex-AFIP)
- Autenticación **WSAA** completa: firma PKCS#7, NTP cacheado, cache de tokens en disco cifrado con DPAPI (Windows) / JSON plano (Linux/macOS)
- Emisión de comprobantes **domésticos (WSFEv1)**: FA, FB, FC + Notas de Débito y Crédito A/B/C
- Emisión de comprobantes de **exportación (WSFEXv1)**: Factura, Nota de Débito, Nota de Crédito
- Resolución automática del cliente vía **Padrón A5** (ws_sr_constancia_inscripcion)
- Generación de **URL del QR** oficial de ARCA para imprimir en el comprobante
- Excepciones tipadas: `ARCAAuthException`, `ARCAServiceException`, `ARCAValidationException`
- Configuración via `ARCAOptions` con switch homologación / producción
- Extensión `AddARCAClient()` para DI con Microsoft.Extensions.DependencyInjection
- Validación FluentValidation completa de los inputs
- Soporte para Concepto Productos / Servicios / Otros
- Soporte para impuestos múltiples (IVA + otros impuestos: nacionales, provinciales, municipales, internos)
- Soporte para múltiples monedas (Pesos, Dolares, Euro)

### Infraestructura y publicación

- Suite de tests xUnit + Moq + FluentAssertions 7.x + Coverlet
- Cobertura inicial 25% (Validators, Dictionaries, Options, DTOs, Exceptions al 100%)
- Workflow `ci.yml`: build + test + coverage gate ≥ 25% + verify NuGet pack
- Workflow `release.yml`: publica `.nupkg` + `.snupkg` a NuGet.org en tag SemVer y crea GitHub Release con notas auto-generadas
- Dependabot configurado con grouping minor/patch + bloqueos para FluentAssertions v8+ (licencia comercial) y bumps majors riesgosos
- `LICENSE` MIT
- `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md` (vulnerability reporting)
- README profesional con badges + ToC + recetario por escenario
- `docs/COOKBOOK.md` con pitfalls comunes de ARCA (certificados, WSAA, cache de tokens, códigos de error, Docker, concurrencia)
- Sample runnable `samples/QuickStart` que emite una FA en homologación
- SourceLink habilitado — debug into source de la lib desde el consumidor
- Symbols `.snupkg` publicados junto al `.nupkg`
- README packaged dentro del nupkg (se muestra en NuGet.org)
- Sponsors: GitHub Sponsors + [Cafecito](https://cafecito.app/edirosolini)

### Seguridad

- Reset del git history para purgar claves privadas que habían quedado en commits iniciales. Si tenés un fork anterior a este release, **borralo y volvé a clonar**.
