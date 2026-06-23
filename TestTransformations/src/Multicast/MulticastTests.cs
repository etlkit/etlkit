using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.Multicast
{
    [Collection("Transformations")]
    public class MulticastTests : TransformationsTestBase
    {
        public MulticastTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
            public int Col3 => Col1;
        }

        [Fact]
        public void DuplicateDataInto3Destinations()
        {
            //Arrange
            var sourceTable = new TwoColumnsTableFixture("Source");
            sourceTable.InsertTestData();
            var dest1Table = new TwoColumnsTableFixture("Destination1");
            var dest2Table = new TwoColumnsTableFixture("Destination2");
            var dest3Table = new TwoColumnsTableFixture("Destination3");

            var source = new DbSource<MySimpleRow>(SqlConnection, "Source");
            var dest1 = new DbDestination<MySimpleRow>(SqlConnection, "Destination1");
            var dest2 = new DbDestination<MySimpleRow>(SqlConnection, "Destination2");
            var dest3 = new DbDestination<MySimpleRow>(SqlConnection, "Destination3");

            //Act
            var multicast = new Multicast<MySimpleRow>();
            source.LinkTo(multicast);
            multicast.LinkTo(dest1);
            multicast.LinkTo(dest2);
            multicast.LinkTo(dest3);
            source.Execute();
            dest1.Wait();
            dest2.Wait();
            dest3.Wait();

            //Assert
            dest1Table.AssertTestData();
            dest2Table.AssertTestData();
            dest3Table.AssertTestData();
        }
    }
}
