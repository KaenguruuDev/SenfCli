namespace SenfCli.Handlers;

public static class KeyCommandHandler
{
	public static async Task ListSshKeys(string? profileName = null)
	{
		var config = Config.Load();
		var profile = ResolveProfile(config, profileName);
		ValidateProfile(profile);

		var authHandler = new SshAuthHandler(profile!.SshKeyPath, profile.Username);
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
			ConsoleHelper.WriteDetail($"Fingerprint: {SshAuthHandler.GetPublicKeyFingerprint(key.PublicKey ?? "")}");
			ConsoleHelper.WriteDetail($"Created: {key.CreatedAt:yyyy-MM-dd HH:mm:ss}");
			ConsoleHelper.WriteInfo("");
		}
	}

	public static async Task AddSshKey(string publicKey, string keyName, string? profileName = null)
	{
		var config = Config.Load();
		var profile = ResolveProfile(config, profileName);
		ValidateProfile(profile);

		if (string.IsNullOrWhiteSpace(publicKey))
		{
			ConsoleHelper.WriteError("Public key cannot be empty.");
			Environment.Exit(1);
		}

		if (!TryValidatePublicKeyForOnboarding(publicKey, out var validationError))
		{
			ConsoleHelper.WriteError(validationError);
			ConsoleHelper.WriteDetail("Supported formats: ssh-ed25519.");
			Environment.Exit(1);
		}

		var authHandler = new SshAuthHandler(profile!.SshKeyPath, profile.Username);
		var client = new SenfApiClient(profile.ApiUrl, authHandler);

		var response = await client.CreateSshKeyAsync(publicKey, keyName);

		if (response != null)
		{
			ConsoleHelper.WriteSuccess($"SSH key '{keyName}' added successfully");
			ConsoleHelper.WriteDetail($"ID: {response.Id}");
			ConsoleHelper.WriteDetail($"Fingerprint: {SshAuthHandler.GetPublicKeyFingerprint(response.PublicKey ?? "")}");
		}
	}

	public static async Task DeleteSshKey(int keyId, string? profileName = null)
	{
		var config = Config.Load();
		var profile = ResolveProfile(config, profileName);
		ValidateProfile(profile);

		var authHandler = new SshAuthHandler(profile!.SshKeyPath, profile.Username);
		var client = new SenfApiClient(profile.ApiUrl, authHandler);

		await client.DeleteSshKeyAsync(keyId);
		ConsoleHelper.WriteSuccess($"SSH key {keyId} deleted successfully");
	}

	private static SshProfile? ResolveProfile(Config config, string? profileName)
	{
		if (!string.IsNullOrEmpty(profileName))
		{
			var profile = config.GetProfile(profileName);
			if (profile == null)
			{
				ConsoleHelper.WriteError($"Profile '{profileName}' not found.");
				ConsoleHelper.WriteDetail("Run 'senf profile list' to see available profiles.");
				Environment.Exit(1);
			}

			return profile;
		}

		if (!string.IsNullOrEmpty(config.DefaultProfile))
			return config.GetProfile(config.DefaultProfile);

		return config.Profiles.TryGetValue("default", out var defaultProfile) ? defaultProfile : null;
	}

	private static void ValidateProfile(SshProfile? profile)
	{
		if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
			string.IsNullOrWhiteSpace(profile.SshKeyPath) ||
			profile.SshKeyId < 0)
		{
			ConsoleHelper.WriteError("No valid profile configured.");
			ConsoleHelper.WriteDetail(
				"Run 'senf profile set <profile-name> --username <username> --ssh-key <path> --api-url <url>' first.");
			ConsoleHelper.WriteDetail("Profile must have a verified SSH key ID.");
			Environment.Exit(1);
		}
	}

	private static bool TryValidatePublicKeyForOnboarding(string publicKey, out string error)
		=> SshKeyValidation.TryValidateEd25519PublicKey(publicKey, out error);
}