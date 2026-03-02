# Senf - Environment File Management CLI

Senf is a command-line tool for managing environment files (.env) across multiple projects. It provides functionality to securely push and pull environment files to and from a remote server using SSH authentication.

## Installation

### Building from Source

```powershell
dotnet build
```

For release build:

```powershell
dotnet build --configuration Release
```

For published executable:

```powershell
dotnet publish --configuration Release --output ./bin/publish
```

The executable will be available at `./bin/publish/SenfCli.exe` on Windows.

### Running the Tool

You can run Senf directly with:

```bash
dotnet run -- <command> [options]
```

Or use the published executable:

```bash
./SenfCli.exe <command> [options]
```

## Configuration

Before using Senf, you need to configure your SSH credentials. Each project can have its own API URL.

### Initial Setup

Set your SSH credentials once:

```bash
senf config <username> <ssh-key-path>
```

Example:

```bash
senf config myuser C:\Users\myuser\.ssh\id_rsa
```

Your configuration is stored in `~/.senf/config.json`.

## Usage

### Initialize a Project

Set up a new project to manage its .env file:

```bash
senf init <path-to-env> <project-name> [--api-url <url>]
```

Example:

```bash
senf init .env MyWebApp
senf init config/.env.local MyService --api-url "http://192.168.1.100:5227"
```

This creates the specified .env file if it doesn't exist and registers the project in your configuration. If no API URL is specified, it defaults to `http://localhost:5227`.

### Push Environment File

Upload your local .env file to the server:

```bash
senf push
```

This command must be run from the project directory or a subdirectory of the project's base path. The .env file must contain at least one key=value pair.

### Pull Environment File

Download the .env file from the server:

```bash
senf pull
```

This command must be run from the project directory or a subdirectory of the project's base path. The downloaded file will overwrite your local .env file.

### View Help

Display available commands:

```bash
senf help
senf -h
senf --help
```

## Workflow Example

1. Configure credentials:

```bash
senf config developer C:\Users\developer\.ssh\id_rsa
```

2. Initialize a project with your API server URL:

```bash
cd C:\Projects\MyApp
senf init .env MyApp --api-url "http://192.168.1.100:5227"
```

3. Create your .env file:

```
DATABASE_URL=postgresql://localhost/mydb
API_KEY=secret_key_here
```

4. Push to the server:

```bash
senf push
```

5. On another machine, initialize with the same API URL and pull:

```bash
cd C:\Projects\MyApp
senf init .env MyApp --api-url "http://192.168.1.100:5227"
senf pull
```

## Configuration File Location

Senf stores configuration at:

- Linux/macOS: `~/.senf/config.json`
- Windows: `%USERPROFILE%\.senf\config.json`

Example config.json:

```json
{
  "username": "myuser",
  "sshKeyPath": "C:\\Users\\myuser\\.ssh\\id_rsa",
  "projects": [
    {
      "projectName": "MyApp",
      "envPath": "C:\\Projects\\MyApp\\.env",
      "basePath": "C:\\Projects\\MyApp",
      "apiUrl": "http://192.168.1.100:5227"
    },
    {
      "projectName": "MyService",
      "envPath": "C:\\Projects\\MyService\\config\\.env.local",
      "basePath": "C:\\Projects\\MyService",
      "apiUrl": "http://localhost:5227"
    }
  ]
}
```

Note that each project has its own API URL setting, allowing you to point different projects to different API servers.

## Requirements

- .NET 10.0 or higher
- SSH key for authentication
- Access to a Senf API server

## Error Handling

The tool provides descriptive error messages when issues occur. Common error scenarios include:

- No project initialized in current directory
- SSH credentials not configured
- .env file not found or is empty
- SSH key file does not exist
- Server connection issues
