using System.Linq;
using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.ExcelSource
{
    [Collection("FlatFilesToDatabase")]
    public class ExcelSourceBlankRowsTests : FlatFileConnectorsTestBase
    {
        public ExcelSourceBlankRowsTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MyDataRow
        {
            [ExcelColumn(0)]
            public int No { get; set; }

            [ExcelColumn(1)]
            public string ID { get; set; }

            [ExcelColumn(2)]
            public string Desc { get; set; }
        }

        [Fact]
        public void ExcelNoBlankRows()
        {
            //Arrange
            //Act
            var result = LoadExcelIntoMemory("res/Excel/DemoExcel_BlankRows_OK.xlsx");
            //Assert
            Assert.True(result.Count == 6);
        }

        [Fact]
        public void ExcelWithBlankRows()
        {
            //Arrange
            //Act
            var result = LoadExcelIntoMemory("res/Excel/DemoExcel_BlankRows_Error.xlsx");
            //Assert
            Assert.True(result.Count == 6);
        }

        private static IList<MyDataRow> LoadExcelIntoMemory(string filename)
        {
            var dest = new MemoryDestination<MyDataRow>();

            var source = new ExcelSource<MyDataRow>(filename)
            {
                Range = new ExcelRange(1, 3),
                HasNoHeader = true,
            };

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            return dest.Data.ToList();
        }

        public class MySimpleRow
        {
            [ExcelColumn(0)]
            public int Col1 { get; set; }

            [ExcelColumn(1)]
            public string Col2 { get; set; }
        }

        [Fact]
        public void IgnoreBlankRows()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestinationBlankRows");

            //Act
            var source = new ExcelSource<MySimpleRow>("res/Excel/TwoColumnBlankRow.xlsx")
            {
                IgnoreBlankRows = true,
                HasNoHeader = true,
            };
            var dest = new DbDestination<MySimpleRow>(
                SqlConnection,
                "ExcelDestinationBlankRows",
                2
            );

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
