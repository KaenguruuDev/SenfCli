using System.Net;

namespace SenfCli.Handlers;

public static class ShareCommandHandler
{
    public static async Task CreateShare()
    {
        var config = Config.Load();
        var project = config.GetCurrentProject();

        if (project == null)
        {
            ConsoleHelper.WriteError("No project found for current directory.");
            ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
            Environment.Exit(1);
        }

        var profile = LoadAndValidateProfileForProject(config, project);
        var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
        var client = new SenfApiClient(profile.ApiUrl, authHandler);

        var envFile = await client.GetEnvFileAsync(project.ProjectName);
        if (envFile == null)
        {
            ConsoleHelper.WriteError($"Env file not found on server for project '{project.ProjectName}'.");
            ConsoleHelper.WriteDetail("Push the env file first before creating shares.");
            Environment.Exit(1);
        }

        var usersResponse = await client.GetUsersAsync();
        var users = usersResponse?.Users ?? [];
        if (users.Count == 0)
        {
            ConsoleHelper.WriteError("No users returned by the server.");
            Environment.Exit(1);
        }

        var selectedUserId = PromptForUser(users);
        if (selectedUserId == null)
        {
            ConsoleHelper.WriteInfo("Share creation cancelled.");
            return;
        }

        var shareMode = PromptForShareMode();
        if (shareMode == null)
        {
            ConsoleHelper.WriteInfo("Share creation cancelled.");
            return;
        }

        var response = await client.ShareEnvFileAsync(envFile.Id, selectedUserId.Value, shareMode.Value);
        if (response != null)
        {
            ConsoleHelper.WriteSuccess("Share created successfully.");
            ConsoleHelper.WriteDetail($"Project: {response.EnvFileName}");
            ConsoleHelper.WriteDetail($"Shared to: {response.SharedToUsername} (ID: {response.SharedToUserId})");
            ConsoleHelper.WriteDetail($"Mode: {DescribeShareMode(response.ShareMode)}");
        }
    }

    public static async Task ShowStatus(bool showAll)
    {
        var config = Config.Load();
        ProjectConfig? project = null;

        if (!showAll)
        {
            project = config.GetCurrentProject();
            if (project == null)
            {
                ConsoleHelper.WriteError("No project found for current directory.");
                ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
                Environment.Exit(1);
            }
        }

        var profile = showAll
            ? LoadAndValidateDefaultProfile(config)
            : LoadAndValidateProfileForProject(config, project!);

        var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
        var client = new SenfApiClient(profile.ApiUrl, authHandler);

        var sharesResponse = await client.GetActiveSharesAsync();
        var sharedResponse = await client.GetSharedFilesAsync();

        var shares = sharesResponse?.Shares ?? [];
        var sharedFiles = sharedResponse?.Files ?? [];

        if (project != null)
        {
            shares = shares
                .Where(s => string.Equals(s.EnvFileName, project.ProjectName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            sharedFiles = sharedFiles
                .Where(f => string.Equals(f.EnvFileName, project.ProjectName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (shares.Count == 0 && sharedFiles.Count == 0)
        {
            ConsoleHelper.WriteInfo("No active shares found.");
            return;
        }

        if (shares.Count > 0)
        {
            ConsoleHelper.WriteSuccess($"Shares created by you ({shares.Count}):");
            foreach (var share in shares)
            {
                ConsoleHelper.WriteInfo($"Project: {share.EnvFileName}");
                ConsoleHelper.WriteDetail($"Shared to: {share.SharedToUsername} (ID: {share.SharedToUserId})");
                ConsoleHelper.WriteDetail($"Mode: {DescribeShareMode(share.ShareMode)}");
                ConsoleHelper.WriteDetail($"Created: {share.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                ConsoleHelper.WriteInfo(string.Empty);
            }
        }

        if (sharedFiles.Count > 0)
        {
            ConsoleHelper.WriteSuccess($"Shares available to you ({sharedFiles.Count}):");
            foreach (var shared in sharedFiles)
            {
                ConsoleHelper.WriteInfo($"Project: {shared.EnvFileName}");
                ConsoleHelper.WriteDetail($"Owner: {shared.OwnerUsername} (ID: {shared.OwnerUserId})");
                ConsoleHelper.WriteDetail($"Mode: {DescribeShareMode(shared.ShareMode)}");
                ConsoleHelper.WriteDetail($"Shared: {shared.SharedAt:yyyy-MM-dd HH:mm:ss}");
                ConsoleHelper.WriteInfo(string.Empty);
            }
        }
    }

    public static async Task RemoveShare()
    {
        var config = Config.Load();
        var project = config.GetCurrentProject();

        if (project == null)
        {
            ConsoleHelper.WriteError("No project found for current directory.");
            ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
            Environment.Exit(1);
        }

        var profile = LoadAndValidateProfileForProject(config, project);
        var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);
        var client = new SenfApiClient(profile.ApiUrl, authHandler);

        var sharesResponse = await client.GetActiveSharesAsync();
        var shares = sharesResponse?.Shares ?? [];
        shares = shares
            .Where(s => string.Equals(s.EnvFileName, project.ProjectName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (shares.Count == 0)
        {
            ConsoleHelper.WriteInfo("No shares found for this project.");
            return;
        }

        var selectedShare = PromptForShareRemoval(shares);
        if (selectedShare == null)
        {
            ConsoleHelper.WriteInfo("Share removal cancelled.");
            return;
        }

        try
        {
            await client.RemoveShareAsync(selectedShare.EnvFileId, selectedShare.SharedToUserId);
            ConsoleHelper.WriteSuccess("Share removed successfully.");
            ConsoleHelper.WriteDetail($"Project: {selectedShare.EnvFileName}");
            ConsoleHelper.WriteDetail($"User: {selectedShare.SharedToUsername} (ID: {selectedShare.SharedToUserId})");
        }
        catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            ConsoleHelper.WriteError("Share not found or already removed.");
            Environment.Exit(1);
        }
    }

    private static SshProfile LoadAndValidateProfileForProject(Config config, ProjectConfig project)
    {
        var profile = config.GetProfileForProject(project);
        if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
            string.IsNullOrWhiteSpace(profile.SshKeyPath) ||
            profile.SshKeyId < 0)
        {
            ConsoleHelper.WriteError("SSH credentials not configured.");
            ConsoleHelper.WriteDetail(
                "Run 'senf profile set <profile-name> --username <username> --ssh-key <path> --api-url <url>' first.");

            if (!string.IsNullOrEmpty(project.ProfileName))
                ConsoleHelper.WriteDetail($"This project uses profile: {project.ProfileName}");

            Environment.Exit(1);
        }

        return profile;
    }

    private static SshProfile LoadAndValidateDefaultProfile(Config config)
    {
        var profileName = config.DefaultProfile ?? (config.Profiles.ContainsKey("default") ? "default" : null);
        var profile = profileName != null ? config.GetProfile(profileName) : null;

        if (profile == null || string.IsNullOrWhiteSpace(profile.Username) ||
            string.IsNullOrWhiteSpace(profile.SshKeyPath) ||
            profile.SshKeyId < 0)
        {
            ConsoleHelper.WriteError("No valid default profile configured.");
            ConsoleHelper.WriteDetail(
                "Run 'senf profile set <profile-name> --username <username> --ssh-key <path> --api-url <url>' first.");
            Environment.Exit(1);
        }

        return profile;
    }

    private static int? PromptForUser(List<UserSummaryResponse> users)
    {
        ConsoleHelper.WriteSuccess($"Found {users.Count} user(s):");
        for (var i = 0; i < users.Count; i++)
        {
            var user = users[i];
            ConsoleHelper.WriteInfo($"[{i + 1}] {user.Username} (ID: {user.Id})");
        }

        while (true)
        {
            ConsoleHelper.Ask("Select a user by number or ID (leave empty to cancel): ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (!int.TryParse(input, out var value))
            {
                ConsoleHelper.WriteError("Enter a valid number.");
                continue;
            }

            var byIndex = value - 1;
            if (byIndex >= 0 && byIndex < users.Count)
                return users[byIndex].Id;

            if (users.Any(u => u.Id == value))
                return value;

            ConsoleHelper.WriteError("User not found. Try again.");
        }
    }

    private static ShareMode? PromptForShareMode()
    {
        ConsoleHelper.WriteInfo("Select share mode:");
        ConsoleHelper.WriteInfo("[1] read-only");
        ConsoleHelper.WriteInfo("[2] read-write");

        while (true)
        {
            ConsoleHelper.Ask("Choose 1 or 2 (leave empty to cancel): ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (input is "1" or "ro" or "read-only" or "readonly")
                return ShareMode.ReadOnly;

            if (input is "2" or "rw" or "read-write" or "readwrite")
                return ShareMode.ReadWrite;

            ConsoleHelper.WriteError("Invalid choice. Enter 1 for read-only or 2 for read-write.");
        }
    }

    private static ShareResponse? PromptForShareRemoval(List<ShareResponse> shares)
    {
        ConsoleHelper.WriteSuccess($"Found {shares.Count} share(s):");
        for (var i = 0; i < shares.Count; i++)
        {
            var share = shares[i];
            ConsoleHelper.WriteInfo($"[{i + 1}] {share.SharedToUsername} (ID: {share.SharedToUserId})");
            ConsoleHelper.WriteDetail($"Mode: {DescribeShareMode(share.ShareMode)}");
        }

        while (true)
        {
            ConsoleHelper.Ask("Select a share by number (leave empty to cancel): ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (!int.TryParse(input, out var value))
            {
                ConsoleHelper.WriteError("Enter a valid number.");
                continue;
            }

            var index = value - 1;
            if (index >= 0 && index < shares.Count)
                return shares[index];

            ConsoleHelper.WriteError("Selection out of range. Try again.");
        }
    }

    private static string DescribeShareMode(ShareMode mode)
        => mode == ShareMode.ReadWrite ? "read-write" : "read-only";
}
