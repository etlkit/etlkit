using System;
using JetBrains.Annotations;
using Microsoft.Extensions.AI;

namespace EtlKit.AI.Models;

/// <summary>
/// Serializable POCO with a subset of Microsoft.Extensions.AI.ChatOptions fields.
/// Use ConvertOptions to map these values into Microsoft ChatOptions at runtime.
/// </summary>
[PublicAPI]
[Serializable]
public sealed class ChatOptions
{
    /// <summary>
    /// Sampling temperature. Higher values make the output more random.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Nucleus sampling parameter. Consider tokens with top_p probability mass.
    /// </summary>
    public float? TopP { get; set; }

    /// <summary>
    /// Gets or sets the number of most probable tokens that the model considers when generating the next part of the text.
    /// </summary>
    /// <remarks>
    /// This property reduces the probability of generating nonsense. A higher value gives more diverse answers, while a lower value is more conservative.
    /// </remarks>
    public int? TopK { get; set; }

    /// <summary>
    /// Frequency penalty discourages repeated tokens.
    /// </summary>
    public float? FrequencyPenalty { get; set; }

    /// <summary>
    /// Presence penalty encourages introducing new tokens.
    /// </summary>
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// Maximum number of output tokens to generate.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Optional stop sequences. Generation will stop when any of these sequences appears.
    /// </summary>
    public string[]? StopSequences { get; set; }

    /// <summary>
    /// Desired response format. If null, the caller may set a default.
    /// </summary>
    public string? ResponseFormat { get; set; }

    /// <summary>Gets or sets the model ID for the chat request.</summary>
    public string? ModelId { get; set; }

    /// <summary>Gets or sets an optional identifier used to associate a request with an existing conversation.</summary>
    /// <related type="Article" href="https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai#stateless-vs-stateful-clients">Stateless vs. stateful clients.</related>
    public string? ConversationId { get; set; }

    /// <summary>Gets or sets additional per-request instructions to be provided to the <see cref="IChatClient"/>.</summary>
    public string? Instructions { get; set; }

    /// <summary>Gets or sets a seed value used by a service to control the reproducibility of results.</summary>
    public long? Seed { get; set; }

    /// <summary>
    /// Convert this POCO into Microsoft.Extensions.AI.ChatOptions.
    /// </summary>
    public static Microsoft.Extensions.AI.ChatOptions ConvertOptions(ChatOptions? source)
    {
        if (source is null)
            return new Microsoft.Extensions.AI.ChatOptions();

        return new Microsoft.Extensions.AI.ChatOptions
        {
            Temperature = source.Temperature,
            TopP = source.TopP,
            TopK = source.TopK,
            FrequencyPenalty = source.FrequencyPenalty,
            PresencePenalty = source.PresencePenalty,
            MaxOutputTokens = source.MaxOutputTokens,
            StopSequences = source.StopSequences,
            ResponseFormat = GetResponseFormat(source),
            ModelId = source.ModelId,
            ConversationId = source.ConversationId,
            Instructions = source.Instructions,
            Seed = source.Seed,
        };
    }

    private static ChatResponseFormat? GetResponseFormat(ChatOptions? source)
    {
        if (source?.ResponseFormat is null)
            return null;
        return source.ResponseFormat.ToLowerInvariant() == "json"
            ? ChatResponseFormat.Json
            : ChatResponseFormat.Text;
    }
}
