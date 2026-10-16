using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AgentController.Services;

/// <summary>
/// Keeps the API keys entered in the interface in a local file.
/// Each key is encrypted with ASP.NET Core Data Protection, the file never holds a key in clear text.
/// </summary>
public class ApiKeyStore
{
    private readonly string _filePath;
    private readonly IDataProtector _protector;
    private readonly object _lock = new();

    public ApiKeyStore(string filePath, IDataProtectionProvider dataProtection)
    {
        _filePath = filePath;
        _protector = dataProtection.CreateProtector("AgentController.ApiKeys");
    }

    public string? Get(string provider)
    {
        lock (_lock)
        {
            if (!ReadFile().TryGetValue(provider, out var encrypted))
                return null;

            try
            {
                return _protector.Unprotect(encrypted);
            }
            catch (CryptographicException)
            {
                // The Data Protection keys changed (new machine or user): the saved key cannot be read any more
                return null;
            }
        }
    }

    public void Set(string provider, string key)
    {
        lock (_lock)
        {
            var keys = ReadFile();
            keys[provider] = _protector.Protect(key);
            WriteFile(keys);
        }
    }

    public void Delete(string provider)
    {
        lock (_lock)
        {
            var keys = ReadFile();
            if (keys.Remove(provider))
                WriteFile(keys);
        }
    }

    private Dictionary<string, string> ReadFile()
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_filePath)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void WriteFile(Dictionary<string, string> keys)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(keys));

        // Only the current user can read the file
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
