using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowDuplication
{
    [Collection("Transformations")]
    public class RowDuplicationDynamicObjectTests : TransformationsTestBase
    {
        public RowDuplicationDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void NoParameter()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("RowDuplicationSource");
            source2Columns.InsertTestData();

            var source = new DbSource(SqlConnection, "RowDuplicationSource");
            EtlKit.DataFlow.RowDuplication duplication = new EtlKit.DataFlow.RowDuplication();
            var dest = new MemoryDestination();

            //Act
            source.LinkTo(duplication);
            duplication.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 1 && (row as dynamic).Col2 == "Test1");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 1 && (row as dynamic).Col2 == "Test1");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 2 && (row as dynamic).Col2 == "Test2");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 2 && (row as dynamic).Col2 == "Test2");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 3 && (row as dynamic).Col2 == "Test3");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 3 && (row as dynamic).Col2 == "Test3");
                }
            );
        }

        [Fact]
        public void WithPredicate()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("RowDuplicationSource");
            source2Columns.InsertTestData();

            var source = new DbSource(SqlConnection, "RowDuplicationSource");
            EtlKit.DataFlow.RowDuplication duplication = new EtlKit.DataFlow.RowDuplication(row =>
            {
                dynamic r = row;
                return r.Col1 == 1 || r.Col2 == "Test3";
            });
            var dest = new MemoryDestination();

            //Act
            source.LinkTo(duplication);
            duplication.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 1 && (row as dynamic).Col2 == "Test1");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 1 && (row as dynamic).Col2 == "Test1");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 2 && (row as dynamic).Col2 == "Test2");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 3 && (row as dynamic).Col2 == "Test3");
                },
                row =>
                {
                    Assert.True((row as dynamic).Col1 == 3 && (row as dynamic).Col2 == "Test3");
                }
            );
        }
    }
}
