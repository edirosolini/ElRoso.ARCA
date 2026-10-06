# ElRoso.ARCA

[![CI](https://github.com/edirosolini/ElRoso.ARCA/actions/workflows/ci.yml/badge.svg?branch=mainline)](https://github.com/edirosolini/ElRoso.ARCA/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/edirosolini/ElRoso.ARCA/branch/mainline/graph/badge.svg)](https://codecov.io/gh/edirosolini/ElRoso.ARCA)
[![NuGet](https://img.shields.io/nuget/v/ElRoso.ARCA.svg?label=NuGet)](https://www.nuget.org/packages/ElRoso.ARCA)
[![Downloads](https://img.shields.io/nuget/dt/ElRoso.ARCA.svg)](https://www.nuget.org/packages/ElRoso.ARCA)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

> **Cliente .NET 9 para facturación electrónica con ARCA (ex-AFIP).** Hecho para que devs argentinos no sufran integrando como sufrimos los que vinimos antes.

**Soporta:**
- ✅ Autenticación WSAA (PKCS#7 + token cache cifrado con `IDataProtection`)
- ✅ Facturación doméstica WSFEv1 (FA / FB / FC + Notas de Crédito y Débito)
- ✅ Facturación exportación WSFEXv1 (Factura, NC, ND)
- ✅ Padrón A5 (consulta de inscripción ARCA por CUIT, `IPadronService` desde v3.0.0)
- ✅ Generación del QR oficial para impresión
- ✅ **Verificación de comprobantes recibidos vía WSCDC** (desde v1.1.0)
- ✅ **Notificaciones del Domicilio Fiscal Electrónico (e-Ventanilla) vía WSCComu** (desde v1.1.0)

---

## 📑 Tabla de contenidos

- [Instalación](#-instalación)
- [Quick Start — Factura A en 5 líneas](#-quick-start--factura-a-en-5-líneas)
- [Configuración](#%EF%B8%8F-configuración)
- [Manejo de certificados](#-manejo-de-certificados)
- [Tipos de comprobante soportados](#-tipos-de-comprobante-soportados)
- [Recetario por escenario](#-recetario-por-escenario)
  - [Factura A con CUIT del cliente](#factura-a-con-cuit-del-cliente)
  - [Factura B / Factura C (Monotributo)](#factura-b--factura-c-monotributo)
  - [Notas de Crédito y Débito](#notas-de-crédito-y-débito)
  - [Factura de Servicios](#factura-de-servicios)
  - [Consultar un CUIT en el Padrón A5](#consultar-un-cuit-en-el-padrón-a5)
  - [Verificar un comprobante recibido (WSCDC)](#verificar-un-comprobante-recibido-wscdc)
  - [Leer notificaciones del DFE (e-Ventanilla)](#leer-notificaciones-del-dfe-e-ventanilla)
  - [Factura de Exportación](#factura-de-exportación)
- [Manejo de errores](#-manejo-de-errores)
- [Ambientes (Homologación vs Producción)](#-ambientes-homologación-vs-producción)
- [QR code para impresión](#-qr-code-para-impresión)
- [Cookbook — Pitfalls comunes](./docs/COOKBOOK.md)
- [Contribuir](#-contribuir)
- [Apoyar el proyecto](#%EF%B8%8F-apoyar-el-proyecto)

---

## 📦 Instalación

```bash
dotnet add package ElRoso.ARCA
```

> **Requisitos:** .NET 9.0 o superior. Funciona en Windows, Linux y macOS. El cache de tokens se cifra con **`IDataProtection`** en todas las plataformas; `AddARCAClient` registra un provider por defecto y respeta el que ya tengas configurado (ver [Cookbook → cache de tokens](./docs/COOKBOOK.md#cache-de-tokens)).

> ⚠️ **Upgrade a 3.0.0:** `VATConditionARCAEnum` cambió de valores (`RESPONSABLE_INSCRIPTO` pasa de 7 a 1). Si lo guardás como entero, migrá `7 → 1` antes de desplegar. Pasos en el [CHANGELOG](./CHANGELOG.md#300--2026-10-06).

---

## ⚡ Quick Start — Factura A en 5 líneas

```csharp
// 1) Registrá la lib en DI
builder.Services.AddARCAClient(options =>
{
    options.IsProduction        = false;                    // homologación
    options.CertificatePath     = "/certs/mi-empresa.pfx";
    options.CertificatePassword = "miPassword";
});

// 2) Emití la factura
var response = await billingService.AuthorizeAsync(new BillingDocumentNumberingRequest
{
    BillingDocumentType       = BillingDocumentTypeARCAEnum.FA,
    BillingDocumentBookPrefix = 1,
    BillingDocumentDate       = DateTime.Today,
    Currency                  = "Pesos",
    ExchangeRate              = 1,
    ConceptType               = ConceptTypeARCAEnum.Products,
    AmountTax                 = 826.45,
    BillingDocumentNumberingTaxes = [new() { PercentageTax = "21.00", BaseAmount = 826.45, Amount = 173.55 }],
    IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
    Client         = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 30987654321 },
});

Console.WriteLine(response.Result
    ? $"CAE: {response.CAE} — Nro: {response.BillingDocumentNumber}"
    : $"Errores: {string.Join("; ", response.Errors)}");
```

---

## ⚙️ Configuración

### `appsettings.json`

```json
{
  "Arca": {
    "IsProduction": false,
    "CertificatePath": "/certs/mi-empresa.pfx",
    "CertificatePassword": "",
    "TokenCacheDirectory": "/tmp/arca-tokens",
    "SoapTimeoutSeconds": 30
  }
}
```

> **🔒 Seguridad:** **nunca** commitees `CertificatePassword` en texto plano. Usá variables de entorno, Azure Key Vault, AWS Secrets Manager, o User Secrets en desarrollo.

### Registro en `Program.cs`

```csharp
builder.Services.AddARCAClient(options =>
{
    var cfg = builder.Configuration.GetSection("Arca");
    options.IsProduction        = cfg.GetValue<bool>("IsProduction");
    options.CertificatePath     = cfg["CertificatePath"]!;
    options.CertificatePassword = cfg["CertificatePassword"];
    options.TokenCacheDirectory = cfg["TokenCacheDirectory"] ?? Path.GetTempPath();
    options.SoapTimeoutSeconds  = cfg.GetValue<int>("SoapTimeoutSeconds", 30);
});
```

### Padrón dentro de la emisión (`ResolveReceiverFromPadron`)

Por defecto (`true`), al emitir a un receptor con CUIT `AuthorizeAsync` consulta el Padrón A5 con el CUIT del emisor y usa la condición frente al IVA que deriva de ahí (si la puede determinar) y la razón social.

Apagala cuando **ya resolvés la condición del receptor por tu cuenta** (por ejemplo, con tu propia consulta a `IPadronService` antes de elegir la letra del comprobante), o cuando el CUIT emisor no delegó el servicio `ws_sr_constancia_inscripcion` y ARCA responde *"Este token no le permite actuar en representacion de la CUIT …"*:

```csharp
options.ResolveReceiverFromPadron = false;
```

- Con `false` la emisión no pide el ticket del Padrón ni lo consulta: manda la condición que pusiste con `ClientRequest.SetCondition(...)`. Si no es una condición válida, tira `ARCAValidationException` antes de pedir el CAE.
- Un receptor sin CUIT sigue saliendo como `CONSUMIDOR_FINAL`. WSFEv1 usa siempre el CUIT del emisor.
- Si no la tocás, **no cambia nada**.

---

## 🔐 Manejo de certificados

ARCA requiere un certificado X.509 con clave privada (`.pfx` o `.p12`) para firmar los tokens de sesión (WSAA).

### Generar el certificado paso a paso

```bash
# 1. Generar la clave privada
openssl genrsa -out empresa.key 2048

# 2. Generar el CSR (Certificate Signing Request)
openssl req -new -key empresa.key -subj "/C=AR/O=Mi Empresa/CN=20123456789/serialNumber=CUIT 20123456789" -out empresa.csr

# 3. Cargar el CSR en el portal ARCA → "Administración de Certificados Digitales"
#    y descargar el .crt firmado.

# 4. Combinar clave privada + certificado en un .pfx
openssl pkcs12 -export -in empresa.crt -inkey empresa.key -out empresa.pfx -passout pass:miPassword
```

### Docker

```yaml
services:
  myapp:
    volumes:
      - ./certs:/certs:ro
    environment:
      Arca__CertificatePath: /certs/empresa.pfx
      Arca__CertificatePassword: ${ARCA_CERT_PASSWORD}
```

> ⚠️ **El `.pfx` contiene la clave privada.** Nunca lo commitees. Si por error te pasó, **revocá el certificado en ARCA inmediatamente**.

---

## 📋 Tipos de comprobante soportados

| Enum | Código ARCA | Descripción |
|------|-------------|-------------|
| `FA` | 1 | Factura A |
| `NDA` / `NCA` | 2 / 3 | Nota de Débito / Crédito A |
| `FB` | 6 | Factura B |
| `NDB` / `NCB` | 7 / 8 | Nota de Débito / Crédito B |
| `FC` | 11 | Factura C |
| `NDC` / `NCC` | 12 / 13 | Nota de Débito / Crédito C |
| `InvoiceExport` | 19 | Factura de Exportación |
| `DebitNoteExport` / `CreditNoteExport` | 20 / 21 | Notas Débito / Crédito Exportación |
| `Remittances` | 91 | Remito R |

---

## 📖 Recetario por escenario

### Factura A con CUIT del cliente

```csharp
new BillingDocumentNumberingRequest
{
    BillingDocumentType       = BillingDocumentTypeARCAEnum.FA,
    BillingDocumentBookPrefix = 1,
    BillingDocumentDate       = DateTime.Today,
    Currency                  = "Pesos",
    ExchangeRate              = 1,
    ConceptType               = ConceptTypeARCAEnum.Products,
    AmountTax                 = 1000,
    BillingDocumentNumberingTaxes =
    [
        new() { PercentageTax = "21.00", BaseAmount = 1000, Amount = 210 }
    ],
    IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
    Client         = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 30987654321 },
}
```

> La lib consulta automáticamente el **Padrón** para resolver el nombre y la condición IVA del cliente. Si el Padrón no permite determinar la condición, usa la que pusiste con `Client.SetCondition(...)`; sin ninguna, tira `ARCAValidationException` (ver [Consultar un CUIT en el Padrón A5](#consultar-un-cuit-en-el-padrón-a5)). La letra (`FA`, `FB`, `FC`) la elegís vos.

### Factura B / Factura C (Monotributo)

```csharp
// FC — Monotributo no discrimina IVA
new BillingDocumentNumberingRequest
{
    BillingDocumentType       = BillingDocumentTypeARCAEnum.FC,
    BillingDocumentBookPrefix = 1,
    BillingDocumentDate       = DateTime.Today,
    Currency                  = "Pesos",
    ExchangeRate              = 1,
    ConceptType               = ConceptTypeARCAEnum.Products,
    AmountTax                 = 1210,                          // total con IVA incluido
    IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
    Client         = new() { DocumentType = DocumentTypeARCAEnum.SIN_IDENTIFICAR, DocumentNumber = 0 },
}
```

### Notas de Crédito y Débito

Deben incluir los comprobantes asociados:

```csharp
new BillingDocumentNumberingRequest
{
    BillingDocumentType = BillingDocumentTypeARCAEnum.NCA,
    BillingDocumentNumberingAssociateds =
    [
        new()
        {
            BillingDocumentType       = BillingDocumentTypeARCAEnum.FA,
            BillingDocumentNumber     = 42,
            BillingDocumentBookPrefix = 1,
            BillingDocumentDate       = new DateTime(2026, 1, 15),
        }
    ],
    // resto de campos como Factura A...
}
```

### Factura de Servicios

Concepto `Services` requiere rango de fechas y vencimiento:

```csharp
ConceptType        = ConceptTypeARCAEnum.Services,
DateOfServicesFrom = new DateTime(2026, 3, 1),
DateOfServicesTo   = new DateTime(2026, 3, 31),
PaymentDue         = new DateTime(2026, 4, 10),
```

### Consultar un CUIT en el Padrón A5

Trae los datos de inscripción de cualquier CUIT: nombre, domicilio fiscal, condición frente al IVA, actividades, impuestos y monotributo.

```csharp
var padron = sp.GetRequiredService<IPadronService>();

var persona = await padron.GetPersonaAsync(
    representedCuit: 20123456789,   // CUIT que firma la consulta
    cuit:            30987654321);  // CUIT a consultar

if (!persona.Found)
    Console.WriteLine("ARCA no tiene ese CUIT.");   // no tira excepción
else
{
    Console.WriteLine($"{persona.DisplayName} — {persona.KeyStatus}");
    Console.WriteLine($"IVA: {persona.VATCondition} (receptor WSFEv1: {persona.ReceiverVATConditionId})");
    Console.WriteLine($"{persona.FiscalAddress?.Street}, {persona.FiscalAddress?.Locality} ({persona.FiscalAddress?.ProvinceName})");
    Console.WriteLine($"Actividad principal: {persona.MainActivity?.Description}");
    foreach (var error in persona.Errors) Console.WriteLine($"  {error}");
}
```

> Requiere el servicio **`ws_sr_constancia_inscripcion`** adherido. `VATCondition` usa `VATConditionARCAEnum`, cuyo valor numérico es el código de condición IVA del receptor de ARCA. Se deriva así: monotributo (bloque de monotributo o impuesto 20) → Monotributo, IVA (30) → Responsable Inscripto, IVA exento (32) → Exento. Si aparece el impuesto 34, o en cualquier otro caso, queda en `null`: el Padrón nunca deduce Sujeto No Categorizado. `ReceiverVATConditionId` es el mismo valor como entero. `Taxes` y `MonotributoTaxes` traen siempre la lista cruda.
>
> Al emitir a un CUIT, `IBillingDocumentNumberingService` usa esa misma condición. Si queda en `null`, respeta la que pusiste con `request.Client.SetCondition(...)`.

### Verificar un comprobante recibido (WSCDC)

Cuando recibís una Factura A de un proveedor, antes de pagarla podés validarla contra ARCA:

```csharp
var verifier = sp.GetRequiredService<IInvoiceVerificationService>();

var response = await verifier.VerifyAsync(new InvoiceVerificationRequest
{
    BillingDocumentType       = BillingDocumentTypeARCAEnum.FA,
    BillingDocumentBookPrefix = 1,                          // Punto de venta
    BillingDocumentNumber     = 42,                         // Nro. de comprobante
    BillingDocumentDate       = new DateTime(2026, 5, 1),
    TotalAmount               = 1210.00,
    AuthorizationCode         = "75123456789012",           // CAE / CAI / CAEA
    AuthorizationMode         = AuthorizationModeARCAEnum.CAE,
    IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
});

if (response.IsAuthorized)
    Console.WriteLine($"✅ CAE válido. Procesado: {response.ProcessedDate:yyyy-MM-dd}");
else
{
    Console.WriteLine("❌ NO autorizado:");
    foreach (var obs in response.Observations) Console.WriteLine($"  {obs}");
}
```

> Requiere el servicio **`wscdc`** adherido en el portal de ARCA. Sample runnable en [`samples/InvoiceVerification/`](./samples/InvoiceVerification/).

### Leer notificaciones del DFE (e-Ventanilla)

Consulta el buzón fiscal electrónico — ideal para correr en background y notificar al usuario apenas llega algo.

```csharp
var mailbox = sp.GetRequiredService<IElectronicMailboxService>();

var inbox = await mailbox.ListAsync(new MailboxQueryRequest
{
    IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
    Page = 1,
    PageSize = 50,
});

foreach (var msg in inbox.Messages)
    Console.WriteLine($"[{msg.Id}] {msg.PublishedDate:yyyy-MM-dd} {msg.StateName} {msg.Subject}");

// Leer el contenido completo (marca como leída en ARCA):
var detail = await mailbox.ConsumeAsync(new MailboxConsumeRequest
{
    IssuingCompany = new() { DocumentNumber = 20123456789 },
    MessageId = inbox.Messages[0].Id,
});

Console.WriteLine(detail.Message?.Body);
foreach (var a in detail.Message?.Attachments ?? [])
    File.WriteAllBytes(a.FileName, a.Content);
```

> Requiere `wsccomu` adherido + DFE constituido. Sample en [`samples/ElectronicMailbox/`](./samples/ElectronicMailbox/). Producción URL: ver Cookbook.

### Factura de Exportación

```csharp
new BillingDocumentNumberingRequest
{
    BillingDocumentType = BillingDocumentTypeARCAEnum.InvoiceExport,
    BillingDocumentId   = 1,                                  // ID único de la factura de exportación
    Currency            = "Dolares",
    ExchangeRate        = 1050.50,
    ConceptType         = ConceptTypeARCAEnum.Products,
    AmountTax           = 500.00,
    Client = new()
    {
        DocumentType   = DocumentTypeARCAEnum.CUIT,
        DocumentNumber = 55123456789,
        CountryId      = 123,                                 // código de país ARCA
        Address        = "123 Main St",
        ClientLanguage = "Inglés",
    },
    Items = [ new() { ItemDescription = "Software license", Amount = 500.00 } ],
}
```

---

## ⚠️ Manejo de errores

La lib lanza excepciones tipadas:

```csharp
try
{
    var response = await billingService.AuthorizeAsync(request);
}
catch (ARCAValidationException ex)
{
    // Validaciones locales (FluentValidation) — no llegó a la red
    foreach (var err in ex.Errors) Console.WriteLine(err);
}
catch (ARCAAuthException ex)
{
    // Falla de autenticación: certificado mal, WSAA caído, firma PKCS#7 fallida
    Console.WriteLine(ex.Message);
}
catch (ARCAServiceException ex)
{
    // ARCA devolvió un error desde WSFEv1/WSFEXv1/Padrón
    Console.WriteLine($"Code {ex.ErrorCode}: {ex.Message}");
}
```

---

## 🌐 Ambientes (Homologación vs Producción)

| Servicio | Homologación (`IsProduction=false`) | Producción (`IsProduction=true`) |
|----------|-------------------------------------|----------------------------------|
| WSAA     | `wsaahomo.afip.gov.ar`              | `wsaa.afip.gov.ar`               |
| WSFEv1   | `wswhomo.afip.gov.ar`               | `servicios1.afip.gov.ar`         |
| WSFEXv1  | `wswhomo.afip.gov.ar`               | `servicios1.afip.gov.ar`         |
| Padrón   | `awshomo.afip.gov.ar`               | `aws.afip.gov.ar`                |

> **Tip:** siempre arrancá en homologación. Un mismo certificado no sirve para ambos ambientes — necesitás dos certificados separados.

---

## 🔳 QR code para impresión

```csharp
var qr = response.QRCode();
// Devuelve: https://www.afip.gob.ar/fe/qr/?p=eyJ2ZXIiOjEsImZl...
```

Esa URL la generás como QR (con QRCoder, ZXing, etc.) y la imprimís en el ticket o PDF junto con el resto de los datos del CAE.

---

## 🤝 Contribuir

PRs y issues bienvenidos. Antes de mandar un PR grande, abrí una [Discussion](https://github.com/edirosolini/ElRoso.ARCA/discussions) para alinear el approach. Ver [CONTRIBUTING.md](./CONTRIBUTING.md).

Si encontraste una vulnerabilidad de seguridad, **no la reportes como issue público** — ver [SECURITY.md](./SECURITY.md).

---

## ❤️ Apoyar el proyecto

Si esta lib te ahorró horas de sufrimiento con ARCA, considerá:

- ⭐ Darle una **star al repo**
- 💬 Compartirlo con otros devs argentinos
- ☕ [Invitarme un cafecito](https://cafecito.app/edirosolini)
- 💙 [GitHub Sponsors](https://github.com/sponsors/edirosolini) *(en aprobación)*

---

## 📜 Licencia

[MIT](./LICENSE) © Di Rosolini Ezequiel (El Roso)
