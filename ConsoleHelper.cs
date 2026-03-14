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

    public static void WriteError(string message)
    {
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
}
