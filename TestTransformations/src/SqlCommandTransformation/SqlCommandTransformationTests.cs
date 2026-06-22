using System.Text.Json;
using EtlKit;
using EtlKit.ClickHouse.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.SqlCommandTransformation
{
    public class SqlCommandTransformationTests : TransformationsTestBase
    {
        public SqlCommandTransformationTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void ConvertIntoObject()
        {
            //Arrange
            _ = new TwoColumnsTableFixture("DestinationRowTransformation");

            var rowsCountBefore = RowCountTask.Count(SqlConnection, "DestinationRowTransformation");

            dynamic obj = new ExpandoObject();
            obj.Col1 = 123;
            obj.Col2 = "abc";

            var settings = new MemorySource<ExpandoObject>([(ExpandoObject)obj]);

            var query = new EtlKit.DataFlow.SqlCommandTransformation
            {
                ConnectionManager = SqlConnection,
                SqlTemplate =
                    "Insert INTO DestinationRowTransformation VALUES({{Col1}}, '{{Col2}}')",
            };

            var dest = new MemoryDestination<ExpandoObject>();

            //Act
            settings.LinkTo(query);
            query.LinkTo(dest);
            settings.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                rowsCountBefore + 1,
                RowCountTask.Count(SqlConnection, "DestinationRowTransformation")
            );
        }

        [Fact]
        public void SqlConnection_WhenSaveValidJsonStringWithNestedJsonStringProperty_ThenSaveValidJsonString()
        {
            //Arrange
            var fixture = new TwoColumnsTableFixture("DestinationRowTransformation");

            var rowsCountBefore = RowCountTask.Count(SqlConnection, "DestinationRowTransformation");

            dynamic obj = new ExpandoObject();
            obj.Col1 = 123;
            obj.Col2 = "{\"respondent_id\":1,\"Response\":\"{\\\"respondent_id\\\":1}\"}";

            var settings = new MemorySource<ExpandoObject>([(ExpandoObject)obj]);

            var query = new EtlKit.DataFlow.SqlCommandTransformation
            {
                ConnectionManager = SqlConnection,
                SqlTemplate =
                    "Insert INTO DestinationRowTransformation VALUES({{Col1}}, '{{Col2}}')",
            };

            var dest = new MemoryDestination<ExpandoObject>();

            //Act
            settings.LinkTo(query);
            query.LinkTo(dest);
            settings.Execute();
            dest.Wait();

            var expando = new ExpandoObject();

            var actions = fixture
                .TableDefinition.Columns.Select(column => new Action<object>(value =>
                    expando.TryAdd(column.Name, value)
                ))
                .ToArray();

            var task = new SqlTask(
                "select",
                $"select * from {fixture.TableDefinition.Name}",
                null,
                null,
                actions
            )
            {
                ConnectionManager = fixture.Connection,
            };
            task.ExecuteReader();

            Assert.NotEmpty(expando);

            //Assert
            Assert.Equal(
                rowsCountBefore + 1,
                RowCountTask.Count(SqlConnection, "DestinationRowTransformation")
            );
        }

        [Fact]
        public void ClickHouse_WhenSaveValidJsonStringWithNestedJsonStringProperty_ThenSaveInvalidJson()
        {
            //Arrange
            var connectionString = EtlKit
                .TestShared.Helper.Config.ClickHouseConnection.ConnectionString("DataFlow")
                .Value;

            var testTable = "TableWithInvalidJson";
            var table = new TableDefinition(
                testTable,
                [new("Col1", "UInt32", false, true), new("Col2", "String", true, false)]
            );

            var builder = new EtlKit.ClickHouse.ConnectionStrings.ClickHouseConnectionStringBuilder
            {
                ConnectionString = connectionString,
            };

            using var con = new ClickHouseConnectionManager(builder.ConnectionString);

            DropTableTask.DropIfExists(con, testTable);

            CreateTableTask.Create(con, table);

            dynamic obj = new ExpandoObject();
            obj.Col1 = 123;
            obj.Col2 = "{\"respondent_id\":1,\"Response\":\"{\\\"respondent_id\\\":1}\"}";

            var jsonExpando = GetExpandoFromJsonString(obj.Col2);

            Assert.NotNull(jsonExpando);

            var settings = new MemorySource<ExpandoObject>([(ExpandoObject)obj]);

            var query = new EtlKit.DataFlow.SqlCommandTransformation
            {
                ConnectionManager = con,
                SqlTemplate = "Insert INTO TableWithInvalidJson VALUES({{Col1}}, '{{Col2}}')",
            };

            var dest = new MemoryDestination<ExpandoObject>();

            //Act
            settings.LinkTo(query);
            query.LinkTo(dest);
            settings.Execute();
            dest.Wait();

            IDictionary<string, object> expando = new ExpandoObject();

            var actions = table
                .Columns.Select(column => new Action<object>(value =>
                    expando.TryAdd(column.Name, value)
                ))
                .ToArray();

            var task = new SqlTask("select", $"select * from {table.Name}", null, null, actions)
            {
                ConnectionManager = con,
            };
            task.ExecuteReader();

            //Assert
            Assert.Equal(1, RowCountTask.Count(con, "TableWithInvalidJson"));

            Assert.NotEmpty(expando);

            Assert.NotNull(expando["Col2"]);

            jsonExpando = GetExpandoFromJsonString(expando["Col2"].ToString());

            Assert.Null(jsonExpando);
        }

        [Fact]
        public void ClickHouse_WhenEscapeBackslashInNestedJsonStringProperty_ThenSaveValidJson()
        {
            //Arrange
            var connectionString = EtlKit
                .TestShared.Helper.Config.ClickHouseConnection.ConnectionString("DataFlow")
                .Value;

            var testTable = "TableWithValidJson";
            var table = new TableDefinition(
                testTable,
                [new("Col1", "UInt32", false, true), new("Col2", "String", true, false)]
            );

            var builder = new EtlKit.ClickHouse.ConnectionStrings.ClickHouseConnectionStringBuilder
            {
                ConnectionString = connectionString,
            };

            using var con = new ClickHouseConnectionManager(builder.ConnectionString);

            DropTableTask.DropIfExists(con, testTable);

            CreateTableTask.Create(con, table);

            dynamic obj = new ExpandoObject();
            obj.Col1 = 123;
            obj.Col2 = "{\"respondent_id\":1,\"Response\":\"{\\\"respondent_id\\\":1}\"}";

            var jsonExpando = GetExpandoFromJsonString(obj.Col2);

            Assert.NotNull(jsonExpando);

            // Important: we should use EscapeBackslash to save JSON string to ClickHouse properly
            obj.Col2 = EscapeBackslash(obj.Col2);

            var settings = new MemorySource<ExpandoObject>([(ExpandoObject)obj]);

            var query = new EtlKit.DataFlow.SqlCommandTransformation
            {
                ConnectionManager = con,
                SqlTemplate = "Insert INTO TableWithValidJson VALUES({{Col1}}, '{{Col2}}')",
            };

            var dest = new MemoryDestination<ExpandoObject>();

            //Act
            settings.LinkTo(query);
            query.LinkTo(dest);
            settings.Execute();
            dest.Wait();

            IDictionary<string, object> expando = new ExpandoObject();

            var actions = table
                .Columns.Select(column => new Action<object>(value =>
                    expando.TryAdd(column.Name, value)
                ))
                .ToArray();

            var task = new SqlTask("select", $"select * from {table.Name}", null, null, actions)
            {
                ConnectionManager = con,
            };
            task.ExecuteReader();

            //Assert
            Assert.Equal(1, RowCountTask.Count(con, "TableWithValidJson"));

            Assert.NotEmpty(expando);

            Assert.NotNull(expando["Col2"]);

            jsonExpando = GetExpandoFromJsonString(expando["Col2"].ToString());

            Assert.NotNull(jsonExpando);
        }

        private static string EscapeBackslash(string input)
        {
            return input?.Replace("\\", "\\\\");
        }

        [CanBeNull]
        private static ExpandoObject GetExpandoFromJsonString(string jsonString)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonString);
                var result = new ExpandoObject();
                var dictionary = (IDictionary<string, object>)result;

                foreach (var element in doc.RootElement.EnumerateObject())
                {
                    dictionary[element.Name] =
                        element.Value.ValueKind == JsonValueKind.Object
                            ? ParseJsonObject(element.Value)
                            : element.Value.ToString();
                }

                return result;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static ExpandoObject ParseJsonObject(JsonElement jsonElement)
        {
            var expandoObject = new ExpandoObject();
            var dictionary = (IDictionary<string, object>)expandoObject;

            foreach (var element in jsonElement.EnumerateObject())
            {
                dictionary[element.Name] =
                    element.Value.ValueKind == JsonValueKind.Object
                        ? ParseJsonObject(element.Value)
                        : element.Value.ToString();
            }

            return expandoObject;
        }
    }
}
