# Contribuyendo a ElRoso.ARCA

¡Gracias por querer contribuir! Este proyecto existe para que devs argentinos no sufran integrando con ARCA (ex-AFIP) como sufrimos los que vinimos antes.

Toda contribución que reduzca ese sufrimiento es bienvenida: bugs reportados con repro, mejoras de docs, samples para casos de uso reales, fixes, y features nuevos.

---

## Antes de empezar

- **¿Tenés una duda de uso?** Abrí una [Discussion](https://github.com/edirosolini/ElRoso.ARCA/discussions) — no abras un Issue para preguntas.
- **¿Encontraste un bug?** Abrí un [Issue](https://github.com/edirosolini/ElRoso.ARCA/issues/new/choose) usando el template de bug.
- **¿Querés agregar un feature grande?** Abrí primero un Issue o Discussion para alinear el approach antes de codear. Evita PRs grandes que terminan rechazados.
- **Vulnerabilidades de seguridad:** **no** abrir issue público. Ver [SECURITY.md](./SECURITY.md).

## Stack y prerequisitos

- .NET SDK **9.0** o superior
- Git
- (Opcional) Cuenta de ARCA en ambiente de **homologación** con certificado para correr tests de integración manuales

## Levantar el proyecto localmente

```bash
git clone https://github.com/edirosolini/ElRoso.ARCA.git
cd ElRoso.ARCA
dotnet restore
dotnet build
dotnet test
```

## Estructura del repo

```
ElRoso.ARCA.sln
├── ARCA/                       # Proyecto principal de la lib
│   ├── Caching/                # FileTokenCache (cifrado con IDataProtection)
│   ├── Connected Services/     # Proxies SOAP autogenerados (NO EDITAR a mano)
│   ├── Domains/                # Enums, Requests, Responses, interfaces públicas
│   ├── Exceptions/             # ARCAAuthException, ARCAServiceException, ARCAValidationException
│   ├── Options/                # ARCAOptions
│   ├── Services/               # Implementaciones internal sealed
│   └── Validations/            # Validators de FluentValidation
└── tests/
    └── ElRoso.ARCA.Tests/      # xUnit + Moq + FluentAssertions + Coverlet
```

## Convenciones de código

- **Idioma:** XML docs y comentarios pueden estar en **inglés y/o español** (ambos son válidos en este repo).
- **Nombres** (clases, métodos, variables, archivos): **inglés**.
- **Mensajes de error visibles al usuario final** (FluentValidation `.WithMessage`, ARCAServiceException, etc.): **español**.
- `StyleCop.Analyzers` está activo — no rompas warnings nuevos.
- Excepciones tipadas: nunca `throw new Exception(...)`. Usar `ARCAAuthException`, `ARCAServiceException`, `ARCAValidationException`.
- Servicios internos: `internal sealed`. Las interfaces públicas viven en `Domains/Services/`.
- DI: todos los services son **Singleton**.
- **Dependencias:** `Directory.Build.props` activa `NuGetAudit` en modo `all` con `NU1903` (High) y `NU1904` (Critical) como error: un paquete con advisory High o Critical, directo o transitivo, rompe el restore. Se arregla subiendo el paquete a la versión parcheada. Si no hay parche, la única salida es `NuGetAuditSuppress` sobre ese advisory, con un comentario que lo justifique y enlace al seguimiento.
- `dotnet list package` restaura solo y el gate corta antes del listado: usar `--no-restore` después de un `dotnet restore`.

## Tests

- Mínimo de cobertura actual: **25%** global, irá creciendo con el tiempo. No subir un PR que la haga bajar.
- Para tests que requieran ARCA real: usar **homologación**, nunca producción. Estos tests NO van en la suite automática — solo en samples.

## CI

- Los lunes a las 08:00 (hora argentina), *Audit* lista los paquetes NuGet vulnerables aunque no haya commits; si hay High o Critical, falla y avisa por Telegram con el link al run. Para que el aviso salga, el repo necesita los secrets `TELEGRAM_NOTIFIER_TOKEN` y `TELEGRAM_NOTIFIER_RECIPIENT_IDS`; sin ellos el job lo dice como warning y no avisa.

## Commits

Formato preferido (no obligatorio):
```
Categoría: descripción corta en imperativo

Detalle opcional en líneas siguientes si el cambio es complejo.
```

Categorías sugeridas: `fix`, `feat`, `docs`, `test`, `refactor`, `ci`, `deps`.

Ejemplo:
```
feat: support FCE (Factura de Crédito Electrónica) MiPyME

Adds the FCE document type (codes 201-213) and the optional fields
required by RG 4367/E.
```

## Pull Requests

1. Forkeá el repo y hacé tu trabajo en una branch (`feature/xxx`, `fix/yyy`).
2. Si tu PR agrega un feature, **agregá tests** que lo cubran.
3. Corré `dotnet build` y `dotnet test` localmente antes de abrir el PR.
4. En el PR, describí **el problema que resolvés** (no solo el cómo). Si hay Issue relacionado, linkealo.
5. Aceptá feedback rápido — el revisor probablemente sufrió ARCA tanto como vos y quiere lo mismo: una lib limpia.

## Releases (solo maintainers)

El tag lo crea CI. Alcanza con que el bump entre a `mainline`:

1. Subir `<Version>` en `ARCA/ElRoso.ARCA.csproj` — **es lo que se publica**, y va en el mismo PR
   que el cambio.
2. Actualizar `CHANGELOG.md` con esa versión y sus cambios.
3. Mergear a `mainline`. El workflow *Tag on version bump* lee el `<Version>`, crea el tag `vX.Y.Z`
   si todavía no existe y llama a *Release*, que arma, testea y publica a NuGet.org.

⚠️ **El `<Version>` del csproj es la fuente de verdad, no el tag.** Cuando el tag se creaba a mano
y el bump se olvidaba, salía una versión con otra adentro: `v2.2.0` se publicó con `2.1.2` en el
csproj. Por eso el paso 1 va primero y el tag ya no lo pone una persona.

Si hace falta publicar algo fuera de este camino, *Release* sigue teniendo `workflow_dispatch` con
la versión como input. En ese caso **no** crea GitHub Release salvo que el tag ya exista.

## Code of Conduct

Esperamos que todos los participantes sigan el [Código de Conducta](./CODE_OF_CONDUCT.md).

---

¡Gracias por contribuir! Cada PR que mergeás le evita horas de sufrimiento a otro dev argentino.
