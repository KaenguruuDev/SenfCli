namespace SenfCli;

using System.Net;

public class CommandHandlers
{
    public static async Task Init(string envPath, string projectName, string apiUrl = "http://localhost:5227")
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
                ApiUrl = apiUrl
            };

            config.AddOrUpdateProject(project);
            config.Save();

            ConsoleHelper.WriteSuccess($"Initialized project '{projectName}' with env file: {fullPath}");
            ConsoleHelper.WriteDetail($"Base path: {basePath}");
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

            if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf config <username> <ssh-key-path>' first.");
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

            var authHandler = new SshAuthHandler(config.SshKeyPath, config.Username);
            var client = new SenfApiClient(project.ApiUrl ?? "http://localhost:5227", authHandler);

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

            if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf config <username> <ssh-key-path>' first.");
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(config.SshKeyPath, config.Username);
            var client = new SenfApiClient(project.ApiUrl ?? "http://localhost:5227", authHandler);

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

    public static void SetCredentials(string username, string sshKeyPath)
    {
        try
        {
            if (!File.Exists(sshKeyPath))
            {
                ConsoleHelper.WriteError($"SSH key not found at: {sshKeyPath}");
                Environment.Exit(1);
            }

            var config = Config.Load();
            config.Username = username;
            config.SshKeyPath = sshKeyPath;

            config.Save();

            ConsoleHelper.WriteSuccess("Credentials configured successfully");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error setting credentials: {ex.Message}");
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

            if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf config <username> <ssh-key-path>' first.");
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(config.SshKeyPath, config.Username);
            var client = new SenfApiClient(project.ApiUrl ?? "http://localhost:5227", authHandler);

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

            if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf config <username> <ssh-key-path>' first.");
                Environment.Exit(1);
            }

            if (string.IsNullOrWhiteSpace(publicKey))
            {
                ConsoleHelper.WriteError("Public key cannot be empty.");
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(config.SshKeyPath, config.Username);
            var client = new SenfApiClient(project.ApiUrl ?? "http://localhost:5227", authHandler);

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

            if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.SshKeyPath))
            {
                ConsoleHelper.WriteError("SSH credentials not configured.");
                ConsoleHelper.WriteDetail("Run 'senf config <username> <ssh-key-path>' first.");
                Environment.Exit(1);
            }

            var authHandler = new SshAuthHandler(config.SshKeyPath, config.Username);
            var client = new SenfApiClient(project.ApiUrl ?? "http://localhost:5227", authHandler);

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
