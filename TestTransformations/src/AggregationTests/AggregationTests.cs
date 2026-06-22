using System.Diagnostics.CodeAnalysis;
using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.AggregationTests
{
    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
    public class AggregationTests
    {
        [Serializable]
        public class MyRow
        {
            public int Id { get; set; }
            public double DetailValue { get; set; }
        }

        public class MyAggRow
        {
            public double AggValue { get; set; }
        }

        [Fact]
        public void AggregateSimple()
        {
            //Arrange
            var source = new MemorySource<MyRow>
            {
                DataAsList = new List<MyRow>
                {
                    new() { Id = 1, DetailValue = 3.5 },
                    new() { Id = 2, DetailValue = 4.5 },
                    new() { Id = 3, DetailValue = 2.0 },
                },
            };

            var agg = new Aggregation<MyRow, MyAggRow>(
                (row, aggRow) => aggRow.AggValue += row.DetailValue
            );

            var dest = new MemoryDestination<MyAggRow>();

            //Act
            source.LinkTo(agg);
            agg.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Single(dest.Data, ar => Math.Abs(ar.AggValue - 10) < double.Epsilon);
        }

        [Serializable]
        private class MyRowNullable
        {
            public int Id { get; set; }
            public double? DetailValue { get; set; }
        }

        [Serializable]
        private class MyAggRowNullable
        {
            public double? AggValue { get; set; } = 0;
        }

        [Fact]
        public void AggregateWithNull()
        {
            //Arrange
            var source = new MemorySource<MyRowNullable>
            {
                DataAsList = new List<MyRowNullable>
                {
                    new() { Id = 1, DetailValue = 3.5 },
                    new() { Id = 0, DetailValue = null },
                    new() { Id = 2, DetailValue = 4.5 },
                    new() { Id = 3, DetailValue = 2.0 },
                    new() { Id = 4, DetailValue = null },
                },
            };

            var agg = new Aggregation<MyRowNullable, MyAggRowNullable>(
                (row, aggRow) => aggRow.AggValue += row.DetailValue ?? 0
            );

            var dest = new MemoryDestination<MyAggRowNullable>();

            //Act
            source.LinkTo(agg);
            agg.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Single(dest.Data, ar => ar.AggValue.HasValue);
            Assert.Single(dest.Data, ar => Math.Abs(ar.AggValue!.Value - 10.0) < double.Epsilon);
        }
    }
}
