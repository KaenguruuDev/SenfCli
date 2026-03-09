using Renci.SshNet;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SenfCli;

public class SshAuthHandler(string sshKeyPath, string username)
{
	private PrivateKeyFile? _keyFile;

	public void TestKeyLoad()
	{
		try
		{
			_ = GetKeyFile();
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to load SSH key from '{sshKeyPath}': {ex.Message}", ex);
		}
	}

	public string GetPublicKeyString()
	{
		var keyFile = GetKeyFile();

		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);

		var keyTypeString = keyFile.Key.ToString() ?? "ssh-rsa";
		var keyType = keyTypeString.Split(' ')[0];
		WriteString(writer, keyType);

		foreach (var param in keyFile.Key.Public)
			WriteBigInteger(writer, param);

		var publicKeyBytes = ms.ToArray();

		var hash = SHA256.HashData(publicKeyBytes);
		return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
	}

	private static void WriteString(BinaryWriter writer, string str)
	{
		var bytes = Encoding.UTF8.GetBytes(str);
		writer.Write(IPAddress.HostToNetworkOrder(bytes.Length));
		writer.Write(bytes);
	}

	private static void WriteBigInteger(BinaryWriter writer, Renci.SshNet.Common.BigInteger bigInt)
	{
		var bytes = bigInt.ToByteArray().Reverse().ToArray();

		var i = 0;
		while (i < bytes.Length - 1 && bytes[i] == 0 && bytes[i + 1] < 0x80)
			i++;

		var trimmedBytes = new byte[bytes.Length - i];
		Array.Copy(bytes, i, trimmedBytes, 0, trimmedBytes.Length);

		writer.Write(IPAddress.HostToNetworkOrder(trimmedBytes.Length));
		writer.Write(trimmedBytes);
	}

	private PrivateKeyFile GetKeyFile()
	{
		_keyFile ??= new PrivateKeyFile(sshKeyPath);
		return _keyFile;
	}

	public void AddAuthHeaders(HttpRequestMessage request)
	{
		var nonce = Guid.NewGuid().ToString("N");
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var method = request.Method.Method.ToUpperInvariant();
		var pathAndQuery = BuildPathAndQuery(request);
		var message = $"{timestamp}:{nonce}:{method}:{pathAndQuery}";

		var signature = SignWithSshKeygen(message);

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

	private static string GetSignatureNamespace()
	{
		var configured = Environment.GetEnvironmentVariable("SSH_SIGNATURE_NAMESPACE");
		return string.IsNullOrWhiteSpace(configured) ? "senf-api-auth" : configured.Trim();
	}

	private static bool ConfirmSshKeygenSigning()
	{
		ConsoleHelper.WriteWarning("ssh-keygen signing uses temporary files for the message/signature.");
		ConsoleHelper.Ask("Proceed with ssh-keygen signing? (y/N): ");
		var response = Console.ReadLine()?.Trim().ToLowerInvariant();
		return response is "y" or "yes";
	}

	private string SignWithSshKeygen(string message)
	{
		if (!ConfirmSshKeygenSigning())
			throw new InvalidOperationException("ssh-keygen signing was not confirmed by the user.");

		var tempFile = Path.GetTempFileName();
		var sigFile = tempFile + ".sig";

		try
		{
			File.WriteAllText(tempFile, message);

			var startInfo = new ProcessStartInfo
			{
				FileName = "ssh-keygen",
				Arguments = $"-Y sign -f \"{sshKeyPath}\" -n \"{GetSignatureNamespace()}\" \"{tempFile}\"",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using var process = Process.Start(startInfo);
			if (process == null)
				throw new InvalidOperationException("Failed to start ssh-keygen process.");

			process.WaitForExit(5000);
			if (!process.HasExited)
			{
				process.Kill(true);
				throw new InvalidOperationException("ssh-keygen signing timed out.");
			}

			var stderr = process.StandardError.ReadToEnd().Trim();
			if (process.ExitCode != 0)
				throw new InvalidOperationException($"ssh-keygen signing failed: {stderr}");

			if (!File.Exists(sigFile))
				throw new InvalidOperationException("ssh-keygen did not produce a signature file.");

			var armoredSignatureBytes = File.ReadAllBytes(sigFile);
			return Convert.ToBase64String(armoredSignatureBytes);
		}
		finally
		{
			if (File.Exists(tempFile))
				File.Delete(tempFile);
			if (File.Exists(sigFile))
				File.Delete(sigFile);
		}
	}
}