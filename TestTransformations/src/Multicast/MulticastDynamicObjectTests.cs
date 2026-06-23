using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.Multicast
{
    [Collection("Transformations")]
    public class MulticastDynamicObjectTests : TransformationsTestBase
    {
        public MulticastDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SplitInto2Tables()
        {
            //Arrange
            var sourceTable = new TwoColumnsTableFixture("Source");
            sourceTable.InsertTestData();
            var dest1Table = new TwoColumnsTableFixture("Destination1");
            var dest2Table = new TwoColumnsTableFixture("Destination2");

            var source = new DbSource<ExpandoObject>(SqlConnection, "Source");
            var dest1 = new DbDestination<ExpandoObject>(SqlConnection, "Destination1");
            var dest2 = new DbDestination<ExpandoObject>(SqlConnection, "Destination2");

            //Act
            var multicast = new Multicast<ExpandoObject>();

            source.LinkTo(multicast);
            multicast.LinkTo(dest1);
            multicast.LinkTo(dest2);
            source.Execute();
            dest1.Wait();
            dest2.Wait();

            //Assert
            dest1Table.AssertTestData();
            dest2Table.AssertTestData();
        }
    }
}
