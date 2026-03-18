using System.Diagnostics.CodeAnalysis;

namespace SenfCli;

public static class ConsoleHelper
{
	public static void WriteSuccess(string message)
	{
		Logger.Info($"SUCCESS: {message}");
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine($"✅ {message}");
		Console.ResetColor();
	}

	public static void WriteError(string message, Exception? e = null)
	{
		if (e != null)
			Logger.Error(e, message);
		else
			Logger.Error(message);

		Console.ForegroundColor = ConsoleColor.Red;
		Console.Error.WriteLine($"❌ {message}");
		Console.ResetColor();
	}

	public static void WriteWarning(string message)
	{
		Logger.Warn(message);
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.WriteLine($"⚠️  {message}");
		Console.ResetColor();
	}

	public static void WriteInfo(string message)
	{
		Logger.Info(message);
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine($"💡 {message}");
		Console.ResetColor();
	}

	public static void WriteDetail(string message)
	{
		Logger.Debug(message);
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.WriteLine($"  {message}");
		Console.ResetColor();
	}

	public static void Ask(string message)
	{
		Logger.Info($"PROMPT: {message}");
		Console.ForegroundColor = ConsoleColor.DarkYellow;
		Console.WriteLine($"❓ {message}");
		Console.ResetColor();
	}

	public static void ErrorIfProjectIsNull([NotNull] ProjectConfig? project)
	{
		if (project != null)
			return;

		WriteError("No project found for current directory.");
		WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
		Environment.Exit(1);
	}
}