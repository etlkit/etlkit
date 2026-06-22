using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.CrossJoinTests
{
    public class CrossJoinTests
    {
        [Fact]
        public void CrossJoinStringWithInt()
        {
            //Arrange
            var source1 = new MemorySource<string>
            {
                DataAsList = new List<string> { "A", "B" },
            };
            var source2 = new MemorySource<int>
            {
                DataAsList = new List<int> { 1, 2, 3 },
            };
            var crossJoin = new CrossJoin<string, int, string>((data1, data2) => data1 + data2);
            var dest = new MemoryDestination<string>();

            //Act
            source1.LinkTo(crossJoin.InMemoryTarget);
            source2.LinkTo(crossJoin.PassingTarget);
            crossJoin.LinkTo(dest);
            source1.Execute();
            source2.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(6, dest.Data.Count);
            Assert.Collection(
                dest.Data,
                s => Assert.Equal("A1", s),
                s => Assert.Equal("B1", s),
                s => Assert.Equal("A2", s),
                s => Assert.Equal("B2", s),
                s => Assert.Equal("A3", s),
                s => Assert.Equal("B3", s)
            );
        }
    }
}
