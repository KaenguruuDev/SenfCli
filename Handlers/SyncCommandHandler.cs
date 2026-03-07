using System.Net;

namespace SenfCli.Handlers;

public static class SyncCommandHandler
{
	private static SshProfile LoadAndValidateProfileForProject(Config config, ProjectConfig project)
	{
		var profile = config.GetProfileForProject(project);
		if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
		    string.IsNullOrWhiteSpace(profile.SshKeyPath))
		{
			ConsoleHelper.WriteError("SSH credentials not configured.");
			ConsoleHelper.WriteDetail(
				"Run 'senf profile set <profile-name> --username <username> --ssh-key <path>' first.");

			if (!string.IsNullOrEmpty(project.ProfileName))
				ConsoleHelper.WriteDetail($"This project uses profile: {project.ProfileName}");

			Environment.Exit(1);
		}

		return profile;
	}

	private static Dictionary<string, string?> ParseEnv(string content, out List<string> order)
	{
		var map = new Dictionary<string, string?>();
		order = new List<string>();
		using var reader = new StringReader(content ?? string.Empty);
		string? line;
		while ((line = reader.ReadLine()) != null)
		{
			line = line.Trim();
			if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
				continue;
			var idx = line.IndexOf('=');
			if (idx <= 0)
				continue;
			var key = line.Substring(0, idx).Trim();
			var val = line.Substring(idx + 1);
			if (!order.Contains(key)) order.Add(key);
			map[key] = val;
		}

		return map;
	}

	public static async Task Push()
	{
		try
		{
			var config = Config.Load();
			var project = config.GetCurrentProject();

			if (project == null)
			{
				ConsoleHelper.WriteError("No project found for current directory.");
				ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
				Environment.Exit(1);
			}

			var profile = LoadAndValidateProfileForProject(config, project);
			if (string.IsNullOrWhiteSpace(project.EnvPath) || !File.Exists(project.EnvPath))
			{
				ConsoleHelper.WriteError($"Env file not found: {project.EnvPath}");
				Environment.Exit(1);
			}

			var content = await File.ReadAllTextAsync(project.EnvPath);
			if (string.IsNullOrWhiteSpace(content))
			{
				ConsoleHelper.WriteError($"Env file is empty: {project.EnvPath}");
				ConsoleHelper.WriteDetail("Add at least one key=value line before pushing.");
				Environment.Exit(1);
			}

			var fileHash = Config.ComputeFileHash(project.EnvPath);

			var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
			var client = new SenfApiClient(profile.ApiUrl, authHandler);

			var latestRemote = await client.GetEnvFileAsync(project.ProjectName);
			var latestRemoteHash = latestRemote != null ? Config.ComputeStringHash(latestRemote.Content!) : null;

			var storedHashInfo = Config.GetFileHash(project.ProjectName, profile.SshKeyId)?.Hash;

			if (latestRemoteHash != null && storedHashInfo != null && latestRemoteHash != storedHashInfo)
			{
				ConsoleHelper.WriteWarning("The remote env file has changed since the last pull.");
				Console.Write("Choose (o)verwrite / (r)econcile / (c)ancel: ");
				var response = Console.ReadLine()?.Trim().ToLower();

				if (response == "r" || response == "reconcile")
				{
					await Reconcile();
					Console.Write("Do you want to push the merged file now? (y/n): ");
					var pushResp = Console.ReadLine()?.Trim().ToLower();
					if (pushResp != "y" && pushResp != "yes")
					{
						ConsoleHelper.WriteInfo("Push cancelled after reconcile.");
						return;
					}

					// Re-read content and recompute hash after reconcile
					content = await File.ReadAllTextAsync(project.EnvPath);
					fileHash = Config.ComputeFileHash(project.EnvPath);
				}
				else if (response == "o" || response == "overwrite")
				{
					// proceed with overwrite
				}
				else
				{
					ConsoleHelper.WriteInfo("Push cancelled.");
					return;
				}
			}

			try
			{
				await client.UpdateEnvFileAsync(project.ProjectName, content);
				ConsoleHelper.WriteSuccess($"Pushed env file for project '{project.ProjectName}'");

				if (profile.SshKeyId > -1)
					Config.SaveFileHash(project.ProjectName, profile.SshKeyId, fileHash);
			}
			catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				await client.CreateOrReplaceEnvFileAsync(project.ProjectName, content);
				ConsoleHelper.WriteSuccess($"Created project '{project.ProjectName}' and pushed env file");

				if (profile.SshKeyId > -1)
				{
					Config.SaveFileHash(project.ProjectName, profile.SshKeyId, fileHash);
				}
			}
		}
		catch (SenfApiException ex)
		{
			ConsoleHelper.WriteError(ex.Message);
			Environment.Exit(1);
		}
		catch (Exception ex)
		{
			ConsoleHelper.WriteError($"Error during push: {ex.Message}");
			Environment.Exit(1);
		}
	}

	public static async Task Pull()
	{
		try
		{
			var config = Config.Load();
			var project = config.GetCurrentProject();

			if (project == null)
			{
				ConsoleHelper.WriteError("No project found for current directory.");
				ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
				Environment.Exit(1);
			}

			var profile = LoadAndValidateProfileForProject(config, project);
			if (!File.Exists(profile.SshKeyPath))
			{
				ConsoleHelper.WriteError($"SSH key file not found: {profile.SshKeyPath}");
				Environment.Exit(1);
			}

			if (File.Exists(project.EnvPath) && profile.SshKeyId > -1)
			{
				var currentHash = Config.ComputeFileHash(project.EnvPath);
				var storedHashInfo = Config.GetFileHash(project.ProjectName, profile.SshKeyId);

				if (storedHashInfo != null && storedHashInfo.Hash != currentHash)
				{
					ConsoleHelper.WriteWarning("The contents of the env file were changed since the last push.");
					ConsoleHelper.WriteDetail($"Local file: {project.EnvPath}");
					Console.Write("Choose (o)verwrite / (r)econcile / (c)ancel: ");
					var response = Console.ReadLine()?.Trim().ToLower();

					if (response == "r" || response == "reconcile")
					{
						await Reconcile();
						ConsoleHelper.WriteInfo("Reconcile finished. Pull cancelled.");
						return;
					}

					if (response == "o" || response == "overwrite")
					{
						// proceed with overwrite
					}
					else
					{
						ConsoleHelper.WriteInfo("Pull cancelled.");
						return;
					}
				}
			}

			var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
			try
			{
				authHandler.TestKeyLoad();
			}
			catch (Exception ex)
			{
				ConsoleHelper.WriteError($"Failed to load SSH key: {ex.Message}");
				ConsoleHelper.WriteDetail($"Key path: {profile.SshKeyPath}");
				Environment.Exit(1);
			}

			var client = new SenfApiClient(profile.ApiUrl, authHandler);
			var envFile = await client.GetEnvFileAsync(project.ProjectName);

			if (envFile is null)
			{
				ConsoleHelper.WriteError($"Env file not found on server for project '{project.ProjectName}'");
				Environment.Exit(1);
			}

			var directory = Path.GetDirectoryName(project.EnvPath);
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory!);

			await File.WriteAllTextAsync(project.EnvPath, envFile.Content ?? string.Empty);
			if (profile.SshKeyId > -1)
			{
				var newHash = Config.ComputeFileHash(project.EnvPath);
				Config.SaveFileHash(project.ProjectName, profile.SshKeyId, newHash);
			}

			ConsoleHelper.WriteSuccess($"Pulled env file for project '{project.ProjectName}'");
			ConsoleHelper.WriteDetail($"Saved to: {project.EnvPath}");
		}
		catch (SenfApiException ex)
		{
			ConsoleHelper.WriteError(ex.Message);
			Environment.Exit(1);
		}
		catch (Exception ex)
		{
			ConsoleHelper.WriteError($"Error during pull: {ex.Message}");
			Environment.Exit(1);
		}
	}

	public static async Task Reconcile()
	{
		try
		{
			var config = Config.Load();
			var project = config.GetCurrentProject();

			if (project == null)
			{
				ConsoleHelper.WriteError("No project found for current directory.");
				ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
				Environment.Exit(1);
			}

			var profile = LoadAndValidateProfileForProject(config, project);
			if (string.IsNullOrWhiteSpace(project.EnvPath))
			{
				ConsoleHelper.WriteError("Project has no env path configured.");
				Environment.Exit(1);
			}

			var localContent = File.Exists(project.EnvPath)
				? await File.ReadAllTextAsync(project.EnvPath)
				: string.Empty;

			var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
			var client = new SenfApiClient(profile.ApiUrl, authHandler);
			var remote = await client.GetEnvFileAsync(project.ProjectName);

			if (remote == null)
			{
				ConsoleHelper.WriteWarning($"No env file exists on server for project '{project.ProjectName}'.");
				Console.Write("Create remote from local and push? (Y/n): ");
				var resp = Console.ReadLine()?.Trim().ToLower();
				if (resp == "y" || resp == "yes")
				{
					await client.CreateOrReplaceEnvFileAsync(project.ProjectName, localContent);
					ConsoleHelper.WriteSuccess("Created remote env file from local content.");
					if (profile.SshKeyId > -1)
						Config.SaveFileHash(project.ProjectName, profile.SshKeyId,
							Config.ComputeStringHash(localContent));
					return;
				}

				ConsoleHelper.WriteInfo("Reconcile cancelled.");
				return;
			}

			var remoteContent = remote.Content ?? string.Empty;

			if (localContent == remoteContent)
			{
				ConsoleHelper.WriteInfo("Local and remote are identical — nothing to reconcile.");
				return;
			}

			var localMap = ParseEnv(localContent, out var localOrder);
			var remoteMap = ParseEnv(remoteContent, out var remoteOrder);

			var allKeys = new List<string>();
			allKeys.AddRange(localOrder.Where(k => !allKeys.Contains(k)));
			allKeys.AddRange(remoteOrder.Where(k => !allKeys.Contains(k)));

			var merged = new Dictionary<string, string?>();

			foreach (var key in allKeys)
			{
				localMap.TryGetValue(key, out var lval);
				remoteMap.TryGetValue(key, out var rval);

				if (lval == rval || lval != null && rval == null)
				{
					merged[key] = lval;
					continue;
				}

				if (lval == null && rval != null)
				{
					merged[key] = rval;
					continue;
				}

				while (true)
				{
					Console.WriteLine($"Conflict for key: {key}");
					Console.WriteLine($"  Local : {lval}");
					Console.WriteLine($"  Remote: {rval}");
					Console.Write("Choose (l)ocal / (r)emote / (e)dit / (s)kip (keep local) / (a)bort: ");
					var choice = Console.ReadLine()?.Trim().ToLower();
					if (choice is "l" or "local")
					{
						merged[key] = lval;
						break;
					}

					if (choice is "r" or "remote")
					{
						merged[key] = rval;
						break;
					}

					if (choice is "e" or "edit")
					{
						Console.Write("Enter replacement value: ");
						var edited = Console.ReadLine() ?? string.Empty;
						merged[key] = edited;
						break;
					}

					if (choice is "s" or "skip")
					{
						merged[key] = lval;
						break;
					}

					if (choice is "a" or "abort")
					{
						ConsoleHelper.WriteInfo("Reconcile aborted by user.");
						return;
					}

					Console.WriteLine("Invalid choice — please enter l, r, e, s, or a.");
				}
			}

			var outLines = new List<string>();
			foreach (var key in allKeys)
			{
				if (merged.TryGetValue(key, out var val) && val != null)
					outLines.Add($"{key}={val}");
			}

			var outContent = string.Join(Environment.NewLine, outLines) + Environment.NewLine;
			await File.WriteAllTextAsync(project.EnvPath, outContent);
			ConsoleHelper.WriteSuccess($"Wrote merged env to: {project.EnvPath}");

			if (profile.SshKeyId > -1)
			{
				var newHash = Config.ComputeFileHash(project.EnvPath);
				Config.SaveFileHash(project.ProjectName, profile.SshKeyId, newHash);
			}

			ConsoleHelper.WriteDetail("Resolve any remaining issues and run 'senf push' to update remote.");
		}
		catch (SenfApiException ex)
		{
			ConsoleHelper.WriteError(ex.Message);
			Environment.Exit(1);
		}
		catch (Exception ex)
		{
			ConsoleHelper.WriteError($"Error during reconcile: {ex.Message}");
			Environment.Exit(1);
		}
	}
}