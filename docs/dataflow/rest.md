# Calling REST services

The `RestTransformation` (in the optional `EtlKit.Rest` package) calls a REST/HTTP endpoint once per
input row and writes the response back into the row. It is a non-blocking transformation that
operates on dynamic rows (`ExpandoObject`), so it fits naturally between any source and destination.

> Add the package reference `EtlKit.Rest` to use `RestTransformation`.

## How it works

For each row the transformation:

1. renders the request URL and body from the row (both are [DotLiquid](https://github.com/dotliquid/dotliquid)
   templates, so `{{ FieldName }}` is replaced with the row's field value),
2. sends the HTTP request,
3. deserializes the JSON response into an `ExpandoObject` and stores it in the field named by
   `ResultField`,
4. forwards the (now enriched) row to its output.

## Configuration

The request is described by a `RestMethodInfo` object assigned to the `RestMethodInfo` property:

| `RestMethodInfo` member | Meaning |
|---|---|
| `Url` | Request URL — a Liquid template rendered from the row |
| `Method` | HTTP method: `GET`, `POST`, `PUT`, `DELETE`, `HEAD`, `OPTIONS`, `TRACE` |
| `Body` | Request body — a Liquid template rendered from the row |
| `Headers` | `Dictionary<string,string>` of request headers |
| `RetryCount` | Number of retries on failure |
| `RetryInterval` | Pause between retries, in seconds |

Fields on the transformation control where the outcome is written:

| Property | Default | Meaning |
|---|---|---|
| `ResultField` | required | Field that receives the deserialized JSON response |
| `HttpCodeField` | `null` | Field that receives the HTTP status code (optional) |
| `RawResponseField` | `null` | Field that receives the raw response string (optional) |
| `ExceptionField` | — | Field that receives the exception when `FailOnError` is `false` |
| `FailOnError` | `true` | When `true`, a failed call throws and stops the flow; when `false`, the error is written to `ExceptionField` and the flow continues |

## Example

```csharp
var rest = new RestTransformation
{
    RestMethodInfo = new RestMethodInfo
    {
        Url = "https://api.example.com/customers/{{ CustomerId }}",
        Method = "GET",
        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
        RetryCount = 3,
        RetryInterval = 2
    },
    ResultField = "CustomerData",
    HttpCodeField = "StatusCode",
    FailOnError = false,
    ExceptionField = "Error"
};

source.LinkTo(rest);
rest.LinkTo(destination);
source.Execute();
destination.Wait();
```

Each row that flows through `rest` gains a `CustomerData` field holding the parsed response and a
`StatusCode` field with the HTTP code. A `POST` with a templated body looks like this:

```csharp
RestMethodInfo = new RestMethodInfo
{
    Url = "https://api.example.com/orders",
    Method = "POST",
    Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
    Body = "{ \"orderId\": {{ Id }}, \"item\": \"{{ Item }}\" }"
}
```

## XML configuration

In an [XML-defined data flow](xml-configuration.md), the element is `<RestTransformation>` and
`<RestMethodInfo>` carries the request definition:

```xml
<RestTransformation>
    <ResultField>CustomerData</ResultField>
    <RestMethodInfo>
        <Url>https://api.example.com/customers/{{ CustomerId }}</Url>
        <Method>GET</Method>
        <RetryCount>3</RetryCount>
        <RetryInterval>2</RetryInterval>
    </RestMethodInfo>
    <LinkTo>
        <DbDestination>
            <TableName>customers</TableName>
        </DbDestination>
    </LinkTo>
</RestTransformation>
```
