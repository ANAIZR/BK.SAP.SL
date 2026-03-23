using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SapPortalIntegration.Application.Contracts.Response.HttpClients;
using SapPortalIntegration.Core.Interfaces;
using Shared.Configuration;
using Shared.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SapPortalIntegration.Infrastructure.Sap;

public class SapServiceLayerClient : ISapServiceLayerClient
{
    private readonly HttpClient _httpClient;
    private readonly UdoConfiguration _udoConfiguration;
    private string? _cachedSessionId;
    private DateTime _sessionExpiresAt = DateTime.MinValue;
    private readonly object _lockObject = new object();


    public SapServiceLayerClient(IHttpClientFactory clientFactory, IOptions<UdoConfiguration> udoConfiguration)
    {
        _httpClient = clientFactory.CreateClient("HttpClientWithSSLUntrusted");
        _udoConfiguration = udoConfiguration.Value;

        if (string.IsNullOrWhiteSpace(_udoConfiguration.Url))
            throw new ArgumentException("La URL de UdoConfiguration no está configurada correctamente.");

        _httpClient.BaseAddress = new Uri(_udoConfiguration.Url);

        LogBL.Debug($"[CONSTRUCTOR] HttpClient.BaseAddress configurado: {_httpClient.BaseAddress}", "UdoIntegrationHttpClient");
    }

    // ==========================
    // AUTH
    // ==========================
    private async Task<bool> EnsureAuthenticatedAsync()
    {
        lock (_lockObject)
        {
            if (!string.IsNullOrEmpty(_cachedSessionId) &&
                DateTime.UtcNow < _sessionExpiresAt &&
                _httpClient.DefaultRequestHeaders.Contains("Cookie"))
            {
                LogBL.Debug($"[SAP-SESSION] Sesión válida hasta {_sessionExpiresAt:yyyy-MM-dd HH:mm:ss}, reutilizando existente", "UdoIntegrationHttpClient");
                return true;
            }
        }

        LogBL.Debug("[SAP-SESSION] Sesión no válida o inexistente, iniciando login", "UdoIntegrationHttpClient");
        return await LoginAsync();
    }

    private async Task<bool> LoginAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            LogBL.Debug("[SAP-LOGIN] Iniciando proceso de login en SAP Service Layer", "UdoIntegrationHttpClient");

            // limpiar estado anterior
            lock (_lockObject)
            {
                _cachedSessionId = null;
                _sessionExpiresAt = DateTime.MinValue;
            }
            if (_httpClient.DefaultRequestHeaders.Contains("Cookie"))
                _httpClient.DefaultRequestHeaders.Remove("Cookie");

            var payload = new
            {
                _udoConfiguration.CompanyDB,
                _udoConfiguration.UserName,
                _udoConfiguration.Password,
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver()
                }),
                Encoding.UTF8,
                "application/json"
            );

            HttpResponseMessage response = await _httpClient.PostAsync("b1s/v2/Login", content);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                stopwatch.Stop();
                LogBL.Debug($"[SAP-LOGIN] Login fallido - StatusCode: {response.StatusCode} - Duración: {stopwatch.ElapsedMilliseconds}ms", "UdoIntegrationHttpClient");
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<UdoLoginResponse>(json);

            // Guardar cookies correctamente: solo "NAME=VALUE"
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                var cookiePairs = setCookies
                    .Select(c => c.Split(';', 2)[0].Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                if (_httpClient.DefaultRequestHeaders.Contains("Cookie"))
                    _httpClient.DefaultRequestHeaders.Remove("Cookie");

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", string.Join("; ", cookiePairs));

                LogBL.Debug($"[SAP-LOGIN] Cookies usadas: {string.Join("; ", cookiePairs)}", "UdoIntegrationHttpClient");
            }

            var isSuccess = !string.IsNullOrEmpty(result?.SessionId);

            stopwatch.Stop();

            if (!isSuccess)
            {
                LogBL.Debug($"[SAP-LOGIN] Login fallido - SessionId vacío - Duración: {stopwatch.ElapsedMilliseconds}ms", "UdoIntegrationHttpClient");
                return false;
            }

            lock (_lockObject)
            {
                _cachedSessionId = result!.SessionId;

                var sessionTimeoutMinutes = result.SessionTimeout > 0 ? result.SessionTimeout : 30;
                _sessionExpiresAt = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes - 1);
            }

            LogBL.Debug($"[SAP-LOGIN] Login exitoso - SessionId: {_cachedSessionId} - Expira: {_sessionExpiresAt:yyyy-MM-dd HH:mm:ss} - Duración: {stopwatch.ElapsedMilliseconds}ms", "UdoIntegrationHttpClient");
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogBL.Debug($"[SAP-LOGIN] Error en login - Excepción: {ex.Message} - Duración: {stopwatch.ElapsedMilliseconds}ms", "UdoIntegrationHttpClient");
            return false;
        }
    }

}
