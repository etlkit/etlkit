using System.Threading;
using System.Threading.Tasks;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowTransformation
{
    [Collection("Transformations")]
    public class RowTransformationFluentNotationTests : TransformationsTestBase
    {
        public RowTransformationFluentNotationTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [PublicAPI]
        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public async Task Linking3Transformations()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("SourceMultipleLinks");
            source2Columns.InsertTestData();
            var dest2Columns = new TwoColumnsTableFixture("DestinationMultipleLinks");

            var source = new DbSource<string[]>(SqlConnection, "SourceMultipleLinks");
            var dest = new DbDestination<string[]>(SqlConnection, "DestinationMultipleLinks");
            var trans1 = new RowTransformation<string[]>(row => row);
            var trans2 = new RowTransformation<string[]>(row => row);
            var trans3 = new RowTransformation<string[]>(row => row);

            //Act
            source.LinkTo(trans1).LinkTo(trans2).LinkTo(trans3).LinkTo(dest);
            await source.ExecuteAsync(CancellationToken.None);
            await dest.Completion.ConfigureAwait(true);

            //Assert
            dest2Columns.AssertTestData();
        }

        [Fact]
        public async Task UsingFluentVoidPredicate()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("SourceMultipleLinks");
            source2Columns.InsertTestData();
            source2Columns.InsertTestDataSet2();
            var dest2Columns = new TwoColumnsTableFixture("DestinationMultipleLinks");

            var source = new DbSource<MySimpleRow>(SqlConnection, "SourceMultipleLinks");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "DestinationMultipleLinks");
            var trans1 = new RowTransformation<MySimpleRow>(row => row);

            //Act
            source.LinkTo(trans1, row => row.Col1 < 4, row => row.Col1 >= 4).LinkTo(dest);

            await source.ExecuteAsync(CancellationToken.None);
            await dest.Completion.ConfigureAwait(true);

            //Assert
            dest2Columns.AssertTestData();
        }

        private class MyOtherRow
        {
            [ColumnMap("Col1")]
            public int ColA { get; set; }

            [ColumnMap("Col2")]
            public string ColB { get; set; }
        }

        [Fact]
        public void UsingDifferentObjectTypes()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("SourceMultipleLinks");
            source2Columns.InsertTestData();
            var dest2Columns = new TwoColumnsTableFixture("DestinationMultipleLinks");

            var source = new DbSource<MySimpleRow>(SqlConnection, "SourceMultipleLinks");
            var dest = new DbDestination<MyOtherRow>(SqlConnection, "DestinationMultipleLinks");
            var trans1 = new RowTransformation<MySimpleRow, MyOtherRow>(row => new MyOtherRow
            {
                ColA = row.Col1,
                ColB = row.Col2,
            });

            //Act
            source.LinkTo<MyOtherRow>(trans1).LinkTo(dest);

            //Assert
            source.Execute();
            dest.Wait();
            dest2Columns.AssertTestData();
        }
    }
}
