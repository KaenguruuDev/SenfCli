using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenfCli;

public class SenfApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SshAuthHandler _authHandler;

    public SenfApiClient(string apiUrl, SshAuthHandler authHandler)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _authHandler = authHandler;
    }

    public async Task<EnvFileResponse?> GetEnvFileAsync(string name)
    {
        var response = await SendWithAuthRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"/env?name={Uri.EscapeDataString(name)}"));

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new SenfApiException("Failed to get env file", response.StatusCode, errorContent);
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<EnvFileResponse>(json);
    }

    public async Task UpdateEnvFileAsync(string name, string content)
    {
        var body = JsonSerializer.Serialize(new { content });
        var response = await SendWithAuthRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, $"/env?name={Uri.EscapeDataString(name)}");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return request;
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new SenfApiException("Failed to update env file", response.StatusCode, errorContent);
        }
    }

    public async Task CreateOrReplaceEnvFileAsync(string name, string content)
    {
        var body = JsonSerializer.Serialize(new { content });
        var response = await SendWithAuthRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/env?name={Uri.EscapeDataString(name)}");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return request;
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new SenfApiException("Failed to create/replace env file", response.StatusCode, errorContent);
        }
    }

    public async Task<SshKeysListResponse?> GetSshKeysAsync()
    {
        var response = await SendWithAuthRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "/keys"));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new SenfApiException("Failed to get SSH keys", response.StatusCode, errorContent);
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SshKeysListResponse>(json);
    }

    public async Task<SshKeyResponse?> CreateSshKeyAsync(string publicKey, string name)
    {
        var body = JsonSerializer.Serialize(new { publicKey, name });
        var response = await SendWithAuthRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/keys");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return request;
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new SenfApiException("Failed to create SSH key", response.StatusCode, errorContent);
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SshKeyResponse>(json);
    }

    public async Task DeleteSshKeyAsync(int keyId)
    {
        var response = await SendWithAuthRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Delete, $"/keys?keyId={keyId}"));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new SenfApiException("Failed to delete SSH key", response.StatusCode, errorContent);
        }
    }

    private async Task<HttpResponseMessage> SendWithAuthRetryAsync(Func<HttpRequestMessage> requestFactory)
    {
        var firstAttempt = requestFactory();
        _authHandler.AddAuthHeaders(firstAttempt, SignatureEncodingMode.BackendCompatible);
        var response = await _httpClient.SendAsync(firstAttempt);

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();

        var secondAttempt = requestFactory();
        _authHandler.AddAuthHeaders(secondAttempt, SignatureEncodingMode.SshBlob);
        var secondResponse = await _httpClient.SendAsync(secondAttempt);

        if (secondResponse.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return secondResponse;

        secondResponse.Dispose();

        var thirdAttempt = requestFactory();
        _authHandler.AddAuthHeaders(thirdAttempt, SignatureEncodingMode.Raw);
        return await _httpClient.SendAsync(thirdAttempt);
    }
}

public class SenfApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }
    public string? ErrorMessage { get; }

    public SenfApiException(string message, HttpStatusCode statusCode, string responseBody)
        : base(FormatErrorMessage(message, statusCode, responseBody))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ErrorMessage = ExtractErrorMessage(responseBody);
    }

    private static string FormatErrorMessage(string message, HttpStatusCode statusCode, string responseBody)
    {
        var errorMsg = ExtractErrorMessage(responseBody);
        var statusCodeNumber = (int)statusCode;

        if (!string.IsNullOrEmpty(errorMsg))
            return $"{statusCodeNumber} {statusCode} - {errorMsg}";

        return $"{statusCodeNumber} {statusCode} - {message}";
    }

    private static string? ExtractErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorProp))
                return errorProp.GetString();

            if (root.TryGetProperty("message", out var messageProp))
                return messageProp.GetString();
        }
        catch
        {
            // If JSON parsing fails, return the raw body if it's short
            if (responseBody.Length < 100)
                return responseBody;
        }

        return null;
    }
}

public class EnvFileResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class SshKeyResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("publicKey")]
    public string? PublicKey { get; set; }

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class SshKeysListResponse
{
    [JsonPropertyName("keys")]
    public List<SshKeyResponse> Keys { get; set; } = new();
}
