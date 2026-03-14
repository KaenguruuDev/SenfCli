using System.Text;
using DotMake.CommandLine;
using SenfCli.Handlers;

namespace SenfCli;

public static class Program
{
	public static async Task Main(string[] args)
	{
		Console.OutputEncoding = Encoding.UTF8;
		try
		{
			await Cli.RunAsync<RootCommand>(args);
		}
		catch (Exception e)
		{
			ConsoleHelper.WriteError($"An error occurred while executing your command: {e.Message}");
		}
	}
}

[CliCommand(Description = "Senf - Environment file management CLI")]
public class RootCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf --help' to see available commands");
}

[CliCommand(Description = "Initialize a new project", Name = "init", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class InitCommand
{
	[CliArgument(Description = "Path to the .env file")]
	public string EnvPath { get; set; } = null!;

	[CliArgument(Description = "Project name")]
	public string ProjectName { get; set; } = null!;

	[CliOption(Description = "Optional profile to use (uses default if not specified)")]
	public string? UserProfile { get; set; }

	public async Task RunAsync()
		=> await InitCommandHandler.Init(EnvPath, ProjectName, UserProfile);
}

[CliCommand(Description = "Push current env file to the server", Name = "push", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class PushCommand
{
	public async Task RunAsync()
		=> await SyncCommandHandler.Push();
}

[CliCommand(Description = "Pull env file from the server", Name = "pull", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class PullCommand
{
	public async Task RunAsync()
		=> await SyncCommandHandler.Pull();
}

[CliCommand(Description = "Interactively reconcile local and remote env files", Name = "reconcile",
	Parent = typeof(RootCommand), ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class ReconcileCommand
{
	public async Task RunAsync()
		=> await SyncCommandHandler.Reconcile();
}

[CliCommand(Description = "Manage authentication profiles", Name = "profile", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class ProfileCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf profile --help' to see available subcommands");
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
		=> await ProfileCommandHandler.CreateOrUpdateProfile(Name, Username, SshKey, ApiUrl, Default);
}

[CliCommand(Description = "List all profiles", Name = "list", Parent = typeof(ProfileCommand))]
public class ProfileListCommand
{
	public void Run()
		=> ProfileCommandHandler.ListProfiles();
}

[CliCommand(Description = "Delete a profile", Name = "delete", Parent = typeof(ProfileCommand))]
public class ProfileDeleteCommand
{
	[CliArgument(Description = "Profile name to delete")]
	public string Name { get; set; } = null!;

	public void Run()
		=> ProfileCommandHandler.DeleteProfile(Name);
}

[CliCommand(Description = "Set default profile", Name = "default", Parent = typeof(ProfileCommand))]
public class ProfileDefaultCommand
{
	[CliArgument(Description = "Profile name to set as default")]
	public string Name { get; set; } = null!;

	public void Run()
		=> ProfileCommandHandler.SetDefaultProfile(Name);
}

[CliCommand(Description = "Manage project settings", Name = "project", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class ProjectCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf project --help' to see available subcommands");
}

[CliCommand(Description = "Set profile for current project", Name = "set-profile", Parent = typeof(ProjectCommand))]
public class ProjectSetProfileCommand
{
	[CliArgument(Description = "Profile name (or use --clear to unset)", Required = false)]
	public string? Name { get; set; }

	[CliOption(Description = "Clear profile (use default)")]
	public bool Clear { get; set; }

	public void Run()
		=> ProjectCommandHandler.SetProjectProfile(Clear ? null : Name);
}

[CliCommand(Description = "Manage SSH keys on the server", Name = "key", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class KeyCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf key --help' to see available subcommands");
}

[CliCommand(Description = "List all SSH keys", Name = "list", Parent = typeof(KeyCommand))]
public class KeyListCommand
{
	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
		=> await KeyCommandHandler.ListSshKeys(Profile);
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
			publicKey = PublicKey;
		else
			publicKey = await Console.In.ReadToEndAsync();

		publicKey = publicKey?.Trim() ?? string.Empty;
		await KeyCommandHandler.AddSshKey(publicKey, Name, Profile);
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
		=> await KeyCommandHandler.DeleteSshKey(KeyId, Profile);
}

[CliCommand(Description = "Manage env file sharing", Name = "share", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class ShareCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf share --help' to see available subcommands");
}

[CliCommand(Description = "Create a share for the current project", Name = "create", Parent = typeof(ShareCommand))]
public class ShareCreateCommand
{
	public async Task RunAsync()
		=> await ShareCommandHandler.CreateShare();
}

[CliCommand(Description = "Show share status", Name = "status", Parent = typeof(ShareCommand))]
public class ShareStatusCommand
{
	[CliOption(Description = "Show all active shares")]
	public bool All { get; set; }

	public async Task RunAsync()
		=> await ShareCommandHandler.ShowStatus(All);
}

[CliCommand(Description = "Remove a share for the current project", Name = "remove", Parent = typeof(ShareCommand))]
public class ShareRemoveCommand
{
	public async Task RunAsync()
		=> await ShareCommandHandler.RemoveShare();
}

[CliCommand(Description = "Admin operations", Name = "admin", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class AdminCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf admin --help' to see available subcommands");
}

[CliCommand(Description = "Manage invites", Name = "invite", Parent = typeof(AdminCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class AdminInviteCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf admin invite --help' to see available subcommands");
}

[CliCommand(Description = "Create a new invite", Name = "create", Parent = typeof(AdminInviteCommand))]
public class AdminInviteCreateCommand
{
	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
		=> await InviteCommandHandler.CreateInvite(Profile);
}

[CliCommand(Description = "List invites", Name = "list", Parent = typeof(AdminInviteCommand))]
public class AdminInviteListCommand
{
	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
		=> await InviteCommandHandler.ListInvites(Profile);
}

[CliCommand(Description = "Remove an invite", Name = "remove", Parent = typeof(AdminInviteCommand))]
public class AdminInviteRemoveCommand
{
	[CliArgument(Description = "Invite token")]
	public string Token { get; set; } = null!;

	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
		=> await InviteCommandHandler.RemoveInvite(Token, Profile);
}

[CliCommand(Description = "Manage users", Name = "users", Parent = typeof(AdminCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class AdminUsersCommand
{
	public void Run()
		=> ConsoleHelper.WriteInfo("Use 'senf admin users --help' to see available subcommands");
}

[CliCommand(Description = "List users", Name = "list", Parent = typeof(AdminUsersCommand))]
public class AdminUsersListCommand
{
	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
		=> await AdminUsersCommandHandler.ListUsers(Profile);
}

[CliCommand(Description = "Remove a user", Name = "remove", Parent = typeof(AdminUsersCommand))]
public class AdminUsersRemoveCommand
{
	[CliArgument(Description = "User ID to delete")]
	public int UserId { get; set; }

	[CliOption(Description = "Profile to use (defaults to default profile)", Required = false)]
	public string? Profile { get; set; }

	public async Task RunAsync()
		=> await AdminUsersCommandHandler.RemoveUser(UserId, Profile);
}

[CliCommand(Description = "Join with an invite", Name = "join", Parent = typeof(RootCommand),
	ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class JoinCommand
{
	[CliOption(Description = "API URL for the backend", Required = false)]
	public string? ApiUrl { get; set; }

	[CliOption(Description = "Invite token", Required = false)]
	public string? Token { get; set; }

	[CliOption(Description = "Username to join as", Required = false)]
	public string? Username { get; set; }

	[CliOption(Description = "SSH public key (repeatable)", Required = false)]
	public string[]? Key { get; set; }

	[CliOption(Description = "Path to SSH public key file (repeatable)", Required = false)]
	public string[]? KeyFile { get; set; }

	public async Task RunAsync()
		=> await JoinCommandHandler.Join(ApiUrl, Token, Username,
			Key?.ToList(), KeyFile?.ToList());
}