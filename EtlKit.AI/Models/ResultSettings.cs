using JetBrains.Annotations;

namespace EtlKit.AI.Models;

/// <summary>
/// Groups result-related settings for AIBatchTransformation: schema, field names and identifiers.
/// </summary>
[PublicAPI]
public sealed class ResultSettings
{
    /// <summary>
    /// Output field name to write the deserialized result item into.
    /// The input data is enriched with the result through this field.
    /// </summary>
    public string ResultField { get; set; } = null!;

    /// <summary>
    /// The JSON path in the response which contains the results array (e.g. "results").
    /// </summary>
    public string ResultItemsJsonPath { get; set; } = null!;

    /// <summary>
    /// Output field for an exception when FailOnError=false.
    /// </summary>
    public string ExceptionField { get; set; } = null!;

    /// <summary>
    /// Input field name used to match input rows with result items (required).
    /// </summary>
    public string InputDataIdentificationField { get; set; } = null!;

    /// <summary>
    /// Result item field name used to match items with input rows (required).
    /// </summary>
    public string ResultDataIdentificationField { get; set; } = null!;

    /// <summary>
    /// JSON schema string used to validate each result item in the response (per item json schema validation).
    /// </summary>
    public string ResultsJsonSchema { get; set; } = null!;

    /// <summary>
    /// Output field for the raw response text (per item if found, useful for diagnostics).
    /// If null or empty, raw response will not be written.
    /// </summary>
    public string? RawResponseField { get; set; }

    /// <summary>
    /// Output field for HTTP status code (used for HttpStatusCodeException handling).
    /// </summary>
    public string? HttpCodeField { get; set; }
}
