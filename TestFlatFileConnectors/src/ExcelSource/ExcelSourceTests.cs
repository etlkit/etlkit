using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.ExcelSource
{
    [Collection("FlatFilesToDatabase")]
    public class ExcelSourceTests : FlatFileConnectorsTestBase
    {
        public ExcelSourceTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            [ExcelColumn(0)]
            public int Col1 { get; set; }

            [ExcelColumn(1)]
            public string Col2 { get; set; }
        }

        [Fact]
        public void SimpleData()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestination1");

            //Act
            var source = new ExcelSource<MySimpleRow>("res/Excel/TwoColumnData.xlsx")
            {
                HasNoHeader = true,
            };
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "ExcelDestination1", 2);

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        public class OneExcelColumn
        {
            public int Col1 { get; set; }

            [ExcelColumn(1)]
            public string Col2 { get; set; }
        }

        [Fact]
        public void OnlyOneExcelColumn()
        {
            //Arrange
            var _ = new TwoColumnsTableFixture("ExcelDestination2");

            //Act
            var source = new ExcelSource<OneExcelColumn>("res/Excel/TwoColumnData.xlsx")
            {
                HasNoHeader = true,
            };
            var dest = new DbDestination<OneExcelColumn>(SqlConnection, "ExcelDestination2", 2);

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                3,
                RowCountTask.Count(
                    SqlConnection,
                    "ExcelDestination2",
                    "Col1 = 0 AND Col2 LIKE 'Test%'"
                )
            );
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class ExcelDataSheet2
        {
            [ExcelColumn(1)]
            public string Col2 { get; set; }

            [ExcelColumn(2)]
            public decimal? Col4 { get; set; }
            public string Empty { get; set; } = "";

            [ExcelColumn(0)]
            public int Col3 { get; set; }
        }

        [Fact]
        public void DataOnSheet2WithRange()
        {
            //Arrange
            var _ = new FourColumnsTableFixture("ExcelDestination3");

            //Act
            var source = new ExcelSource<ExcelDataSheet2>("res/Excel/DataOnSheet2.xlsx")
            {
                Range = new ExcelRange(2, 4, 5, 9),
                SheetName = "Sheet2",
                HasNoHeader = true,
            };

            var dest = new DbDestination<ExcelDataSheet2>(SqlConnection, "ExcelDestination3");

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(5, RowCountTask.Count(SqlConnection, "ExcelDestination3"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "ExcelDestination3",
                    "Col2 = 'Wert1' AND Col3 = 5 AND Col4 = 1"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "ExcelDestination3",
                    "Col2 IS NULL AND Col3 = 0 AND Col4 = 1.2"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "ExcelDestination3",
                    "Col2 IS NULL AND Col3 = 7 AND Col4 = 1.234"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "ExcelDestination3",
                    "Col2 = 'Wert4' AND Col3 = 8 AND Col4 = 1.2345"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "ExcelDestination3",
                    "Col2 = 'Wert5' AND Col3 = 9 AND Col4 = 2"
                )
            );
        }

        [Serializable]
        public class Excel21Cols
        {
            [ExcelColumn(0)]
            public int Col1 { get; set; }

            [ExcelColumn(1)]
            public string Col2 { get; set; }

            [ExcelColumn(13)]
            public string N { get; set; }

            [ExcelColumn(21)]
            public string V { get; set; }
        }

        [Fact]
        public void Exceeding20Columns()
        {
            //Arrange

            //Act
            var source = new ExcelSource<Excel21Cols>("res/Excel/MoreThan20Cols.xlsx")
            {
                Range = new ExcelRange(1, 2),
                HasNoHeader = true,
            };

            var dest = new MemoryDestination<Excel21Cols>();

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            Assert.Collection(
                dest.Data,
                r => Assert.True(r.Col1 == 1 && r.Col2 == "Test1" && r.N == "N" && r.V == "V"),
                r => Assert.True(r.Col1 == 2 && r.Col2 == "Test2" && r.N == "N" && r.V == "V"),
                r => Assert.True(r.Col1 == 3 && r.Col2 == "Test3" && r.N == "N" && r.V == "V")
            );
        }
    }
}
