namespace SenfCli;

using System.Net;

public class CommandHandlers
{
    public static async Task Init(string envPath, string projectName, string? profileName = null)
    {
        try
        {
            var config = Config.Load();

            var fullPath = Path.GetFullPath(envPath);
            var directory = Path.GetDirectoryName(fullPath);
            var basePath = directory ?? Directory.GetCurrentDirectory();

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
                ConsoleHelper.WriteInfo($"Created directory: {directory}");
            }

            if (!File.Exists(fullPath))
            {
                File.WriteAllText(fullPath, string.Empty);
                ConsoleHelper.WriteInfo($"Created new .env file: {fullPath}");
            }
            else
            {
                ConsoleHelper.WriteInfo($"Using existing .env file: {fullPath}");
            }

            var project = new ProjectConfig
            {
                ProjectName = projectName,
                EnvPath = fullPath,
                BasePath = basePath,
                ProfileName = profileName
            };

            config.AddOrUpdateProject(project);
            config.Save();

            ConsoleHelper.WriteSuccess($"Initialized project '{projectName}' with env file: {fullPath}");
            ConsoleHelper.WriteDetail($"Base path: {basePath}");
            if (!string.IsNullOrEmpty(profileName))
            {
                ConsoleHelper.WriteDetail($"Profile: {profileName}");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error during init: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task Push()
    {
        try
        {
            var config = Config.Load();
            var project = config.GetCurrentProject();

            if (project == null)
            {
                ConsoleHelper.WriteError("No project found for current directory.");
                ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
                Environment.Exit(1);
            }

            var profile = config.GetProfileForProject(project);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Username) || string.IsNullOrWhiteSpace(profile.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf profile set <profile-name> --username <username> --ssh-key <path>' first.");
                if (!string.IsNullOrEmpty(project.ProfileName))
                {
                    ConsoleHelper.WriteDetail($"This project uses profile: {project.ProfileName}");
                }
                Environment.Exit(1);
            }

            if (string.IsNullOrWhiteSpace(project.EnvPath) || !File.Exists(project.EnvPath))
            {
                ConsoleHelper.WriteError($"Env file not found: {project.EnvPath}");
                Environment.Exit(1);
            }

            var content = File.ReadAllText(project.EnvPath);
            if (string.IsNullOrWhiteSpace(content))
            {
                ConsoleHelper.WriteError($"Env file is empty: {project.EnvPath}");
                ConsoleHelper.WriteDetail("Add at least one key=value line before pushing.");
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(profile.SshKeyPath!, profile.Username!);
            var client = new SenfApiClient(profile.ApiUrl ?? "http://localhost:5227", authHandler);

            try
            {
                await client.UpdateEnvFileAsync(project.ProjectName!, content);
                ConsoleHelper.WriteSuccess($"Pushed env file for project '{project.ProjectName}'");
            }
            catch (SenfApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                await client.CreateOrReplaceEnvFileAsync(project.ProjectName!, content);
                ConsoleHelper.WriteSuccess($"Created project '{project.ProjectName}' and pushed env file");
            }

        }
        catch (SenfApiException ex)
        {
            ConsoleHelper.WriteError(ex.Message);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error during push: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task Pull()
    {
        try
        {
            var config = Config.Load();
            var project = config.GetCurrentProject();

            if (project == null)
            {
                ConsoleHelper.WriteError("No project found for current directory.");
                ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
                Environment.Exit(1);
            }

            var profile = config.GetProfileForProject(project);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Username) || string.IsNullOrWhiteSpace(profile.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf profile set <profile-name> --username <username> --ssh-key <path>' first.");
                if (!string.IsNullOrEmpty(project.ProfileName))
                {
                    ConsoleHelper.WriteDetail($"This project uses profile: {project.ProfileName}");
                }
                Environment.Exit(1);
            }

            if (!File.Exists(profile.SshKeyPath))
            {
                ConsoleHelper.WriteError($"SSH key file not found: {profile.SshKeyPath}");
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(profile.SshKeyPath, profile.Username);

            // Test the key can be loaded
            try
            {
                authHandler.TestKeyLoad();
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Failed to load SSH key: {ex.Message}");
                ConsoleHelper.WriteDetail($"Key path: {profile.SshKeyPath}");
                Environment.Exit(1);
            }

            var client = new SenfApiClient(profile.ApiUrl ?? "http://localhost:5227", authHandler);

            var envFile = await client.GetEnvFileAsync(project.ProjectName!);

            if (envFile == null)
            {
                ConsoleHelper.WriteError($"Env file not found on server for project '{project.ProjectName}'");
                Environment.Exit(1);
            }

            var directory = Path.GetDirectoryName(project.EnvPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            File.WriteAllText(project.EnvPath!, envFile.Content ?? string.Empty);

            ConsoleHelper.WriteSuccess($"Pulled env file for project '{project.ProjectName}'");
            ConsoleHelper.WriteDetail($"Saved to: {project.EnvPath}");
        }
        catch (SenfApiException ex)
        {
            ConsoleHelper.WriteError(ex.Message);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error during pull: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static void CreateOrUpdateProfile(string profileName, string? username = null, string? sshKeyPath = null, string? apiUrl = null, bool setAsDefault = false)
    {
        try
        {
            var config = Config.Load();

            // Get existing profile or create new one
            if (!config.Profiles.TryGetValue(profileName, out var profile))
            {
                profile = new SshProfile();
                config.Profiles[profileName] = profile;
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(username))
            {
                profile.Username = username;
            }

            if (!string.IsNullOrEmpty(sshKeyPath))
            {
                if (!File.Exists(sshKeyPath))
                {
                    ConsoleHelper.WriteError($"SSH key not found at: {sshKeyPath}");
                    Environment.Exit(1);
                }
                profile.SshKeyPath = sshKeyPath;
            }

            if (!string.IsNullOrEmpty(apiUrl))
            {
                profile.ApiUrl = apiUrl;
            }

            if (setAsDefault)
            {
                config.DefaultProfile = profileName;
            }

            config.Save();

            ConsoleHelper.WriteSuccess($"Profile '{profileName}' configured successfully");
            if (!string.IsNullOrEmpty(username))
            {
                ConsoleHelper.WriteDetail($"Username: {username}");
            }
            if (!string.IsNullOrEmpty(sshKeyPath))
            {
                ConsoleHelper.WriteDetail($"SSH Key: {sshKeyPath}");
            }
            if (!string.IsNullOrEmpty(apiUrl))
            {
                ConsoleHelper.WriteDetail($"API URL: {apiUrl}");
            }
            if (setAsDefault)
            {
                ConsoleHelper.WriteDetail($"Set as default profile");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error configuring profile: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static void ListProfiles()
    {
        try
        {
            var config = Config.Load();

            if (config.Profiles.Count == 0)
            {
                ConsoleHelper.WriteInfo("No profiles configured");
                ConsoleHelper.WriteDetail("Run 'senf profile set <name> --username <username> --ssh-key <path>' to create one.");
                return;
            }

            ConsoleHelper.WriteSuccess($"Found {config.Profiles.Count} profile(s):");
            foreach (var (name, profile) in config.Profiles)
            {
                var isDefault = name == config.DefaultProfile;
                var marker = isDefault ? " (default)" : "";
                Console.WriteLine($"\n  {name}{marker}");
                Console.WriteLine($"    Username: {profile.Username ?? "(not set)"}");
                Console.WriteLine($"    SSH Key: {profile.SshKeyPath ?? "(not set)"}");
                Console.WriteLine($"    API URL: {profile.ApiUrl ?? "(not set)"}");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error listing profiles: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static void DeleteProfile(string profileName)
    {
        try
        {
            var config = Config.Load();

            if (!config.Profiles.ContainsKey(profileName))
            {
                ConsoleHelper.WriteError($"Profile '{profileName}' not found");
                Environment.Exit(1);
            }

            config.Profiles.Remove(profileName);

            // Clear default if we deleted the default profile
            if (config.DefaultProfile == profileName)
            {
                config.DefaultProfile = null;
            }

            // Clear profile reference from projects using this profile
            foreach (var project in config.Projects.Where(p => p.ProfileName == profileName))
            {
                project.ProfileName = null;
            }

            config.Save();

            ConsoleHelper.WriteSuccess($"Profile '{profileName}' deleted successfully");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error deleting profile: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static void SetDefaultProfile(string profileName)
    {
        try
        {
            var config = Config.Load();

            if (!config.Profiles.ContainsKey(profileName))
            {
                ConsoleHelper.WriteError($"Profile '{profileName}' not found");
                Environment.Exit(1);
            }

            config.DefaultProfile = profileName;
            config.Save();

            ConsoleHelper.WriteSuccess($"Default profile set to '{profileName}'");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error setting default profile: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static void SetProjectProfile(string? profileName)
    {
        try
        {
            var config = Config.Load();
            var project = config.GetCurrentProject();

            if (project == null)
            {
                ConsoleHelper.WriteError("No project found for current directory.");
                ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
                Environment.Exit(1);
            }

            if (!string.IsNullOrEmpty(profileName) && !config.Profiles.ContainsKey(profileName))
            {
                ConsoleHelper.WriteError($"Profile '{profileName}' not found");
                Environment.Exit(1);
            }

            project.ProfileName = profileName;
            config.Save();

            if (string.IsNullOrEmpty(profileName))
            {
                ConsoleHelper.WriteSuccess($"Cleared profile for project '{project.ProjectName}' (will use default)");
            }
            else
            {
                ConsoleHelper.WriteSuccess($"Set profile '{profileName}' for project '{project.ProjectName}'");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error setting project profile: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task ListSshKeys()
    {
        try
        {
            var config = Config.Load();
            var project = config.GetCurrentProject();

            if (project == null)
            {
                ConsoleHelper.WriteError("No project found for current directory.");
                ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
                Environment.Exit(1);
            }

            var profile = config.GetProfileForProject(project);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Username) || string.IsNullOrWhiteSpace(profile.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf profile set <profile-name> --username <username> --ssh-key <path>' first.");
                if (!string.IsNullOrEmpty(project.ProfileName))
                {
                    ConsoleHelper.WriteDetail($"This project uses profile: {project.ProfileName}");
                }
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(profile.SshKeyPath!, profile.Username!);
            var client = new SenfApiClient(profile.ApiUrl ?? "http://localhost:5227", authHandler);

            var response = await client.GetSshKeysAsync();

            if (response?.Keys == null || response.Keys.Count == 0)
            {
                ConsoleHelper.WriteInfo("No SSH keys configured");
                return;
            }

            ConsoleHelper.WriteSuccess($"Found {response.Keys.Count} SSH key(s):");
            foreach (var key in response.Keys)
            {
                Console.WriteLine($"  ID: {key.Id}");
                Console.WriteLine($"  Name: {key.Name}");
                Console.WriteLine($"  Fingerprint: {key.Fingerprint}");
                Console.WriteLine($"  Created: {key.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();
            }
        }
        catch (SenfApiException ex)
        {
            ConsoleHelper.WriteError(ex.Message);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error listing SSH keys: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task AddSshKey(string publicKey, string keyName)
    {
        try
        {
            var config = Config.Load();
            var project = config.GetCurrentProject();

            if (project == null)
            {
                ConsoleHelper.WriteError("No project found for current directory.");
                ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
                Environment.Exit(1);
            }

            var profile = config.GetProfileForProject(project);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Username) || string.IsNullOrWhiteSpace(profile.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf profile set <profile-name> --username <username> --ssh-key <path>' first.");
                if (!string.IsNullOrEmpty(project.ProfileName))
                {
                    ConsoleHelper.WriteDetail($"This project uses profile: {project.ProfileName}");
                }
                Environment.Exit(1);
            }

            if (string.IsNullOrWhiteSpace(publicKey))
            {
                ConsoleHelper.WriteError("Public key cannot be empty.");
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(profile.SshKeyPath!, profile.Username!);
            var client = new SenfApiClient(profile.ApiUrl ?? "http://localhost:5227", authHandler);

            var response = await client.CreateSshKeyAsync(publicKey, keyName);

            if (response != null)
            {
                ConsoleHelper.WriteSuccess($"SSH key '{keyName}' added successfully");
                ConsoleHelper.WriteDetail($"ID: {response.Id}");
                ConsoleHelper.WriteDetail($"Fingerprint: {response.Fingerprint}");
            }
        }
        catch (SenfApiException ex)
        {
            ConsoleHelper.WriteError(ex.Message);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error adding SSH key: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task DeleteSshKey(int keyId)
    {
        try
        {
            var config = Config.Load();
            var project = config.GetCurrentProject();

            if (project == null)
            {
                ConsoleHelper.WriteError("No project found for current directory.");
                ConsoleHelper.WriteDetail("Run 'senf init [path-to-env] [project-name]' first.");
                Environment.Exit(1);
            }

            var profile = config.GetProfileForProject(project);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Username) || string.IsNullOrWhiteSpace(profile.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf profile set <profile-name> --username <username> --ssh-key <path>' first.");
                if (!string.IsNullOrEmpty(project.ProfileName))
                {
                    ConsoleHelper.WriteDetail($"This project uses profile: {project.ProfileName}");
                }
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(profile.SshKeyPath!, profile.Username!);
            var client = new SenfApiClient(profile.ApiUrl ?? "http://localhost:5227", authHandler);

            await client.DeleteSshKeyAsync(keyId);
            ConsoleHelper.WriteSuccess($"SSH key {keyId} deleted successfully");
        }
        catch (SenfApiException ex)
        {
            ConsoleHelper.WriteError(ex.Message);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error deleting SSH key: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
