using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowDuplication
{
    [Collection("Transformations")]
    public class RowDuplicationTests : TransformationsTestBase
    {
        public RowDuplicationTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void NoParameter()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("RowDuplicationSource");
            source2Columns.InsertTestData();

            var source = new DbSource<MySimpleRow>(SqlConnection, "RowDuplicationSource");
            var duplication = new RowDuplication<MySimpleRow>();
            var dest = new MemoryDestination<MySimpleRow>();

            //Act
            source.LinkTo(duplication);
            duplication.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                d => Assert.True(d.Col1 == 1 && d.Col2 == "Test1"),
                d => Assert.True(d.Col1 == 1 && d.Col2 == "Test1"),
                d => Assert.True(d.Col1 == 2 && d.Col2 == "Test2"),
                d => Assert.True(d.Col1 == 2 && d.Col2 == "Test2"),
                d => Assert.True(d.Col1 == 3 && d.Col2 == "Test3"),
                d => Assert.True(d.Col1 == 3 && d.Col2 == "Test3")
            );
        }

        [Fact]
        public void DuplicateTwice()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("RowDuplicationSource");
            source2Columns.InsertTestData();

            var source = new DbSource<MySimpleRow>(SqlConnection, "RowDuplicationSource");
            var duplication = new RowDuplication<MySimpleRow>(2);
            var dest = new MemoryDestination<MySimpleRow>();

            //Act
            source.LinkTo(duplication);
            duplication.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                d => Assert.True(d.Col1 == 1 && d.Col2 == "Test1"),
                d => Assert.True(d.Col1 == 1 && d.Col2 == "Test1"),
                d => Assert.True(d.Col1 == 1 && d.Col2 == "Test1"),
                d => Assert.True(d.Col1 == 2 && d.Col2 == "Test2"),
                d => Assert.True(d.Col1 == 2 && d.Col2 == "Test2"),
                d => Assert.True(d.Col1 == 2 && d.Col2 == "Test2"),
                d => Assert.True(d.Col1 == 3 && d.Col2 == "Test3"),
                d => Assert.True(d.Col1 == 3 && d.Col2 == "Test3"),
                d => Assert.True(d.Col1 == 3 && d.Col2 == "Test3")
            );
        }

        [Fact]
        public void WithPredicate()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("RowDuplicationSource");
            source2Columns.InsertTestData();

            var source = new DbSource<MySimpleRow>(SqlConnection, "RowDuplicationSource");
            var duplication = new RowDuplication<MySimpleRow>(row =>
                row.Col1 == 1 || row.Col2 == "Test3"
            );
            var dest = new MemoryDestination<MySimpleRow>();

            //Act
            source.LinkTo(duplication);
            duplication.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                d => Assert.True(d.Col1 == 1 && d.Col2 == "Test1"),
                d => Assert.True(d.Col1 == 1 && d.Col2 == "Test1"),
                d => Assert.True(d.Col1 == 2 && d.Col2 == "Test2"),
                d => Assert.True(d.Col1 == 3 && d.Col2 == "Test3"),
                d => Assert.True(d.Col1 == 3 && d.Col2 == "Test3")
            );
        }
    }
}
