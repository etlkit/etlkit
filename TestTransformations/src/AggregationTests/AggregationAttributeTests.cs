using System.Diagnostics.CodeAnalysis;
using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.AggregationTests
{
    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
    public class AggregationAttributeTests
    {
        [Serializable]
        public class MySumRow
        {
            [AggregateColumn("DetailValue", AggregationMethod.Sum)]
            public double AggValue { get; set; }
        }

        [Serializable]
        public class MySumRowNullable
        {
            [AggregateColumn("DetailValue", AggregationMethod.Sum)]
            public double? AggValue { get; set; }
        }

        [Serializable]
        public class MyMaxRow
        {
            [AggregateColumn("DetailValue", AggregationMethod.Max)]
            public float AggValue { get; set; }
        }

        [Serializable]
        public class MyMinRow
        {
            [AggregateColumn("DetailValue", AggregationMethod.Min)]
            public long? AggValue { get; set; }
        }

        [Serializable]
        public class MyCountRow
        {
            [AggregateColumn("DetailValue", AggregationMethod.Count)]
            public uint AggValue { get; set; }
        }

        [Serializable]
        public class MyInputRow
        {
            public int Id { get; set; }
            public double? DetailValue { get; set; }
        }

        [Fact]
        public void AggregateSum()
        {
            //Arrange
            var sourceData = new List<MyInputRow>
            {
                new() { Id = 1, DetailValue = 3.5 },
                new() { Id = 2, DetailValue = 4.5 },
                new() { Id = 3, DetailValue = 2.0 },
            };
            MemoryDestination<MySumRow> dest = CreateFlow<MySumRow>(sourceData);

            //Assert
            Assert.Single(dest.Data, ar => Math.Abs(ar.AggValue - 10.0F) < double.Epsilon);
        }

        [Fact]
        public void AggregateSumWithNullable()
        {
            //Arrange
            var sourceData = new List<MyInputRow>
            {
                new() { Id = 1, DetailValue = 3.5 },
                new() { Id = 2, DetailValue = 4.5 },
                new() { Id = 3, DetailValue = 2.0 },
                new() { Id = 4, DetailValue = null },
            };
            MemoryDestination<MySumRowNullable> dest = CreateFlow<MySumRowNullable>(sourceData);

            //Assert
            Assert.Single(dest.Data, ar => ar.AggValue.HasValue);
            Assert.Single(dest.Data, ar => Math.Abs(ar.AggValue!.Value - 10.0F) < double.Epsilon);
        }

        [Fact]
        public void AggregateMax()
        {
            //Arrange
            var sourceData = new List<MyInputRow>
            {
                new() { DetailValue = 3.5F },
                new() { DetailValue = 4.5F },
                new() { DetailValue = 2.0F },
            };
            MemoryDestination<MyMaxRow> dest = CreateFlow<MyMaxRow>(sourceData);

            //Assert
            Assert.Single(dest.Data, ar => Math.Abs(ar.AggValue - 4.5F) < float.Epsilon);
        }

        [Fact]
        public void AggregateMin()
        {
            //Arrange
            var sourceData = new List<MyInputRow>
            {
                new() { DetailValue = 3 },
                new() { DetailValue = 4 },
                new() { DetailValue = 2 },
            };
            MemoryDestination<MyMinRow> dest = CreateFlow<MyMinRow>(sourceData);

            //Assert
            Assert.Collection(dest.Data, ar => Assert.True(ar.AggValue == 2));
        }

        [Fact]
        public void AggregateCount()
        {
            //Arrange
            var sourceData = new List<MyInputRow>
            {
                new() { DetailValue = 5 },
                new() { DetailValue = 7 },
                new() { DetailValue = 8 },
            };
            MemoryDestination<MyCountRow> dest = CreateFlow<MyCountRow>(sourceData);

            //Assert
            Assert.Collection(dest.Data, ar => Assert.True(ar.AggValue == 3));
        }

        private static MemoryDestination<T> CreateFlow<T>(List<MyInputRow> sourceData)
        {
            var source = new MemorySource<MyInputRow> { DataAsList = sourceData };

            var agg = new Aggregation<MyInputRow, T>();

            var dest = new MemoryDestination<T>();

            //Act
            source.LinkTo(agg);
            agg.LinkTo(dest);
            source.Execute();
            dest.Wait();
            return dest;
        }
    }
}
