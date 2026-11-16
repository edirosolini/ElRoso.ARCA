// <copyright file="CertificadosX509Service.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core
{
    using ElRoso.ARCA.Billing;
    using ElRoso.ARCA.Core;
    using ElRoso.ARCA.Read;
    using Microsoft.Extensions.Logging;
    using System.Collections.Concurrent;
    using System.Security.Cryptography.Pkcs;
    using System.Security.Cryptography.X509Certificates;

    internal sealed class CertificateService : ICertificateService
    {
        // Certificate cache keyed by file path — one cert per company, loaded once per app lifetime.
        // Caché de certificados por ruta — un cert por empresa, cargado una sola vez por ciclo de vida.
        private static readonly ConcurrentDictionary<string, X509Certificate2> certCache = new();

        private readonly ILogger<CertificateService> logger;

        public CertificateService(ILogger<CertificateService> logger)
        {
            this.logger = logger;
        }

        public byte[] SignMessage(byte[] messageBytes, X509Certificate2 signingCert)
        {
            try
            {
                var content = new ContentInfo(messageBytes);
                var signedCms = new SignedCms(content);
                var signer = new CmsSigner(signingCert) { IncludeOption = X509IncludeOption.EndCertOnly };

                logger.LogInformation("Signing PKCS#7 message with certificate {Subject}.", signingCert.Subject);
                signedCms.ComputeSignature(signer);

                return signedCms.Encode();
            }
            catch (Exception ex)
            {
                throw new ARCAAuthException("Failed to sign the login ticket request (PKCS#7).", ex);
            }
        }

        public X509Certificate2 LoadCertificate(string filePath, string? password)
        {
            // Cache by file path — password is not part of the key because the same file always
            // uses the same password. Avoids disk I/O and PFX parsing on every token refresh.
            // Caché por ruta — la contraseña no forma parte de la clave porque el mismo archivo
            // siempre usa la misma contraseña. Evita I/O y parseo del PFX en cada renovación.
            return certCache.GetOrAdd(filePath, path =>
            {
                try
                {
                    logger.LogInformation("Loading certificate from {Path}.", path);
                    var bytes = File.ReadAllBytes(path);
                    return password is not null
                        ? new X509Certificate2(bytes, password, X509KeyStorageFlags.PersistKeySet)
                        : new X509Certificate2(bytes);
                }
                catch (Exception ex)
                {
                    throw new ARCAAuthException($"Failed to load certificate from '{path}'.", ex);
                }
            });
        }
    }
}
