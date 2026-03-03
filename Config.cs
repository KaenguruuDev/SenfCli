using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenfCli;

public class SshProfile
{
    public string? Username { get; set; }
    public string? SshKeyPath { get; set; }
    public string? ApiUrl { get; set; }
}

public class Config
{
    public string? DefaultProfile { get; set; }

    [JsonPropertyName("profiles")]
    public Dictionary<string, SshProfile> Profiles { get; set; } = new();

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
        return JsonSerializer.Deserialize<Config>(json) ?? new Config();
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

    public SshProfile? GetProfileForProject(ProjectConfig? project)
    {
        if (project == null)
            return null;

        // Try project-specific profile first
        if (!string.IsNullOrEmpty(project.ProfileName) && Profiles.TryGetValue(project.ProfileName, out var projectProfile))
            return projectProfile;

        // Fall back to default profile
        if (!string.IsNullOrEmpty(DefaultProfile) && Profiles.TryGetValue(DefaultProfile, out var defaultProfile))
            return defaultProfile;

        // If no default set, try "default" profile name
        if (Profiles.TryGetValue("default", out var namedDefault))
            return namedDefault;

        return null;
    }

    public SshProfile? GetProfile(string? profileName)
    {
        if (string.IsNullOrEmpty(profileName))
            return null;

        return Profiles.TryGetValue(profileName, out var profile) ? profile : null;
    }
}

public class ProjectConfig
{
    public string? ProjectName { get; set; }
    public string? EnvPath { get; set; }
    public string? BasePath { get; set; }
    public string? ProfileName { get; set; }
}
