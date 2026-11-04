using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CakeOrders.Infrastructure.Rahkaran;

/// <summary>
/// Owns one authenticated Rahkaran session and the CookieContainer shared by
/// every Rahkaran HTTP client in this application process.
/// </summary>
public sealed class RahkaranAuthenticationSession : IDisposable
{
    private const string SessionPath = "Services/Framework/AuthenticationService.svc/session";
    private const string LoginPath = "Services/Framework/AuthenticationService.svc/login";
    private const string AuthenticationProbePath =
        "ReceiptAndPayment/TreasuryStructure/Services/TreasuryStructureService.svc/GetAllCashes";

    private readonly RahkaranOptions _options;
    private readonly HttpClient _authenticationClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _authenticated;

    public CookieContainer Cookies { get; }

    public RahkaranAuthenticationSession(
        CookieContainer cookies,
        IOptions<RahkaranOptions> options)
    {
        Cookies = cookies;
        _options = options.Value;

        var handler = new HttpClientHandler
        {
            CookieContainer = Cookies,
            UseCookies = true,
            AllowAutoRedirect = true
        };

        _authenticationClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120))
        };
    }

    public async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_authenticated)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_authenticated)
                return;

            if (!_options.Enabled)
                throw new InvalidOperationException(
                    "Rahkaran integration is disabled. Set Rahkaran:Enabled=true only after configuring credentials and validating the environment.");

            if (string.IsNullOrWhiteSpace(_options.Username) ||
                string.IsNullOrWhiteSpace(_options.Password))
                throw new InvalidOperationException(
                    "Rahkaran credentials are not configured. Set Rahkaran__Username and Rahkaran__Password as protected server environment variables.");

            if (!Uri.TryCreate(_options.ServiceBaseUrl, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
                throw new InvalidOperationException("Rahkaran:ServiceBaseUrl is not a valid absolute HTTP(S) URL.");

            // Step 1: obtain a fresh server session and its RSA public key.
            using var sessionResponse = await _authenticationClient.GetAsync(
                new Uri(baseUri, SessionPath), cancellationToken);
            sessionResponse.EnsureSuccessStatusCode();

            var session = await sessionResponse.Content.ReadFromJsonAsync<RahkaranSessionResponse>(
                cancellationToken: cancellationToken);

            if (session is null ||
                string.IsNullOrWhiteSpace(session.Id) ||
                string.IsNullOrWhiteSpace(session.Rsa?.M) ||
                string.IsNullOrWhiteSpace(session.Rsa?.E))
                throw new InvalidOperationException("Rahkaran session response did not contain id and RSA public key values.");

            // Step 2: encrypt sessionId + "**" + password using RSA PKCS#1 v1.5.
            byte[] encryptedPassword;
            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = Convert.FromHexString(session.Rsa.M),
                    Exponent = Convert.FromHexString(session.Rsa.E)
                });

                var plainText = session.Id + "**" + _options.Password;
                encryptedPassword = rsa.Encrypt(
                    Encoding.UTF8.GetBytes(plainText),
                    RSAEncryptionPadding.Pkcs1);
            }

            var loginPayload = new RahkaranLoginRequest
            {
                SessionId = session.Id,
                Username = _options.Username,
                Password = Convert.ToHexString(encryptedPassword)
            };

            // Step 3: login. HttpClientHandler stores Set-Cookie headers automatically.
            using var loginResponse = await _authenticationClient.PostAsJsonAsync(
                new Uri(baseUri, LoginPath), loginPayload, cancellationToken);
            loginResponse.EnsureSuccessStatusCode();

            // Step 4: verify the authenticated cookie against a protected endpoint.
            // A successful login HTTP response alone is not treated as proof of authentication.
            using var probeResponse = await _authenticationClient.GetAsync(
                new Uri(baseUri, AuthenticationProbePath), cancellationToken);
            if (probeResponse.StatusCode == HttpStatusCode.Unauthorized ||
                probeResponse.StatusCode == HttpStatusCode.Forbidden)
                throw new HttpRequestException(
                    $"Rahkaran login returned {(int)loginResponse.StatusCode}, but the protected GetAllCashes verification returned {(int)probeResponse.StatusCode}.");

            probeResponse.EnsureSuccessStatusCode();
            _authenticated = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _authenticated = false;

    public void Dispose()
    {
        _authenticationClient.Dispose();
        _gate.Dispose();
    }

    private sealed class RahkaranSessionResponse
    {
        public string? Id { get; init; }
        public RahkaranRsaKey? Rsa { get; init; }
    }

    private sealed class RahkaranRsaKey
    {
        public string? M { get; init; }
        public string? E { get; init; }
    }

    private sealed class RahkaranLoginRequest
    {
        public string SessionId { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }
}
