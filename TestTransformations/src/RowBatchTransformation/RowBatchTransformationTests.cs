using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowBatchTransformation;

[Collection("Transformations")]
public class RowBatchTransformationTests : TransformationsTestBase
{
    public RowBatchTransformationTests(TransformationsDatabaseFixture fixture)
        : base(fixture) { }

    private sealed class IntBatchTrans : RowBatchTransformation<int, int>
    {
        public IntBatchTrans(Func<int[], int[]> impl)
        {
            BatchTransform = impl;
        }
    }

    [Fact]
    public void ShouldProcessBatchesAndRemainderAndKeepOrder()
    {
        // Arrange: 10 numbers, batch size 4 → batches: [0..3],[4..7],[8..9]
        var data = Enumerable.Range(0, 10).ToArray();
        var source = new MemorySource<int>(data);
        var dest = new MemoryDestination<int>();

        var trans = new IntBatchTrans(batch => batch.Select(x => x * 2).ToArray())
        {
            BatchSize = 4,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: same number of elements and order preserved
        Assert.Equal(data.Length, dest.Data.Count);
        Assert.Equal(data.Select(x => x * 2), dest.Data);
    }

    [Fact]
    public void ErrorHandling_WithErrorLink_ShouldRouteErrorsAndContinue()
    {
        // Arrange: throw an exception in the second batch
        var data = Enumerable.Range(0, 8).ToArray(); // two full batches of 4
        var source = new MemorySource<int>(data);
        var dest = new MemoryDestination<int>();
        var errorDest = new MemoryDestination<EtlKitError>();

        int batchCall = 0;
        var trans = new IntBatchTrans(batch =>
        {
            batchCall++;
            if (batchCall == 2)
                throw new InvalidOperationException("boom");
            return batch.Select(x => x + 1).ToArray();
        })
        {
            BatchSize = 4,
        };

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        trans.LinkErrorTo(errorDest);
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        // Assert: first batch processed (4 records), second sent 4 errors, no results for the second batch
        Assert.Equal(4, dest.Data.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, dest.Data);
        Assert.Equal(4, errorDest.Data.Count);
    }

    [Fact]
    public void ErrorHandling_WithoutErrorLink_ShouldThrow()
    {
        // Arrange
        var data = Enumerable.Range(0, 5).ToArray();
        var source = new MemorySource<int>(data);
        var dest = new MemoryDestination<int>();

        var trans = new IntBatchTrans(_ => throw new Exception("err")) { BatchSize = 3 };

        // Act + Assert
        source.LinkTo(trans);
        trans.LinkTo(dest);
        Assert.Throws<AggregateException>(() =>
        {
            source.Execute();
            dest.Wait();
        });
    }

    [Fact]
    public void BoundedCapacity_Set_ShouldReinitializeAndProcess()
    {
        // Arrange: verify that setting BoundedCapacity triggers reinitialization
        // of internal blocks and the pipeline still works correctly.
        var data = Enumerable.Range(1, 20).ToArray();
        var source = new MemorySource<int>(data);
        var dest = new MemoryDestination<int>();

        var trans = new IntBatchTrans(batch => batch.Select(x => x * 10).ToArray())
        {
            BatchSize = 5,
        };

        // Set buffer capacity limit. TPL Dataflow requirement: BatchSize <= BoundedCapacity.
        // Ensure the setter triggers reinitialization without errors and the pipeline works correctly.
        trans.BoundedCapacity = 5; // equals BatchSize

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: all elements processed correctly
        Assert.Equal(data.Length, dest.Data.Count);
        Assert.Equal(data.Select(x => x * 10), dest.Data);
    }

    [Fact]
    public void BeforeBatchTransform_ShouldPreprocessData()
    {
        // Arrange: verify that BeforeBatchTransform is invoked and modifies the input batch
        var data = new[] { 1, 2, 3, 4, 5 };
        var source = new MemorySource<int>(data);
        var dest = new MemoryDestination<int>();

        // BatchTransform returns input as-is; all modification is in BeforeBatchTransform
        var trans = new IntBatchTrans(batch => batch) { BatchSize = 2 };
        trans.BeforeBatchTransform = batch => batch.Select(x => x + 100).ToArray();

        // Act
        source.LinkTo(trans);
        trans.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: output elements are shifted by +100
        Assert.Equal(data.Length, dest.Data.Count);
        Assert.Equal(data.Select(x => x + 100), dest.Data);
    }

    [Fact]
    public void BatchTransform_ReturnsNull_ShouldThrow()
    {
        // Arrange: BatchTransform returns null — should throw InvalidOperationException
        var data = new[] { 1, 2, 3 };
        var source = new MemorySource<int>(data);
        var dest = new MemoryDestination<int>();

        var trans = new IntBatchTrans(_ => null!) { BatchSize = 3 };

        // Act + Assert
        source.LinkTo(trans);
        trans.LinkTo(dest);
        var ex = Assert.Throws<AggregateException>(() =>
        {
            source.Execute();
            dest.Wait();
        });

        // Ensure inner cause is InvalidOperationException with expected message
        Assert.Contains(
            ex.InnerExceptions,
            e => e is InvalidOperationException && e.Message.Contains("BatchTransform вернул null.")
        );
    }
}
