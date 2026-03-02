using Renci.SshNet;
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
        var data = Encoding.UTF8.GetBytes(message);

        var signatureBlob = keyFile.Key.Sign(data);

        return mode switch
        {
            SignatureEncodingMode.BackendCompatible => Convert.ToBase64String(ToBackendCompatibleSignature(signatureBlob)),
            SignatureEncodingMode.SshBlob => Convert.ToBase64String(signatureBlob),
            _ => Convert.ToBase64String(TryExtractRawSignature(signatureBlob))
        };
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


