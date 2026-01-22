using System;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Plugin.Services;

namespace Aurum.Services;

/// <summary>
/// Service to handle data anonymization and privacy compliance
/// </summary>
public class PrivacyService
{
    private readonly Configuration configuration;
    private readonly IPluginLog log;
    private string? _cachedAnonymousId;

    public PrivacyService(IPluginLog log, Configuration configuration)
    {
        this.log = log;
        this.configuration = configuration;
    }

    /// <summary>
    /// Gets a stable, anonymized identifier for the current user.
    /// This ID is salted and hashed to prevent reverse engineering of the character name or account ID.
    /// It is unique to this plugin installation/config.
    /// </summary>
    public string GetAnonymousId()
    {
        if (!string.IsNullOrEmpty(_cachedAnonymousId))
        {
            return _cachedAnonymousId;
        }

        // Generate a random salt if one doesn't exist
        // This ensures the hash is unique to this user even if they have the same name as someone else
        if (string.IsNullOrEmpty(configuration.PrivacySalt))
        {
            configuration.PrivacySalt = GenerateSalt();
            configuration.Save();
        }

        // We use a combination of local install ID (if we had one) or just random generation.
        // Actually, let's make it persist in config so it's stable for this user.
        if (string.IsNullOrEmpty(configuration.AnonymousId))
        {
            configuration.AnonymousId = Guid.NewGuid().ToString("N");
            configuration.Save();
        }

        _cachedAnonymousId = configuration.AnonymousId;
        return _cachedAnonymousId;
    }

    /// <summary>
    /// Anonymizes a character name (e.g. for logs or shared data)
    /// </summary>
    public string AnonymizeName(string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return "Unknown";
        
        // Simple hash with salt
        if (string.IsNullOrEmpty(configuration.PrivacySalt))
        {
            configuration.PrivacySalt = GenerateSalt();
            configuration.Save();
        }

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(characterName + configuration.PrivacySalt);
        var hash = sha256.ComputeHash(bytes);
        
        // Take first 8 chars of hash
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLowerInvariant();
    }

    /// <summary>
    /// Checks if analytics/telemetry is allowed by user settings
    /// </summary>
    public bool IsAnalyticsAllowed()
    {
        return configuration.AllowAnonymousAnalytics;
    }

    /// <summary>
    /// Checks if community data sync is allowed
    /// </summary>
    public bool IsDataSyncAllowed()
    {
        return configuration.AllowCommunityDataSync;
    }

    private string GenerateSalt()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }
}
