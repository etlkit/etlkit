using EtlKit.DataFlow;

namespace EtlKit.TestFlatFileConnectors.CsvDestination
{
    public class CsvDestinationNullHandlingTests
    {
        [Serializable]
        public class MySimpleRow
        {
            [Index(0)]
            public int Col1 { get; set; }

            [Index(1)]
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
            var dest = new CsvDestination<MySimpleRow>("./IgnoreNullValues.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./IgnoreNullValues.csv"),
                File.ReadAllText("res/CsvDestination/TwoColumns.csv")
            );
        }

        [Fact]
        public void IgnoreWithStringArray()
        {
            //Arrange
            var source = new MemorySource<string[]>
            {
                DataAsList = new List<string[]>
                {
                    null,
                    new[] { "1", "Test1" },
                    null,
                    new[] { "2", "Test2" },
                    new[] { "3", "Test3" },
                    null,
                },
            };

            //Act
            var dest = new CsvDestination<string[]>("./IgnoreNullValuesStringArray.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./IgnoreNullValuesStringArray.csv"),
                File.ReadAllText("res/CsvDestination/TwoColumnsNoHeader.csv")
            );
        }
    }
}
