using System.Threading;
using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationDataTypeTests : DatabaseConnectorsTestBase
    {
        private readonly CultureInfo _previousCulture;

        public DbDestinationDataTypeTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture)
        {
            _previousCulture = CultureInfo.CurrentCulture;
        }

        public enum EnumType
        {
            Value1 = 1,
            Value2 = 2,
        }

        // Each culture for each database

        public static TheoryData<IConnectionManager, CultureInfo> GetCombinations()
        {
            var result = new TheoryData<IConnectionManager, CultureInfo>();
            foreach (var culture in AllLocalCultures)
            {
                foreach (var connection in AllSqlConnections)
                {
                    result.Add(connection, culture);
                }
            }
            return result;
        }

        [Theory]
        [MemberData(nameof(GetCombinations))]
        public void MixedTypes(IConnectionManager connection, CultureInfo culture)
        {
            //Arrange
            CultureInfo.CurrentCulture = culture;
            CreateTableTask.Create(
                connection,
                "datatypedestination",
                new List<TableColumn>
                {
                    new(nameof(MyDataTypeRow.IntCol), "INT", allowNulls: false),
                    new(nameof(MyDataTypeRow.LongCol), "BIGINT", allowNulls: true),
                    new(nameof(MyDataTypeRow.DecimalCol), "FLOAT", allowNulls: true),
                    new(nameof(MyDataTypeRow.DoubleCol), "FLOAT", allowNulls: true),
                    new(nameof(MyDataTypeRow.DateTimeCol), "DATETIME", allowNulls: true),
                    new(nameof(MyDataTypeRow.DateCol), "DATE", allowNulls: true),
                    new(nameof(MyDataTypeRow.StringCol), "VARCHAR(200)", allowNulls: true),
                    new(nameof(MyDataTypeRow.CharCol), "CHAR(1)", allowNulls: true),
                    new(nameof(MyDataTypeRow.NullCol), "CHAR(1)", allowNulls: true),
                    new(nameof(MyDataTypeRow.EnumCol), "INT", allowNulls: true),
                    new(nameof(MyDataTypeRow.StringAsDecimal), "DECIMAL(12,10)", allowNulls: true),
                    new(nameof(MyDataTypeRow.StringAsInt), "INT", allowNulls: true),
                    new(nameof(MyDataTypeRow.StringAsDatetime), "DATETIME", allowNulls: true),
                    new(nameof(MyDataTypeRow.StringAsDate), "DATE", allowNulls: true),
                    new(
                        nameof(MyDataTypeRow.StringAsBool),
                        connection.ConnectionManagerType == ConnectionManagerType.SqlServer
                            ? "BIT"
                            : "BOOL",
                        allowNulls: true
                    ),
                }
            );
            var connectionCulture = connection.ConnectionCulture;

            var source = new MemorySource<MyDataTypeRow>(
                new List<MyDataTypeRow>
                {
                    new()
                    {
                        IntCol = 1,
                        LongCol = -1,
                        DecimalCol = 2.3M,
                        DoubleCol = 5.4,
                        DateTimeCol = new DateTime(2010, 1, 1, 10, 10, 10, DateTimeKind.Local),
                        DateCol = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local),
                        StringCol = "Test",
                        CharCol = 'T',
                        NullCol = null,
                        EnumCol = EnumType.Value2,
                        StringAsDecimal = 13.4566m.ToString(connectionCulture),
                        StringAsInt = "1",
                        StringAsDatetime = "2010-01-01 10:10:10",
                        StringAsDate = "2020-01-01",
                        StringAsBool =
                            connection.ConnectionManagerType == ConnectionManagerType.MySql
                                ? "1"
                                : "True",
                    },
                }
            );

            //Act
            var dest = new DbDestination<MyDataTypeRow>(connection, "datatypedestination");
            source.LinkTo(dest);
            source.Execute(CancellationToken.None);
            dest.Wait();

            //Assert
            //            IntCol LongCol DecimalCol DoubleCol   DateTimeCol DateCol StringCol CharCol DecimalStringCol NullCol
            //1 - 1  2.3 5.4 2010 - 01 - 01 10:10:10.100 2020 - 01 - 01  Test T   13.4566000000   NULL
            SqlTask.ExecuteReaderSingleLine(
                connection,
                "Check data",
                "SELECT * FROM datatypedestination",
                col => Assert.Equal(1, Convert.ToInt32(col)),
                col => Assert.True(Convert.ToInt64(col) == -1),
                col => Assert.Equal(2.3M, Convert.ToDecimal(col)),
                col => Assert.Equal(5.4M, Convert.ToDecimal(col)),
                col =>
                    Assert.True(
                        Convert.ToDateTime(col)
                            == new DateTime(2010, 1, 1, 10, 10, 10, DateTimeKind.Utc)
                    ),
                col =>
                    Assert.True(
                        Convert.ToDateTime(col)
                            == new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    ),
                col => Assert.Equal("Test", Convert.ToString(col)),
                col => Assert.True(Convert.ToString(col) == "T" || Convert.ToString(col) == "84"),
                Assert.Null,
                col => Assert.Equal(2, Convert.ToInt32(col)),
                col =>
                    Assert.True(
                        Convert.ToString(col, connectionCulture)?.Replace("0", "")
                            == 13.4566m.ToString(connectionCulture)
                    ),
                col => Assert.Equal(1, Convert.ToInt32(col)),
                col =>
                    Assert.Equal(
                        new DateTime(2010, 1, 1, 10, 10, 10, DateTimeKind.Utc),
                        Convert.ToDateTime(col)
                    ),
                col =>
                    Assert.Equal(
                        new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        Convert.ToDateTime(col)
                    ),
                col => Assert.True(Convert.ToBoolean(col))
            );
        }

        private class MyDataTypeRow
        {
            [UsedImplicitly]
            public int Id { get; set; }
            public int IntCol { get; set; }
            public long LongCol { get; set; }
            public decimal DecimalCol { get; set; }
            public double DoubleCol { get; set; }
            public DateTime DateTimeCol { get; set; }
            public DateTime DateCol { get; set; }
            public string StringCol { get; set; }
            public char CharCol { get; set; }
            public string NullCol { get; set; }
            public EnumType EnumCol { get; set; }

            public string StringAsDecimal { get; set; }
            public string StringAsInt { get; set; }
            public string StringAsDatetime { get; set; }
            public string StringAsDate { get; set; }
            public string StringAsBool { get; set; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CultureInfo.CurrentCulture = _previousCulture;
            }
            base.Dispose(disposing);
        }
    }
}
