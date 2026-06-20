using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowTransformation
{
    [Collection("Transformations")]
    public class RowTransformationDynamicObjectTests : TransformationsTestBase
    {
        public RowTransformationDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void ConvertIntoObject()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("DestinationRowTransformationDynamic");
            var source = new CsvSource<ExpandoObject>("res/RowTransformation/TwoColumns.csv");

            //Act
            var trans = new RowTransformation<ExpandoObject>(csvdata =>
            {
                dynamic c = csvdata;
                c.Col1 = c.Header1;
                c.Col2 = c.Header2;
                return c;
            });
            var dest = new DbDestination<ExpandoObject>(
                SqlConnection,
                "DestinationRowTransformationDynamic"
            );
            source.LinkTo(trans);
            trans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
