using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace SenfCli.Handlers;

public static class AdminUsersCommandHandler
{
    public static async Task ListUsers(string? profileName)
    {
        var config = Config.Load();
        var profile = LoadAndValidateProfile(config, profileName);

        var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
        var client = new SenfApiClient(profile.ApiUrl, authHandler);

        var response = await client.GetUsersAsync();
        var users = response?.Users ?? [];

        if (users.Count == 0)
        {
            ConsoleHelper.WriteInfo("No users returned by the server.");
            return;
        }

        ConsoleHelper.WriteSuccess($"Found {users.Count} user(s):");
        foreach (var user in users)
        {
            ConsoleHelper.WriteInfo($"{user.Username} (ID: {user.Id})");
        }
    }

    public static async Task RemoveUser(int userId, string? profileName)
    {
        if (userId <= 0)
        {
            ConsoleHelper.WriteError("User ID must be a positive number.");
            Environment.Exit(1);
        }

        var config = Config.Load();
        var profile = LoadAndValidateProfile(config, profileName);

        ConsoleHelper.Ask($"Delete user {userId}? (y/N): ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (response != "y" && response != "yes")
        {
            ConsoleHelper.WriteInfo("User removal cancelled.");
            return;
        }

        var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
        var client = new SenfApiClient(profile.ApiUrl, authHandler);

        try
        {
            await client.DeleteUserAsync(userId);
            ConsoleHelper.WriteSuccess($"User {userId} deleted successfully.");
        }
        catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            ConsoleHelper.WriteError("User not found.");
            Environment.Exit(1);
        }
        catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            ConsoleHelper.WriteError("You are not authorized to delete this user.");
            Environment.Exit(1);
        }
    }

    private static SshProfile LoadAndValidateProfile(Config config, string? profileName)
    {
        var profile = ResolveProfile(config, profileName);
        if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
            string.IsNullOrWhiteSpace(profile.SshKeyPath) ||
            string.IsNullOrWhiteSpace(profile.ApiUrl) ||
            profile.SshKeyId < 0)
        {
            ConsoleHelper.WriteError("No valid profile configured.");
            ConsoleHelper.WriteDetail(
                "Run 'senf profile set <profile-name> --username <username> --ssh-key <path> --api-url <url>' first.");
            Environment.Exit(1);
        }

        if (!File.Exists(profile.SshKeyPath))
        {
            ConsoleHelper.WriteError($"SSH key file not found: {profile.SshKeyPath}");
            Environment.Exit(1);
        }

        return profile;
    }

    private static SshProfile? ResolveProfile(Config config, string? profileName)
    {
        if (!string.IsNullOrEmpty(profileName))
        {
            var profile = config.GetProfile(profileName);
            if (profile == null)
            {
                ConsoleHelper.WriteError($"Profile '{profileName}' not found.");
                ConsoleHelper.WriteDetail("Run 'senf profile list' to see available profiles.");
                Environment.Exit(1);
            }

            return profile;
        }

        if (!string.IsNullOrEmpty(config.DefaultProfile))
            return config.GetProfile(config.DefaultProfile);

        return config.Profiles.TryGetValue("default", out var defaultProfile) ? defaultProfile : null;
    }
}
