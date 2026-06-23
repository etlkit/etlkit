using System.Diagnostics.CodeAnalysis;
using EtlKit.DataFlow;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace EtlKit.TestTransformations.AggregationTests
{
    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
    public class AggregationGroupingAttributeTests
    {
        [Serializable]
        public class MyRow
        {
            public int Id { get; set; }

            public string ClassName { get; set; }

            public double DetailValue { get; set; }
        }

        [Serializable]
        public class MyAggRow
        {
            [GroupColumn(nameof(MyRow.ClassName))]
            public string GroupName { get; set; }

            [AggregateColumn(nameof(MyRow.DetailValue), AggregationMethod.Sum)]
            public double AggValue { get; set; }
        }

        [Fact]
        public void WithGrouping()
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

            var agg = new Aggregation<MyRow, MyAggRow>();

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
                    Assert.Null(ar.GroupName);
                }
            );
        }

        [Serializable]
        private class MyRowNullable
        {
            public int Id { get; set; }
            public int? ClassId { get; set; }
            public double? DetailValue { get; set; }
        }

        [Serializable]
        private class MyAggRowNullable
        {
            [GroupColumn("ClassId")]
            public int? GroupId { get; set; }

            [AggregateColumn("DetailValue", AggregationMethod.Sum)]
            public double? AggValue { get; set; }
        }

        [Fact]
        public void WithNullable()
        {
            //Arrange
            var source = new MemorySource<MyRowNullable>
            {
                DataAsList = new List<MyRowNullable>
                {
                    new()
                    {
                        Id = 1,
                        ClassId = 1,
                        DetailValue = 3.5,
                    },
                    new()
                    {
                        Id = 2,
                        ClassId = 1,
                        DetailValue = 6.5,
                    },
                    new()
                    {
                        Id = 3,
                        ClassId = 2,
                        DetailValue = 1.2,
                    },
                    new()
                    {
                        Id = 4,
                        ClassId = 2,
                        DetailValue = 2.3,
                    },
                    new()
                    {
                        Id = 5,
                        ClassId = 2,
                        DetailValue = 16.5,
                    },
                    new()
                    {
                        Id = 6,
                        ClassId = 3,
                        DetailValue = 30.0,
                    },
                    new()
                    {
                        Id = 6,
                        ClassId = null,
                        DetailValue = 14.5,
                    },
                    new()
                    {
                        Id = 6,
                        ClassId = null,
                        DetailValue = 15.5,
                    },
                },
            };

            var agg = new Aggregation<MyRowNullable, MyAggRowNullable>();

            var dest = new MemoryDestination<MyAggRowNullable>();

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
                    Assert.Equal(1, ar.GroupId);
                },
                ar =>
                {
                    Assert.Equal(20, ar.AggValue);
                    Assert.Equal(2, ar.GroupId);
                },
                ar =>
                {
                    Assert.Equal(30, ar.AggValue);
                    Assert.Equal(3, ar.GroupId);
                },
                ar =>
                {
                    Assert.Equal(30, ar.AggValue);
                    Assert.Null(ar.GroupId);
                }
            );
        }

        [Serializable]
        public class MyRowMultiple
        {
            public int Id { get; set; }
            public string Class1Name { get; set; }
            public string Class2Name { get; set; }
            public int DetailValue1 { get; set; }
            public double DetailValue2 { get; set; }
        }

        [Serializable]
        public class MyAggRowMultiple
        {
            [GroupColumn("Class1Name")]
            public string Group1Name { get; set; }

            [GroupColumn("Class2Name")]
            public string Group2Name { get; set; }

            [AggregateColumn("DetailValue1", AggregationMethod.Sum)]
            public double AggValue1 { get; set; }

            [AggregateColumn("DetailValue2", AggregationMethod.Count)]
            public int AggValue2 { get; set; }
        }

        [Fact]
        public void WithMultipleGroupingAndAggregation()
        {
            //Arrange
            var source = new MemorySource<MyRowMultiple>
            {
                DataAsList = new List<MyRowMultiple>
                {
                    new()
                    {
                        Id = 1,
                        Class1Name = "Class",
                        Class2Name = "1",
                        DetailValue1 = 4,
                    },
                    new()
                    {
                        Id = 2,
                        Class1Name = "Class",
                        Class2Name = "1",
                        DetailValue1 = 6,
                    },
                    new()
                    {
                        Id = 3,
                        Class1Name = "Class2",
                        Class2Name = null,
                        DetailValue1 = 3,
                    },
                    new()
                    {
                        Id = 4,
                        Class1Name = "Class2",
                        Class2Name = null,
                        DetailValue1 = 7,
                    },
                    new()
                    {
                        Id = 5,
                        Class1Name = "Class",
                        Class2Name = "3",
                        DetailValue1 = 10,
                    },
                },
            };

            var agg = new Aggregation<MyRowMultiple, MyAggRowMultiple>();

            var dest = new MemoryDestination<MyAggRowMultiple>();

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
                    Assert.Equal(10, ar.AggValue1);
                    Assert.Equal(2, ar.AggValue2);
                    Assert.Equal("Class", ar.Group1Name);
                    Assert.Equal("1", ar.Group2Name);
                },
                ar =>
                {
                    Assert.Equal(10, ar.AggValue1);
                    Assert.Equal(2, ar.AggValue2);
                    Assert.Equal("Class2", ar.Group1Name);
                    Assert.Null(ar.Group2Name);
                },
                ar =>
                {
                    Assert.Equal(10, ar.AggValue1);
                    Assert.Equal(1, ar.AggValue2);
                    Assert.Equal("Class", ar.Group1Name);
                    Assert.Equal("3", ar.Group2Name);
                }
            );
        }
    }
}
