namespace SenfCli.Handlers;

public static class KeyCommandHandler
{
	public static async Task ListSshKeys(string? profileName = null)
	{
		try
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
				Console.WriteLine($"  ID: {key.Id}");
				Console.WriteLine($"  Name: {key.Name}");
				Console.WriteLine($"  Fingerprint: {key.Fingerprint}");
				Console.WriteLine($"  Created: {key.CreatedAt:yyyy-MM-dd HH:mm:ss}");
				Console.WriteLine();
			}
		}
		catch (SenfApiException ex)
		{
			ConsoleHelper.WriteError(ex.Message);
			Environment.Exit(1);
		}
		catch (Exception ex)
		{
			ConsoleHelper.WriteError($"Error listing SSH keys: {ex.Message}");
			Environment.Exit(1);
		}
	}

	public static async Task AddSshKey(string publicKey, string keyName, string? profileName = null)
	{
		try
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
		catch (SenfApiException ex)
		{
			ConsoleHelper.WriteError(ex.Message);
			Environment.Exit(1);
		}
		catch (Exception ex)
		{
			ConsoleHelper.WriteError($"Error adding SSH key: {ex.Message}");
			Environment.Exit(1);
		}
	}

	public static async Task DeleteSshKey(int keyId, string? profileName = null)
	{
		try
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
		catch (SenfApiException ex)
		{
			ConsoleHelper.WriteError(ex.Message);
			Environment.Exit(1);
		}
		catch (Exception ex)
		{
			ConsoleHelper.WriteError($"Error deleting SSH key: {ex.Message}");
			Environment.Exit(1);
		}
	}
}