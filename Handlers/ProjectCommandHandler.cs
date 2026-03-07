namespace SenfCli.Handlers;

public static class ProjectCommandHandler
{
	public static void SetProjectProfile(string? profileName)
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

			if (!string.IsNullOrEmpty(profileName) && !config.Profiles.ContainsKey(profileName))
			{
				ConsoleHelper.WriteError($"Profile '{profileName}' not found");
				Environment.Exit(1);
			}

			project.ProfileName = profileName;
			config.Save();

			if (string.IsNullOrEmpty(profileName))
			{
				ConsoleHelper.WriteSuccess($"Cleared profile for project '{project.ProjectName}' (will use default)");
			}
			else
			{
				ConsoleHelper.WriteSuccess($"Set profile '{profileName}' for project '{project.ProjectName}'");
			}
		}
		catch (Exception ex)
		{
			ConsoleHelper.WriteError($"Error setting project profile: {ex.Message}");
			Environment.Exit(1);
		}
	}
}