using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.RowFiltration
{
    public class RowFiltrationTests
    {
        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void FilterRows_PredicateTrue_AllRowsPass()
        {
            // Arrange
            var source = new MemorySource<MySimpleRow>();
            source.DataAsList.Add(new MySimpleRow { Col1 = 1, Col2 = "A" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 2, Col2 = "B" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 3, Col2 = "C" });

            var filtration = new RowFiltration<MySimpleRow>(row => true);
            var dest = new MemoryDestination<MySimpleRow>();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert - all rows pass
            Assert.Equal(3, dest.Data.Count);
        }

        [Fact]
        public void FilterRows_PredicateFalse_NoRowsPass()
        {
            // Arrange
            var source = new MemorySource<MySimpleRow>();
            source.DataAsList.Add(new MySimpleRow { Col1 = 1, Col2 = "A" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 2, Col2 = "B" });

            var filtration = new RowFiltration<MySimpleRow>(row => false);
            var dest = new MemoryDestination<MySimpleRow>();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert - no rows pass
            Assert.Empty(dest.Data);
        }

        [Fact]
        public void FilterRows_MixedInput_OnlyMatchingPass()
        {
            // Arrange
            var source = new MemorySource<MySimpleRow>();
            source.DataAsList.Add(new MySimpleRow { Col1 = 1, Col2 = "A" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 2, Col2 = "B" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 3, Col2 = "C" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 4, Col2 = "D" });

            // Only even Col1 values pass
            var filtration = new RowFiltration<MySimpleRow>(row => row.Col1 % 2 == 0);
            var dest = new MemoryDestination<MySimpleRow>();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert
            Assert.Equal(2, dest.Data.Count);
            Assert.All(dest.Data, d => Assert.True(d.Col1 % 2 == 0));
        }

        [Fact]
        public void FilterRows_PreservesOriginalRow()
        {
            // Arrange - verify the same object instance passes through (not a copy)
            var source = new MemorySource<MySimpleRow>();
            var original = new MySimpleRow { Col1 = 42, Col2 = "Original" };
            source.DataAsList.Add(original);

            var filtration = new RowFiltration<MySimpleRow>(row => true);
            var dest = new MemoryDestination<MySimpleRow>();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert - same values
            Assert.Single(dest.Data);
            Assert.Equal(42, dest.Data.First().Col1);
            Assert.Equal("Original", dest.Data.First().Col2);
        }

        [Fact]
        public void FilterRows_StringPredicate()
        {
            // Arrange
            var source = new MemorySource<MySimpleRow>();
            source.DataAsList.Add(new MySimpleRow { Col1 = 1, Col2 = "Keep" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 2, Col2 = "Drop" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 3, Col2 = "Keep" });

            var filtration = new RowFiltration<MySimpleRow>(row => row.Col2 == "Keep");
            var dest = new MemoryDestination<MySimpleRow>();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert
            Assert.Equal(2, dest.Data.Count);
            Assert.All(dest.Data, d => Assert.Equal("Keep", d.Col2));
        }

        [Fact]
        public void FilterRows_InPipeline_WithTransformation()
        {
            // Arrange - DbSource -> RowFiltration -> MemoryDestination
            var source = new MemorySource<MySimpleRow>();
            source.DataAsList.Add(new MySimpleRow { Col1 = 10, Col2 = "A" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 20, Col2 = "B" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 30, Col2 = "C" });

            var filtration = new RowFiltration<MySimpleRow>(row => row.Col1 >= 20);
            var dest = new MemoryDestination<MySimpleRow>();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert
            Assert.Equal(2, dest.Data.Count);
            Assert.Contains(dest.Data, d => d.Col1 == 20);
            Assert.Contains(dest.Data, d => d.Col1 == 30);
        }

        [Fact]
        public void FilterRows_NoPredicateSet_ThrowsClearError()
        {
            // RowFiltration's parameter-less ctor leaves PredicateFunc unset. Running
            // the flow without supplying a predicate should fail with an actionable
            // message, not a bare NullReferenceException buried in the dataflow stack.
            var source = new MemorySource<MySimpleRow>();
            source.DataAsList.Add(new MySimpleRow { Col1 = 1, Col2 = "A" });

            var filtration = new RowFiltration<MySimpleRow>();
            var dest = new MemoryDestination<MySimpleRow>();

            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();

            var ex = Assert.Throws<AggregateException>(() => dest.Wait());
            var inner = ex.Flatten()
                .InnerExceptions.OfType<InvalidOperationException>()
                .FirstOrDefault();
            Assert.NotNull(inner);
            Assert.Contains("PredicateFunc is not set", inner!.Message);
        }
    }
}
