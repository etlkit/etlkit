using System;
using System.ClientModel;
using EtlKit.Common;
using EtlKit.AI.Models;

using JetBrains.Annotations;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace EtlKit.AI;

/// <summary>
/// Factory for creating an IChatClient based on ApiSettings.
/// </summary>
[PublicAPI]
internal static class AIChatClientFactory
{
    public const string DefaultApiModel = "gpt-4.1-mini";

    /// <summary>
    /// Creates an IChatClient by using a custom factory (if provided),
    /// or builds an OpenAI.Chat.ChatClient from settings/environment variables.
    /// </summary>
    /// <param name="settings">API settings (may contain ApiKey/ApiModel).</param>
    /// <param name="customFactory">Optional user provided factory for the client.</param>
    public static IChatClient Create(
        ApiSettings settings,
        Func<ApiSettings, IChatClient>? customFactory = null
    )
    {
        if (customFactory != null)
            return customFactory(settings);

        if (settings is null)
            throw new EtlKitException("ApiSettings is required to create ChatClient");

        var key =
            settings.ApiKey ?? throw new EtlKitException("ApiKey is required to create ChatClient");

        var model = settings.ApiModel ?? DefaultApiModel;

        if (
            !string.IsNullOrEmpty(settings.ApiBaseUrl)
            && Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out var baseUri)
        )
        {
            return new ChatClient(
                model,
                new ApiKeyCredential(settings.ApiKey),
                new OpenAIClientOptions { Endpoint = baseUri }
            ).AsIChatClient();
        }

        return new ChatClient(model, key).AsIChatClient();
    }
}
