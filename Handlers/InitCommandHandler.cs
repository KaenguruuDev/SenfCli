namespace SenfCli.Handlers;

public static class InitCommandHandler
{
	public static async Task Init(string envPath, string projectName, string? profileName = null)
	{
		var config = Config.Load();

		var fullPath = Path.GetFullPath(envPath);
		var directory = Path.GetDirectoryName(fullPath);
		var basePath = directory ?? Directory.GetCurrentDirectory();

		if (!Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory!);
			ConsoleHelper.WriteInfo($"Created directory: {directory}");
		}

		if (!File.Exists(fullPath))
		{
			await File.WriteAllTextAsync(fullPath, string.Empty);
			ConsoleHelper.WriteInfo($"Created new .env file: {fullPath}");
		}
		else
			ConsoleHelper.WriteInfo($"Using existing .env file: {fullPath}");

		if (profileName is null && config.DefaultProfile is null)
		{
			ConsoleHelper.WriteError($"No default profile is set. Specify the profile with --user-profile");
			Environment.Exit(-1);
			return;
		}
		
		var project = new ProjectConfig
		{
			ProjectName = projectName,
			EnvPath = fullPath,
			BasePath = basePath,
			ProfileName = profileName ?? config.DefaultProfile!
		};

		config.AddOrUpdateProject(project);
		config.Save();

		ConsoleHelper.WriteSuccess($"Initialized project '{projectName}' with env file: {fullPath}");
		ConsoleHelper.WriteDetail($"Base path: {basePath}");
			ConsoleHelper.WriteDetail($"Profile: {profileName ?? config.DefaultProfile!}");
	}
}