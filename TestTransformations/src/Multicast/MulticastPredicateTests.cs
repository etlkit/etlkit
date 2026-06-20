using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.Multicast
{
    [Collection("Transformations")]
    public class MulticastPredicateTests : TransformationsTestBase
    {
        public MulticastPredicateTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void PredicateFilteringWithInteger()
        {
            //Arrange
            var sourceTable = new TwoColumnsTableFixture("Source");
            sourceTable.InsertTestData();
            var _ = new TwoColumnsTableFixture("Destination1");
            var __ = new TwoColumnsTableFixture("Destination2");

            var source = new DbSource<MySimpleRow>(SqlConnection, "Source");
            var dest1 = new DbDestination<MySimpleRow>(SqlConnection, "Destination1");
            var dest2 = new DbDestination<MySimpleRow>(SqlConnection, "Destination2");

            //Act
            var multicast = new Multicast<MySimpleRow>();
            source.LinkTo(multicast);
            multicast.LinkTo(dest1, row => row.Col1 <= 2);
            multicast.LinkTo(dest2, row => row.Col1 > 2);
            source.Execute();
            dest1.Wait();
            dest2.Wait();

            //Assert
            Assert.Equal(
                1,
                RowCountTask.Count(SqlConnection, "Destination1", "Col1 = 1 AND Col2='Test1'")
            );
            Assert.Equal(
                1,
                RowCountTask.Count(SqlConnection, "Destination1", "Col1 = 2 AND Col2='Test2'")
            );
            Assert.Equal(
                1,
                RowCountTask.Count(SqlConnection, "Destination2", "Col1 = 3 AND Col2='Test3'")
            );
        }
    }
}
