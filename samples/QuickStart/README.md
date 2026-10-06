# QuickStart Sample

Emite una **Factura A** en el ambiente de **homologación** de ARCA y muestra el CAE + URL del QR.

## Qué demuestra

- Setup mínimo de `ElRoso.ARCA` con Microsoft.Extensions.DependencyInjection
- Lectura de configuración desde `appsettings.json` + variables de entorno
- Llamada a `IBillingDocumentNumberingService.AuthorizeAsync()`
- Manejo de las tres excepciones tipadas (`ARCAValidationException`, `ARCAAuthException`, `ARCAServiceException`)
- Acceso al CAE, número de comprobante, datos del cliente resuelto por Padrón, y URL del QR

## Cómo correrlo

```bash
# 1. Copiá el template de config
cp appsettings.json.example appsettings.json

# 2. Editá appsettings.json con tu cert y CUITs
#    - Arca:CertificatePath  → ruta absoluta a tu .pfx de homologación
#    - Arca:CertificatePassword → password del .pfx (vacío si no tiene)
#    - Sample:IssuerCuit     → tu CUIT emisor
#    - Sample:ClientCuit     → CUIT receptor (Responsable Inscripto, sino usar FB)

# 3. Corré
dotnet run
```

Salida esperada (caso feliz):

```
12:34:56 info: ElRoso.ARCA.Samples.QuickStart.Program[0] Environment: Homologation
12:34:56 info: ElRoso.ARCA.Samples.QuickStart.Program[0] Issuer CUIT: 20123456789
12:34:58 info: ElRoso.ARCA.Samples.QuickStart.Program[0] ✅ CAE: 75123456789012
12:34:58 info: ElRoso.ARCA.Samples.QuickStart.Program[0] Document number: 1
12:34:58 info: ElRoso.ARCA.Samples.QuickStart.Program[0] CAE expires: 2026-05-23
12:34:58 info: ElRoso.ARCA.Samples.QuickStart.Program[0] Client (from Padrón): EMPRESA EJEMPLO SA (RESPONSABLE_INSCRIPTO)
12:34:58 info: ElRoso.ARCA.Samples.QuickStart.Program[0] QR URL: https://www.afip.gob.ar/fe/qr/?p=eyJ2ZXIi...
```

## Variables de entorno (opcional)

Cualquier valor del `appsettings.json` se puede sobrescribir con env vars prefijadas con `ARCA_`:

```bash
ARCA_Arca__CertificatePath=/tmp/cert.pfx \
ARCA_Sample__IssuerCuit=20111111111 \
dotnet run
```

Esto es útil para no tocar el `appsettings.json` y para deploys en Docker / CI.

## Si algo falla

- **`ARCAAuthException`** — problema con el certificado, contraseña, o servicio no adherido en el portal ARCA
- **`ARCAValidationException`** — algún campo del request no pasa la validación local (FluentValidation)
- **`ARCAServiceException`** — ARCA devolvió un error de negocio (revisar `response.Errors` y el código en la excepción)

Más detalle en el [Cookbook](../../docs/COOKBOOK.md).
