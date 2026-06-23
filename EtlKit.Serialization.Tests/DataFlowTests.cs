using System.Dynamic;
using System.Text;
using System.Xml.Serialization;
using EtlKit.DataFlow;
using EtlKit.Serialization.DataFlow;
using JetBrains.Annotations;

namespace EtlKit.Serialization.Tests
{
    public sealed class DataFlowTests : IDisposable
    {
        private readonly string _csvUri;

        public DataFlowTests()
        {
            var csv = GetCsv();
            _csvUri = CreateFile(csv, "csv");
        }

        [Fact]
        public void DataFlow_ReadFromXml_ShouldBePassed()
        {
            var referenceId = Guid.NewGuid();
            var name = Guid.NewGuid().ToString();
            const int ms = 100;
            var xml =
                @$"<EtlDataFlowStep>
			        <ReferenceId>
                        {referenceId}
                    </ReferenceId>
			        <Name>
                        {name}
                    </Name>
			        <TimeoutMilliseconds>{ms}</TimeoutMilliseconds>
                    <CustomCsvSource>
                        <Bool>True</Bool>
                        <Char>#</Char>
                        <Byte>1</Byte>
                        <DateTime>11.07.1976</DateTime>
                        <Guid>{referenceId}</Guid>
                        <NullBool>True</NullBool>
                        <NullChar>#</NullChar>
                        <NullByte>1</NullByte>
                        <NullDateTime>11.07.1976</NullDateTime>
                        <NullGuid>{referenceId}</NullGuid>
                        <Int>-1</Int>
                        <Uint>1</Uint>
                        <Long>-1</Long>
                        <Ulong>1</Ulong>
                        <Short>-1</Short>
                        <Ushort>1</Ushort>
                        <Double>1.0</Double>
                        <NullInt>-1</NullInt>
                        <NullUint>1</NullUint>
                        <NullLong>-1</NullLong>
                        <NullUlong>1</NullUlong>
                        <NullShort>-1</NullShort>
                        <NullUshort>1</NullUshort>
                        <NullDouble>-1.0</NullDouble>
                        <Uri>{_csvUri}</Uri>
                        <Strings>
                            <string>test</string>
                            <string><![CDATA[test<!"""">]]></string>
                            <string><![CDATA[test1<!"""">]]><![CDATA[test2<!"""">]]></string>
                        </Strings>
                        <Stream type=""MemoryStream"" />
                        <Enum>Value2</Enum>
                        <NullEnum>Value1</NullEnum>
                        <IntegerList>
                            <int>1</int>
                            <int>2</int>
                            <int>3</int>
                        </IntegerList>
                        <Configuration>
                            <Delimiter>;</Delimiter>
                            <Escape>#</Escape>
                            <Quote>$</Quote>
                        </Configuration>
                        <Metadata>
                            <Source>TestSystem</Source>
                            <Version>1.0</Version>
                            <Settings>
                                <Timeout>30</Timeout>
                                <Retries>3</Retries>
                            </Settings>
                        </Metadata>
                        <LinkTo>
                            <JsonTransformation>
                                <Mappings>
                                    <Col1>
                                        <Name>data</Name>
                                        <Path>$.Data.Id</Path>
                                    </Col1>
                                    <Col2>
                                        <Name>data</Name>
                                        <Path>$.Data.Name</Path>
                                    </Col2>
                                </Mappings>
                                <LinkTo>
                                    <MemoryDestination></MemoryDestination>
                                </LinkTo>
                            </JsonTransformation>
                        </LinkTo>
                    </CustomCsvSource>
		        </EtlDataFlowStep>";

            using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
            var serializer = new XmlSerializer(typeof(EtlDataFlowStep));
            var step = (EtlDataFlowStep)serializer.Deserialize(stream)!;

            step.Invoke(CancellationToken.None);

            //Assert
            Assert.NotNull(step);
            Assert.Equal(referenceId, step.ReferenceId);
            Assert.Equal(name, step.Name);
            Assert.Equal(ms, step.TimeoutMilliseconds);

            Assert.IsType<CustomCsvSource>(step.Source);
            var customCsvSource = (CustomCsvSource)step.Source;
            Assert.Equal(_csvUri, customCsvSource.Uri);
            Assert.Equal(referenceId, customCsvSource.Guid);
            Assert.Equal(referenceId, customCsvSource.NullGuid);
            Assert.Equal(";", customCsvSource.Configuration.Delimiter);
            Assert.Equal('#', customCsvSource.Configuration.Escape);
            Assert.NotNull(customCsvSource.Strings);
            Assert.NotEmpty(customCsvSource.Strings);

            var strings = customCsvSource.Strings.ToArray();
            Assert.Equal(3, strings.Length);
            Assert.Equal("test", strings[0]);
            Assert.Equal("test<!\"\">", strings[1]);
            Assert.Equal("test1<!\"\">test2<!\"\">", strings[2]);
            Assert.NotNull(customCsvSource.Stream);
            Assert.IsType<MemoryStream>(customCsvSource.Stream);
            Assert.Equal(CustomCsvSource.EnumType.Value2, customCsvSource.Enum);
            Assert.Equal(CustomCsvSource.EnumType.Value1, customCsvSource.NullEnum);
            Assert.NotNull(customCsvSource.IntegerList);
            Assert.NotEmpty(customCsvSource.IntegerList);
            Assert.Equal(new[] { 1, 2, 3 }, customCsvSource.IntegerList);

            // Verify Metadata (IDictionary<string, object?>) deserialization
            Assert.NotNull(customCsvSource.Metadata);
            Assert.Equal(3, customCsvSource.Metadata!.Count);
            Assert.True(customCsvSource.Metadata.ContainsKey("Source"));
            Assert.True(customCsvSource.Metadata.ContainsKey("Version"));
            Assert.True(customCsvSource.Metadata.ContainsKey("Settings"));
            Assert.Equal("TestSystem", customCsvSource.Metadata["Source"]);
            Assert.Equal("1.0", customCsvSource.Metadata["Version"]);

            var settings = customCsvSource.Metadata["Settings"] as IDictionary<string, object?>;
            Assert.NotNull(settings);
            Assert.True(settings!.ContainsKey("Timeout"));
            Assert.True(settings.ContainsKey("Retries"));
            Assert.Equal("30", settings["Timeout"]);
            Assert.Equal("3", settings["Retries"]);

            Assert.Equal('#', customCsvSource.Char);
            Assert.Equal((byte)1, customCsvSource.Byte);
            Assert.Equal(-1, customCsvSource.Int);
            Assert.Equal(1u, customCsvSource.Uint);
            Assert.Equal(-1, customCsvSource.NullInt);
            Assert.Equal(-1L, customCsvSource.Long);
            Assert.Equal(1ul, customCsvSource.Ulong);
            Assert.Equal(-1L, customCsvSource.NullLong);
            Assert.Equal(1.0, customCsvSource.Double);
            Assert.Equal(-1.0, customCsvSource.NullDouble);
            Assert.IsType<double>(customCsvSource.NullDouble);

            Assert.NotNull(step.Destinations);
            Assert.NotEmpty(step.Destinations);
            Assert.All(
                step.Destinations,
                d => Assert.IsAssignableFrom<MemoryDestination<ExpandoObject>>(d)
            );
            var destItem = Assert.Single(step.Destinations);

            var dest = (MemoryDestination<ExpandoObject>)destItem;
            Assert.NotNull(dest.Data);
            Assert.NotEmpty(dest.Data);
            Assert.Equal(3, dest.Data.Count);

            dynamic col = new ExpandoObject();
            col.Col1 = 1;
            col.Col2 = "Test1";
            Assert.Contains(dest.Data, item => ExpandoEquals(item, (ExpandoObject)col));
            col.Col1 = 2;
            col.Col2 = "Test2";
            Assert.Contains(dest.Data, item => ExpandoEquals(item, (ExpandoObject)col));
            col.Col1 = 3;
            col.Col2 = "Test3";
            Assert.Contains(dest.Data, item => ExpandoEquals(item, (ExpandoObject)col));
        }

        [Fact]
        public void DataFlow_ReadFromXml_NotSetNullableShouldBeProcessed()
        {
            var referenceId = Guid.NewGuid();
            var name = Guid.NewGuid().ToString();
            const int ms = 100;
            var xml =
                @$"<EtlDataFlowStep>
			        <ReferenceId>
                        {referenceId}
                    </ReferenceId>
			        <Name>
                        {name}
                    </Name>
			        <TimeoutMilliseconds>{ms}</TimeoutMilliseconds>
                    <CustomCsvSource>
                        <NullBool/>
                        <NullChar/>
                        <NullByte/>
                        <NullDateTime/>
                        <NullGuid/>
                        <NullInt/>
                        <NullUint/>
                        <NullLong/>
                        <NullUlong/>
                        <NullShort/>
                        <NullUshort/>
                        <NullDouble/>
                        <NullEnum/>
                        <LinkTo>
                            <MemoryDestination/>
                        </LinkTo>
                    </CustomCsvSource>
		        </EtlDataFlowStep>";

            using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
            var serializer = new XmlSerializer(typeof(EtlDataFlowStep));
            var step = (EtlDataFlowStep)serializer.Deserialize(stream)!;

            //Assert
            Assert.NotNull(step);
            Assert.Equal(referenceId, step.ReferenceId);
            Assert.Equal(name, step.Name);

            Assert.IsType<CustomCsvSource>(step.Source);
            var customCsvSource = (CustomCsvSource)step.Source;
            Assert.Null(customCsvSource.NullBool);
            Assert.False(customCsvSource.NullBool.HasValue);
            Assert.Null(customCsvSource.NullChar);
            Assert.False(customCsvSource.NullChar.HasValue);
            Assert.Null(customCsvSource.NullByte);
            Assert.False(customCsvSource.NullByte.HasValue);
            Assert.Null(customCsvSource.NullDateTime);
            Assert.False(customCsvSource.NullDateTime.HasValue);
            Assert.Null(customCsvSource.NullGuid);
            Assert.False(customCsvSource.NullGuid.HasValue);
            Assert.Null(customCsvSource.NullEnum);
            Assert.False(customCsvSource.NullEnum.HasValue);
            Assert.Null(customCsvSource.NullShort);
            Assert.False(customCsvSource.NullShort.HasValue);
            Assert.Null(customCsvSource.NullUshort);
            Assert.False(customCsvSource.NullUshort.HasValue);
            Assert.Null(customCsvSource.NullInt);
            Assert.False(customCsvSource.NullInt.HasValue);
            Assert.Null(customCsvSource.NullUint);
            Assert.False(customCsvSource.NullUint.HasValue);
            Assert.Null(customCsvSource.NullLong);
            Assert.False(customCsvSource.NullLong.HasValue);
            Assert.Null(customCsvSource.NullUlong);
            Assert.False(customCsvSource.NullUlong.HasValue);
            Assert.Null(customCsvSource.NullDouble);
            Assert.False(customCsvSource.NullDouble.HasValue);
        }

        [Fact]
        public void DataFlow_TransformationError_ShouldBeHandled()
        {
            // Arrange
            var referenceId = Guid.NewGuid();
            var name = Guid.NewGuid().ToString();
            const int ms = 100;
            var xml =
                @$"<EtlDataFlowStep>
			        <ReferenceId>
                        {referenceId}
                    </ReferenceId>
			        <Name>
                        {name}
                    </Name>
			        <TimeoutMilliseconds>{ms}</TimeoutMilliseconds>
                    <CustomCsvSource>
                        <Uri>{_csvUri}</Uri>
                        <Configuration>
                            <Delimiter>;</Delimiter>
                            <Escape>#</Escape>
                            <Quote>$</Quote>
                        </Configuration>
                        <LinkTo>
                            <BrokenTransformation>
                                <LinkTo>
                                    <MemoryDestination>
                                    </MemoryDestination>
                                </LinkTo>
                                <LinkErrorTo>
                                    <ErrorLogDestination/>
                                </LinkErrorTo>
                            </BrokenTransformation>
                        </LinkTo>
                    </CustomCsvSource>
		        </EtlDataFlowStep>";

            using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
            var serializer = new XmlSerializer(typeof(EtlDataFlowStep));

            // Act
            var step = (EtlDataFlowStep)serializer.Deserialize(stream)!;
            step.Invoke(CancellationToken.None);

            // Assert
            Assert.NotNull(step.ErrorDestinations);
            var errorDestinationItem = Assert.Single(step.ErrorDestinations);
            var errorDestination = Assert.IsType<ErrorLogDestination>(errorDestinationItem);

            Assert.Collection(
                errorDestination.Errors,
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    ),
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    ),
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    )
            );
        }

        [Fact]
        public void DataFlow_AITransformation_ShouldBePassed()
        {
            // Arrange
            var referenceId = Guid.NewGuid();
            var name = Guid.NewGuid().ToString();
            var xml =
                @$"<EtlDataFlowStep>
			        <ReferenceId>
                        {referenceId}
                    </ReferenceId>
			        <Name>
                        {name}
                    </Name>
			        <TimeoutMilliseconds>100</TimeoutMilliseconds>
                    <CustomCsvSource>
                        <Uri>{_csvUri}</Uri>
                        <Configuration>
                            <Delimiter>;</Delimiter>
                            <Escape>#</Escape>
                            <Quote>$</Quote>
                        </Configuration>
                        <LinkTo>
                            <AIBatchTransformation>
						<BatchSize>10</BatchSize>
						<ApiSettings>
							<ApiKey>ApiKey</ApiKey>
							<ApiType>openai</ApiType>
							<ApiModel>ApiModel</ApiModel>
							<ApiBaseUrl>https://localhost</ApiBaseUrl>
						</ApiSettings>
						<FailOnError>false</FailOnError>
						<ResultSettings>
						<ResultItemsJsonPath>results</ResultItemsJsonPath>
						<ResultField>Result</ResultField>
						<RawResponseField>Response</RawResponseField>
						<ExceptionField>Exception</ExceptionField>
						<HttpCodeField>HttpCode</HttpCodeField>
						<InputDataIdentificationField>respondent_id</InputDataIdentificationField>
						<ResultDataIdentificationField>respondent_id</ResultDataIdentificationField>
						<ResultsJsonSchema></ResultsJsonSchema>
						</ResultSettings>
                        <PromptParameters>
                          <ReasonsList>12345</ReasonsList>
                          <MaxTokens>1000</MaxTokens>
                          <Temperature>0.7</Temperature>
                          <NestedConfig>
                            <Model>gpt-4</Model>
                            <Version>v1</Version>
                          </NestedConfig>
                        </PromptParameters>
						<Prompt>Prompt</Prompt>
                                <LinkTo>
                                    <MemoryDestination>
                                    </MemoryDestination>
                                </LinkTo>
                                <LinkErrorTo>
                                    <ErrorLogDestination/>
                                </LinkErrorTo>
                            </AIBatchTransformation>
                        </LinkTo>
                    </CustomCsvSource>
		        </EtlDataFlowStep>";

            using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
            var serializer = new XmlSerializer(typeof(EtlDataFlowStep));

            // Act
            var step = (EtlDataFlowStep)serializer.Deserialize(stream)!;
            step.Invoke(CancellationToken.None);

            // Assert - Verify basic deserialization
            Assert.NotNull(step.Source);
            Assert.IsType<CustomCsvSource>(step.Source);
            Assert.NotNull(step.Destinations);
            Assert.NotEmpty(step.Destinations);

            // Verify ErrorDestinations structure
            Assert.NotNull(step.ErrorDestinations);
            var errorDest = Assert.Single(step.ErrorDestinations);
            Assert.IsType<ErrorLogDestination>(errorDest);
        }

        [Theory]
        [InlineData(nameof(MemoryDestination))]
        [InlineData(nameof(DbDestination))]
        [InlineData(nameof(CsvDestination))]
        [InlineData(nameof(CustomCsvDestination<string>))]
        public void DataFlow_Deserialize_ShouldReturnErrorDestinations_NotEmpty(string dest)
        {
            // Arrange
            var xml =
                @$"<EtlDataFlowStep>
                    <CustomCsvSource>
                        <LinkTo>
                            <{dest}>
                                <LinkErrorTo>
                                    <ErrorLogDestination/>
                                </LinkErrorTo>
                            </{dest}>
                        </LinkTo>
                    </CustomCsvSource>
		        </EtlDataFlowStep>";

            using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
            var serializer = new XmlSerializer(typeof(EtlDataFlowStep));

            // Act
            var step = (EtlDataFlowStep)serializer.Deserialize(stream)!;

            // Assert
            Assert.NotNull(step.ErrorDestinations);
            var singleErrorDest = Assert.Single(step.ErrorDestinations);
            Assert.IsAssignableFrom<ErrorLogDestination>(singleErrorDest);
        }

        [Fact]
        public void Should_LinkAllErrors()
        {
            // Arrange
            var xml =
                @"<EtlDataFlowStep>
                    <CustomCsvSource>
                    </CustomCsvSource>
		        </EtlDataFlowStep>";
            var errorLogDestination = new ErrorLogDestination();

            // Act
            EtlDataFlowStep step = DataFlowXmlReader.Deserialize<EtlDataFlowStep>(
                xml,
                errorLogDestination
            );

            // Assert
            Assert.NotNull(step.ErrorDestinations);
            var singleErrorDest = Assert.Single(step.ErrorDestinations);
            Assert.IsAssignableFrom<ErrorLogDestination>(singleErrorDest);
        }

        [Fact]
        public void DataFlow_ReadFromXml_CheckConnectionManagerToDispose()
        {
            var referenceId = Guid.NewGuid();
            var name = Guid.NewGuid().ToString();
            const int ms = 100;
            var xml =
                @$"<EtlDataFlowStep>
			        <ReferenceId>
                        {referenceId}
                    </ReferenceId>
			        <Name>
                        {name}
                    </Name>
			        <TimeoutMilliseconds>{ms}</TimeoutMilliseconds>
                    <MemorySource>
                        <ConnectionManager type=""PostgresConnectionManager"">
                            <ConnectionString type=""PostgresConnectionString"">
                                <Value>Server=local;Port=123;Database=test;User ID=userId;Password=secret;</Value>
                            </ConnectionString>
                        </ConnectionManager>
                        <LinkTo>
                            <MemoryDestination />
                        </LinkTo>
                    </MemorySource>
		        </EtlDataFlowStep>";

            using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
            var serializer = new XmlSerializer(typeof(EtlDataFlowStep));
            using var step = (EtlDataFlowStep)serializer.Deserialize(stream)!;

            step.Invoke(CancellationToken.None);

            //Assert
            Assert.NotNull(step);
            Assert.Equal(referenceId, step.ReferenceId);
            Assert.Equal(name, step.Name);
            Assert.Equal(1, step.ConnectionManagerCount());
        }

        private static bool ExpandoEquals(ExpandoObject a, ExpandoObject b)
        {
            var dictA = (IDictionary<string, object?>)a;
            var dictB = (IDictionary<string, object?>)b;
            if (dictA.Count != dictB.Count)
                return false;
            foreach (var pair in dictB)
            {
                if (!dictA.TryGetValue(pair.Key, out var value))
                    return false;
                if (!Equals(value, pair.Value))
                    return false;
            }
            return true;
        }

        private static string GetCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("data");
            builder.AppendLine(@"{""Data"": {""Id"": 1, ""Name"": ""Test1""}}");
            builder.AppendLine(@"{""Data"": {""Id"": 2, ""Name"": ""Test2""}}");
            builder.AppendLine(@"{""Data"": {""Id"": 3, ""Name"": ""Test3""}}");
            return builder.ToString();
        }

        private static string CreateFile(string content, string ext)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.{ext}");
            File.WriteAllText(path, content);
            return path;
        }

#pragma warning disable S1144 // Unused private types or members should be removed
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        private sealed class CustomCsvSource : CsvSource
        {
            public DateTime DateTime { get; set; }
            public DateTime? NullDateTime { get; set; } = null;
            public Guid Guid { get; set; } = Guid.Empty;
            public Guid? NullGuid { get; set; } = null;
            public char Char { get; set; } = (char)0;
            public char? NullChar { get; set; } = null;
            public byte Byte { get; set; } = 0;
            public byte? NullByte { get; set; } = null;
            public bool Bool { get; set; }
            public bool? NullBool { get; set; } = null;
            public int Int { get; set; } = 0;
            public int? NullInt { get; set; } = null;
            public uint Uint { get; set; } = 0;
            public uint? NullUint { get; set; } = null;
            public long Long { get; set; } = 0;
            public long? NullLong { get; set; } = null;
            public ulong Ulong { get; set; } = 0;
            public ulong? NullUlong { get; set; } = null;
            public short Short { get; set; }
            public short? NullShort { get; set; } = null;
            public ushort Ushort { get; set; }
            public ushort? NullUshort { get; set; } = null;
            public double Double { get; set; } = 0;
            public double? NullDouble { get; set; } = null;
            public IEnumerable<string> Strings { get; set; } = null!;
            public Stream Stream { get; set; } = null!;
            public EnumType Enum { get; set; } = EnumType.Value1;
            public EnumType? NullEnum { get; set; } = null;
            public List<int> IntegerList { get; set; } = null!;
            public IDictionary<string, object?>? Metadata { get; set; } = null;

            public enum EnumType
            {
                Value1 = 1,
                Value2 = 2,
            }
        }

        private sealed class CustomCsvDestination<T> : CsvDestination<T> { }

        public void Dispose()
        {
            try
            {
                File.Delete(_csvUri);
            }
            catch
            {
                // ignore
            }
        }
    }
}
