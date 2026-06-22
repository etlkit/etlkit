using System.Threading;
using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBSource
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbSourceDataTypeTests : DatabaseConnectorsTestBase
    {
        public DbSourceDataTypeTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        private enum EnumType
        {
            Value1 = 1,
            Value2 = 2,
        }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public static IEnumerable<CultureInfo> Cultures => AllLocalCultures;

        // Each culture for each database

        public static IEnumerable<object[]> Combinations =>
            Connections.SelectMany(_ => Cultures, (conn, culture) => new[] { conn[0], culture });

        [Theory]
        [MemberData(nameof(Combinations))]
        public void ReadDifferentTypes(IConnectionManager connection, CultureInfo culture)
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = culture;
                //Arrange
                CreateTableTask.Create(
                    connection,
                    "different_type_table",
                    new List<TableColumn>
                    {
                        new("int_col", "INT", false),
                        new("long_col", "BIGINT", true),
                        new("decimal_col", "FLOAT", true),
                        new("double_col", "FLOAT", true),
                        new("datetime_col", "DATETIME", true),
                        new("date_col", "DATE", true),
                        new("string_col", "VARCHAR(200)", true),
                        new("char_col", "CHAR(1)", true),
                        new("decimal_string_col", "DECIMAL(12,10)", true),
                        new("null_col", "CHAR(1)", true),
                        new("enum_col", "INT", true),
                    }
                );

                SqlTask.ExecuteNonQuery(
                    connection,
                    "Insert test data",
                    $@"INSERT INTO different_type_table 
                    (int_col, long_col, decimal_col, double_col, datetime_col, date_col
, string_col, char_col, decimal_string_col, null_col, enum_col) 
                VALUES (1, -1, 2.3, 5.4, '2010-01-01 10:00:00', '2020-01-01', 'Test', 'T', 13.4566, NULL, 2 )"
                );
                //Act
                var source = new DbSource<MyDataTypeRow>(connection, "different_type_table");
                var dest = new MemoryDestination<MyDataTypeRow>();

                source.LinkTo(dest);
                source.Execute(CancellationToken.None);
                dest.Wait();

                //Assert
                Assert.Equal(1, dest.Data.First().IntCol);
                Assert.Equal(-1, dest.Data.First().LongCol);
                Assert.Equal(2.3M, dest.Data.First().DecimalCol);
                Assert.True(dest.Data.First().DoubleCol is >= 5.4 and < 5.5);
                Assert.Equal(
                    "2010-01-01 10:00:00",
                    dest.Data.First().DateTimeCol.ToString("yyyy-MM-dd hh:mm:ss")
                );
                Assert.Equal("2020-01-01", dest.Data.First().DateCol.ToString("yyyy-MM-dd"));
                Assert.Equal("Test", dest.Data.First().StringCol);
                Assert.Equal('T', dest.Data.First().CharCol);
                Assert.StartsWith(
                    13.4566m.ToString(CultureInfo.CurrentCulture),
                    dest.Data.First().DecimalStringCol
                );
                Assert.Null(dest.Data.First().NullCol);
                Assert.Equal(EnumType.Value2, dest.Data.First().EnumCol);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        private class MyDataTypeRow
        {
            [ColumnMap("id")]
            public int Id { get; set; }

            [ColumnMap("int_col")]
            public int IntCol { get; set; }

            [ColumnMap("long_col")]
            public long LongCol { get; set; }

            [ColumnMap("decimal_col")]
            public decimal DecimalCol { get; set; }

            [ColumnMap("double_col")]
            public double DoubleCol { get; set; }

            [ColumnMap("datetime_col")]
            public DateTime DateTimeCol { get; set; }

            [ColumnMap("date_col")]
            public DateTime DateCol { get; set; }

            [ColumnMap("string_col")]
            public string StringCol { get; set; }

            [ColumnMap("char_col")]
            public char CharCol { get; set; }

            [ColumnMap("decimal_string_col")]
            public string DecimalStringCol { get; set; }

            [ColumnMap("null_col")]
            public string NullCol { get; set; }

            [ColumnMap("enum_col")]
            public EnumType EnumCol { get; set; }
        }
    }
}
