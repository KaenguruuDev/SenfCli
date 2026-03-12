namespace SenfCli;

public static class SshKeyValidation
{
    public static bool TryValidateEd25519PublicKey(string publicKey, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(publicKey))
        {
            error = "Public key cannot be empty.";
            return false;
        }

        var parts = publicKey.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = "Unsupported key format: expected '<type> <base64> [comment]'.";
            return false;
        }

        var keyType = parts[0];
        var keyData = parts[1];

        if (!string.Equals(keyType, "ssh-ed25519", StringComparison.Ordinal))
        {
            error = $"Unsupported key format: '{keyType}'.";
            return false;
        }

        try
        {
            _ = Convert.FromBase64String(keyData);
        }
        catch
        {
            error = "Unsupported key format: invalid base64 payload.";
            return false;
        }

        return true;
    }
}
