using System.Net;

namespace SenfCli.Handlers;

public static class SyncCommandHandler
{
	private static string DescribeShareMode(ShareMode mode)
		=> mode == ShareMode.ReadWrite ? "read-write" : "read-only";

	private static async Task<SharedEnvFileResponse?> GetSharedEnvFileAsync(SenfApiClient client, string projectName)
	{
		var sharedResponse = await client.GetSharedFilesAsync();
		var sharedFiles = sharedResponse?.Files ?? [];
		var match = sharedFiles.FirstOrDefault(f =>
			string.Equals(f.EnvFileName, projectName, StringComparison.OrdinalIgnoreCase));

		if (match == null)
			return null;

		var detailed = await client.GetSharedFileAsync(match.ShareId);
		return detailed ?? match;
	}

	private static SshProfile LoadAndValidateProfileForProject(Config config, ProjectConfig project)
	{
		var profile = config.GetProfileForProject(project);
		if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
			string.IsNullOrWhiteSpace(profile.SshKeyPath) ||
			profile.SshKeyId < 0)
		{
			ConsoleHelper.WriteError("SSH credentials not configured.");
			ConsoleHelper.WriteDetail(
				"Run 'senf profile set <profile-name> --username <username> --ssh-key <path> --api-url <url>' first.");

			if (!string.IsNullOrEmpty(project.ProfileName))
				ConsoleHelper.WriteDetail($"This project uses profile: {project.ProfileName}");

			Environment.Exit(1);
		}

		return profile;
	}

	private static Dictionary<string, string?> ParseEnv(string content, out List<string> order)
	{
		var map = new Dictionary<string, string?>();
		order = [];
		using var reader = new StringReader(content);
		while (reader.ReadLine() is { } line)
		{
			line = line.Trim();
			if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
				continue;
			var idx = line.IndexOf('=');
			if (idx <= 0)
				continue;
			var key = line[..idx].Trim();
			var val = line[(idx + 1)..];
			if (!order.Contains(key)) order.Add(key);
			map[key] = val;
		}

		return map;
	}

	public static async Task Push()
	{
		var config = Config.Load();
		var project = config.GetCurrentProject();

		ConsoleHelper.ErrorIfProjectIsNull(project);

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
		var sharedFile = await GetSharedEnvFileAsync(client, project.ProjectName);
		var isShared = sharedFile != null;
		if (isShared && sharedFile!.ShareMode == ShareMode.ReadOnly)
		{
			ConsoleHelper.WriteError(
				$"This env file is shared with {DescribeShareMode(sharedFile.ShareMode)} access. Push is not allowed.");
			Environment.Exit(1);
		}

		EnvFileResponse? latestRemote = null;
		if (!isShared)
			latestRemote = await client.GetEnvFileAsync(project.ProjectName);

		var latestRemoteContent = isShared ? sharedFile!.Content : latestRemote?.Content;
		var latestRemoteHash = latestRemoteContent != null ? Config.ComputeStringHash(latestRemoteContent) : null;

		var storedHashInfo = Config.GetFileHash(project.ProjectName, profile.SshKeyId)?.Hash;

		if (latestRemoteHash != null && storedHashInfo != null && latestRemoteHash != storedHashInfo)
		{
			ConsoleHelper.WriteWarning("The remote env file has changed since the last pull.");
			ConsoleHelper.Ask("Choose (o)verwrite / (r)econcile / (c)cancel: ");
			var response = Console.ReadLine()?.Trim().ToLower();

			switch (response)
			{
				case "r" or "reconcile":
					{
						await Reconcile();
						ConsoleHelper.Ask("Do you want to push the merged file now? (y/n): ");
						var pushResp = Console.ReadLine()?.Trim().ToLower();
						if (pushResp != "y" && pushResp != "yes")
						{
							ConsoleHelper.WriteInfo("Push cancelled after reconcile.");
							return;
						}

						// Re-read content and recompute hash after reconcile
						content = await File.ReadAllTextAsync(project.EnvPath);
						fileHash = Config.ComputeFileHash(project.EnvPath);
						break;
					}
				case "o":
				case "overwrite":
					// proceed with overwrite
					break;
				default:
					ConsoleHelper.WriteInfo("Push cancelled.");
					return;
			}
		}

		try
		{
			if (isShared)
				await client.UpdateSharedFileAsync(sharedFile!.ShareId, content);
			else
				await client.UpdateEnvFileAsync(project.ProjectName, content);

			ConsoleHelper.WriteSuccess($"Pushed env file for project '{project.ProjectName}'");

			Config.SaveFileHash(project.ProjectName, profile.SshKeyId, fileHash);
		}
		catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
		{
			ConsoleHelper.WriteError("You do not have write access to this shared env file.");
			Environment.Exit(1);
		}
		catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			if (isShared)
			{
				ConsoleHelper.WriteError("Shared env file not found or access revoked.");
				Environment.Exit(1);
			}

			await client.CreateOrReplaceEnvFileAsync(project.ProjectName, content);
			ConsoleHelper.WriteSuccess($"Created project '{project.ProjectName}' and pushed env file");

			Config.SaveFileHash(project.ProjectName, profile.SshKeyId, fileHash);
		}
	}

	public static async Task Pull()
	{
		var config = Config.Load();
		var project = config.GetCurrentProject();

		ConsoleHelper.ErrorIfProjectIsNull(project);

		var profile = LoadAndValidateProfileForProject(config, project);
		if (!File.Exists(profile.SshKeyPath))
		{
			ConsoleHelper.WriteError($"SSH key file not found: {profile.SshKeyPath}");
			Environment.Exit(1);
		}

		if (File.Exists(project.EnvPath))
		{
			var currentHash = Config.ComputeFileHash(project.EnvPath);
			var storedHashInfo = Config.GetFileHash(project.ProjectName, profile.SshKeyId);

			if (storedHashInfo != null && storedHashInfo.Hash != currentHash)
			{
				ConsoleHelper.WriteWarning("The contents of the env file were changed since the last push.");
				ConsoleHelper.WriteDetail($"Local file: {project.EnvPath}");
				ConsoleHelper.Ask("Choose (o)verwrite / (r)econcile / (c)ancel: ");
				var response = Console.ReadLine()?.Trim().ToLower();

				switch (response)
				{
					case "r" or "reconcile":
						await Reconcile();
						ConsoleHelper.WriteInfo("Reconcile finished. Pull cancelled.");
						return;
					case "o" or "overwrite":
						// proceed with overwrite
						break;
					default:
						ConsoleHelper.WriteInfo("Pull cancelled.");
						return;
				}
			}
		}

		var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
		var client = new SenfApiClient(profile.ApiUrl, authHandler);
		var envFile = await client.GetEnvFileAsync(project.ProjectName);
		var sharedFile = envFile == null ? await GetSharedEnvFileAsync(client, project.ProjectName) : null;

		if (envFile is null && sharedFile is null)
		{
			ConsoleHelper.WriteError($"Env file not found on server for project '{project.ProjectName}'");
			Environment.Exit(1);
		}

		var directory = Path.GetDirectoryName(project.EnvPath);
		if (!Directory.Exists(directory))
			Directory.CreateDirectory(directory!);

		var contentToWrite = envFile?.Content ?? sharedFile?.Content ?? string.Empty;
		await File.WriteAllTextAsync(project.EnvPath, contentToWrite);
		var newHash = Config.ComputeFileHash(project.EnvPath);
		Config.SaveFileHash(project.ProjectName, profile.SshKeyId, newHash);

		if (sharedFile != null)
			ConsoleHelper.WriteSuccess($"Pulled shared env file for project '{project.ProjectName}'");
		else
			ConsoleHelper.WriteSuccess($"Pulled env file for project '{project.ProjectName}'");
		ConsoleHelper.WriteDetail($"Saved to: {project.EnvPath}");
	}

	public static async Task Reconcile()
	{
		var config = Config.Load();
		var project = config.GetCurrentProject();

		ConsoleHelper.ErrorIfProjectIsNull(project);

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
		var sharedRemote = remote == null ? await GetSharedEnvFileAsync(client, project.ProjectName) : null;

		if (remote == null && sharedRemote == null)
		{
			ConsoleHelper.WriteWarning($"No env file exists on server for project '{project.ProjectName}'.");
			Console.Write("Create remote from local and push? (Y/n): ");
			var resp = Console.ReadLine()?.Trim().ToLower();
			if (resp == "y" || resp == "yes")
			{
				await client.CreateOrReplaceEnvFileAsync(project.ProjectName, localContent);
				ConsoleHelper.WriteSuccess("Created remote env file from local content.");
				Config.SaveFileHash(project.ProjectName, profile.SshKeyId,
					Config.ComputeStringHash(localContent));
				return;
			}

			ConsoleHelper.WriteInfo("Reconcile cancelled.");
			return;
		}

		var remoteContent = remote?.Content ?? sharedRemote?.Content ?? string.Empty;

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
				ConsoleHelper.WriteWarning($"Conflict for key: {key}");
				ConsoleHelper.WriteDetail($"Local : {lval}");
				ConsoleHelper.WriteDetail($"Remote: {rval}");
				ConsoleHelper.Ask("Choose (l)ocal / (r)emote / (e)dit / (s)kip (keep local) / (a)bort: ");
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
					ConsoleHelper.Ask("Enter replacement value: ");
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

				ConsoleHelper.WriteError("Invalid choice — please enter l, r, e, s, or a.");
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

		var newHash = Config.ComputeFileHash(project.EnvPath);
		Config.SaveFileHash(project.ProjectName, profile.SshKeyId, newHash);

		ConsoleHelper.WriteDetail("Resolve any remaining issues and run 'senf push' to update remote.");
	}
}