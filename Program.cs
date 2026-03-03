using System.Text;
using DotMake.CommandLine;

namespace SenfCli;

public class Program
{
	public static async Task Main(string[] args)
	{
		Console.OutputEncoding = Encoding.UTF8;
		await Cli.RunAsync<RootCommand>(args);
	}
}

[CliCommand(Description = "Senf - Environment file management CLI")]
public class RootCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf --help' to see available commands");

}

// ======= Init Command =======
[CliCommand(Description = "Initialize a new project", Name = "init", Parent = typeof(RootCommand), ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class InitCommand
{
	[CliArgument(Description = "Path to the .env file")]
	public string EnvPath { get; set; } = null!;

	[CliArgument(Description = "Project name")]
	public string ProjectName { get; set; } = null!;

	[CliOption(Description = "Optional profile to use (uses default if not specified)")]
	public string? UserProfile { get; set; }

	public async Task RunAsync()
	{
		await CommandHandlers.Init(EnvPath, ProjectName, UserProfile);
	}
}

// ======= Push Command =======
[CliCommand(Description = "Push current env file to the server", Name = "push", Parent = typeof(RootCommand), ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class PushCommand
{
	public async Task RunAsync()
	{
		await CommandHandlers.Push();
	}
}

// ======= Pull Command =======
[CliCommand(Description = "Pull env file from the server", Name = "pull", Parent = typeof(RootCommand), ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class PullCommand
{
	public async Task RunAsync()
	{
		await CommandHandlers.Pull();
	}
}

// ======= Profile Commands =======
[CliCommand(Description = "Manage authentication profiles", Name = "profile", Parent = typeof(RootCommand), ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class ProfileCommand
{
	public void Run()
	{
		ConsoleHelper.WriteInfo("Use 'senf profile --help' to see available subcommands");
	}
}

[CliCommand(Description = "Create or update a profile", Name = "set", Parent = typeof(ProfileCommand))]
public class ProfileSetCommand
{
	[CliArgument(Description = "Profile name")]
	public string Name { get; set; } = null!;

	[CliOption(Description = "SSH username")]
	public string? Username { get; set; }

	[CliOption(Description = "Path to SSH private key")]
	public string? SshKey { get; set; }

	[CliOption(Description = "API URL for the backend")]
	public string? ApiUrl { get; set; }

	[CliOption(Description = "Set as default profile")]
	public bool Default { get; set; }

	public async Task RunAsync()
	{
		await CommandHandlers.CreateOrUpdateProfile(Name, Username, SshKey, ApiUrl, Default);
	}
}

[CliCommand(Description = "List all profiles", Name = "list", Parent = typeof(ProfileCommand))]
public class ProfileListCommand
{
	public void Run()
	{
		CommandHandlers.ListProfiles();
	}
}

[CliCommand(Description = "Delete a profile", Name = "delete", Parent = typeof(ProfileCommand))]
public class ProfileDeleteCommand
{
	[CliArgument(Description = "Profile name to delete")]
	public string Name { get; set; } = null!;

	public void Run()
	{
		CommandHandlers.DeleteProfile(Name);
	}
}

[CliCommand(Description = "Set default profile", Name = "default", Parent = typeof(ProfileCommand))]
public class ProfileDefaultCommand
{
	[CliArgument(Description = "Profile name to set as default")]
	public string Name { get; set; } = null!;

	public void Run()
	{
		CommandHandlers.SetDefaultProfile(Name);
	}
}

// ======= Project Commands =======
[CliCommand(Description = "Manage project settings", Name = "project", Parent = typeof(RootCommand), ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class ProjectCommand
{
	public void Run()
	{
		ConsoleHelper.WriteInfo("Use 'senf project --help' to see available subcommands");
	}
}

[CliCommand(Description = "Set profile for current project", Name = "set-profile", Parent = typeof(ProjectCommand))]
public class ProjectSetProfileCommand
{
	[CliArgument(Description = "Profile name (or use --clear to unset)", Required = false)]
	public string? Name { get; set; }

	[CliOption(Description = "Clear profile (use default)")]
	public bool Clear { get; set; }

	public void Run()
	{
		CommandHandlers.SetProjectProfile(Clear ? null : Name);
	}
}

// ======= Key Commands =======
[CliCommand(Description = "Manage SSH keys on the server", Name = "key", Parent = typeof(RootCommand), ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class KeyCommand
{
	public void Run()
	{
		ConsoleHelper.WriteInfo("Use 'senf key --help' to see available subcommands");
	}
}

[CliCommand(Description = "List all SSH keys", Name = "list", Parent = typeof(KeyCommand))]
public class KeyListCommand
{
	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
	{
		await CommandHandlers.ListSshKeys(Profile);
	}
}

[CliCommand(Description = "Add an SSH public key", Name = "add", Parent = typeof(KeyCommand))]
public class KeyAddCommand
{
	[CliArgument(Description = "Key name")]
	public string Name { get; set; } = null!;

	[CliArgument(Description = "Public key content (read from stdin if not provided)", Required = false)]
	public string? PublicKey { get; set; }

	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
	{
		string publicKey;

		if (!string.IsNullOrEmpty(PublicKey))
		{
			publicKey = PublicKey;
		}
		else
		{
			// Read from stdin
			publicKey = await Console.In.ReadToEndAsync();
		}

		publicKey = publicKey?.Trim() ?? string.Empty;
		await CommandHandlers.AddSshKey(publicKey, Name, Profile);
	}
}

[CliCommand(Description = "Delete an SSH key by ID", Name = "delete", Parent = typeof(KeyCommand))]
public class KeyDeleteCommand
{
	[CliArgument(Description = "Key ID to delete")]
	public int KeyId { get; set; }

	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
	{
		await CommandHandlers.DeleteSshKey(KeyId, Profile);
	}
}
