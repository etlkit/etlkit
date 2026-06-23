using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowMultiplication
{
    [Collection("Transformations")]
    public class RowMultiplicationDynamicObjectTests : TransformationsTestBase
    {
        public RowMultiplicationDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void ReturningNewDynamicObject()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("RowMultiplicationSource");
            source2Columns.InsertTestData();

            var source = new DbSource(SqlConnection, "RowMultiplicationSource");
            EtlKit.DataFlow.RowMultiplication multiplication =
                new EtlKit.DataFlow.RowMultiplication(row =>
                {
                    var result = new List<ExpandoObject>();
                    dynamic r = row;
                    for (var i = 0; i <= r.Col1; i++)
                    {
                        dynamic newdynamic = new ExpandoObject();
                        newdynamic.Col3 = i * r.Col1;
                        result.Add(newdynamic);
                    }
                    return result;
                });
            var dest = new MemoryDestination();

            //Act
            source.LinkTo(multiplication);
            multiplication.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 0);
                },
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 1);
                },
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 0);
                },
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 2);
                },
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 4);
                },
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 0);
                },
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 3);
                },
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 6);
                },
                d =>
                {
                    Assert.True(((dynamic)d).Col3 == 9);
                }
            );
        }
    }
}
