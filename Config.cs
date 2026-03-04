using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace SenfCli;

public class SshProfile
{
	[JsonPropertyName("username")]
	public string? Username { get; set; }

	[JsonPropertyName("ssh_key_path")]
	public string? SshKeyPath { get; set; }

	[JsonPropertyName("api_url")]
	public string? ApiUrl { get; set; }

	[JsonPropertyName("ssh_key_id")]
	public int? SshKeyId { get; set; }
}

public class ProjectConfig
{
	[JsonPropertyName("project_name")]
	public string? ProjectName { get; set; }

	[JsonPropertyName("env_path")]
	public string? EnvPath { get; set; }

	[JsonPropertyName("base_path")]
	public string? BasePath { get; set; }

	[JsonPropertyName("profile_name")]
	public string? ProfileName { get; set; }
}

public class FileHashInfo
{
	[JsonPropertyName("hash")]
	public string Hash { get; set; } = null!;

	[JsonPropertyName("timestamp")]
	public DateTimeOffset Timestamp { get; set; }
}

public class Config
{
	[JsonPropertyName("default_profile")]
	public string? DefaultProfile { get; set; }

	[JsonPropertyName("profiles")]
	public Dictionary<string, SshProfile> Profiles { get; set; } = new();

	[JsonPropertyName("projects")]
	public List<ProjectConfig> Projects { get; set; } = new();

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private static readonly string ConfigDir =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".senf");

	private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");
	private static readonly string FileHashesFile = Path.Combine(ConfigDir, "file-hashes.json");

	public static Config Load()
	{
		if (!Directory.Exists(ConfigDir))
			Directory.CreateDirectory(ConfigDir);

		if (!File.Exists(ConfigFile))
			return new Config();

		var json = File.ReadAllText(ConfigFile);
		return JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? new Config();
	}

	public void Save()
	{
		if (!Directory.Exists(ConfigDir))
			Directory.CreateDirectory(ConfigDir);

		var json = JsonSerializer.Serialize(this, JsonOptions);
		File.WriteAllText(ConfigFile, json);
	}

	public ProjectConfig? GetProjectByBasePath(string basePath)
	{
		var normalizedPath = Path.GetFullPath(basePath)
			.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return Projects.FirstOrDefault(p =>
		{
			if (string.IsNullOrEmpty(p.BasePath)) return false;
			var projectPath = Path.GetFullPath(p.BasePath)
				.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return string.Equals(projectPath, normalizedPath, StringComparison.OrdinalIgnoreCase);
		});
	}

	public ProjectConfig? GetCurrentProject()
	{
		var cwd = Directory.GetCurrentDirectory();

		var exactMatch = GetProjectByBasePath(cwd);
		if (exactMatch != null)
			return exactMatch;

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
			Projects.Remove(existing);

		Projects.Add(project);
	}

	public SshProfile? GetProfileForProject(ProjectConfig? project)
	{
		if (project == null)
			return null;

		if (!string.IsNullOrEmpty(project.ProfileName) &&
		    Profiles.TryGetValue(project.ProfileName, out var projectProfile))
			return projectProfile;

		if (!string.IsNullOrEmpty(DefaultProfile) && Profiles.TryGetValue(DefaultProfile, out var defaultProfile))
			return defaultProfile;

		return Profiles.GetValueOrDefault("default");
	}

	public SshProfile? GetProfile(string? profileName) =>
		!string.IsNullOrEmpty(profileName)
			? Profiles.GetValueOrDefault(profileName)
			: null;

	public static string ComputeFileHash(string filePath)
	{
		using var sha256 = SHA256.Create();
		using var stream = File.OpenRead(filePath);
		var hash = sha256.ComputeHash(stream);
		return Convert.ToHexString(hash);
	}

	public static void SaveFileHash(string projectName, int sshKeyId, string hash)
	{
		if (!Directory.Exists(ConfigDir))
			Directory.CreateDirectory(ConfigDir);

		var hashes = LoadFileHashes();
		var key = $"{projectName}:{sshKeyId}";
		hashes[key] = new FileHashInfo { Hash = hash, Timestamp = DateTimeOffset.UtcNow };

		var json = JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true });
		File.WriteAllText(FileHashesFile, json);
	}

	public static FileHashInfo? GetFileHash(string projectName, int sshKeyId)
	{
		var hashes = LoadFileHashes();
		var key = $"{projectName}:{sshKeyId}";
		return hashes.GetValueOrDefault(key);
	}

	private static Dictionary<string, FileHashInfo> LoadFileHashes()
	{
		if (!File.Exists(FileHashesFile))
			return new Dictionary<string, FileHashInfo>();

		try
		{
			var json = File.ReadAllText(FileHashesFile);
			return JsonSerializer.Deserialize<Dictionary<string, FileHashInfo>>(json, JsonOptions) ??
			       new Dictionary<string, FileHashInfo>();
		}
		catch
		{
			return [];
		}
	}
}