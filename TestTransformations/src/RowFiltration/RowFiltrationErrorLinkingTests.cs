using EtlKit.DataFlow;
using EtlKit.Primitives;

namespace EtlKit.TestTransformations.RowFiltration
{
    public class RowFiltrationErrorLinkingTests
    {
        [Serializable]
        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void ThrowExceptionInPredicate_WithErrorHandler()
        {
            // Arrange
            var source = new MemorySource<MySimpleRow>();
            source.DataAsList.Add(new MySimpleRow { Col1 = 1, Col2 = "A" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 2, Col2 = null }); // will throw
            source.DataAsList.Add(new MySimpleRow { Col1 = 3, Col2 = "C" });

            var filtration = new RowFiltration<MySimpleRow>(row =>
            {
                if (row.Col2 == null)
                    throw new Exception("Null Col2!");
                return row.Col1 > 0;
            });
            var dest = new MemoryDestination<MySimpleRow>();
            var errorDest = new MemoryDestination<EtlKitError>();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            filtration.LinkErrorTo(errorDest);
            source.Execute();
            dest.Wait();
            errorDest.Wait();

            // Assert - row 2 goes to error, rows 1 and 3 pass
            Assert.Equal(2, dest.Data.Count);
            Assert.Single(errorDest.Data);
            Assert.Contains("Null Col2!", errorDest.Data.First().ErrorText);
        }

        [Fact]
        public void ThrowExceptionInPredicate_WithoutErrorHandler()
        {
            // Arrange
            var source = new MemorySource<MySimpleRow>();
            source.DataAsList.Add(new MySimpleRow { Col1 = 1, Col2 = "A" });
            source.DataAsList.Add(new MySimpleRow { Col1 = 2, Col2 = null });

            var filtration = new RowFiltration<MySimpleRow>(row =>
            {
                if (row.Col2 == null)
                    throw new Exception("Null Col2!");
                return true;
            });
            var dest = new MemoryDestination<MySimpleRow>();

            // Act & Assert - exception propagates
            source.LinkTo(filtration);
            filtration.LinkTo(dest);

            Assert.Throws<AggregateException>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }
    }
}
