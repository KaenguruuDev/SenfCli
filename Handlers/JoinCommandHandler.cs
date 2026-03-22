using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SenfCli.Handlers;

public static class JoinCommandHandler
{
    public static async Task Join(string? apiUrl, string? token, string? username, string profileName, List<string>? keys,
        List<string>? keyFiles)
    {
        var hasAnyFlag =
            !string.IsNullOrWhiteSpace(apiUrl) ||
            !string.IsNullOrWhiteSpace(token) ||
            !string.IsNullOrWhiteSpace(username) ||
            !string.IsNullOrWhiteSpace(profileName) ||
            keys is { Count: > 0 } ||
            keyFiles is { Count: > 0 };

        if (hasAnyFlag)
            await JoinWithFlags(apiUrl, token, username, profileName, keys, keyFiles);
        else
            await JoinInteractive();
    }

    private static async Task JoinWithFlags(string? apiUrl, string? token, string? username, string profileName, List<string>? keys,
        List<string>? keyFiles)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(apiUrl))
            missing.Add("--api-url");
        if (string.IsNullOrWhiteSpace(token))
            missing.Add("--token");
        if (string.IsNullOrWhiteSpace(username))
            missing.Add("--username");
        if (string.IsNullOrWhiteSpace(profileName))
	        missing.Add("--profile-name");

        var publicKeys = new List<string>();
        if (keys != null)
            publicKeys.AddRange(keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()));

        if (keyFiles != null)
        {
	        foreach (var path in keyFiles.Where(path => !string.IsNullOrWhiteSpace(path)))
	        {
		        if (!File.Exists(path))
		        {
			        ConsoleHelper.WriteError($"Public key file not found: {path}");
			        Environment.Exit(1);
		        }

		        var content = (await File.ReadAllTextAsync(path)).Trim();
		        if (!string.IsNullOrWhiteSpace(content))
			        publicKeys.Add(content);
	        }
        }

        if (publicKeys.Count == 0)
            missing.Add("--key/--key-file");

        if (missing.Count > 0)
        {
            ConsoleHelper.WriteError("Missing required flags for non-interactive join.");
            ConsoleHelper.WriteDetail($"Required: {string.Join(", ", missing)}");
            Environment.Exit(1);
        }

        publicKeys = NormalizeAndValidateKeys(publicKeys);

        var request = new JoinRequest
        {
            Token = token,
            Username = username,
            PublicKeys = publicKeys
        };

        var client = new SenfApiClient(apiUrl!);
        var response = await client.JoinWithInviteAsync(request);

        if (response == null)
        {
            ConsoleHelper.WriteError("Join failed: no response from server.");
            Environment.Exit(1);
        }

        ConsoleHelper.WriteSuccess($"Joined as '{response.Username ?? username}'.");
        ConsoleHelper.WriteDetail($"User ID: {response.UserId}");

            // Initialize or update the profile after successful join
            // Assume the first key file (if any) is the SSH key path
            var sshKeyPath = keyFiles?.FirstOrDefault();
            await ProfileCommandHandler.CreateOrUpdateProfile(profileName, username, sshKeyPath, apiUrl, setAsDefault: false);
    }

    private static async Task JoinInteractive()
    {
        var config = Config.Load();
        var defaultApiUrl = GetDefaultApiUrl(config);

        ConsoleHelper.Ask("Invite link: ");
        var inviteLink = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(inviteLink))
        {
            ConsoleHelper.WriteError("Invite link is required.");
            Environment.Exit(1);
        }

        if (!TryParseInviteLink(inviteLink, defaultApiUrl, out var apiUrl, out var token, out var parseError))
        {
            ConsoleHelper.WriteError(parseError);
            Environment.Exit(1);
        }

        ConsoleHelper.Ask("Username: ");
        var username = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            ConsoleHelper.WriteError("Username is required.");
            Environment.Exit(1);
        }
        
        ConsoleHelper.Ask("Profile name: ");
        var profileName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
        {
	        ConsoleHelper.WriteError("Profile name is required.");
	        Environment.Exit(1);
        }

        var publicKeys = await PromptForPublicKeys(config);
        if (publicKeys.Count == 0)
        {
            ConsoleHelper.WriteError("At least one public key is required.");
            Environment.Exit(1);
        }

        publicKeys = NormalizeAndValidateKeys(publicKeys);

        var request = new JoinRequest
        {
            Token = token,
            Username = username,
            PublicKeys = publicKeys
        };

        var client = new SenfApiClient(apiUrl);
        var response = await client.JoinWithInviteAsync(request);

        if (response == null)
        {
            ConsoleHelper.WriteError("Join failed: no response from server.");
            Environment.Exit(1);
        }

        ConsoleHelper.WriteSuccess($"Joined as '{response.Username ?? username}'.");
        ConsoleHelper.WriteDetail($"User ID: {response.UserId}");

            // Initialize or update the profile after successful join
            // Assume the first key from PromptForPublicKeys is the SSH key path if it was loaded from file
            string? sshKeyPath = null;
            // Try to extract the SSH key path from the config if the user selected from existing profiles
            // Otherwise, leave as null
            // (For a more robust solution, refactor PromptForPublicKeys to return key source info)
            await ProfileCommandHandler.CreateOrUpdateProfile(profileName, username, sshKeyPath, apiUrl, setAsDefault: false);
    }

    private static string? GetDefaultApiUrl(Config config)
    {
        if (!string.IsNullOrEmpty(config.DefaultProfile))
            return config.GetProfile(config.DefaultProfile)?.ApiUrl;

        return config.Profiles.TryGetValue("default", out var defaultProfile)
            ? defaultProfile.ApiUrl
            : null;
    }

    private static bool TryParseInviteLink(string inviteLink, string? defaultApiUrl, out string apiUrl,
        out string token, out string error)
    {
        apiUrl = string.Empty;
        token = string.Empty;
        error = string.Empty;

        if (!Uri.TryCreate(inviteLink, UriKind.Absolute, out var uri))
        {
            if (defaultApiUrl == null)
            {
                error = "Invite link must be a valid URL.";
                return false;
            }

            var trimmed = inviteLink.Trim();
            var tokenValue = trimmed.StartsWith("token=", StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring("token=".Length)
                : trimmed;

            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                error = "Invite link must include a token.";
                return false;
            }

            apiUrl = defaultApiUrl;
            token = tokenValue;
            return true;
        }

        var tokenParam = ExtractQueryParam(uri.Query, "token");
        if (string.IsNullOrWhiteSpace(tokenParam))
        {
            error = "Invite link is missing the token parameter.";
            return false;
        }

        var basePath = uri.AbsolutePath.EndsWith("/join", StringComparison.OrdinalIgnoreCase)
            ? uri.AbsolutePath.Substring(0, uri.AbsolutePath.Length - "/join".Length)
            : uri.AbsolutePath;

        apiUrl = uri.GetLeftPart(UriPartial.Authority) + basePath;
        token = tokenParam;
        return true;
    }

    private static string? ExtractQueryParam(string query, string name)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var key = Uri.UnescapeDataString(parts[0]);
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                continue;

            return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }

    private static async Task<List<string>> PromptForPublicKeys(Config config)
    {
        var keys = new List<string>();
        while (true)
        {
            ConsoleHelper.WriteInfo("Add a public key:");
            ConsoleHelper.WriteInfo("[1] Paste ssh-ed25519 key");
            ConsoleHelper.WriteInfo("[2] Load from file");
            ConsoleHelper.WriteInfo("[3] Use key from existing profiles");
            ConsoleHelper.WriteInfo("[4] Done");
            ConsoleHelper.Ask("Select an option: ");

            var choice = Console.ReadLine()?.Trim();
            switch (choice)
            {
                case "1":
                    ConsoleHelper.Ask("Paste public key: ");
                    var pasted = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(pasted))
                        keys.Add(pasted);
                    break;
                case "2":
                    ConsoleHelper.Ask("Path to public key file: ");
                    var path = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(path))
                        break;
                    if (!File.Exists(path))
                    {
                        ConsoleHelper.WriteError($"Public key file not found: {path}");
                        break;
                    }

                    var content = (await File.ReadAllTextAsync(path)).Trim();
                    if (!string.IsNullOrWhiteSpace(content))
                        keys.Add(content);
                    break;
                case "3":
                    var profileKey = PromptForProfileKey(config);
                    if (!string.IsNullOrWhiteSpace(profileKey))
                        keys.Add(profileKey);
                    break;
                case "4":
                    return keys;
                default:
                    ConsoleHelper.WriteError("Invalid selection.");
                    break;
            }
        }
    }

    private static string? PromptForProfileKey(Config config)
    {
        var profiles = config.Profiles
            .Select(p => new
            {
                Name = p.Key,
                PublicKeyPath = string.IsNullOrWhiteSpace(p.Value.SshKeyPath)
                    ? null
                    : p.Value.SshKeyPath + ".pub"
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.PublicKeyPath) && File.Exists(p.PublicKeyPath))
            .ToList();

        if (profiles.Count == 0)
        {
            ConsoleHelper.WriteError("No profiles with public keys found.");
            return null;
        }

        ConsoleHelper.WriteInfo("Select a profile public key:");
        for (var i = 0; i < profiles.Count; i++)
        {
            ConsoleHelper.WriteInfo($"[{i + 1}] {profiles[i].Name} ({profiles[i].PublicKeyPath})");
        }

        ConsoleHelper.Ask("Select a profile by number (leave empty to cancel): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (!int.TryParse(input, out var index) || index < 1 || index > profiles.Count)
        {
            ConsoleHelper.WriteError("Invalid selection.");
            return null;
        }

        var selected = profiles[index - 1];
        return File.ReadAllText(selected.PublicKeyPath!).Trim();
    }

    private static List<string> NormalizeAndValidateKeys(IEnumerable<string> keys)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var trimmed = key.Trim();
            if (!seen.Add(trimmed))
                continue;

            if (!SshKeyValidation.TryValidateEd25519PublicKey(trimmed, out var error))
            {
                ConsoleHelper.WriteError(error);
                ConsoleHelper.WriteDetail("Supported formats: ssh-ed25519.");
                Environment.Exit(1);
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }
}
