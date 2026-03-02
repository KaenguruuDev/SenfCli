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
				"config" => HandleConfig(args),
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
			await Console.Error.WriteLineAsync("✗ Usage: senf init <path-to-env> <project-name> [--api-url <url>]");
			return 1;
		}

		var apiUrl = "http://localhost:5227";

		for (int i = 3; i < args.Length - 1; i++)
		{
			if (args[i] != "--api-url")
				continue;

			apiUrl = args[i + 1];
			break;
		}

		await CommandHandlers.Init(args[1], args[2], apiUrl);
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

	private static int HandleConfig(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("✗ Usage: senf config <username> <ssh-key-path>");
			return 1;
		}

		var username = args[1];
		var sshKeyPath = args[2];

		CommandHandlers.SetCredentials(username, sshKeyPath);
		return 0;
	}

	private static async Task<int> HandleKey(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("✗ Usage: senf key <list|add|delete> [options]");
			Console.Error.WriteLine("  senf key list                              List all SSH keys");
			Console.Error.WriteLine("  senf key add <path> <name>                 Add an SSH public key");
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
		if (args.Length < 4)
		{
			Console.Error.WriteLine("✗ Usage: senf key add <public-key-path> <name>");
			return 1;
		}

		await CommandHandlers.AddSshKey(args[2], args[3]);
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
		                      [--api-url <url>]                   Optional API URL (default: http://localhost:5227)
		                    config <username> <ssh-key-path>      Configure SSH credentials
		                    push                                   Push current env file to the server
		                    pull                                   Pull env file from the server
		                    key                                    Manage SSH keys
		                      list                                 List all SSH keys
		                      add <path> <name>                    Add an SSH public key
		                      delete <key-id>                      Delete an SSH key by ID
		                    help                                   Show this help message

		                  Examples:
		                    senf init .env my-project
		                    senf init .env my-project --api-url http://api.example.com:5227
		                    senf config john ~/.ssh/id_rsa
		                    senf key list
		                    senf key add ~/.ssh/id_rsa.pub john-desktop
		                    senf key delete 42
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