namespace SenfCli.Handlers;

public static class ProfileCommandHandler
{
	public static async Task CreateOrUpdateProfile(string profileName, string? username = null,
		string? sshKeyPath = null, string? apiUrl = null, bool setAsDefault = false)
	{
		var config = Config.Load();
		if (!config.Profiles.TryGetValue(profileName, out var profile))
		{
			profile = new SshProfile();
			config.Profiles[profileName] = profile;
		}

		var credentialsChanged = false;

		if (!string.IsNullOrEmpty(username))
		{
			profile.Username = username;
			credentialsChanged = true;
		}

		if (!string.IsNullOrEmpty(sshKeyPath))
		{
			if (!File.Exists(sshKeyPath))
			{
				ConsoleHelper.WriteError($"SSH key not found at: {sshKeyPath}");
				Environment.Exit(1);
			}

			profile.SshKeyPath = sshKeyPath;
			credentialsChanged = true;
		}

		if (!string.IsNullOrEmpty(apiUrl))
		{
			if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var parsedUri) ||
			    parsedUri.Scheme != Uri.UriSchemeHttps)
			{
				ConsoleHelper.WriteError("API URL must be a valid HTTPS URL (e.g., https://api.example.com).");
				Environment.Exit(1);
			}

			profile.ApiUrl = apiUrl;
			credentialsChanged = true;
		}

		if (credentialsChanged)
			profile.SshKeyId = -1;

		if (setAsDefault)
			config.DefaultProfile = profileName;

		if (!string.IsNullOrEmpty(profile.Username) &&
		    !string.IsNullOrEmpty(profile.SshKeyPath) &&
		    !string.IsNullOrEmpty(profile.ApiUrl))
		{
			try
			{
				ConsoleHelper.WriteInfo("Verifying SSH key with backend...");
				var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
				var ourFingerprint = authHandler.GetPublicKeyString();

				var client = new SenfApiClient(profile.ApiUrl, authHandler);
				var response = await client.GetSshKeysAsync();
				if (response?.Keys == null || response.Keys.Count == 0)
				{
					ConsoleHelper.WriteError("SSH key verification failed: backend returned no registered SSH keys.");
					Environment.Exit(1);
				}

				var matchingKey = response.Keys.FirstOrDefault(k => k.Fingerprint == ourFingerprint);
				if (matchingKey == null)
				{
					ConsoleHelper.WriteError("SSH key verification failed: no matching key found in backend.");
					ConsoleHelper.WriteDetail("Register the corresponding public key, then run 'senf profile set' again.");
					Environment.Exit(1);
				}

				profile.SshKeyId = matchingKey.Id;
				ConsoleHelper.WriteSuccess($"SSH key verified (Key ID: {matchingKey.Id})");
			}
			catch (Exception ex)
			{
				ConsoleHelper.WriteError($"Failed to verify SSH key with backend: {ex.Message}");
				Environment.Exit(1);
			}
		}

		config.Save();

		ConsoleHelper.WriteSuccess($"Profile '{profileName}' configured successfully");
		if (!string.IsNullOrEmpty(username))
			ConsoleHelper.WriteDetail($"Username: {username}");

		if (!string.IsNullOrEmpty(sshKeyPath))
			ConsoleHelper.WriteDetail($"SSH Key: {sshKeyPath}");

		if (!string.IsNullOrEmpty(apiUrl))
			ConsoleHelper.WriteDetail($"API URL: {apiUrl}");

		ConsoleHelper.WriteDetail($"SSH Key ID: {profile.SshKeyId}");

		if (setAsDefault)
			ConsoleHelper.WriteDetail("Set as default profile");
	}

	public static void ListProfiles()
	{
		var config = Config.Load();

		if (config.Profiles.Count == 0)
		{
			ConsoleHelper.WriteInfo("No profiles configured");
			ConsoleHelper.WriteDetail(
				"Run 'senf profile set <name> --username <username> --ssh-key <path>' to create one.");
			return;
		}

		ConsoleHelper.WriteSuccess($"Found {config.Profiles.Count} profile(s):");
		foreach (var (name, profile) in config.Profiles)
		{
			var isDefault = name == config.DefaultProfile;
			var marker = isDefault ? " (default)" : "";
			ConsoleHelper.WriteInfo($"{name}{marker}");
			ConsoleHelper.WriteDetail($"Username: {profile.Username}");
			ConsoleHelper.WriteDetail($"SSH Key: {profile.SshKeyPath}");
			ConsoleHelper.WriteDetail($"API URL: {profile.ApiUrl}");
			ConsoleHelper.WriteDetail($"SSH Key ID: {profile.SshKeyId}");
		}
	}

	public static void DeleteProfile(string profileName)
	{
		var config = Config.Load();

		if (!config.Profiles.ContainsKey(profileName))
		{
			ConsoleHelper.WriteError($"Profile '{profileName}' not found");
			Environment.Exit(1);
		}

		var affectedProfiles = config.Projects.Where(p => p.ProfileName == profileName).ToArray();

		ConsoleHelper.WriteWarning(
			$"Deleting '{profileName}' will require re-configuration of {affectedProfiles.Length} profile(s)");
		ConsoleHelper.Ask("Are you sure you want to overwrite them? (Y/n): ");
		var response = Console.ReadLine()?.Trim().ToLower();
		if (response != "y" && response != "yes" && response != "")
		{
			ConsoleHelper.WriteInfo("Cancel.");
			return;
		}

		config.Profiles.Remove(profileName);
		if (config.DefaultProfile == profileName)
			config.DefaultProfile = null;

		foreach (var project in affectedProfiles)
			project.ProfileName = config.Profiles.First().Key;

		config.Save();

		ConsoleHelper.WriteSuccess($"Profile '{profileName}' deleted successfully");
	}

	public static void SetDefaultProfile(string profileName)
	{
		var config = Config.Load();

		if (!config.Profiles.ContainsKey(profileName))
		{
			ConsoleHelper.WriteError($"Profile '{profileName}' not found");
			Environment.Exit(1);
		}

		config.DefaultProfile = profileName;
		config.Save();

		ConsoleHelper.WriteSuccess($"Default profile set to '{profileName}'");
	}
}