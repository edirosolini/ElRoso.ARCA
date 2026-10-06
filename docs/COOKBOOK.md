# Cookbook — Pitfalls y soluciones

Cosas que en su momento me hicieron perder horas. Acá las anoto para que vos no las sufras.

> 💡 Si encontrás un pitfall que no está acá, abrí un PR. La idea es que este cookbook crezca con la comunidad.

---

## Tabla de contenidos

- [Certificados](#certificados)
- [Autenticación WSAA](#autenticación-wsaa)
- [Cache de tokens](#cache-de-tokens)
- [Cliente y Padrón](#cliente-y-padrón)
- [Códigos de error ARCA típicos](#códigos-de-error-arca-típicos)
- [Notas de Crédito y Débito](#notas-de-crédito-y-débito)
- [Factura de Exportación](#factura-de-exportación)
- [WSCDC — Verificación de comprobantes recibidos](#wscdc--verificación-de-comprobantes-recibidos)
- [WSCComu — DFE / e-Ventanilla](#wsccomu--dfe--e-ventanilla)
- [Docker y deploy](#docker-y-deploy)
- [Concurrencia y reintentos](#concurrencia-y-reintentos)

---

## Certificados

### "The certificate's CN must match the CUIT"

Cuando generás el CSR, el `CN` (Common Name) debe ser el CUIT exacto del emisor, sin guiones:

```bash
openssl req -new -key empresa.key \
  -subj "/C=AR/O=Mi Empresa/CN=20123456789/serialNumber=CUIT 20123456789" \
  -out empresa.csr
```

Si lo subiste mal al portal de ARCA, **revocalo y generá uno nuevo** — no se edita.

### "El mismo certificado funciona en homologación y producción"

**No.** Son dos certificados distintos, firmados por dos CA distintas. Subí dos CSRs separados:

- Homologación: portal con CUIT en ambiente "Testing"
- Producción: portal con CUIT en ambiente "Producción"

### "¿Puedo poner el .pfx en variables de entorno?"

Mejor montalo como archivo (volume en Docker, secret en Kubernetes). Si vas por env var, codificalo a base64:

```bash
ARCA_CERT_BASE64=$(base64 -w0 empresa.pfx)
```

Y al arrancar la app:

```csharp
var pfxBytes = Convert.FromBase64String(Environment.GetEnvironmentVariable("ARCA_CERT_BASE64")!);
var tempPath = Path.Combine(Path.GetTempPath(), "arca.pfx");
File.WriteAllBytes(tempPath, pfxBytes);
options.CertificatePath = tempPath;
```

### "El certificado se vence"

ARCA emite certificados con vencimiento de 2 años. Marcalo en el calendario. Cuando renueves:

1. Generá nuevo CSR
2. Subilo al portal
3. Descargá el nuevo `.crt`
4. Armá el nuevo `.pfx`
5. Reemplazá el archivo (rolling deploy)
6. La lib invalida el cache de tokens automáticamente porque la huella del cert cambia

---

## Autenticación WSAA

### "Error 0: Computador no autorizado a acceder al servicio"

Tu CUIT no tiene habilitado el servicio en cuestión. En el portal:

- **Administrador de Relaciones de Clave Fiscal → Adherir Servicio**
- Buscar: `Facturación Electrónica`, `wsfe`, `ws_sr_constancia_inscripcion` (Padrón A5), etc.
- Vincular con el certificado (CSR uploadeado)

### "The TA token is invalid / expired"

Causas comunes:

1. **Tu reloj está desincronizado.** WSAA es estricto con el campo `GenerationTime`. La lib usa NTP cacheado, pero si NTP falla la lib cae a `DateTime.UtcNow`. Si tu server tiene drift > 5min, esto explota.
2. **Estás usando el TA de homologación contra producción.** Cada ambiente tiene su propio TA. La lib usa archivos separados (`ARCA_token_v2_wsfe_20123456789.bin`).
3. **El TA expiró mientras lo usabas.** Tiene ~12 horas de vida. La lib refresca automáticamente, pero si tu request es exactamente en el borde…

### "PKCS#7 signing failed"

Si el cert se cargó bien pero falla la firma:

- Verificá que el `.pfx` tenga clave privada exportable
- Si lo generaste con `openssl pkcs12`, asegurate de pasar `-export` (no `-nokeys`)

---

## Cache de tokens

### "¿Dónde se guardan los TAs?"

En el directorio configurado en `TokenCacheDirectory` (default: `Path.GetTempPath()`). Cada combinación servicio + CUIT genera un archivo:

```
ARCA_token_v2_wsfe_20123456789.bin
ARCA_token_v2_ws_sr_constancia_inscripcion_20123456789.bin
ARCA_token_v2_wsfex_20123456789.bin
```

El `v2` es la versión del formato del archivo. Los archivos sin `v2` los escribió una versión anterior de la lib (vencimiento en hora local del host, sin cifrar fuera de Windows): se ignoran, no se leen. Podés borrarlos.

### "Los archivos están cifrados"

Sí, en todas las plataformas: el payload se protege con `IDataProtector` (purpose `ElRoso.ARCA.TokenCache`) antes de tocar el disco. Un `Token` + `Sign` de WSAA autoriza a facturar durante 12 h, así que no puede quedar legible ni siquiera dentro de un contenedor efímero.

`AddARCAClient` llama a `services.AddDataProtection()`, que es todo `TryAdd`: si tu app ya configuró su propio key ring (`PersistKeysToFileSystem`, `ProtectKeysWith...`), ese gana. Si no configurás nada, DataProtection usa su ubicación default y las claves pueden ser efímeras — el cache sigue funcionando, pero se invalida en cada arranque.

> Si vas a **persistir** `TokenCacheDirectory` (un volumen montado, por ejemplo), persistí también el key ring de DataProtection. Sin el key ring los archivos son ilegibles y cada arranque pide un TA nuevo.

### "Migré la app de un server a otro y los tokens no sirven"

Esperado: el key ring de DataProtection quedó en el server viejo. Borrá el directorio y la próxima request va a refrescar contra WSAA. No es bug, es feature de seguridad.

### "Cancelo el request y ARCA igual me emite el CAE"

Toda la superficie asincrona recibe un `CancellationToken` y lo respeta: cancelar aborta el canal WCF y devuelve el control con `OperationCanceledException`.

⚠️ **Pero abortar el canal corta la espera local, no el trabajo remoto.** Si ARCA ya proceso el `FECAESolicitar`, el CAE existe aunque vos ya no estes escuchando. Por eso la lib loguea un warning con CUIT, tipo, punto de venta y numero del comprobante en vuelo:

```
CAE request abandoned by the caller for CUIT 20123456789, type 1, point of sale 1, number 4521.
ARCA may have authorized it — reconcile before re-issuing.
```

Ante ese warning, **no re-emitas**: reconciliá con `GetLastAuthorizedNumberAsync` + `GetAuthorizedAsync` (ver [v2.2.0](../CHANGELOG.md)). Re-emitir fabrica un duplicado fiscal que despues hay que anular con nota de credito.

### "Redeployeo y pierdo el cache de tokens"

El TA vive 12 h y **WSAA no emite un segundo TA mientras el primero siga vigente** para el mismo (CUIT, servicio): responde algo del tipo `El CEE ya posee un TA valido para el acceso al WSN solicitado`. Si `TokenCacheDirectory` vive en el filesystem efímero del container, cada deploy tira el TA que ARCA sigue considerando vivo.

Persistí el directorio — y con él, el key ring de DataProtection:

```yaml
volumes:
  - ./ARCA/tokens:/ARCA/tokens
  - ./DataProtection/keys:/DataProtection/keys
```

> El `uniqueId` del `loginTicketRequest` **no** necesita persistencia: se deriva del reloj (segundos unix), así que sigue creciendo entre reinicios aunque el proceso arranque de cero.

### "Tengo concurrencia alta y se corrompen los archivos"

No debería: la lib usa `SemaphoreSlim` por clave para serializar escrituras/lecturas. Si ves corrupción, abrí un issue con repro.

---

## Cliente y Padrón

### "El nombre del cliente sale en blanco"

La lib consulta automáticamente el Padrón A5 cuando le pasás un `ClientRequest` con `DocumentType=CUIT`. Si el padrón devuelve vacío:

- Verificá que tu CUIT tenga adherido el servicio `ws_sr_constancia_inscripcion`
- El CUIT del cliente debe estar **inscripto** en AFIP (no de baja)
- Para consumidor final usá `DocumentType=SIN_IDENTIFICAR` y `DocumentNumber=0`

### "¿Tengo que pasar la Condición IVA del cliente?"

Con CUIT, la lib la resuelve desde el Padrón y pisa lo que hayas puesto. Si el Padrón no permite determinarla (por ejemplo, la persona tiene el impuesto 34, o está inactiva y sin impuestos de IVA), usa la que fijaste con `request.Client.SetCondition(...)`. Si no fijaste ninguna, la emisión tira `ARCAValidationException` antes de pedir el CAE.

Para consumidor final sin CUIT, la lib la fuerza a `CONSUMIDOR_FINAL` automáticamente.

El valor numérico de `VATConditionARCAEnum` es el código de condición IVA del receptor de ARCA (`RESPONSABLE_INSCRIPTO = 1`, `CONSUMIDOR_FINAL = 5`, `MONOTRIBUTO = 6`, …). Hasta 2.3.0 `RESPONSABLE_INSCRIPTO` valía 7: si lo guardaste como entero, ver la migración en el [CHANGELOG](../CHANGELOG.md#300--2026-10-06).

### "Quiero ver la condición IVA de un CUIT sin emitir"

Usá `IPadronService.GetPersonaAsync(representedCuit, cuit)`. Devuelve `VATCondition` con la misma derivación que usa la emisión, más domicilio, actividades e impuestos. Un CUIT inexistente vuelve con `Found = false`, sin excepción. Ejemplo en el [README](../README.md#consultar-un-cuit-en-el-padrón-a5).

---

## Códigos de error ARCA típicos

| Código | Significado | Solución habitual |
|--------|-------------|-------------------|
| 10015  | Fecha del comprobante inválida | La fecha debe estar entre hoy-10 días y hoy+10 días |
| 10016  | Fecha de servicio inválida | Para servicios, `DateOfServicesFrom <= DateOfServicesTo <= PaymentDue` |
| 10017  | Comprobante asociado inválido | Punto de venta + tipo + número de la nota debe existir y ser del mismo CUIT emisor |
| 10018  | Importes inconsistentes | `Total = AmountTax + AmountNotTax + TaxAmount + OtherTaxAmount` (validar redondeo) |
| 10048  | Cliente no inscripto en padrón | Para FA, el receptor debe ser Responsable Inscripto. Si no, usar FB |
| 10063  | CAE ya autorizado | El número de comprobante ya tiene CAE — estás re-enviando el mismo |

> **Lista completa:** [Manual del desarrollador WSFEv1](https://www.afip.gob.ar/ws/WSFEV1/manual_desarrollador_COMPG_v4_4_1.pdf) (sección "Códigos de errores").

---

## Notas de Crédito y Débito

### "Olvidé el `BillingDocumentNumberingAssociateds` y ARCA lo aceptó"

No siempre lo valida ARCA, pero **es obligatorio fiscalmente**. La lib lo valida con FluentValidation antes de mandar la request.

### "El comprobante asociado tiene fecha de hace 5 años"

ARCA permite asociar comprobantes viejos en notas — no hay límite de tiempo. Si la NC asocia una factura de 2020, está OK.

### "Una NC puede asociar comprobantes de distinto tipo?"

Sí. Una `NCA` puede asociar varias `FA`. Una `NCB` no puede asociar una `FA` (debe ser de la misma clase A/B/C).

---

## Factura de Exportación

### "¿Por qué `Items` es obligatorio solo en exportación?"

Porque WSFEXv1 requiere detalle de items, mientras que WSFEv1 acepta un solo importe total. La lib lo valida.

### "Currency = 'Dolares' pero el monto está en pesos"

`AmountTax` siempre va **en la moneda del comprobante** (no convertido). `ExchangeRate` se usa para informar la cotización al fisco — la convertibilidad la calcula AFIP.

### "Cómo armo el `BillingDocumentId`"

Es un ID único secuencial **tuyo** (no de ARCA). La lib no lo genera — tenés que llevar el contador en tu DB. Empezá en 1 e incrementá. Si dos exportaciones tienen el mismo ID, ARCA las rechaza.

---

## WSCDC — Verificación de comprobantes recibidos

### "¿Sirve para listar todas las facturas que recibí?"

**No.** WSCDC valida **un comprobante específico a la vez**. Vos le pasás todos los datos del comprobante (CUIT emisor + tipo + nro + fecha + total + CAE) y ARCA responde si lo conoce y está autorizado. Sirve cuando ya tenés la factura en mano (PDF, XML, papel) y querés confirmar autenticidad.

Para listar comprobantes recibidos, hoy no hay WS oficial — solo el portal de ARCA. Ver [ROADMAP](./ROADMAP.md).

### Servicio que hay que adherir

En el portal ARCA: **Administrador de Relaciones de Clave Fiscal → Adherir Servicio → `Constatación de Comprobantes`** (también listado como `wscdc`). Mismo certificado que usás para WSFEv1, pero el **TA es separado** (la lib lo cachea aparte).

### Diferencias CAE / CAI / CAEA

| Modo | Cuándo lo ves | En `AuthorizationMode` |
|------|---------------|------------------------|
| **CAE** | Facturas electrónicas emitidas online (lo más común) | `AuthorizationModeARCAEnum.CAE` |
| **CAI** | Controladores fiscales (cajas registradoras viejas, talonarios pre-impresos) | `AuthorizationModeARCAEnum.CAI` |
| **CAEA** | Facturación electrónica anticipada (volúmenes altos con corte) | `AuthorizationModeARCAEnum.CAEA` |

Si dudás, probá con CAE primero. Si rechaza con observación "modo incorrecto", pasá a CAI.

### "El CAE es válido pero el comprobante igual da rechazado"

Pasa cuando los datos enviados no matchean exactamente lo que ARCA tiene registrado:
- **Importe total** debe ser EXACTAMENTE igual al del comprobante (redondeo a 2 decimales — ojo con .005 que se redondea distinto)
- **Fecha del comprobante** en formato yyyy-MM-dd
- **CUIT del emisor** sin guiones
- **Tipo de comprobante** debe usar el código ARCA (FA=1, FB=6, etc.)

### Códigos de error típicos del WSCDC

| Código | Significado | Solución |
|--------|-------------|----------|
| 10048  | El CAE no corresponde al CUIT emisor | Verificar que el CUIT está bien escrito |
| 10050  | El CAE no corresponde al tipo de comprobante | El tipo es FA pero quizá es FB |
| 10056  | El importe total no coincide | Re-leer el PDF, puede haber un decimal mal |
| 600    | Token inválido | TA expiró — la lib refresca solo, pero si insiste revisar el servicio adherido |

### "Querés validar al recibir cada factura electrónica de proveedores"

Si tu app procesa facturas electrónicas automáticamente (lectura de XML AFIP-Reportes, etc.), correr WSCDC al ingestar cada una es una buena práctica antifraude. Costo: un round-trip SOAP por factura. Bajo en absoluto, alto en escala — considerá cache en tu lado para no re-validar lo mismo cada vez.

## WSCComu — DFE / e-Ventanilla

### Prerequisitos

1. **Constituir el Domicilio Fiscal Electrónico** en el portal de ARCA (una sola vez, no hay WS para esto)
2. **Adherir el servicio `wsccomu`** ("Consumir Comunicaciones de Ventanilla Electrónica") en Administrador de Relaciones
3. **Delegar** al CUIT del certificado si vas a operar para terceros

### URL de producción TBD

Al día de hoy, ARCA solo publica oficialmente el endpoint de **homologación**:

```
https://stable-middleware-tecno-ext.afip.gob.ar/ve-ws/services/veconsumer
```

El endpoint productivo no está públicamente documentado. Cuando tu CUIT pasa a producción:

1. Pedir el endpoint a `webservices-desa@arca.gob.ar`
2. Setear `ARCAOptions.WsccomuUrl = "<url-prod>"`

Por eso la propiedad `WsccomuUrl` es `public` settable (a diferencia de `WsfeUrl` que es internal computed).

### Paginación

El WSCComu pagina **del lado del servidor**:

```csharp
var page1 = await mailbox.ListAsync(new MailboxQueryRequest { Page = 1, PageSize = 50, ... });
// page1.TotalPages tells you how many pages exist
// page1.TotalItems tells you total count

for (int p = 1; p <= page1.TotalPages; p++)
{
    var page = await mailbox.ListAsync(new MailboxQueryRequest { Page = p, PageSize = 50, ... });
    // process page.Messages
}
```

### Estados de las notificaciones

Cada notificación tiene un `StateId` (int) + `StateName` (string). Los valores cambian entre organismos. Para listar los estados válidos en un ambiente, ARCA expone `consultarEstados` (no implementado en `v1.1.0` — backlog).

Para filtrar por estado:

```csharp
var soloNuevas = await mailbox.ListAsync(new MailboxQueryRequest
{
    IssuingCompany = issuer,
    StateId = 1,  // típicamente "Nueva" — confirmar con tu instancia ARCA
});
```

### Consumir vs Listar

| Operación | Qué hace | Marca como leída? |
|-----------|----------|-------------------|
| `ListAsync` | Lista resúmenes (sin cuerpo, sin adjuntos) | ❌ NO |
| `ConsumeAsync` | Devuelve el mensaje completo (cuerpo + adjuntos) | ✅ SÍ |

⚠️ **`ConsumeAsync` marca la notificación como leída en ARCA.** No se puede revertir desde el WS. Si tu UI ofrece "preview sin marcar como leído", usá solo `ListAsync` para esos.

### Notificación tácita a los 5 días hábiles

ARCA considera una notificación "tácitamente notificada" si pasan 5 días hábiles desde la publicación sin que el contribuyente la consulte. **Importante**: el simple acto de listar via WSCComu **no resetea ese reloj** — solo `ConsumeAsync` (que marca como leída) lo hace.

Recomendado: correr `ListAsync` cada hora en background. Si aparece algo "Nueva", alertar al usuario. El usuario decide si lo abre (= `ConsumeAsync`).

### Adjuntos

Los adjuntos vienen en base64 ya decodificados en `Attachment.Content` (byte[]). El nombre original está en `Attachment.FileName`. El MIME type no se devuelve — inferir de la extensión:

```csharp
foreach (var a in message.Attachments)
{
    var mime = Path.GetExtension(a.FileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".xml" => "application/xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };
    // ...
}
```

### Códigos de error típicos

| Error | Significado | Solución |
|-------|-------------|----------|
| Fault `Acceso no autorizado` | Servicio `wsccomu` no adherido al CUIT | Adherir en Administrador de Relaciones |
| Fault `No tiene DFE constituido` | El CUIT no tiene DFE habilitado | Constituir DFE en el portal (una vez) |
| Token / sign error | TA expirado o de otro servicio | La lib refresca solo — si insiste, borrar el `.bin` del token cache |

## Docker y deploy

### "El TimeZone en Linux es UTC y los datos quedan corridos"

ARCA razona en hora de pared argentina. **La lib no depende del `TZ` del proceso:** resuelve la zona por nombre (`America/Argentina/Buenos_Aires`) para armar los timestamps del `loginTicketRequest`, y evalúa el vencimiento del TA como instante UTC. Corré el container con el `TZ` que quieras — el resultado es el mismo.

Tu lógica de negocio (fechas de comprobante, vencimientos) sí **debe vivir en hora AR**, y para eso sigue siendo cómodo:

```dockerfile
ENV TZ=America/Argentina/Buenos_Aires
```

> Si tu imagen no trae la base de zonas horarias (`tzdata`) o corre con globalización invariante, la lib cae a un UTC-3 fijo. Funciona, pero pierde precisión si Argentina vuelve a aplicar horario de verano.

### "Mi imagen Docker no encuentra el .pfx"

Volumen mal montado o path equivocado:

```yaml
volumes:
  - ./certs:/certs:ro                 # ojo con permisos
environment:
  Arca__CertificatePath: /certs/empresa.pfx
```

Verificá dentro del container con `ls -la /certs`.

---

## Concurrencia y reintentos

### "Dos requests concurrentes al mismo servicio refrescan el TA dos veces"

No deberían — la lib usa `SemaphoreSlim` por clave (servicio+CUIT). Solo una refresca, las demás esperan y usan el TA refrescado.

### "WSFEv1 devuelve timeout intermitente"

WSFEv1 puede tener latencia alta (>10s) en horas pico. La lib usa el timeout configurado en `SoapTimeoutSeconds` (default 30). Si tu app necesita más resilience:

- Subí `SoapTimeoutSeconds` a 60
- Implementá retry con backoff exponencial en tu lado (la lib no reintentea sola — el reintento lo dejamos al consumidor para evitar duplicar comprobantes)

### "Hice retry y emití el mismo comprobante dos veces"

Pasa, y es más fácil de lo que parece: **ARCA otorga el CAE antes de que vos lo persistas**. Si tu guardado local falla en esa ventana —una excepción, un deadlock, un timeout de base— el comprobante queda autorizado en ARCA y ausente en tu base. El reintento natural (volver a llamar `AuthorizeAsync`) saca un **segundo CAE para la misma venta**: un duplicado fiscal que después hay que anular con nota de crédito.

Desde **v2.2.0** la lib te da con qué distinguirlo. Ante un fallo de persistencia, **consultá antes de reintentar**:

```csharp
// 1. ¿ARCA está más adelante que mi último número guardado?
var ultimoEnArca = await numbering.GetLastAuthorizedNumberAsync(
    issuingCompany, BillingDocumentTypeARCAEnum.FC, bookPrefix: 3);

if (ultimoEnArca > miUltimoNumeroGuardado)
{
    // 2. Sí: ARCA autorizó algo que no llegué a guardar. Lo recupero en vez de re-emitir.
    var comprobante = await numbering.GetAuthorizedAsync(
        issuingCompany, BillingDocumentTypeARCAEnum.FC, bookPrefix: 3, ultimoEnArca);

    if (comprobante.IsApproved)
    {
        // Reconciliar: guardar comprobante.CAE / CAEExpirationDate en el registro local.
        // NO llamar AuthorizeAsync — eso emitiría un CAE duplicado.
    }
}
else
{
    // 3. No: la autorización nunca ocurrió, el reintento es seguro.
    await numbering.AuthorizeAsync(request);
}
```

⚠️ **`GetAuthorizedAsync` no devuelve el importe.** El proxy de `FECompConsultar` expone el CAE, su vencimiento, la fecha de proceso, el punto de venta y el tipo — no `ImpTotal`. Para reconciliar alcanza, porque el importe ya lo tenés en tu propio comprobante; si necesitás validarlo contra ARCA, mirá la deuda técnica en el [ROADMAP](ROADMAP.md).

⚠️ **La lib no reintenta sola, a propósito.** El reintento queda del lado del consumidor justamente para que esta decisión —consultar o re-emitir— la tome quien conoce el estado de su base.

---

¿Falta algo? [Abrí una Discussion](https://github.com/edirosolini/ElRoso.ARCA/discussions) o un PR a este archivo.
