using EtlKit.DataFlow;
using EtlKit.TestPerformance.ETLBoxTests.Performance.Helper;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestPerformance.ETLBoxTests.Performance
{
    public class MemoryDestinationTests
    {
        private readonly ITestOutputHelper _output;

        public MemoryDestinationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /*
         * X Rows with 1027 bytes per Row (1020 bytes data + 7 bytes for sql server)
         */
        [Theory, InlineData(100000, 0.5)]
        public void CSVIntoMemDest(int numberOfRows, double deviation)
        {
            //Arrange
            BigDataCsvSource.CreateCsvFileIfNeeded(numberOfRows);

            var sourceNonGeneric = new CsvSource(
                BigDataCsvSource.GetCompleteFilePath(numberOfRows)
            );
            var destNonGeneric = new MemoryDestination();
            var sourceGeneric = new CsvSource<CsvData>(
                BigDataCsvSource.GetCompleteFilePath(numberOfRows)
            );
            var destGeneric = new MemoryDestination<CsvData>();
            var sourceDynamic = new CsvSource<ExpandoObject>(
                BigDataCsvSource.GetCompleteFilePath(numberOfRows)
            );
            var destDynamic = new MemoryDestination<ExpandoObject>();

            sourceNonGeneric.ReleaseGCPressureRowCount = 500;
            sourceGeneric.ReleaseGCPressureRowCount = 500;
            sourceDynamic.ReleaseGCPressureRowCount = 500;
            //Act
            var teNonGeneric = GetETLBoxTime(numberOfRows, sourceNonGeneric, destNonGeneric);
            var teGeneric = GetETLBoxTime(numberOfRows, sourceGeneric, destGeneric);
            var teDynamic = GetETLBoxTime(numberOfRows, sourceDynamic, destDynamic);

            //Assert
            Assert.Equal(numberOfRows, destNonGeneric.Data.Count);
            Assert.Equal(numberOfRows, destGeneric.Data.Count);
            Assert.Equal(numberOfRows, destDynamic.Data.Count);
            Assert.True(
                new[]
                {
                    teGeneric.TotalMilliseconds,
                    teNonGeneric.TotalMilliseconds,
                    teDynamic.TotalMilliseconds,
                }.Max()
                    < new[]
                    {
                        teGeneric.TotalMilliseconds,
                        teNonGeneric.TotalMilliseconds,
                        teDynamic.TotalMilliseconds,
                    }.Max() * (deviation + 1)
            );
        }

        private TimeSpan GetETLBoxTime<T>(
            int numberOfRows,
            CsvSource<T> source,
            MemoryDestination<T> dest
        )
        {
            source.LinkTo(dest);
            var timeElapsedETLBox = BigDataHelper.LogExecutionTime(
                $"Copying Csv into Memory Destination with {numberOfRows} rows of data using ETLBox",
                () =>
                {
                    source.Execute();
                    dest.Wait();
                }
            );
            if (typeof(T) == typeof(string[]))
                _output.WriteLine(
                    "Elapsed "
                        + timeElapsedETLBox.TotalSeconds
                        + " seconds for ETLBox (Non generic)."
                );
            else
                _output.WriteLine(
                    "Elapsed " + timeElapsedETLBox.TotalSeconds + " seconds for ETLBox (Generic)."
                );
            return timeElapsedETLBox;
        }
    }
}
