using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.AggregationTests
{
    public class AggregationExceptionTests
    {
        public class MyRow
        {
            public int Id { get; set; }
            public string ClassName { get; set; }
            public double DetailValue { get; set; }
        }

        public class MyAggRow
        {
            public string GroupName { get; set; }
            public double AggValue { get; set; }
        }

        [Fact]
        public void ExceptionInAggregationFunction()
        {
            //Arrange
            var source = new MemorySource<MyRow>
            {
                DataAsList = new List<MyRow>
                {
                    new() { Id = 1, DetailValue = 3.5 },
                },
            };

            //Act
            var agg = new Aggregation<MyRow, MyAggRow>((_, _) => throw new Exception("Test"));

            var dest = new MemoryDestination<MyAggRow>();

            //Assert
            source.LinkTo(agg);
            agg.LinkTo(dest);

            Assert.Throws<AggregateException>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }

        [Fact]
        public void ExceptionInStoreKeyFunction()
        {
            //Arrange
            var source = new MemorySource<MyRow>
            {
                DataAsList = new List<MyRow>
                {
                    new()
                    {
                        Id = 1,
                        ClassName = "Class1",
                        DetailValue = 3.5,
                    },
                },
            };

            //Act
            var agg = new Aggregation<MyRow, MyAggRow>(
                (row, aggValue) => aggValue.AggValue += row.DetailValue,
                row => row.ClassName,
                (_, _) => throw new Exception("Test")
            );

            var dest = new MemoryDestination<MyAggRow>();

            source.LinkTo(agg);
            agg.LinkTo(dest);

            //Assert
            Assert.Throws<AggregateException>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }
    }
}
