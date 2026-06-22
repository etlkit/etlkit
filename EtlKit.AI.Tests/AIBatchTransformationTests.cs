#nullable enable
using System.Dynamic;
using System.Text.Json;
using EtlKit.Common;
using EtlKit.DataFlow;
using DotLiquid;

using EtlKit.AI.Models;
using EtlKit.Primitives;
using Microsoft.Extensions.AI;
using Moq;
using ChatOptions = EtlKit.AI.Models.ChatOptions;
using Models_ChatOptions = EtlKit.AI.Models.ChatOptions;

namespace EtlKit.AI.Tests;

public class AIBatchTransformationTests
{
    public AIBatchTransformationTests()
    {
        // Ensure CustomLiquidFilters are registered once for DotLiquid usage
        CustomLiquidFilters.EnsureRegistered();
    }

    [Fact]
    public void MissingResult_FailOnErrorTrue_WithoutErrorLink_ShouldThrow()
    {
        // Arrange: two input rows, only one result returned — the second input
        // will cause per-item ID mapping error. With FailOnError=true and no
        // error-link, the pipeline should fail.
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\"}]}"; // no result for id=2
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = 2,
            FailOnError = true, // key branch assertion
        };

        // Act + Assert: expect pipeline to fail
        source.LinkTo(trans);
        trans.LinkTo(dest);
        Assert.Throws<AggregateException>(() =>
        {
            source.Execute();
            dest.Wait();
        });
    }

    [Fact]
    public void Constructor_WithSettings_ShouldAssignApiSettingsAndOptions()
    {
        // Arrange: prepare settings with nested chat options
        var settings = new ApiSettings
        {
            ApiKey = "test_key",
            ApiModel = "gpt-test",
            ChatOptions = new Models_ChatOptions
            {
                Temperature = 0.3f,
                MaxOutputTokens = 256,
                ResponseFormat = "json",
            },
        };

        // Act: use constructor that accepts ApiSettings
        var trans = new AIBatchTransformation(settings)
        {
            // Minimal required fields (conveyor is not started)
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = RS(ResultsSchemaRidOnly()),
        };

        // Assert: reference and option values preserved
        Assert.NotNull(trans.ApiSettings);
        Assert.Equal(settings, trans.ApiSettings);
        Assert.NotNull(trans.ApiSettings.ChatOptions);
        Assert.Equal(settings.ChatOptions, trans.ApiSettings.ChatOptions);
        Assert.Equal(0.3f, trans.ApiSettings.ChatOptions.Temperature);
        Assert.Equal(256, trans.ApiSettings.ChatOptions.MaxOutputTokens);
        Assert.Equal("json", trans.ApiSettings.ChatOptions.ResponseFormat);
    }

    private static string ResultsSchemaRidOnly() =>
        """
            {
            "type": "object",
            "properties": {
              "rid": { "type": "string" }
            },
            "required": ["rid"],
            "additionalProperties": true
            }
            """;

    private static ResultSettings RS(string schema) =>
        new()
        {
            ResultItemsJsonPath = "results",
            ResultField = "result",
            ExceptionField = "ex",
            InputDataIdentificationField = "id",
            ResultDataIdentificationField = "rid",
            ResultsJsonSchema = schema,
        };

    // Schema: rid (string) and sentiment (enum)
    private static string ItemSchema_RidAndSentimentEnum() =>
        """
            {
              "type": "object",
              "properties": {
                "rid": { "type": "string" },
                "sentiment": { "enum": ["positive", "negative", "neutral"] }
              },
              "required": ["rid", "sentiment"],
              "additionalProperties": true
            }
            """;

    // Schema: rid (string) and score in range [-2,2]
    private static string ItemSchema_RidAndScoreRange() =>
        """
            {
              "type": "object",
              "properties": {
                "rid": { "type": "string" },
                "score": { "type": "number", "minimum": -2, "maximum": 2 }
              },
              "required": ["rid", "score"],
              "additionalProperties": true
            }
            """;

    // Schema: only rid (string), no additional properties
    private static string ItemSchema_RidOnly_NoAdditional() =>
        """
            {
              "type": "object",
              "properties": {
                "rid": { "type": "string" }
              },
              "required": ["rid"],
              "additionalProperties": false
            }
            """;

    [Fact]
    public void IdentifierMapping_ShouldMatchByIds()
    {
        // Arrange: input ids = [10,20,30], response contains rid in different order
        var input = new[] { CreateExpando(10), CreateExpando(20), CreateExpando(30) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"20\"},{\"rid\":\"10\"},{\"rid\":\"30\"}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));
        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = new(),
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
                RawResponseField = "raw",
            },
            BatchSize = 3,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: pipeline processed and returned same number of rows
        Assert.Equal(3, dest.Data.Count);
    }

    [Fact]
    public void IdentifierMapping_NumberId_ShouldMatchStringRid()
    {
        // Arrange: numeric id in input and string rid in response — should match via ToString
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\"},{\"rid\":\"2\"}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));
        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = 2,
            FailOnError = true,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: matching via ToString
        Assert.Equal(2, dest.Data.Count);
        Assert.Equal("1", ((object)((dynamic)dest.Data.ElementAt(0)).result.rid).ToString());
        Assert.Equal("2", ((object)((dynamic)dest.Data.ElementAt(1)).result.rid).ToString());
    }

    [Fact]
    public void ValidateParameter_NullPrompt_ShouldThrowInvalidOperationException()
    {
        // Arrange: Prompt is null — ValidateParameter should trigger and throw InvalidOperationException
        var input = new[] { CreateExpando(1) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var trans = new AIBatchTransformation
        {
            ApiSettings = new(),
            Prompt = null!, // check if (field is null)
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = 1,
            FailOnError = true,
        };

        // Act + Assert: expect AggregateException with inner InvalidOperationException("Property 'Prompt' not defined")
        source.LinkTo(trans);
        trans.LinkTo(dest);

        var ex = Assert.Throws<AggregateException>(() =>
        {
            source.Execute();
            dest.Wait();
        });

        Assert.NotNull(ex.InnerExceptions);
        Assert.NotEmpty(ex.InnerExceptions);
        Assert.Contains(
            ex.InnerExceptions,
            e => e is EtlKitException && e.Message.Contains("Property 'Prompt' not defined")
        );
    }

    [Fact]
    public void ResultIdentifierField_Empty_ShouldMarkAllWithException()
    {
        // Arrange: ResultDataIdentificationField is empty ⇒ result dictionary isn't built,
        // no mapping performed, all inputs are marked with ExceptionField
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\"},{\"rid\":\"2\"}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings
        {
            ChatOptions = new Models_ChatOptions { ResponseFormat = "json" },
        };
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            BatchSize = 2,
            FailOnError = false,
            // key field empty is checked by setting it inside ResultSettings
            // We need to override ResultSettings with empty rid
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = string.Empty,
                ResultsJsonSchema = ResultsSchemaRidOnly(),
                RawResponseField = "raw",
            },
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: both rows have no result, have ExceptionField; raw is set
        Assert.Equal(2, dest.Data.Count);
        foreach (var row in dest.Data)
        {
            var d = (IDictionary<string, object?>)row;
            Assert.False(d.ContainsKey("result"));
            Assert.IsType<string>(d["ex"], exactMatch: false);
            Assert.NotNull(d["raw"]);
        }
    }

    [Fact]
    public void FailOnErrorFalse_ShouldFillExceptionAndContinue_OnClientError()
    {
        // Arrange
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        // Client that throws an exception when invoked
        var trans = new AIBatchTransformation(s => new FailingChatClient())
        {
            ApiSettings = new(),
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = 2,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: both rows passed with ExceptionField set
        Assert.Equal(2, dest.Data.Count);
        foreach (var row in dest.Data)
        {
            IDictionary<string, object?> d = row;
            Assert.False(d.ContainsKey("result"));
            Assert.IsType<string>(d["ex"], exactMatch: false);
        }
    }

    [Fact]
    public void HttpStatusCodeException_FailOnErrorFalse_ShouldPopulateHttpFieldsAndResultForAll()
    {
        // Arrange: emulate HTTP error from client, expect ResultField/HttpCodeField/RawResponseField and ExceptionField to be populated for all rows
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var errorContent = "{\"rid\":\"1\",\"error\":\"bad request\"}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new EtlKit.HttpStatusCodeException(
                    System.Net.HttpStatusCode.BadRequest,
                    errorContent
                )
            );

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                HttpCodeField = "code",
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
                RawResponseField = "raw",
            },
            BatchSize = 2,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: both rows passed, HTTP error fields are populated
        Assert.Equal(2, dest.Data.Count);
        foreach (var row in dest.Data)
        {
            var d = (IDictionary<string, object?>)row;
            Assert.IsType<string>(d["ex"], exactMatch: false);
            Assert.Equal("BadRequest", d["code"]);
            Assert.Equal(errorContent, d["raw"]);
            Assert.True(d.ContainsKey("result"));
            // result deserialized from errorContent and contains rid="1"
            var result = (IDictionary<string, object>)d["result"]!;
            Assert.Equal("1", result["rid"]);
        }
    }

    [Fact]
    public void HttpStatusCodeException_WithNonJsonContent_FailOnErrorFalse_ShouldSetNullResult()
    {
        // Arrange: emulate HTTP error with non-JSON content — ResultField should be null
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var errorContent = "<html><body>bad request</body></html>"; // non-JSON
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new EtlKit.HttpStatusCodeException(
                    System.Net.HttpStatusCode.BadRequest,
                    errorContent
                )
            );

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                HttpCodeField = "code",
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
                RawResponseField = "raw",
            },
            BatchSize = 2,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: both rows marked, HTTP fields set, ResultField = null
        Assert.Equal(2, dest.Data.Count);
        foreach (var row in dest.Data)
        {
            var d = (IDictionary<string, object?>)row;
            Assert.IsType<string>(d["ex"], exactMatch: false);
            Assert.Equal("BadRequest", d["code"]);
            Assert.Equal(errorContent, d["raw"]);
            Assert.True(d.ContainsKey("result"));
            Assert.Null(d["result"]);
        }
    }

    [Fact]
    public void HttpStatusCodeException_WithEmptyContent_FailOnErrorFalse_ShouldSetNullResult()
    {
        // Arrange: emulate HTTP error with empty content — ResultField should be null
        var input = new[] { CreateExpando(1) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var errorContent = string.Empty;
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new EtlKit.HttpStatusCodeException(
                    System.Net.HttpStatusCode.BadRequest,
                    errorContent
                )
            );

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                HttpCodeField = "code",
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
                RawResponseField = "raw",
            },
            BatchSize = 1,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: row marked, HTTP fields set, ResultField = null, raw is empty
        Assert.Single(dest.Data);
        var d = (IDictionary<string, object?>)dest.Data.ElementAt(0);
        Assert.IsType<string>(d["ex"]);
        Assert.Equal("BadRequest", d["code"]);
        Assert.Empty((string)d["raw"]!);
        Assert.True(d.ContainsKey("result"));
        Assert.Null(d["result"]);
    }

    [Fact]
    public void PromptTemplate_ShouldRenderJsonArray()
    {
        // Verify that json_array filter serializes ExpandoObject[] correctly
        var input = new[] { CreateExpando("1", "positive"), CreateExpando("2", "negative") };

        var hash = Hash.FromDictionary(new Dictionary<string, object> { ["input"] = input });
        var template = Template.Parse("{{ input | json_array }}");
        var result = template.Render(hash);

        Assert.StartsWith("[{\"id\":\"1\",\"sentiment\":\"positive\"}", result);
        using var doc = JsonDocument.Parse(result);
        var arr = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal("1", arr[0].GetProperty("id").GetString());
        Assert.Equal("negative", arr[1].GetProperty("sentiment").GetString());
    }

    [Fact]
    public void PromptParameters_WithNestedDictionaries_ShouldRenderCorrectly()
    {
        // Arrange: verify that PromptParameters with nested dictionaries work correctly in DotLiquid template
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\"},{\"rid\":\"2\"}]}";
        var mock = new Mock<IChatClient>();

        // Capture the actual prompt sent to the AI client
        string? capturedPrompt = null;
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                IEnumerable<ChatMessage>,
                Microsoft.Extensions.AI.ChatOptions,
                CancellationToken
            >(
                (messages, _, _) =>
                {
                    capturedPrompt = messages.First().Text;
                }
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            // Template with nested parameter access
            Prompt =
                @"{
              ""model"": ""{{ config.model }}"",
              ""test_value"": {{ config.params.test_value }},
              ""max_value"": {{ config.params.settings.max_value }},
              ""language"": ""{{ system.language }}"",
              ""threshold"": {{ threshold }},
              ""tags"": [{% for tag in tags %}""{{ tag }}""{% unless forloop.last %}, {% endunless %}{% endfor %}],
              ""items"": {{ input | json_array }}
            }",
            // PromptParameters as dictionary
            PromptParameters = new Dictionary<string, object?>
            {
                ["config"] = new Dictionary<string, object>
                {
                    ["model"] = "xyz",
                    ["params"] = new Dictionary<string, object>
                    {
                        ["test_value"] = 0.7,
                        ["settings"] = new Dictionary<string, object> { ["max_value"] = 2000 },
                    },
                },
                ["system"] = new Dictionary<string, object?> { ["language"] = "ru" },
                ["threshold"] = 0.95,
                ["tags"] = new[] { "production", "high-priority", "batch-processing" },
            },
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = 2,
            FailOnError = true,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: verify that the prompt was rendered correctly with nested parameters
        Assert.NotNull(capturedPrompt);
        Assert.Contains("\"model\": \"xyz\"", capturedPrompt);
        Assert.Contains("\"test_value\": 0.7", capturedPrompt);
        Assert.Contains("\"max_value\": 2000", capturedPrompt);
        Assert.Contains("\"language\": \"ru\"", capturedPrompt);
        Assert.Contains("\"threshold\": 0.95", capturedPrompt);
        Assert.Contains("\"items\":", capturedPrompt);
        Assert.Contains(
            "\"tags\": [\"production\", \"high-priority\", \"batch-processing\"]",
            capturedPrompt
        );

        // Verify that results were processed correctly
        Assert.Equal(2, dest.Data.Count);
    }

    [Fact]
    public void PromptParameters_ShouldNotOverrideInputParameter()
    {
        // Arrange: verify that even if PromptParameters contains "input", it gets overridden by the actual input
        var input = new[] { CreateExpando(42) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"42\"}]}";
        var mock = new Mock<IChatClient>();

        string? capturedPrompt = null;
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                IEnumerable<ChatMessage>,
                Microsoft.Extensions.AI.ChatOptions,
                CancellationToken
            >(
                (messages, _, _) =>
                {
                    capturedPrompt = messages.First().Text;
                }
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{{ input | json_array }}",
            // Try to override "input" - should be ignored
            PromptParameters = new Dictionary<string, object?>
            {
                ["input"] = "this should be overridden",
            },
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = 1,
            FailOnError = true,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: verify that actual input data was used, not the PromptParameters value
        Assert.NotNull(capturedPrompt);
        Assert.Contains("\"id\":42", capturedPrompt);
        Assert.DoesNotContain("this should be overridden", capturedPrompt);
    }

    [Fact]
    public void MissingResult_FailOnErrorFalse_ShouldSetException()
    {
        // Arrange: response doesn't contain result for id=2
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\"}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));
        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = 2,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: first row enriched, second row has Exception set
        Assert.Equal(2, dest.Data.Count);
        Assert.Equal("1", ((dynamic)dest.Data.ElementAt(0)).result.rid);
        IDictionary<string, object> second = dest.Data.ElementAt(1)!;
        Assert.False(second.ContainsKey("result"));
        Assert.IsType<string>(second["ex"], exactMatch: false);
    }

    [Fact]
    public void MissingInputId_FailOnErrorFalse_ShouldSetException()
    {
        // Arrange: second input row misses id field
        dynamic a = new ExpandoObject();
        a.id = 1;
        dynamic b = new ExpandoObject();
        b.other = 2;
        var input = new[] { (ExpandoObject)a, (ExpandoObject)b };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\"}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));
        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
            },
            BatchSize = 2,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        Assert.Equal(2, dest.Data.Count);
        IDictionary<string, object> first = dest.Data.ElementAt(0)!;
        Assert.True(first.ContainsKey("result"));
        IDictionary<string, object> second = dest.Data.ElementAt(1)!;
        Assert.False(second.ContainsKey("result"));
        Assert.IsType<string>(second["ex"], exactMatch: false);
    }

    [Fact]
    public void FailOnErrorTrue_WithoutErrorLink_ShouldThrow()
    {
        // Arrange: client throws, FailOnError=true, without error-link
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var trans = new AIBatchTransformation(_ => new FailingChatClient())
        {
            ApiSettings = new(),
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
            },
            BatchSize = 2,
            FailOnError = true,
        };

        // Act + Assert
        source.LinkTo(trans);
        trans.LinkTo(dest);
        Assert.Throws<AggregateException>(() =>
        {
            source.Execute();
            dest.Wait();
        });
    }

    [Fact]
    public void FailOnErrorTrue_WithErrorLink_ShouldRouteErrorsAndContinue()
    {
        // Arrange: client throws, errors should go to error buffer per-row
        var input = new[] { CreateExpando(1), CreateExpando(2), CreateExpando(3) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();
        var errorDest = new MemoryDestination<EtlKitError>();

        var trans = new AIBatchTransformation(_ => new FailingChatClient())
        {
            ApiSettings = new(),
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
            },
            BatchSize = 3,
            FailOnError = true,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        trans.LinkErrorTo(errorDest);
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        // Assert: no results, errors count equals to input rows count
        Assert.Empty(dest.Data);
        Assert.Equal(3, errorDest.Data.Count);
    }

    [Fact]
    public void RawResponseField_ShouldBeSet_OnSuccess()
    {
        // Arrange: successful response should be written to RawResponseField
        var input = new[] { CreateExpando(42) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var raw = "{\"results\":[{\"rid\":\"42\"}]}";
        var rawResult = "{\"rid\":\"42\"}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, raw)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
                RawResponseField = "raw",
            },
            BatchSize = 1,
            FailOnError = true,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        Assert.Single(dest.Data);
        Assert.Equal(rawResult, ((dynamic)dest.Data.ElementAt(0)).raw);
        mock.Verify(
            c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Schema_MissingRequired_ShouldSetException_PerItem()
    {
        // Arrange: for id=2 response lacks required field rid (by schema)
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        // Second result element without rid
        var response = "{\"results\":[{\"rid\":\"1\"}, {\"other\":2}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
            },
            BatchSize = 2,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: first enriched, second with Exception (no result for id=2)
        Assert.Equal(2, dest.Data.Count);
        Assert.Equal("1", (object)((dynamic)dest.Data.ElementAt(0)).result.rid);
        var second = (IDictionary<string, object?>)dest.Data.ElementAt(1);
        Assert.False(second.ContainsKey("result"));
        Assert.IsType<string>(second["ex"], exactMatch: false);
    }

    [Fact]
    public void Schema_EnumViolation_ShouldSetException_PerItem()
    {
        // Arrange: sentiment value outside enum
        var input = new[] { CreateExpando(1) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\",\"sentiment\":\"unknown\"}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ItemSchema_RidAndSentimentEnum(),
            },
            BatchSize = 1,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: row marked with Exception due to enum violation
        Assert.Single(dest.Data);
        IDictionary<string, object> row = dest.Data.ElementAt(0)!;
        Assert.True(row.ContainsKey("result")); // result exists with validation error
        Assert.IsType<string>(row["ex"], exactMatch: false);
    }

    [Fact]
    public void Schema_NumberOutOfRange_ShouldSetException_PerItem()
    {
        // Arrange: score goes out of the schema range
        var input = new[] { CreateExpando(1) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\",\"score\":3}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ItemSchema_RidAndScoreRange(),
            },
            BatchSize = 1,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        Assert.Single(dest.Data);
        IDictionary<string, object> row = dest.Data.ElementAt(0)!;
        Assert.IsType<string>(row["ex"], exactMatch: false);
    }

    [Theory]
    [InlineData(true)] // verify raw
    [InlineData(false)] // do not verify raw
    public void Schema_ItemViolation_ShouldSetException_PerItem(bool verifyRaw)
    {
        // Arrange: violate schema at item level (additionalProperties=false),
        // when verifyRaw=true also assert raw element is preserved
        var input = new[] { CreateExpando(7) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var rawItem = "{\"rid\":\"7\",\"x\":1}"; // x is a wrong schema property
        var response = $"{{\"results\":[{rawItem}]}}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ItemSchema_RidOnly_NoAdditional(),
                RawResponseField = verifyRaw ? "raw" : null,
            },
            BatchSize = 1,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        Assert.Single(dest.Data);
        IDictionary<string, object> row = dest.Data.ElementAt(0)!;
        Assert.True(row.ContainsKey("result"));
        Assert.IsType<string>(row["ex"], exactMatch: false);
        if (verifyRaw)
        {
            Assert.Equal(rawItem, ((dynamic)dest.Data.ElementAt(0)).raw);
        }
    }

    [Theory]
    [InlineData("{\"results\":{\"rid\":1}}", 2)] // results is not an array
    [InlineData("[]", 1)] // root is not an object
    public void Response_InvalidRoot_FailOnErrorFalse_ShouldMarkAll(string response, int inputCount)
    {
        // Arrange
        var input = Enumerable.Range(1, inputCount).Select(i => CreateExpando(i)).ToArray();
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = inputCount,
            FailOnError = false,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: all rows are marked with Exception
        Assert.Equal(inputCount, dest.Data.Count);
        foreach (var row in dest.Data)
        {
            IDictionary<string, object?> d = row;
            Assert.False(d.ContainsKey("result"));
            Assert.IsType<string>(d["ex"], exactMatch: false);
        }
    }

    [Fact]
    public void CleanText_FencedJson_ShouldValidateAndMap()
    {
        // Arrange: response is wrapped into ```json ... ``` fenced block
        var input = new[] { CreateExpando(10) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "```json\n{\"results\":[{\"rid\":\"10\"}]}\n```";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var settings = new ApiSettings();
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = RS(ResultsSchemaRidOnly()),
            BatchSize = 1,
            FailOnError = true,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        Assert.Single(dest.Data);
        Assert.Equal("10", (object)((dynamic)dest.Data.ElementAt(0)).result.rid);
    }

    [Fact]
    public void ResultItem_MissingResultId_ShouldSetException_ForAffectedInputs()
    {
        // Arrange: second result item misses rid, it will be ignored by mapping
        var input = new[] { CreateExpando(1), CreateExpando(2) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"1\"},{\"other\":2}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var trans = NewTrans(mock, ResultsSchemaRidOnly(), batchSize: 2, failOnError: false);

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: first enriched, second has Exception
        Assert.Equal(2, dest.Data.Count);
        Assert.Equal("1", (object)((dynamic)dest.Data.ElementAt(0)).result.rid);
        var second = (IDictionary<string, object?>)dest.Data.ElementAt(1);
        Assert.False(second.ContainsKey("result"));
        Assert.IsType<string>(second["ex"], exactMatch: false);
    }

    [Fact]
    public void DuplicateResultIds_FirstWins_ShouldBeMapped()
    {
        // Arrange: two result items have equal rid, the first one should be used
        var input = new[] { CreateExpando(5) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":\"5\",\"v\":1},{\"rid\":\"5\",\"v\":2}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var schema =
            "{ \"type\":\"object\", \"properties\":{ \"rid\":{\"type\":\"string\"}, \"v\":{\"type\":\"number\"} }, \"required\":[\"rid\"], \"additionalProperties\": true }";
        var trans = NewTrans(mock, schema, batchSize: 1, failOnError: true);

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: take the first v=1
        Assert.Single(dest.Data);
        Assert.Equal(1.0, ((dynamic)dest.Data.ElementAt(0)).result.v);
    }

    [Fact]
    public void MissingInputId_WithErrorLinkAndFailOnErrorTrue_ShouldRouteErrorPerRow()
    {
        // Arrange: second input is missing id, errors should be sent to error buffer per-row
        dynamic a = new ExpandoObject();
        a.id = 1;
        dynamic b = new ExpandoObject();
        b.other = 2; // non-existent id
        var input = new[] { (ExpandoObject)a, (ExpandoObject)b };

        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();
        var errorDest = new MemoryDestination<EtlKitError>();

        var response = "{\"results\":[{\"rid\":\"1\"}]}";
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var trans = NewTrans(mock, ResultsSchemaRidOnly(), batchSize: 2, failOnError: true);

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        trans.LinkErrorTo(errorDest);
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        // Assert: output contains both rows, error buffer has one error (for the second row)
        Assert.Equal(2, dest.Data.Count);
        Assert.Single(errorDest.Data);
        IDictionary<string, object> second = dest.Data.ElementAt(1)!;
        Assert.False(second.ContainsKey("result"));
        Assert.IsType<string>(second["ex"], exactMatch: false);
    }

    [Fact]
    public void FailOnErrorFalse_PartialValidBatch_ShouldEnrichSome_AndMarkOthers()
    {
        // Arrange: three inputs, results only for 1 and 3 — second is marked with Exception
        var input = new[] { CreateExpando(1), CreateExpando(2), CreateExpando(3) };
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var response = "{\"results\":[{\"rid\":3},{\"rid\":1}]}"; // reordered and missing 2
        var mock = new Mock<IChatClient>();
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, response)]));

        var trans = NewTrans(mock, ResultsSchemaRidOnly(), batchSize: 3, failOnError: false);

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: id=1 enriched, id=2 Exception, id=3 enriched
        Assert.Equal(3, dest.Data.Count);
        Assert.Equal(1.0, ((dynamic)dest.Data.ElementAt(0)).result.rid);
        IDictionary<string, object> second = dest.Data.ElementAt(1)!;
        Assert.False(second.ContainsKey("result"));
        Assert.IsType<string>(second["ex"], exactMatch: false);
        Assert.Equal(3.0, ((dynamic)dest.Data.ElementAt(2)).result.rid);
    }

    [Theory]
    [InlineData(2, 2, 1)] // one batch → 1 Dispose
    [InlineData(5, 2, 3)] // 5 items, BatchSize=2 → 3 batches → 3 Dispose
    public void ClientFactory_ShouldDispose_PerBatch(
        int totalItems,
        int batchSize,
        int expectedDisposes
    )
    {
        // Arrange
        var input = Enumerable.Range(1, totalItems).Select(i => CreateExpando(i)).ToArray();
        var source = new MemorySource<ExpandoObject>(input);
        var dest = new MemoryDestination<ExpandoObject>();

        var results = string.Join(
            ',',
            Enumerable.Range(1, totalItems).Select(i => $"{{\"rid\":\"{i}\"}}")
        );
        var raw = $"{{\"results\":[{results}]}}";
        var mock = new Mock<IChatClient>(MockBehavior.Strict);
        mock.Setup(c =>
                c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ChatResponse([new(ChatRole.User, raw)]));
        mock.As<IDisposable>().Setup(d => d.Dispose());

        var settings = new ApiSettings();
        var created = 0;
        var trans = new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                created++;
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = ResultsSchemaRidOnly(),
            },
            BatchSize = batchSize,
            FailOnError = true,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        mock.As<IDisposable>().Verify(d => d.Dispose(), Times.Exactly(expectedDisposes));
        Assert.Equal(expectedDisposes, created);
    }

    private static ExpandoObject CreateExpando(object id, string? sentiment = null)
    {
        dynamic o = new ExpandoObject();
        o.id = id;
        if (sentiment != null)
        {
            o.sentiment = sentiment;
        }
        return (ExpandoObject)o;
    }

    private static AIBatchTransformation NewTrans(
        Mock<IChatClient> mock,
        string schema,
        int batchSize,
        bool failOnError,
        string? rawField = null
    )
    {
        var settings = new ApiSettings();
        return new AIBatchTransformation(
            (s) =>
            {
                Assert.Equal(settings, s);
                return mock.Object;
            }
        )
        {
            ApiSettings = settings,
            Prompt = "{\"items\": {{ input | json_array }} }",
            ResultSettings = new ResultSettings
            {
                ResultItemsJsonPath = "results",
                ResultField = "result",
                ExceptionField = "ex",
                InputDataIdentificationField = "id",
                ResultDataIdentificationField = "rid",
                ResultsJsonSchema = schema,
                RawResponseField = rawField,
            },
            BatchSize = batchSize,
            FailOnError = failOnError,
        };
    }

    private sealed class FailingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => Task.FromException<ChatResponse>(new InvalidOperationException("AI error"));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => GetEmptyAsync();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

#pragma warning disable CS1998 // This async method lacks 'await' operators and will run synchronously
        private static async IAsyncEnumerable<ChatResponseUpdate> GetEmptyAsync()
        {
            yield break;
        }
#pragma warning restore CS1998

        public void Dispose() { }
    }
}
