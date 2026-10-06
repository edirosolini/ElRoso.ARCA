# Samples

Proyectos runnable que demuestran cómo usar `ElRoso.ARCA` en escenarios concretos.

## Pre-requisitos

Para correr los samples necesitás:

1. Un **certificado de homologación** de ARCA (`.pfx`). Si no tenés, ver [README → Manejo de certificados](../README.md#-manejo-de-certificados).
2. Tu **CUIT emisor** con el servicio adherido (`wsfe` para facturación, `ws_sr_constancia_inscripcion` para Padrón).
3. .NET SDK 9.0 o superior.

## Configurar un sample

Cada sample tiene un `appsettings.json.example` — copialo a `appsettings.json` y completá los valores:

```bash
cd samples/QuickStart
cp appsettings.json.example appsettings.json
# Editá appsettings.json con tu cert y CUIT
dotnet run
```

> ⚠️ **NUNCA commitees tu `appsettings.json` con datos reales.** Está en `.gitignore`.

## Lista de samples

| Sample | Qué demuestra |
|--------|---------------|
| [QuickStart](./QuickStart/) | Emisión de Factura A mínima en homologación |
| [InvoiceVerification](./InvoiceVerification/) | Verificación de un comprobante recibido vía WSCDC |
| [ElectronicMailbox](./ElectronicMailbox/) | Lectura de notificaciones del DFE / e-Ventanilla vía WSCComu |

## Próximamente

Estos samples están en el roadmap — PRs bienvenidos:

- **Monotributo** — Factura C + Nota de Crédito C
- **ResponsableInscripto** — Factura A + B + Notas
- **Exportacion** — Factura de Exportación + Nota de Crédito Export
- **MultiTenant** — Cómo manejar múltiples CUITs/certs en una sola app
- **Docker** — Sample completo con Dockerfile y montaje de certificado
