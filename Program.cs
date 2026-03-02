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
			await Console.Error.WriteLineAsync("✗ Usage: senf init <path-to-env> <project-name>");
			return 1;
		}

		await CommandHandlers.Init(args[1], args[2]);
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
			Console.Error.WriteLine("✗ Usage: senf config <username> <ssh-key-path> [--api-url <url>]");
			return 1;
		}

		var username = args[1];
		var sshKeyPath = args[2];
		var apiUrl = "http://localhost:5227";

		for (int i = 3; i < args.Length - 1; i++)
		{
			if (args[i] != "--api-url")
				continue;

			apiUrl = args[i + 1];
			break;
		}

		CommandHandlers.SetCredentials(username, sshKeyPath, apiUrl);
		return 0;
	}

	private static int PrintUsage()
	{
		Console.WriteLine("""
		                  Senf - Environment file management CLI

		                  Usage: senf <command> [options]

		                  Commands:
		                    init <path-to-env> <project-name>     Initialize a new project
		                    config <username> <ssh-key-path>      Configure SSH credentials
		                      [--api-url <url>]                   Optional API URL (default: http://localhost:5227)
		                    push                                   Push current env file to the server
		                    pull                                   Pull env file from the server
		                    help                                   Show this help message

		                  Examples:
		                    senf init .env my-project
		                    senf config john ~/.ssh/id_rsa --api-url http://api.example.com:5227
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