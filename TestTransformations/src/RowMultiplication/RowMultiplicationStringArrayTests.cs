using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowMultiplication
{
    [Collection("Transformations")]
    public class RowMultiplicationStringArrayTests : TransformationsTestBase
    {
        public RowMultiplicationStringArrayTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void RandomDoubling()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("RowMultiplicationSource");
            source2Columns.InsertTestData();

            var source = new DbSource<string[]>(SqlConnection, "RowMultiplicationSource");
            var multiplication = new RowMultiplication<string[]>(row =>
            {
                var result = new List<string[]>();
                var id = int.Parse(row[0]);
                for (var i = 0; i < id; i++)
                {
                    result.Add(new[] { (id + i).ToString(), "Test" + (id + i) });
                }
                return result;
            });
            var dest = new MemoryDestination<string[]>();

            //Act
            source.LinkTo(multiplication);
            multiplication.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                d => Assert.True(d[0] == "1" && d[1] == "Test1"),
                d => Assert.True(d[0] == "2" && d[1] == "Test2"),
                d => Assert.True(d[0] == "3" && d[1] == "Test3"),
                d => Assert.True(d[0] == "3" && d[1] == "Test3"),
                d => Assert.True(d[0] == "4" && d[1] == "Test4"),
                d => Assert.True(d[0] == "5" && d[1] == "Test5")
            );
        }
    }
}
