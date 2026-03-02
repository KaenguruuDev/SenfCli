namespace SenfCli.Handlers;

public static class KeyCommandHandler
{
	public static async Task ListSshKeys(string? profileName = null)
	{
		var config = Config.Load();

		SshProfile? profile = null;
		if (!string.IsNullOrEmpty(profileName))
		{
			profile = config.GetProfile(profileName);
			if (profile == null)
			{
				ConsoleHelper.WriteError($"Profile '{profileName}' not found.");
				ConsoleHelper.WriteDetail("Run 'senf profile list' to see available profiles.");
				Environment.Exit(1);
			}
		}
		else
		{
			// Use default profile
			if (!string.IsNullOrEmpty(config.DefaultProfile))
			{
				profile = config.GetProfile(config.DefaultProfile);
			}
			else if (config.Profiles.TryGetValue("default", out var defaultProfile))
			{
				profile = defaultProfile;
			}
		}

		if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
		    string.IsNullOrWhiteSpace(profile.SshKeyPath))
		{
			ConsoleHelper.WriteError("No valid profile configured.");
			ConsoleHelper.WriteDetail(
				"Run 'senf profile set <profile-name> --username <username> --ssh-key <path> --api-url <url>' first.");
			Environment.Exit(1);
		}

		var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
		var client = new SenfApiClient(profile.ApiUrl, authHandler);

		var response = await client.GetSshKeysAsync();

		if (response?.Keys == null || response.Keys.Count == 0)
		{
			ConsoleHelper.WriteInfo("No SSH keys configured");
			return;
		}

		ConsoleHelper.WriteSuccess($"Found {response.Keys.Count} SSH key(s):");
		foreach (var key in response.Keys)
		{
			ConsoleHelper.WriteInfo($"ID: {key.Id}");
			ConsoleHelper.WriteDetail($"Name: {key.Name}");
			ConsoleHelper.WriteDetail($"Fingerprint: {key.Fingerprint}");
			ConsoleHelper.WriteDetail($"Created: {key.CreatedAt:yyyy-MM-dd HH:mm:ss}");
			ConsoleHelper.WriteInfo("");
		}
	}

	public static async Task AddSshKey(string publicKey, string keyName, string? profileName = null)
	{
		var config = Config.Load();

		// Get the profile (specified or default)
		SshProfile? profile = null;
		if (!string.IsNullOrEmpty(profileName))
		{
			profile = config.GetProfile(profileName);
			if (profile == null)
			{
				ConsoleHelper.WriteError($"Profile '{profileName}' not found.");
				ConsoleHelper.WriteDetail("Run 'senf profile list' to see available profiles.");
				Environment.Exit(1);
			}
		}
		else
		{
			// Use default profile
			if (!string.IsNullOrEmpty(config.DefaultProfile))
			{
				profile = config.GetProfile(config.DefaultProfile);
			}
			else if (config.Profiles.TryGetValue("default", out var defaultProfile))
			{
				profile = defaultProfile;
			}
		}

		if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
		    string.IsNullOrWhiteSpace(profile.SshKeyPath))
		{
			ConsoleHelper.WriteError("No valid profile configured.");
			ConsoleHelper.WriteDetail(
				"Run 'senf profile set <profile-name> --username <username> --ssh-key <path> --api-url <url>' first.");
			Environment.Exit(1);
		}

		if (string.IsNullOrWhiteSpace(publicKey))
		{
			ConsoleHelper.WriteError("Public key cannot be empty.");
			Environment.Exit(1);
		}

		if (!TryValidatePublicKeyForOnboarding(publicKey, out var validationError))
		{
			ConsoleHelper.WriteError(validationError);
			ConsoleHelper.WriteDetail("Supported formats: ssh-ed25519, ssh-rsa (RSA >= 2048 bits).");
			Environment.Exit(1);
		}

		var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
		var client = new SenfApiClient(profile.ApiUrl, authHandler);

		var response = await client.CreateSshKeyAsync(publicKey, keyName);

		if (response != null)
		{
			ConsoleHelper.WriteSuccess($"SSH key '{keyName}' added successfully");
			ConsoleHelper.WriteDetail($"ID: {response.Id}");
			ConsoleHelper.WriteDetail($"Fingerprint: {response.Fingerprint}");
		}
	}

	public static async Task DeleteSshKey(int keyId, string? profileName = null)
	{
		var config = Config.Load();

		// Get the profile (specified or default)
		SshProfile? profile = null;
		if (!string.IsNullOrEmpty(profileName))
		{
			profile = config.GetProfile(profileName);
			if (profile == null)
			{
				ConsoleHelper.WriteError($"Profile '{profileName}' not found.");
				ConsoleHelper.WriteDetail("Run 'senf profile list' to see available profiles.");
				Environment.Exit(1);
			}
		}
		else
		{
			// Use default profile
			if (!string.IsNullOrEmpty(config.DefaultProfile))
			{
				profile = config.GetProfile(config.DefaultProfile);
			}
			else if (config.Profiles.TryGetValue("default", out var defaultProfile))
			{
				profile = defaultProfile;
			}
		}

		if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
		    string.IsNullOrWhiteSpace(profile.SshKeyPath))
		{
			ConsoleHelper.WriteError("No valid profile configured.");
			ConsoleHelper.WriteDetail(
				"Run 'senf profile set <profile-name> --username <username> --ssh-key <path> --api-url <url>' first.");
			Environment.Exit(1);
		}

		var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
		var client = new SenfApiClient(profile.ApiUrl, authHandler);

		await client.DeleteSshKeyAsync(keyId);
		ConsoleHelper.WriteSuccess($"SSH key {keyId} deleted successfully");
	}

	private static bool TryValidatePublicKeyForOnboarding(string publicKey, out string error)
	{
		error = string.Empty;

		var parts = publicKey.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2)
		{
			error = "Unsupported key format: expected '<type> <base64> [comment]'.";
			return false;
		}

		var keyType = parts[0];
		var keyData = parts[1];

		if (keyType == "ssh-ed25519")
			return true;

		if (keyType != "ssh-rsa")
		{
			error = $"Unsupported key format: '{keyType}'.";
			return false;
		}

		if (!TryGetRsaBits(keyData, out var bits))
		{
			error = "Unsupported key format: unable to parse RSA key payload.";
			return false;
		}

		if (bits < 2048)
		{
			error = $"RSA key too weak: {bits} bits detected, minimum is 2048 bits.";
			return false;
		}

		return true;
	}

	private static bool TryGetRsaBits(string keyDataBase64, out int bits)
	{
		bits = 0;
		byte[] data;
		try
		{
			data = Convert.FromBase64String(keyDataBase64);
		}
		catch
		{
			return false;
		}

		var offset = 0;
		if (!TryReadSshString(data, ref offset, out var typeBytes))
			return false;

		var type = System.Text.Encoding.ASCII.GetString(typeBytes);
		if (type != "ssh-rsa")
			return false;

		if (!TryReadMpInt(data, ref offset, out _))
			return false;

		if (!TryReadMpInt(data, ref offset, out var modulus))
			return false;

		var start = 0;
		while (start < modulus.Length && modulus[start] == 0)
			start++;

		if (start >= modulus.Length)
			return false;

		var first = modulus[start];
		var leadingZeroBits = 0;
		for (var i = 7; i >= 0; i--)
		{
			if (((first >> i) & 1) == 1)
				break;
			leadingZeroBits++;
		}

		bits = ((modulus.Length - start) * 8) - leadingZeroBits;
		return bits > 0;
	}

	private static bool TryReadSshString(byte[] data, ref int offset, out byte[] value)
	{
		value = Array.Empty<byte>();
		if (!TryReadUInt32(data, ref offset, out var len))
			return false;
		if (len < 0 || offset + len > data.Length)
			return false;

		value = new byte[len];
		Buffer.BlockCopy(data, offset, value, 0, len);
		offset += len;
		return true;
	}

	private static bool TryReadMpInt(byte[] data, ref int offset, out byte[] value)
		=> TryReadSshString(data, ref offset, out value);

	private static bool TryReadUInt32(byte[] data, ref int offset, out int value)
	{
		value = 0;
		if (offset + 4 > data.Length)
			return false;

		value = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
		offset += 4;
		return true;
	}
}