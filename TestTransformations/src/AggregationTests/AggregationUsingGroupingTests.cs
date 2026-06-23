using System.Diagnostics.CodeAnalysis;
using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.AggregationTests
{
    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
    public class AggregationUsingGroupingTests
    {
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        private class MyRow
        {
            public int Id { get; set; }
            public string ClassName { get; set; }
            public double DetailValue { get; set; }
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        private class MyAggRow
        {
            public string GroupName { get; set; }
            public double AggValue { get; set; }
        }

        [Fact]
        public void GroupingAndKeepingKey()
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
                    new()
                    {
                        Id = 2,
                        ClassName = "Class1",
                        DetailValue = 6.5,
                    },
                    new()
                    {
                        Id = 3,
                        ClassName = "Class2",
                        DetailValue = 1.2,
                    },
                    new()
                    {
                        Id = 4,
                        ClassName = "Class2",
                        DetailValue = 2.3,
                    },
                    new()
                    {
                        Id = 5,
                        ClassName = "Class2",
                        DetailValue = 16.5,
                    },
                    new()
                    {
                        Id = 6,
                        ClassName = "Class3",
                        DetailValue = 30.0,
                    },
                    new()
                    {
                        Id = 6,
                        ClassName = null,
                        DetailValue = 14.5,
                    },
                    new()
                    {
                        Id = 6,
                        ClassName = null,
                        DetailValue = 15.5,
                    },
                },
            };

            Aggregation<MyRow, MyAggRow> agg = new Aggregation<MyRow, MyAggRow>(
                (row, aggValue) => aggValue.AggValue += row.DetailValue,
                row => row.ClassName,
                (key, agg) => agg.GroupName = (string)key
            );

            var dest = new MemoryDestination<MyAggRow>();

            //Act
            source.LinkTo(agg);
            agg.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                ar =>
                {
                    Assert.Equal(10, ar.AggValue);
                    Assert.Equal("Class1", ar.GroupName);
                },
                ar =>
                {
                    Assert.Equal(20, ar.AggValue);
                    Assert.Equal("Class2", ar.GroupName);
                },
                ar =>
                {
                    Assert.Equal(30, ar.AggValue);
                    Assert.Equal("Class3", ar.GroupName);
                },
                ar =>
                {
                    Assert.Equal(30, ar.AggValue);
                    Assert.Empty(ar.GroupName);
                }
            );
        }
    }
}
