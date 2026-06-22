using JetBrains.Annotations;

namespace EtlKit.AI.Models;

/// <summary>
/// Settings for AI calls used by the batch transformation.
/// </summary>
[PublicAPI]
public sealed class ApiSettings
{
    /// <summary>
    /// API key to access the provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model identifier to use for requests.
    /// </summary>
    public string? ApiModel { get; set; }

    /// <summary>
    /// API base url to request AI
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>
    /// Serializable chat options (POCO) that are converted to Microsoft.Extensions.AI.ChatOptions at runtime.
    /// </summary>
    public ChatOptions? ChatOptions { get; set; }
}
