using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Utilities;

namespace SenfCli;

public class SshAuthHandler(string sshKeyPath, string username)
{
	private Ed25519PrivateKeyParameters? _privateKey;
	private string? _publicKeyOpenSsh;

	public string GetPublicKeyFingerprint()
	{
		EnsureKeyLoaded();
		
		try
		{
			var parts = _publicKeyOpenSsh!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2)
				return string.Empty;
			
			var base64 = Regex.Unescape(parts[1]);
			var keyBytes = Convert.FromBase64String(base64);
			var hash = SHA256.HashData(keyBytes);
			return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
		}
		catch
		{
			return string.Empty;
		}
	}
	
	public static string GetPublicKeyFingerprint(string publicKey)
	{
		try
		{
			var parts = publicKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2)
				return string.Empty;
			
			var base64 = Regex.Unescape(parts[1]);
			var keyBytes = Convert.FromBase64String(base64);
			var hash = SHA256.HashData(keyBytes);
			return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
		}
		catch
		{
			return string.Empty;
		}
	}

	public string GetPublicKey()
	{
		EnsureKeyLoaded();
		return _publicKeyOpenSsh!;
	}

	public void AddAuthHeaders(HttpRequestMessage request)
	{
		var nonce = Guid.NewGuid().ToString("N");
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var method = request.Method.Method.ToUpperInvariant();
		var pathAndQuery = BuildPathAndQuery(request);
		var message = $"{timestamp}:{nonce}:{method}:{pathAndQuery}";

		var signature = Sign(sshKeyPath, message);

		request.Headers.Add("X-SSH-Username", username);
		request.Headers.Add("X-SSH-Message", timestamp.ToString());
		request.Headers.Add("X-SSH-Nonce", nonce);
		request.Headers.Add("X-SSH-Signature", signature);
	}

	private static string BuildPathAndQuery(HttpRequestMessage request)
	{
		if (request.RequestUri is null)
			throw new InvalidOperationException("Request URI is required for SSH request signing.");

		var uri = request.RequestUri;
		if (uri.IsAbsoluteUri)
			return uri.PathAndQuery;

		var raw = uri.OriginalString;
		if (string.IsNullOrWhiteSpace(raw))
			return "/";

		return raw.StartsWith('/') ? raw : "/" + raw;
	}

	private static string Sign(string privateKeyPath, string message)
	{
		var pem = new Org.BouncyCastle.Utilities.IO.Pem.PemReader(File.OpenText(privateKeyPath)).ReadPemObject();

		var keyParams = OpenSshPrivateKeyUtilities.ParsePrivateKeyBlob(pem.Content);

		if (keyParams is not Ed25519PrivateKeyParameters key)
			throw new InvalidDataException("Key is not Ed25519");

		var signer = new Ed25519Signer();
		signer.Init(true, key);

		var messageBytes = Encoding.UTF8.GetBytes(message);
		signer.BlockUpdate(messageBytes, 0, messageBytes.Length);

		var signature = signer.GenerateSignature();

		return Convert.ToBase64String(signature);
	}

	private void EnsureKeyLoaded()
	{
		if (_privateKey != null)
			return;

		var pem = new Org.BouncyCastle.Utilities.IO.Pem.PemReader(File.OpenText(sshKeyPath)).ReadPemObject();
		var keyParams = OpenSshPrivateKeyUtilities.ParsePrivateKeyBlob(pem.Content);

		if (keyParams is not Ed25519PrivateKeyParameters key)
			throw new InvalidDataException("Key is not Ed25519");

		_privateKey = key;
		_publicKeyOpenSsh = File.ReadAllText($"{sshKeyPath}.pub").Trim();
	}
}