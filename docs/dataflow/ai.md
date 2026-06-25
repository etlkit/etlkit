# AI transformations

The `AIBatchTransformation` (in the optional `EtlKit.AI` package) enriches rows by calling a large
language model. It is built on [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/),
so it works with any provider exposing an `IChatClient` (OpenAI-compatible endpoints, Azure OpenAI,
local models, …).

> Add the package reference `EtlKit.AI` to use `AIBatchTransformation`.

## The batch model

Unlike a row-by-row transformation, `AIBatchTransformation` works on **batches** (it derives from
`RowBatchTransformation<ExpandoObject, ExpandoObject>`). For each batch it:

1. renders a single `Prompt` from the whole batch — the batch is available inside the
   [DotLiquid](https://github.com/dotliquid/dotliquid) template as the variable `input`,
2. calls the model once for the batch,
3. parses the JSON response and extracts the result array at `ResultSettings.ResultItemsJsonPath`,
4. **matches each result item back to its input row** by comparing
   `ResultSettings.ResultDataIdentificationField` (on the result item) with
   `ResultSettings.InputDataIdentificationField` (on the input row), and writes the matched item
   into `ResultSettings.ResultField`.

Batching one model call across many rows is what makes this efficient; the identifier fields are how
each answer finds its way back to the right row. The batch size is the inherited `BatchSize`
property (default `1000`).

## Configuration

`ApiSettings` — how to reach the model:

| Member | Meaning |
|---|---|
| `ApiKey` | Provider API key |
| `ApiModel` | Model identifier (e.g. a chat model name) |
| `ApiBaseUrl` | Provider base URL |
| `ChatOptions` | Optional serializable chat options (temperature, etc.) |

`Prompt` — a DotLiquid template. The batch is the `input` variable; the bundled `json_array` filter
serializes it, e.g. `{ "items": {{ input | json_array }} }`. Use `PromptParameters` to pass extra
values usable in the template by dot notation.

`ResultSettings` — how to read the response back:

| Member | Meaning |
|---|---|
| `ResultField` | Input-row field that receives the matched result item |
| `ResultItemsJsonPath` | JSON path to the results array in the response (e.g. `results`) |
| `InputDataIdentificationField` | Input-row field used to match rows to result items |
| `ResultDataIdentificationField` | Result-item field used to match items to rows |
| `ExceptionField` | Field that receives the exception when `FailOnError` is `false` |
| `ResultsJsonSchema` | Optional per-item JSON schema for validating each result item |
| `RawResponseField` / `HttpCodeField` | Optional diagnostics fields |

`FailOnError` (default `true`) — when `false`, errors are written to `ResultSettings.ExceptionField`
and the flow continues.

## Example

Classifying support tickets:

```csharp
var ai = new AIBatchTransformation
{
    ApiSettings = new ApiSettings
    {
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        ApiModel = "gpt-4o-mini",
        ApiBaseUrl = "https://api.openai.com/v1"
    },
    Prompt =
        "Classify each ticket's sentiment as positive, neutral or negative. " +
        "Return JSON {\"results\":[{\"id\":<id>,\"sentiment\":\"...\"}]}. " +
        "Tickets: {{ input | json_array }}",
    ResultSettings = new ResultSettings
    {
        ResultItemsJsonPath = "results",
        InputDataIdentificationField = "Id",   // field on each input row
        ResultDataIdentificationField = "id",  // field on each result item
        ResultField = "Sentiment",
        ExceptionField = "Error"
    }
};

source.LinkTo(ai);
ai.LinkTo(destination);
source.Execute();
destination.Wait();
```

Each input row keeps its original fields and gains a `Sentiment` field with the model's answer for
that row.

## XML configuration

In an [XML-defined data flow](xml-configuration.md) the element is `<AIBatchTransformation>`:

```xml
<AIBatchTransformation>
    <ApiSettings>
        <ApiKey>...</ApiKey>
        <ApiModel>gpt-4o-mini</ApiModel>
        <ApiBaseUrl>https://api.openai.com/v1</ApiBaseUrl>
    </ApiSettings>
    <Prompt>Classify ... Tickets: {{ input | json_array }}</Prompt>
    <ResultSettings>
        <ResultItemsJsonPath>results</ResultItemsJsonPath>
        <InputDataIdentificationField>Id</InputDataIdentificationField>
        <ResultDataIdentificationField>id</ResultDataIdentificationField>
        <ResultField>Sentiment</ResultField>
        <ExceptionField>Error</ExceptionField>
    </ResultSettings>
</AIBatchTransformation>
```
