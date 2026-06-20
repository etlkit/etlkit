using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EtlKit;
using EtlKit.Common;
using EtlKit.Common.DataFlow;
using DotLiquid;

using EtlKit.AI.Models;

using JetBrains.Annotations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace EtlKit.AI;

/// <summary>
/// Batch AI transformation. Builds a shared prompt from the input batch (via DotLiquid template),
/// invokes the AI client, parses the JSON response and maps result items back to input rows
/// by identifiers (InputDataIdentificationField ↔ ResultDataIdentificationField).
/// </summary>
[PublicAPI]
public sealed class AIBatchTransformation : RowBatchTransformation<ExpandoObject, ExpandoObject>
{
    private static readonly JsonSerializerOptions s_jsonOptions =
        new() { Converters = { new ExpandoObjectConverter() } };

    private static readonly Regex s_jsonCodeBlockRegex = new("```json|```", RegexOptions.Compiled);
    private static readonly string s_resultsParsingError = "Result parsing error";

    /// <summary>
    /// AI invocation settings (API key/model and chat options).
    /// </summary>
    public ApiSettings ApiSettings { get; set; } = null!;

    /// <summary>
    /// DotLiquid prompt template. The batch is available as variable <c>input</c>.
    /// Example: <c>"{"items": {{ input | json_array }} }"</c>
    /// </summary>
    public string Prompt { get; set; } = null!;

    /// <summary>
    /// Additional parameters to use in DotLiquid template.
    /// Supports nested objects and will be accessible in template via dot notation.
    /// This property is automatically populated during XML deserialization.
    /// </summary>
    public IDictionary<string, object?>? PromptParameters { get; set; }

    /// <summary>
    /// Grouped result-related settings (schema, field names and identifiers).
    /// </summary>
    public ResultSettings ResultSettings { get; set; } = new();

    /// <summary>
    /// This property switches the error output stream between LinkTo and LinkErrorTo.
    /// When true, errors lead to an exception; when false, errors are written into ExceptionField and the flow continues.
    /// </summary>
    public bool FailOnError { get; set; } = true;

    private readonly Func<ApiSettings, IChatClient>? _chatClientFactory;

    static AIBatchTransformation()
    {
        // Register DotLiquid filters for JSON (once per process)
        CustomLiquidFilters.EnsureRegistered();
    }

    public AIBatchTransformation()
        : this(logger: null) { }

    /// <summary>
    /// Creates a new instance with an injected logger.
    /// </summary>
    public AIBatchTransformation(ILogger<AIBatchTransformation>? logger)
        : base(logger)
    {
        BatchTransform = batch => InvokeAsync(batch).GetAwaiter().GetResult();
    }

    internal AIBatchTransformation(ApiSettings settings)
        : this()
    {
        ApiSettings = settings;
    }

    /// <summary>
    /// Constructor for tests/mocks.
    /// </summary>
    internal AIBatchTransformation(Func<ApiSettings, IChatClient> chatClientFactory)
        : this()
    {
        _chatClientFactory = chatClientFactory;
    }

    private async Task<ExpandoObject[]> InvokeAsync(ExpandoObject[] input)
    {
        ValidateParameter(ApiSettings, nameof(ApiSettings));
        ValidateParameter(Prompt, nameof(Prompt));
        ValidateParameter(ResultSettings, nameof(ResultSettings));
        ValidateParameter(ResultSettings.ResultField, nameof(ResultSettings.ResultField));
        ValidateParameter(
            ResultSettings.ResultItemsJsonPath,
            nameof(ResultSettings.ResultItemsJsonPath)
        );
        ValidateParameter(ResultSettings.ExceptionField, nameof(ResultSettings.ExceptionField));
        ValidateParameter(
            ResultSettings.InputDataIdentificationField,
            nameof(ResultSettings.InputDataIdentificationField)
        );
        ValidateParameter(
            ResultSettings.ResultDataIdentificationField,
            nameof(ResultSettings.ResultDataIdentificationField)
        );

        var prompt = BuildPrompt(input);

        string responseText;
        IChatClient? client = null;
        Dictionary<string, ExpandoObject?> resultsDict;
        try
        {
            client = CreateChatClient();

            var options = ConvertOptions(ApiSettings.ChatOptions);
            var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
            var response = await client!
                .GetResponseAsync(messages, options, System.Threading.CancellationToken.None)
                .ConfigureAwait(false);

            // Clean response text (remove fenced code blocks) and validate JSON against schema
            responseText = GetCleanText(response.Text);
            var resultItems = GetResponseItemsWithValidation(
                responseText,
                ResultSettings.ResultsJsonSchema
            );
            // Map by identifiers
            resultsDict = BuildResultDictionary(resultItems);
        }
        catch (Exception ex)
        {
            return HandleBatchError(input, ex);
        }
        finally
        {
            client?.Dispose();
        }

        return EnrichByIdsWithValidation(input, resultsDict, responseText);
    }

    [MustDisposeResource]
    private IChatClient? CreateChatClient()
    {
        return AIChatClientFactory.Create(ApiSettings, _chatClientFactory);
    }

    private string BuildPrompt(ExpandoObject[] input)
    {
        var template = Template.Parse(Prompt);

        // Start with custom parameters (if provided)
        var parameters = ParsePromptParameters();

        // Pass the whole batch into template variable "input" (always has priority)
        parameters["input"] = input;

        // Use InvariantCulture to ensure numbers are formatted with "." as decimal separator (JSON standard)
        var result = template.Render(
            new RenderParameters(CultureInfo.InvariantCulture)
            {
                LocalVariables = Hash.FromDictionary(parameters),
            }
        );

        return result;
    }

    private IDictionary<string, object?> ParsePromptParameters()
    {
        return PromptParameters is { Count: > 0 }
            ? PromptParameters
            : new Dictionary<string, object?>();
    }

    private static string GetCleanText(string text)
    {
        return s_jsonCodeBlockRegex.Replace(text ?? string.Empty, string.Empty).Trim();
    }

    private List<ExpandoObject?> GetResponseItemsWithValidation(string response, string jsonSchema)
    {
        try
        {
            var jsonObject = JObject.Parse(response);

            // Use JSONPath to access the results array
            var itemsToken = jsonObject.SelectToken(ResultSettings.ResultItemsJsonPath);

            if (itemsToken is not { Type: JTokenType.Array })
            {
                throw new EtlKitException(
                    $"{s_resultsParsingError}: no array of results found on the json path `{ResultSettings.ResultItemsJsonPath}`."
                );
            }

            var list = new List<ExpandoObject?>();

            foreach (var element in itemsToken.Children())
            {
                var rawText = element.ToString(Formatting.None);
                var resultsItem = DeserializeExpandoObject(rawText);

                if (resultsItem is null)
                {
                    continue;
                }

                SetFieldValue(resultsItem, ResultSettings.RawResponseField, rawText);
                var (isValid, error) = ValidateJsonSchema(rawText, jsonSchema);
                if (!isValid)
                    SetFieldValue(resultsItem, ResultSettings.ExceptionField, error);

                list.Add(resultsItem);
            }

            return list;
        }
        catch (EtlKitException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new EtlKitException($"{s_resultsParsingError}: {e.Message}.", e);
        }
    }

    private Dictionary<string, ExpandoObject?> BuildResultDictionary(
        IEnumerable<ExpandoObject?> resultItems
    )
    {
        var dict = new Dictionary<string, ExpandoObject?>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(ResultSettings.ResultDataIdentificationField))
        {
            return dict;
        }

        foreach (var resultItem in resultItems)
        {
            if (
                resultItem is not IDictionary<string, object?> rd
                || !rd.TryGetValue(ResultSettings.ResultDataIdentificationField, out var ridVal)
                || ridVal == null
            )
            {
                continue;
            }

            var key = Convert.ToString(ridVal, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(key) && !dict.ContainsKey(key))
                dict[key] = resultItem;
        }
        return dict;
    }

    private ExpandoObject[] EnrichByIdsWithValidation(
        ExpandoObject[] input,
        Dictionary<string, ExpandoObject?> resultDict,
        string rawResponse
    )
    {
        foreach (var item in input)
        {
            SetFieldValue(item, ResultSettings.HttpCodeField, "OK");

            ExpandoObject? found = null;
            var res = (IDictionary<string, object?>)item;
            var isFound =
                res.TryGetValue(ResultSettings.InputDataIdentificationField, out var inId)
                && inId?.ToString() is { } key
                && resultDict.TryGetValue(key, out found)
                && found != null;

            if (!isFound)
            {
                var message =
                    inId != null
                        ? $"No matching result found for identifier '{inId}'."
                        : $"Input does not contain identifier field '{ResultSettings.InputDataIdentificationField}'.";
                HandleErrorPerItem(item, new EtlKitException(message), rawResponse);
                continue;
            }

            SetFieldValue(item, ResultSettings.ResultField, found);

            var exception = GetFieldValue(found, ResultSettings.ExceptionField) as string;
            var response = GetFieldValue(found, ResultSettings.RawResponseField);

            if (exception is null)
            {
                SetFieldValue(item, ResultSettings.RawResponseField, response);
                continue;
            }

            HandleErrorPerItem(item, new EtlKitException(exception), response);
        }

        return input;
    }

    private void HandleErrorPerItem(ExpandoObject item, Exception ex, object? rawResponse)
    {
        if (FailOnError && !ErrorHandler.HasErrorBuffer)
        {
            throw ex;
        }

        // Do not throw here — let the base RowBatchTransformation handle escalation policies
        SetFieldValue(item, ResultSettings.ExceptionField, ex.Message);
        SetFieldValue(item, ResultSettings.RawResponseField, rawResponse);
        if (ErrorHandler.HasErrorBuffer)
        {
            ErrorHandler.Send(ex, ErrorHandler.ConvertErrorData(item));
        }
    }

    private ExpandoObject[] HandleBatchError(ExpandoObject[] input, Exception ex)
    {
        if (FailOnError)
        {
            throw ex;
        }

        foreach (var item in input)
        {
            var res = (IDictionary<string, object?>)item;
            res[ResultSettings.ExceptionField] = ex.Message;
            if (ex is HttpStatusCodeException httpEx)
            {
                ApplyErrorHttp(res, httpEx);
            }
        }
        return input;
    }

    private void ApplyErrorHttp(IDictionary<string, object?> res, HttpStatusCodeException httpEx)
    {
        res[ResultSettings.ResultField] = DeserializeExpandoObject(httpEx.Content);
        SetFieldValue(res, ResultSettings.HttpCodeField, httpEx.HttpCode.ToString());
        SetFieldValue(res, ResultSettings.RawResponseField, httpEx.Content);
    }

    private static void SetFieldValue(
        IDictionary<string, object?> res,
        string? field,
        object? value
    )
    {
        if (!string.IsNullOrEmpty(field))
        {
            res[field!] = value;
        }
    }

    private static object? GetFieldValue(IDictionary<string, object?>? res, string? field)
    {
        return res is null || string.IsNullOrEmpty(field) || !res.TryGetValue(field!, out var val)
            ? null
            : val;
    }

    private static ExpandoObject? DeserializeExpandoObject(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ExpandoObject?>(response, s_jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void ValidateParameter(object field, string fieldName)
    {
        if (field is null)
        {
            throw new EtlKitException($"Property '{fieldName}' not defined");
        }
    }

    private static Microsoft.Extensions.AI.ChatOptions ConvertOptions(Models.ChatOptions? source)
    {
        var options = Models.ChatOptions.ConvertOptions(source);

        options.Temperature ??= 0.1f;
        options.ResponseFormat ??= ChatResponseFormat.Json;

        return options;
    }

    private static (bool IsValid, string? error) ValidateJsonSchema(
        string jsonString,
        string schemaString
    )
    {
        if (string.IsNullOrEmpty(schemaString))
        {
            return (true, null);
        }

        try
        {
            var jDocument = JToken.Parse(jsonString);
            var jSchema = JSchema.Parse(schemaString);

            var isValid = jDocument.IsValid(jSchema, out IList<string> validationErrors);

            return !isValid
                ? (false, string.Join("; ", validationErrors ?? Enumerable.Empty<string>()))
                : (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"{s_resultsParsingError}: {ex.Message}");
        }
    }
}
