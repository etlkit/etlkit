using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.ExcelSource
{
    [Collection("FlatFilesToDatabase")]
    public class ExcelSourceStringArrayTests : FlatFileConnectorsTestBase
    {
        public ExcelSourceStringArrayTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MyData
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void SimpleDataNoHeader()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestinationStringArray");

            //Act
            var source = new ExcelSource<string[]>("res/Excel/TwoColumnData.xlsx")
            {
                HasNoHeader = true,
            };
            var trans = new RowTransformation<string[], MyData>(row =>
            {
                var result = new MyData { Col1 = int.Parse(row[0]), Col2 = row[1] };
                return result;
            });
            var dest = new DbDestination<MyData>(SqlConnection, "ExcelDestinationStringArray");

            source.LinkTo(trans);
            trans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
