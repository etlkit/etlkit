using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using Xunit.Abstractions;

namespace EtlKit.TestFlatFileConnectors.CsvDestination
{
    public sealed class CsvDestinationAsyncTests
    {
        private readonly ITestOutputHelper _output;

        public CsvDestinationAsyncTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private class MockDelaySource : DataFlowSource<string[]>
        {
            public override void Execute(CancellationToken cancellationToken)
            {
                Buffer.SendAsync(["1", "2"], cancellationToken).Wait(cancellationToken);
                Thread.Sleep(100);
                Buffer.SendAsync(["3", "4"], cancellationToken).Wait(cancellationToken);
                Buffer.Complete();
            }
        }

        [Theory]
        [InlineData("AsyncTestFile.csv")]
        public async Task WriteAsyncAndCheckLock(string filename)
        {
            //Arrange
            if (File.Exists(filename))
                File.Delete(filename);
            var source = new MockDelaySource();
            var dest = new CsvDestination<string[]>(filename);
            var onCompletionRun = false;
            var fileWasLockedOnCompletion = false;
            Exception exceptionFileLockCheck = null;
            dest.OnCompletion = () =>
            {
                _output.WriteLine("OnCompletion invoked!");
                onCompletionRun = true;
                exceptionFileLockCheck = Record.Exception(
                    () => fileWasLockedOnCompletion = IsFileLocked(filename)
                );
            };

            //Act
            source.LinkTo(dest);
            await source.ExecuteAsync(CancellationToken.None);
            while (!File.Exists(filename))
                await Task.Delay(10);
            await dest.Completion.ConfigureAwait(true);

            //Assert
            Assert.Multiple(
                () => Assert.Null(exceptionFileLockCheck),
                () => Assert.True(onCompletionRun, "OnCompletion action and assertion did run"),
                () =>
                    Assert.False(
                        fileWasLockedOnCompletion,
                        "StreamWriter should be disposed and file unlocked"
                    )
            );
        }

        private static bool IsFileLocked(string filename)
        {
            try
            {
                var file = new FileInfo(filename);
                using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                stream.Close();
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }
    }
}
