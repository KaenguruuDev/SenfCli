namespace SenfCli.Handlers;

public static class ProjectCommandHandler
{
	public static void SetProjectProfile(string? profileName)
	{
		var config = Config.Load();
		var project = config.GetCurrentProject();

		ConsoleHelper.ErrorIfProjectIsNull(project);

		if (!string.IsNullOrEmpty(profileName) && !config.Profiles.ContainsKey(profileName))
		{
			ConsoleHelper.WriteError($"Profile '{profileName}' not found");
			Environment.Exit(1);
		}

		project.ProfileName = profileName!;
		config.Save();

		if (string.IsNullOrEmpty(profileName))
			ConsoleHelper.WriteSuccess($"Cleared profile for project '{project.ProjectName}' (will use default)");
	
		else
			ConsoleHelper.WriteSuccess($"Set profile '{profileName}' for project '{project.ProjectName}'");
	}
}