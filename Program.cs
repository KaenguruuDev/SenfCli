using System.Text;

namespace SenfCli;

public static class Program
{
	private static async Task<int> Main(string[] args)
	{
		// Set UTF-8 encoding for emoji support
		Console.OutputEncoding = Encoding.UTF8;

		try
		{
			if (args.Length == 0)
			{
				PrintUsage();
				return 0;
			}

			var command = args[0].ToLower();

			return command switch
			{
				"init" => await HandleInit(args),
				"push" => await HandlePush(args),
				"pull" => await HandlePull(args),
				"profile" => HandleProfile(args),
				"project" => HandleProject(args),
				"key" => await HandleKey(args),
				"help" or "-h" or "--help" => PrintUsage(),
				_ => PrintUnknownCommand(command)
			};
		}
		catch (Exception ex)
		{
			await Console.Error.WriteLineAsync($"✗ Error: {ex.Message}");
			return 1;
		}
	}

	private static async Task<int> HandleInit(string[] args)
	{
		if (args.Length < 3)
		{
			await Console.Error.WriteLineAsync("✗ Usage: senf init <path-to-env> <project-name> [--user-profile <name>]");
			return 1;
		}

		string? profileName = null;

		for (int i = 3; i < args.Length - 1; i++)
		{
			if (args[i] == "--user-profile")
			{
				profileName = args[i + 1];
				i++;
			}
		}

		await CommandHandlers.Init(args[1], args[2], profileName);
		return 0;
	}

	private static async Task<int> HandlePush(string[] args)
	{
		await CommandHandlers.Push();
		return 0;
	}

	private static async Task<int> HandlePull(string[] args)
	{
		await CommandHandlers.Pull();
		return 0;
	}

	private static int HandleProfile(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("✗ Usage: senf profile <set|list|delete|default> [options]");
			Console.Error.WriteLine("  senf profile set <name> --username <user> --ssh-key <path> --api-url <url> [--default]");
			Console.Error.WriteLine("  senf profile list");
			Console.Error.WriteLine("  senf profile delete <name>");
			Console.Error.WriteLine("  senf profile default <name>");
			return 1;
		}

		var subcommand = args[1].ToLower();

		return subcommand switch
		{
			"set" => HandleProfileSet(args),
			"list" => HandleProfileList(),
			"delete" => HandleProfileDelete(args),
			"default" => HandleProfileDefault(args),
			_ => InvalidProfileSubcommand(subcommand)
		};
	}

	private static int HandleProfileSet(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("✗ Usage: senf profile set <name> [--username <user>] [--ssh-key <path>] [--api-url <url>] [--default]");
			return 1;
		}

		var profileName = args[2];
		string? username = null;
		string? sshKeyPath = null;
		string? apiUrl = null;
		bool setAsDefault = false;

		for (int i = 3; i < args.Length; i++)
		{
			if (args[i] == "--username" && i + 1 < args.Length)
			{
				username = args[i + 1];
				i++;
			}
			else if (args[i] == "--ssh-key" && i + 1 < args.Length)
			{
				sshKeyPath = args[i + 1];
				i++;
			}
			else if (args[i] == "--api-url" && i + 1 < args.Length)
			{
				apiUrl = args[i + 1];
				i++;
			}
			else if (args[i] == "--default")
			{
				setAsDefault = true;
			}
		}

		if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(sshKeyPath) && string.IsNullOrEmpty(apiUrl))
		{
			Console.Error.WriteLine("✗ Must provide at least --username, --ssh-key, or --api-url");
			return 1;
		}

		CommandHandlers.CreateOrUpdateProfile(profileName, username, sshKeyPath, apiUrl, setAsDefault);
		return 0;
	}

	private static int HandleProfileList()
	{
		CommandHandlers.ListProfiles();
		return 0;
	}

	private static int HandleProfileDelete(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("✗ Usage: senf profile delete <name>");
			return 1;
		}

		CommandHandlers.DeleteProfile(args[2]);
		return 0;
	}

	private static int HandleProfileDefault(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("✗ Usage: senf profile default <name>");
			return 1;
		}

		CommandHandlers.SetDefaultProfile(args[2]);
		return 0;
	}

	private static int InvalidProfileSubcommand(string subcommand)
	{
		Console.Error.WriteLine($"✗ Unknown profile subcommand: {subcommand}");
		Console.Error.WriteLine("Use 'senf profile set', 'senf profile list', 'senf profile delete', or 'senf profile default'");
		return 1;
	}

	private static int HandleProject(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("✗ Usage: senf project <set-profile> [options]");
			Console.Error.WriteLine("  senf project set-profile <name>");
			Console.Error.WriteLine("  senf project set-profile --clear");
			return 1;
		}

		var subcommand = args[1].ToLower();

		return subcommand switch
		{
			"set-profile" => HandleProjectSetProfile(args),
			_ => InvalidProjectSubcommand(subcommand)
		};
	}

	private static int HandleProjectSetProfile(string[] args)
	{
		string? profileName = null;

		if (args.Length >= 3)
		{
			if (args[2] == "--clear")
			{
				profileName = null;
			}
			else
			{
				profileName = args[2];
			}
		}
		else
		{
			Console.Error.WriteLine("✗ Usage: senf project set-profile <name> or senf project set-profile --clear");
			return 1;
		}

		CommandHandlers.SetProjectProfile(profileName);
		return 0;
	}

	private static int InvalidProjectSubcommand(string subcommand)
	{
		Console.Error.WriteLine($"✗ Unknown project subcommand: {subcommand}");
		Console.Error.WriteLine("Use 'senf project set-profile'");
		return 1;
	}

	private static async Task<int> HandleKey(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("✗ Usage: senf key <list|add|delete> [options]");
			Console.Error.WriteLine("  senf key list                              List all SSH keys");
			Console.Error.WriteLine("  senf key add <name> [public-key]           Add an SSH public key");
			Console.Error.WriteLine("  senf key delete <key-id>                   Delete an SSH key by ID");
			return 1;
		}

		var subcommand = args[1].ToLower();

		return subcommand switch
		{
			"list" => await HandleKeyList(),
			"add" => await HandleKeyAdd(args),
			"delete" => await HandleKeyDelete(args),
			_ => InvalidKeySubcommand(subcommand)
		};
	}

	private static async Task<int> HandleKeyList()
	{
		await CommandHandlers.ListSshKeys();
		return 0;
	}

	private static async Task<int> HandleKeyAdd(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("✗ Usage: senf key add <name> [public-key]");
			Console.Error.WriteLine("  If public-key is not provided, it will be read from stdin");
			return 1;
		}

		var keyName = args[2];
		string publicKey;

		if (args.Length >= 4)
		{
			// Key provided as argument
			publicKey = args[3];
		}
		else
		{
			// Read from stdin
			publicKey = await Console.In.ReadToEndAsync();
		}

		publicKey = publicKey?.Trim() ?? string.Empty;

		await CommandHandlers.AddSshKey(publicKey, keyName);
		return 0;
	}

	private static async Task<int> HandleKeyDelete(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("✗ Usage: senf key delete <key-id>");
			return 1;
		}

		if (!int.TryParse(args[2], out var keyId))
		{
			Console.Error.WriteLine("✗ Invalid key ID. Must be a number.");
			return 1;
		}

		await CommandHandlers.DeleteSshKey(keyId);
		return 0;
	}

	private static int InvalidKeySubcommand(string subcommand)
	{
		Console.Error.WriteLine($"✗ Unknown key subcommand: {subcommand}");
		Console.Error.WriteLine("Use 'senf key list', 'senf key add', or 'senf key delete'");
		return 1;
	}

	private static int PrintUsage()
	{
		Console.WriteLine("""
		                  Senf - Environment file management CLI

		                  Usage: senf <command> [options]

		                  Commands:
		                    init <path-to-env> <project-name>     Initialize a new project
		                      [--user-profile <name>]             Optional profile to use (uses default if not specified)
		                    profile                               Manage authentication profiles
		                      set <name>                          Create or update a profile
		                        --username <user>                 SSH username
		                        --ssh-key <path>                  Path to SSH private key
		                        --api-url <url>                   API URL for the backend
		                        [--default]                       Set as default profile
		                      list                                List all profiles
		                      delete <name>                       Delete a profile
		                      default <name>                      Set default profile
		                    project                               Manage project settings
		                      set-profile <name>                  Set profile for current project
		                      set-profile --clear                 Clear profile (use default)
		                    push                                  Push current env file to the server
		                    pull                                  Pull env file from the server
		                    key                                   Manage SSH keys on the server
		                      list                                List all SSH keys
		                      add <name> [public-key]             Add an SSH public key (read from stdin if not provided)
		                      delete <key-id>                     Delete an SSH key by ID
		                    help                                  Show this help message

		                  Examples:
		                    # Create a profile
		                    senf profile set default --username alice --ssh-key ~/.ssh/id_ed25519 --api-url https://senf.example.com --default
		                    senf profile set work --username bob --ssh-key ~/.ssh/work_key --api-url https://work.example.com
		                    
		                    # List profiles
		                    senf profile list
		                    
		                    # Initialize project (uses default profile)
		                    senf init .env my-project
		                    
		                    # Initialize project with specific profile
		                    senf init .env my-project --user-profile work
		                    
		                    # Manage project
		                    senf project set-profile work
		                    
		                    # Manage SSH keys
		                    senf key list
		                    senf key add john-desktop < ~/.ssh/id_rsa.pub
		                    senf key add john-desktop "ssh-rsa AAAA..."
		                    senf key delete 42
		                    
		                    # Push/pull env files
		                    senf push
		                    senf pull
		                  """);
		return 0;
	}

	private static int PrintUnknownCommand(string command)
	{
		Console.Error.WriteLine($"✗ Unknown command: {command}");
		Console.Error.WriteLine("Run 'senf help' for usage information");
		return 1;
	}
}