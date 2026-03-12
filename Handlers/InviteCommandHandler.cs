using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace SenfCli.Handlers;

public static class InviteCommandHandler
{
    public static async Task CreateInvite(string? profileName)
    {
        var config = Config.Load();
        var profile = LoadAndValidateProfile(config, profileName);

        var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
        var client = new SenfApiClient(profile.ApiUrl, authHandler);

        var response = await client.CreateInviteAsync();
        if (response == null || string.IsNullOrWhiteSpace(response.JoinUrl) || string.IsNullOrWhiteSpace(response.Token))
        {
            ConsoleHelper.WriteError("Invite creation failed: no response from server.");
            Environment.Exit(1);
        }

        ConsoleHelper.WriteSuccess("Invite created successfully.");
        ConsoleHelper.WriteInfo($"Invite Url: {response.JoinUrl}");
        var localExpiry = response.ExpiresAt.ToLocalTime();
        ConsoleHelper.WriteDetail(
            $"Share this link with the person you want to invite. This invite will expire at {localExpiry:yyyy-MM-dd HH:mm:ss}");
        ConsoleHelper.WriteDetail($"Token: {response.Token}");
    }

    public static async Task ListInvites(string? profileName)
    {
        var config = Config.Load();
        var profile = LoadAndValidateProfile(config, profileName);

        var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
        var client = new SenfApiClient(profile.ApiUrl, authHandler);

        var response = await client.ListInvitesAsync();
        var invites = response?.Invites ?? [];

        if (invites.Count == 0)
        {
            ConsoleHelper.WriteInfo("No active invites found.");
            return;
        }

        ConsoleHelper.WriteSuccess($"Found {invites.Count} invite(s):");
        foreach (var invite in invites)
        {
            var createdAt = invite.CreatedAt.ToLocalTime();
            var expiresAt = invite.ExpiresAt.ToLocalTime();
            var usedAt = invite.UsedAt?.ToLocalTime();

            ConsoleHelper.WriteInfo($"Token: {invite.Token}");
            ConsoleHelper.WriteDetail($"Created by: {invite.CreatedByUsername} (ID: {invite.CreatedByUserId})");
            ConsoleHelper.WriteDetail($"Created: {createdAt:yyyy-MM-dd HH:mm:ss}");
            ConsoleHelper.WriteDetail($"Expires: {expiresAt:yyyy-MM-dd HH:mm:ss}");
            ConsoleHelper.WriteDetail(usedAt.HasValue
                ? $"Used: {usedAt.Value:yyyy-MM-dd HH:mm:ss}"
                : "Used: no");
            ConsoleHelper.WriteInfo(string.Empty);
        }
    }

    public static async Task RemoveInvite(string token, string? profileName)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ConsoleHelper.WriteError("Invite token is required.");
            Environment.Exit(1);
        }

        var config = Config.Load();
        var profile = LoadAndValidateProfile(config, profileName);

        var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
        var client = new SenfApiClient(profile.ApiUrl, authHandler);

        try
        {
            await client.DeleteInviteAsync(token);
            ConsoleHelper.WriteSuccess("Invite removed successfully.");
            ConsoleHelper.WriteDetail($"Token: {token}");
        }
        catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            ConsoleHelper.WriteError("Invite not found.");
            Environment.Exit(1);
        }
        catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            ConsoleHelper.WriteError("You are not authorized to remove this invite.");
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
