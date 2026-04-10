using App.Core.Interfaces;
using App.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace App.Infrastructure.Repositories;

public sealed class FileRememberMeRepository : IRememberMeRepository
{
    private const string Header = "DPAPI";
    private readonly string _filePath;

    public FileRememberMeRepository(string filePath)
    {
        _filePath = filePath;
    }

    public RememberedCredential? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var lines = File.ReadAllLines(_filePath);
        if (lines.Length < 2)
        {
            return null;
        }

        if (lines[0] == Header && lines.Length >= 2)
        {
            return LoadEncrypted(lines[1]);
        }

        var legacyCredential = new RememberedCredential(lines[0], lines[1]);
        Save(legacyCredential);
        return legacyCredential;
    }

    public void Save(RememberedCredential credential)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Remember me encryption requires Windows.");
        }

        var payload = $"{credential.Account}\n{credential.Password}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var encryptedBytes = ProtectedData.Protect(payloadBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllLines(_filePath, new[] { Header, Convert.ToBase64String(encryptedBytes) });
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    private static RememberedCredential? LoadEncrypted(string encryptedPayload)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedPayload);
            var payloadBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            var payload = Encoding.UTF8.GetString(payloadBytes);
            var parts = payload.Split('\n', 2);
            if (parts.Length < 2)
            {
                return null;
            }

            return new RememberedCredential(parts[0], parts[1]);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
