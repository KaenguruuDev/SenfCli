using Renci.SshNet;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SenfCli;

public enum SignatureEncodingMode
{
    BackendCompatible,
    Raw,
    SshBlob
}

public class SshAuthHandler
{
    private readonly string _sshKeyPath;
    private readonly string _username;
    private PrivateKeyFile? _keyFile;

    public SshAuthHandler(string sshKeyPath, string username)
    {
        _sshKeyPath = sshKeyPath;
        _username = username;
    }

    public void TestKeyLoad()
    {
        try
        {
            _ = GetKeyFile();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load SSH key from '{_sshKeyPath}': {ex.Message}", ex);
        }
    }

    public string GetPublicKeyString()
    {
        var keyFile = GetKeyFile();

        // Serialize the public key to SSH wire format
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write key type (e.g., "ssh-rsa")
        var keyTypeString = keyFile.Key.ToString() ?? "ssh-rsa";
        var keyType = keyTypeString.Split(' ')[0];
        WriteString(writer, keyType);

        // Write public key parameters
        foreach (var param in keyFile.Key.Public)
        {
            WriteBigInteger(writer, param);
        }

        var publicKeyBytes = ms.ToArray();

        // Compute SHA256 fingerprint
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(publicKeyBytes);

        // Return in SSH format: SHA256:base64
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
    }

    private void WriteString(BinaryWriter writer, string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        writer.Write(IPAddress.HostToNetworkOrder(bytes.Length));
        writer.Write(bytes);
    }

    private void WriteBigInteger(BinaryWriter writer, Renci.SshNet.Common.BigInteger bigInt)
    {
        // Convert BigInteger to byte array in SSH format
        var bytes = bigInt.ToByteArray().Reverse().ToArray();

        // Remove leading zeros, but keep one if the high bit is set
        int i = 0;
        while (i < bytes.Length - 1 && bytes[i] == 0 && bytes[i + 1] < 0x80)
        {
            i++;
        }

        var trimmedBytes = new byte[bytes.Length - i];
        Array.Copy(bytes, i, trimmedBytes, 0, trimmedBytes.Length);

        writer.Write(IPAddress.HostToNetworkOrder(trimmedBytes.Length));
        writer.Write(trimmedBytes);
    }

    private PrivateKeyFile GetKeyFile()
    {
        _keyFile ??= new PrivateKeyFile(_sshKeyPath);
        return _keyFile;
    }

    public void AddAuthHeaders(HttpRequestMessage request, SignatureEncodingMode mode = SignatureEncodingMode.BackendCompatible)
    {
        var keyFile = GetKeyFile();
        var nonce = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var message = $"{timestamp}:{nonce}";

        var signature = SignMessage(message, keyFile, mode);

        request.Headers.Add("X-SSH-Username", _username);
        request.Headers.Add("X-SSH-Message", timestamp.ToString());
        request.Headers.Add("X-SSH-Nonce", nonce);
        request.Headers.Add("X-SSH-Signature", signature);
    }

    private string SignMessage(string message, PrivateKeyFile keyFile, SignatureEncodingMode mode)
    {
        // On non-Windows, try using ssh-keygen directly for more reliable signing
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var sshKeygenSignature = SignWithSshKeygen(message);
                if (sshKeygenSignature != null)
                    return sshKeygenSignature;
            }
            catch
            {
                // Fall back to SSH.NET if ssh-keygen fails
            }
        }

        var data = Encoding.UTF8.GetBytes(message);
        var signatureBlob = keyFile.Key.Sign(data);

        return mode switch
        {
            SignatureEncodingMode.BackendCompatible => Convert.ToBase64String(ToBackendCompatibleSignature(signatureBlob)),
            SignatureEncodingMode.SshBlob => Convert.ToBase64String(signatureBlob),
            _ => Convert.ToBase64String(TryExtractRawSignature(signatureBlob))
        };
    }

    private string? SignWithSshKeygen(string message)
    {
        try
        {
            var tempFile = Path.GetTempFileName();
            var sigFile = tempFile + ".sig";

            try
            {
                File.WriteAllText(tempFile, message);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "ssh-keygen",
                    Arguments = $"-Y sign -f \"{_sshKeyPath}\" -n file \"{tempFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return null;

                process.WaitForExit(5000);

                if (process.ExitCode != 0 || !File.Exists(sigFile))
                    return null;

                var sigContent = File.ReadAllText(sigFile);
                var sigLines = sigContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                // Extract the base64 signature from ssh-keygen output
                var signatureBase64 = string.Join("", sigLines.Where(l => !l.StartsWith("-----")));

                return signatureBase64;
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
                if (File.Exists(sigFile))
                    File.Delete(sigFile);
            }
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ToBackendCompatibleSignature(byte[] signatureBlob)
    {
        if (TryParseSshBlob(signatureBlob, out var algorithmLength, out var rawSignature))
            return BuildBackendSignature(rawSignature, algorithmLength);

        return BuildBackendSignature(signatureBlob, 1);
    }

    private static bool TryParseSshBlob(byte[] signatureBlob, out uint algorithmLength, out byte[] rawSignature)
    {
        algorithmLength = 0;
        rawSignature = Array.Empty<byte>();

        if (signatureBlob.Length < 8)
            return false;

        try
        {
            var offset = 0;
            algorithmLength = ReadUInt32Be(signatureBlob, ref offset);
            if (algorithmLength <= 0 || algorithmLength > 256 || offset + algorithmLength > signatureBlob.Length)
                return false;

            offset += (int)algorithmLength;

            var signatureLength = ReadUInt32Be(signatureBlob, ref offset);
            if (signatureLength <= 0 || signatureLength > 8192 || offset + signatureLength > signatureBlob.Length)
                return false;

            rawSignature = new byte[signatureLength];
            Buffer.BlockCopy(signatureBlob, offset, rawSignature, 0, (int)signatureLength);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] BuildBackendSignature(byte[] rawSignature, uint algorithmLength)
    {
        if (algorithmLength is 0 or > 256)
            algorithmLength = 1;

        var signatureLength = (uint)rawSignature.Length;
        var result = new byte[8 + signatureLength];

        WriteUInt32Be(result, 0, algorithmLength);
        WriteUInt32Be(result, 4, signatureLength);
        Buffer.BlockCopy(rawSignature, 0, result, 8, rawSignature.Length);
        return result;
    }

    private static byte[] TryExtractRawSignature(byte[] signatureBlob)
    {
        if (signatureBlob.Length < 8)
            return signatureBlob;

        try
        {
            var offset = 0;
            var algorithmLength = ReadUInt32Be(signatureBlob, ref offset);
            if (algorithmLength <= 0 || offset + algorithmLength > signatureBlob.Length)
                return signatureBlob;

            offset += (int)algorithmLength;

            var signatureLength = ReadUInt32Be(signatureBlob, ref offset);
            if (signatureLength <= 0 || offset + signatureLength > signatureBlob.Length)
                return signatureBlob;

            var raw = new byte[signatureLength];
            Buffer.BlockCopy(signatureBlob, offset, raw, 0, (int)signatureLength);
            return raw;
        }
        catch
        {
            return signatureBlob;
        }
    }

    private static uint ReadUInt32Be(byte[] bytes, ref int offset)
    {
        if (offset + 4 > bytes.Length)
            throw new InvalidOperationException("Invalid SSH signature blob.");

        var value = ((uint)bytes[offset] << 24)
                    | ((uint)bytes[offset + 1] << 16)
                    | ((uint)bytes[offset + 2] << 8)
                    | bytes[offset + 3];

        offset += 4;
        return value;
    }

    private static void WriteUInt32Be(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)((value >> 24) & 0xFF);
        bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 3] = (byte)(value & 0xFF);
    }
}


