using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.ExcelSource
{
    [Collection("FlatFilesToDatabase")]
    public class ExcelSourceDynamicObjectTests : FlatFileConnectorsTestBase
    {
        public ExcelSourceDynamicObjectTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleDataNoHeader()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestinationDynamic");

            //Act
            EtlKit.DataFlow.ExcelSource source = new EtlKit.DataFlow.ExcelSource(
                "res/Excel/TwoColumnShiftedData.xlsx"
            )
            {
                Range = new ExcelRange(3, 4),
                HasNoHeader = true,
            };
            var trans = new RowTransformation(row =>
            {
                dynamic r = row;
                r.Col1 = r.Column1;
                r.Col2 = r.Column2;
                return r;
            });
            var dest = new DbDestination(SqlConnection, "ExcelDestinationDynamic");

            source.LinkTo(trans);
            trans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Fact]
        public void SimpleDataWithHeader()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestinationDynamicWithHeader");
            EtlKit.DataFlow.ExcelSource source = new EtlKit.DataFlow.ExcelSource(
                "res/Excel/TwoColumnWithHeader.xlsx"
            );
            var dest = new DbDestination(SqlConnection, "ExcelDestinationDynamicWithHeader");

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Fact]
        public void ShiftedDataWithHeader()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestinationDynamicWithHeader");
            EtlKit.DataFlow.ExcelSource source = new EtlKit.DataFlow.ExcelSource(
                "res/Excel/TwoColumnShiftedDataWithHeader.xlsx"
            )
            {
                Range = new ExcelRange(3, 3),
            };
            var dest = new DbDestination(SqlConnection, "ExcelDestinationDynamicWithHeader");

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
