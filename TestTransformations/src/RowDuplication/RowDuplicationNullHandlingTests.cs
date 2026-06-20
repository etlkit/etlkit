using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.RowDuplication
{
    public class RowDuplicationNullHandlingTests
    {
        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void IgnoreWithObject()
        {
            //Arrange
            var source = new MemorySource<MySimpleRow>
            {
                DataAsList = new List<MySimpleRow>
                {
                    null,
                    new() { Col1 = 1, Col2 = "Test1" },
                    null,
                    new() { Col1 = 2, Col2 = "Test2" },
                    new() { Col1 = 3, Col2 = "Test3" },
                    null,
                },
            };

            //Act
            var duplication = new RowDuplication<MySimpleRow>();
            var dest = new MemoryDestination<MySimpleRow>();
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
    }
}
