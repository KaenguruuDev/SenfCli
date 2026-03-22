using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenfCli;


public class SenfApiClient
{
	private readonly HttpClient _httpClient;
	private readonly string _apiUrl;
	private readonly SshAuthHandler? _sshAuthHandler;

	       public SenfApiClient(string apiUrl, SshAuthHandler authHandler)
	       {
		       _apiUrl = apiUrl;
		       _sshAuthHandler = authHandler;
		       _httpClient = CreateHttpClient(apiUrl);
	       }

	       public SenfApiClient(string apiUrl)
	       {
		       _apiUrl = apiUrl;
		       _sshAuthHandler = null;
		       _httpClient = CreateHttpClient(apiUrl);
	       }

	public async Task<EnvFileResponse?> GetEnvFileAsync(string name)
	{
		var response = await SendWithAuthRetryAsync(() =>
			new HttpRequestMessage(HttpMethod.Get, $"/env?name={Uri.EscapeDataString(name)}"));

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
		var response = await SendWithAuthRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, "/keys"));

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

	public async Task<UsersListResponse?> GetUsersAsync()
	{
		var response = await SendWithAuthRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, "/users"));

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to get users", response.StatusCode, errorContent);
		}

		var json = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<UsersListResponse>(json);
	}

	public async Task DeleteUserAsync(int userId)
	{
		var response = await SendWithAuthRetryAsync(() =>
			new HttpRequestMessage(HttpMethod.Delete, $"/users/{userId}"));

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to delete user", response.StatusCode, errorContent);
		}
	}

	public async Task<InviteCreateResponse?> CreateInviteAsync()
	{
		var response = await SendWithAuthRetryAsync(() =>
			new HttpRequestMessage(HttpMethod.Post, "/invites"));

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to create invite", response.StatusCode, errorContent);
		}

		var json = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<InviteCreateResponse>(json);
	}

	public async Task<InviteListResponse?> ListInvitesAsync()
	{
		var response = await SendWithAuthRetryAsync(() =>
			new HttpRequestMessage(HttpMethod.Get, "/invites"));

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to list invites", response.StatusCode, errorContent);
		}

		var json = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<InviteListResponse>(json);
	}

	public async Task DeleteInviteAsync(string token)
	{
		var response = await SendWithAuthRetryAsync(() =>
			new HttpRequestMessage(HttpMethod.Delete, $"/invites/{Uri.EscapeDataString(token)}"));

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to delete invite", response.StatusCode, errorContent);
		}
	}

	public async Task<JoinResponse?> JoinWithInviteAsync(JoinRequest joinRequest)
	{
		var body = JsonSerializer.Serialize(joinRequest);
		var response = await SendWithoutAuthAsync(() =>
		{
			var request = new HttpRequestMessage(HttpMethod.Post, "/join");
			request.Content = new StringContent(body, Encoding.UTF8, "application/json");
			return request;
		});

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to join with invite", response.StatusCode, errorContent);
		}

		var json = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<JoinResponse>(json);
	}

	public async Task<ShareResponse?> ShareEnvFileAsync(int envFileId, int shareToUserId, ShareMode shareMode)
	{
		var body = JsonSerializer.Serialize(new { envFileId, shareToUserId, shareMode });
		var response = await SendWithAuthRetryAsync(() =>
		{
			var request = new HttpRequestMessage(HttpMethod.Post, "/share");
			request.Content = new StringContent(body, Encoding.UTF8, "application/json");
			return request;
		});

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to create share", response.StatusCode, errorContent);
		}

		var json = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<ShareResponse>(json);
	}

	public async Task RemoveShareAsync(int envFileId, int shareToUserId)
	{
		var body = JsonSerializer.Serialize(new { envFileId, shareToUserId });
		var response = await SendWithAuthRetryAsync(() =>
		{
			var request = new HttpRequestMessage(HttpMethod.Delete, "/share");
			request.Content = new StringContent(body, Encoding.UTF8, "application/json");
			return request;
		});

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to remove share", response.StatusCode, errorContent);
		}
	}

	public async Task<SharesListResponse?> GetActiveSharesAsync()
	{
		var response = await SendWithAuthRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, "/shares"));

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to get shares", response.StatusCode, errorContent);
		}

		var json = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<SharesListResponse>(json);
	}

	public async Task<SharedEnvFilesListResponse?> GetSharedFilesAsync()
	{
		var response = await SendWithAuthRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, "/shared"));

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to get shared env files", response.StatusCode, errorContent);
		}

		var json = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<SharedEnvFilesListResponse>(json);
	}

	public async Task<SharedEnvFileResponse?> GetSharedFileAsync(int shareId)
	{
		var response = await SendWithAuthRetryAsync(() =>
			new HttpRequestMessage(HttpMethod.Get, $"/shared/{shareId}"));

		if (response.StatusCode == HttpStatusCode.NotFound)
			return null;

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to get shared env file", response.StatusCode, errorContent);
		}

		var json = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<SharedEnvFileResponse>(json);
	}

	public async Task UpdateSharedFileAsync(int shareId, string content)
	{
		var body = JsonSerializer.Serialize(new { content });
		var response = await SendWithAuthRetryAsync(() =>
		{
			var request = new HttpRequestMessage(HttpMethod.Patch, $"/shared/{shareId}");
			request.Content = new StringContent(body, Encoding.UTF8, "application/json");
			return request;
		});

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new SenfApiException("Failed to update shared env file", response.StatusCode, errorContent);
		}
	}

	       private async Task<HttpResponseMessage> SendWithAuthRetryAsync(Func<HttpRequestMessage> requestFactory)
	       {
		       EnsureAuthConfigured();
		       var request = requestFactory();
		       _sshAuthHandler!.AddAuthHeaders(request);
		       return await _httpClient.SendAsync(request);
	       }

	       private async Task<HttpResponseMessage> SendWithoutAuthAsync(Func<HttpRequestMessage> requestFactory)
	       {
		       var request = requestFactory();
		       return await _httpClient.SendAsync(request);
	       }

	private void EnsureAuthConfigured()
	{
		if (_sshAuthHandler == null)
			throw new InvalidOperationException("This request requires SSH authentication.");
	}

	       private static HttpClient CreateHttpClient(string apiUrl)
	       {
		       if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
			       throw new InvalidOperationException("Invalid API URL.");

		       if (uri.Scheme == Uri.UriSchemeHttp)
			       ConsoleHelper.WriteWarning("The connected profile is configured with an unsecured HTTP api.");

		       return new HttpClient { BaseAddress = uri };
	       }
}

public class SenfApiException(string message, HttpStatusCode statusCode, string responseBody)
	: Exception(FormatErrorMessage(message, statusCode, responseBody))
{
	public HttpStatusCode StatusCode { get; } = statusCode;
	public string ResponseBody { get; } = responseBody;
	public string? ErrorMessage { get; } = ExtractErrorMessage(responseBody);

	private static string FormatErrorMessage(string message, HttpStatusCode statusCode, string responseBody)
	{
		var errorMsg = ExtractErrorMessage(responseBody);
		var statusCodeNumber = (int)statusCode;
		var friendlyError = MapUserFriendlyAuthError(statusCode, errorMsg);

		if (!string.IsNullOrEmpty(friendlyError))
			return $"{statusCodeNumber} {statusCode} - {friendlyError}";

		return !string.IsNullOrEmpty(errorMsg)
			? $"{statusCodeNumber} {statusCode} - {errorMsg}"
			: $"{statusCodeNumber} {statusCode} - {message}";
	}

	private static string? MapUserFriendlyAuthError(HttpStatusCode statusCode, string? errorMsg)
	{
		if (string.IsNullOrWhiteSpace(errorMsg))
			return null;

		var normalized = errorMsg.ToLowerInvariant();

		if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
		{
			if (normalized.Contains("invalid signature") ||
				normalized.Contains("request-signing") ||
				normalized.Contains("signature mismatch"))
			{
				return "Authentication failed: invalid signature (request-signing mismatch).";
			}

			if (normalized.Contains("nonce already used") || normalized.Contains("replay"))
			{
				return "Authentication failed: nonce already used. Retry so the request is signed again.";
			}
		}

		if (normalized.Contains("unsupported key format") || normalized.Contains("unsupported key") ||
			normalized.Contains("invalid key format"))
			return "SSH key rejected: unsupported key format. Use ssh-ed25519.";

		return null;
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

public class InviteCreateResponse
{
	[JsonPropertyName("token")]
	public string? Token { get; set; }

	[JsonPropertyName("relativeJoinUrl")]
	public string? RelativeJoinUrl { get; set; }

	[JsonPropertyName("expiresAt")]
	public DateTime ExpiresAt { get; set; }
}

public class InviteListResponse
{
	[JsonPropertyName("invites")]
	public List<InviteSummaryResponse> Invites { get; set; } = [];
}

public class InviteSummaryResponse
{
	[JsonPropertyName("token")]
	public string? Token { get; set; }

	[JsonPropertyName("createdByUserId")]
	public int CreatedByUserId { get; set; }

	[JsonPropertyName("createdByUsername")]
	public string? CreatedByUsername { get; set; }

	[JsonPropertyName("createdAt")]
	public DateTime CreatedAt { get; set; }

	[JsonPropertyName("expiresAt")]
	public DateTime ExpiresAt { get; set; }

	[JsonPropertyName("usedAt")]
	public DateTime? UsedAt { get; set; }
}

public class JoinRequest
{
	[JsonPropertyName("token")]
	public string? Token { get; set; }

	[JsonPropertyName("username")]
	public string? Username { get; set; }

	[JsonPropertyName("publicKeys")]
	public List<string> PublicKeys { get; set; } = [];
}

public class JoinResponse
{
	[JsonPropertyName("userId")]
	public int UserId { get; set; }

	[JsonPropertyName("username")]
	public string? Username { get; set; }
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

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("createdAt")]
	public DateTime CreatedAt { get; set; }
}

public class SshKeysListResponse
{
	[JsonPropertyName("keys")]
	public List<SshKeyResponse> Keys { get; set; } = [];
}

public class UsersListResponse
{
	[JsonPropertyName("users")]
	public List<UserSummaryResponse> Users { get; set; } = [];
}

public class UserSummaryResponse
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("username")]
	public string? Username { get; set; }
}

public enum ShareMode
{
	ReadOnly = 0,
	ReadWrite = 1
}

public class ShareResponse
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("envFileId")]
	public int EnvFileId { get; set; }

	[JsonPropertyName("envFileName")]
	public string? EnvFileName { get; set; }

	[JsonPropertyName("sharedToUserId")]
	public int SharedToUserId { get; set; }

	[JsonPropertyName("sharedToUsername")]
	public string? SharedToUsername { get; set; }

	[JsonPropertyName("shareMode")]
	public ShareMode ShareMode { get; set; }

	[JsonPropertyName("createdAt")]
	public DateTime CreatedAt { get; set; }

	[JsonPropertyName("updatedAt")]
	public DateTime UpdatedAt { get; set; }
}

public class SharesListResponse
{
	[JsonPropertyName("shares")]
	public List<ShareResponse> Shares { get; set; } = [];
}

public class SharedEnvFileResponse
{
	[JsonPropertyName("shareId")]
	public int ShareId { get; set; }

	[JsonPropertyName("envFileId")]
	public int EnvFileId { get; set; }

	[JsonPropertyName("envFileName")]
	public string? EnvFileName { get; set; }

	[JsonPropertyName("ownerUserId")]
	public int OwnerUserId { get; set; }

	[JsonPropertyName("ownerUsername")]
	public string? OwnerUsername { get; set; }

	[JsonPropertyName("content")]
	public string? Content { get; set; }

	[JsonPropertyName("lastUpdatedByKeyId")]
	public int LastUpdatedByKeyId { get; set; }

	[JsonPropertyName("shareMode")]
	public ShareMode ShareMode { get; set; }

	[JsonPropertyName("sharedAt")]
	public DateTime SharedAt { get; set; }

	[JsonPropertyName("updatedAt")]
	public DateTime UpdatedAt { get; set; }
}

public class SharedEnvFilesListResponse
{
	[JsonPropertyName("files")]
	public List<SharedEnvFileResponse> Files { get; set; } = [];
}