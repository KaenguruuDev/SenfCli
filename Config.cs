using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenfCli;

public class Config
{
    public string? Username { get; set; }
    public string? SshKeyPath { get; set; }

    [JsonPropertyName("projects")]
    public List<ProjectConfig> Projects { get; set; } = new();

    private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".senf");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    public static Config Load()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        if (!File.Exists(ConfigFile))
            return new Config();

        var json = File.ReadAllText(ConfigFile);
        var config = JsonSerializer.Deserialize<Config>(json) ?? new Config();

        // Migrate old config format
        var needsSave = false;
        if (File.Exists(ConfigFile))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("CurrentProjectName", out var projName) &&
                    root.TryGetProperty("CurrentEnvPath", out var envPath))
                {
                    var oldProject = new ProjectConfig
                    {
                        ProjectName = projName.GetString(),
                        EnvPath = envPath.GetString(),
                        ApiUrl = root.TryGetProperty("ApiUrl", out var apiUrl)
                            ? apiUrl.GetString()
                            : "http://localhost:5227",
                        BasePath = envPath.GetString() != null
                            ? Path.GetDirectoryName(envPath.GetString()!)
                            : null
                    };

                    if (!string.IsNullOrEmpty(oldProject.BasePath) &&
                        config.GetProjectByBasePath(oldProject.BasePath) == null)
                    {
                        config.Projects.Add(oldProject);
                        needsSave = true;
                    }
                }
            }
            catch
            {
                // Ignore migration errors
            }
        }

        // Save if we migrated to clean up old format
        if (needsSave)
        {
            config.Save();
        }

        return config;
    }

    public void Save()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFile, json);
    }

    public ProjectConfig? GetProjectByBasePath(string basePath)
    {
        var normalizedPath = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Projects.FirstOrDefault(p =>
        {
            if (string.IsNullOrEmpty(p.BasePath)) return false;
            var projectPath = Path.GetFullPath(p.BasePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(projectPath, normalizedPath, StringComparison.OrdinalIgnoreCase);
        });
    }

    public ProjectConfig? GetCurrentProject()
    {
        var cwd = Directory.GetCurrentDirectory();

        // Try exact match first
        var exactMatch = GetProjectByBasePath(cwd);
        if (exactMatch != null)
            return exactMatch;

        // Try parent directories
        var current = new DirectoryInfo(cwd);
        while (current != null)
        {
            var match = GetProjectByBasePath(current.FullName);
            if (match != null)
                return match;
            current = current.Parent;
        }

        return null;
    }

    public void AddOrUpdateProject(ProjectConfig project)
    {
        if (string.IsNullOrEmpty(project.BasePath))
            return;

        var existing = GetProjectByBasePath(project.BasePath);
        if (existing != null)
        {
            Projects.Remove(existing);
        }
        Projects.Add(project);
    }
}

public class ProjectConfig
{
    public string? ApiUrl { get; set; }
    public string? ProjectName { get; set; }
    public string? EnvPath { get; set; }
    public string? BasePath { get; set; }
}
