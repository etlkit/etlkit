using EtlKit.Common;
using EtlKit.Common.DataFlow;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestPerformance.Fixtures;
using EtlKit.TestPerformance.Helper;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestPerformance
{
    [Collection("Performance")]
    public class CsvSourceIntoDBTests : PerformanceTestBase
    {
        private readonly ITestOutputHelper _output;

        public CsvSourceIntoDBTests(ITestOutputHelper output, PerformanceDatabaseFixture fixture)
            : base(fixture)
        {
            _output = output;
        }

        private static void ReCreateDestinationTable(
            IConnectionManager connection,
            string tableName
        )
        {
            var tableDef = new TableDefinition(tableName, BigDataCsvSource.DestTableCols);
            DropTableTask.DropIfExists(connection, tableName);
            tableDef.CreateTable(connection);
        }

        /*
         * X Rows with 1027 bytes per Row (1020 bytes data + 7 bytes for sql server)
         */
        [Theory]
        [Trait("Category", "Performance")]
        [
            MemberData(nameof(SqlConnection), 100000, 1000, 0.5),
            MemberData(nameof(MySqlConnection), 100000, 1000, 0.5),
            MemberData(nameof(PostgresConnection), 100000, 1000, 0.5),
            MemberData(nameof(SQLiteConnection), 100000, 1000, 0.5)
        ]
        public void GenericAndDynamicAreNotTooDifferent(
            IConnectionManager connection,
            int numberOfRows,
            int batchSize,
            double deviation
        )
        {
            //Arrange
            BigDataCsvSource.CreateCsvFileIfNeeded(numberOfRows);
            ReCreateDestinationTable(connection, "CsvDestinationNonGenericEtlKit");
            ReCreateDestinationTable(connection, "CsvDestinationBulkInsert");
            ReCreateDestinationTable(connection, "CsvDestinationGenericEtlKit");

            var sourceNonGeneric = new CsvSource(
                BigDataCsvSource.GetCompleteFilePath(numberOfRows)
            );
            var destNonGeneric = new DbDestination(
                connection,
                "CsvDestinationNonGenericEtlKit",
                batchSize
            );
            var sourceGeneric = new CsvSource<CsvData>(
                BigDataCsvSource.GetCompleteFilePath(numberOfRows)
            );
            var destGeneric = new DbDestination<CsvData>(
                connection,
                "CsvDestinationGenericEtlKit",
                batchSize
            );

            //Act
            var timeElapsedEtlKitNonGeneric = GetEtlKitTime(
                numberOfRows,
                sourceNonGeneric,
                destNonGeneric
            );
            var timeElapsedEtlKitGeneric = GetEtlKitTime(numberOfRows, sourceGeneric, destGeneric);

            //Assert
            Assert.Equal(
                numberOfRows,
                RowCountTask.Count(connection, "CsvDestinationNonGenericEtlKit")
            );
            Assert.Equal(
                numberOfRows,
                RowCountTask.Count(connection, "CsvDestinationGenericEtlKit")
            );

            var timeDifference = Math.Abs(
                timeElapsedEtlKitGeneric.TotalMilliseconds
                    - timeElapsedEtlKitNonGeneric.TotalMilliseconds
            );

            var diffPercentage =
                timeDifference
                / Math.Min(
                    timeElapsedEtlKitGeneric.TotalMilliseconds,
                    timeElapsedEtlKitNonGeneric.TotalMilliseconds
                );

            Assert.InRange(diffPercentage, 0.0, deviation);
        }

        private TimeSpan GetEtlKitTime<T>(
            int numberOfRows,
            CsvSource<T> source,
            DbDestination<T> dest
        )
        {
            source.LinkTo(dest);
            var timeElapsedEtlKit = BigDataHelper.LogExecutionTime(
                $"Copying Csv into DB (non generic) with {numberOfRows} rows of data using EtlKit",
                () =>
                {
                    source.Execute();
                    dest.Wait();
                }
            );
            if (typeof(T) == typeof(string[]))
                _output.WriteLine(
                    "Elapsed "
                        + timeElapsedEtlKit.TotalSeconds
                        + " seconds for EtlKit (Non generic)."
                );
            else
                _output.WriteLine(
                    "Elapsed " + timeElapsedEtlKit.TotalSeconds + " seconds for EtlKit (Generic)."
                );
            return timeElapsedEtlKit;
        }

        [Trait("Category", "Performance")]
        [Fact()]
        public void CheckMemoryUsage()
        {
            SqlConnectionManager connection = SqlConnectionManager;
            int numberOfRows = 1000000;
            int batchSize = 1000;
            double deviation = 1.3;

            //Arrange
            BigDataCsvSource.CreateCsvFileIfNeeded(numberOfRows);
            ReCreateDestinationTable(connection, "CsvDestinationWithTransformation");

            var sourceExpando = new CsvSource(BigDataCsvSource.GetCompleteFilePath(numberOfRows));
            var trans = new RowTransformation<ExpandoObject, CsvData>(row =>
            {
                dynamic r = row;
                return new CsvData
                {
                    Col1 = r.Col1,
                    Col2 = r.Col2,
                    Col3 = r.Col3,
                    Col4 = r.Col4,
                };
            });
            var destGeneric = new DbDestination<CsvData>(
                connection,
                "CsvDestinationWithTransformation",
                batchSize
            );
            sourceExpando.LinkTo(trans);
            trans.LinkTo(destGeneric);

            //Act
            long memAfter;
            long memBefore = 0;
            var startCheck = true;
            var count = 1;
            destGeneric.AfterBatchWrite = _ =>
            {
                if (count++ % 50 != 0)
                    return;

                using var proc = Process.GetCurrentProcess();

                memAfter = proc.WorkingSet64;
                if (startCheck)
                {
                    memBefore = memAfter;
                    startCheck = false;
                }
                Assert.InRange(memAfter, 0, memBefore + memBefore * deviation);
            };

            var timeElapsedEtlKit = BigDataHelper.LogExecutionTime(
                $"Copying Csv into DB (non generic) with {numberOfRows} rows of data using EtlKit",
                () =>
                {
                    sourceExpando.Execute();
                    destGeneric.Wait();
                }
            );
            _output.WriteLine(
                "Elapsed "
                    + timeElapsedEtlKit.TotalSeconds
                    + " seconds for EtlKit (Expando to object transformation)."
            );

            //Assert
            Assert.Equal(
                numberOfRows,
                RowCountTask.Count(connection, "CsvDestinationWithTransformation")
            );
            //10.000.000 rows, batch size 10.000: ~8 min
            //10.000.000 rows, batch size  1.000: ~10 min 10 sec
        }

        private static IEnumerable<CsvData> GenerateWithYield(int numberOfRows)
        {
            var i = 0;
            while (i < numberOfRows)
            {
                i++;
                yield return new CsvData
                {
                    Col1 = HashHelper.RandomString(255),
                    Col2 = HashHelper.RandomString(255),
                    Col3 = HashHelper.RandomString(255),
                    Col4 = HashHelper.RandomString(255),
                };
            }
        }

        [Theory]
        [Trait("Category", "Performance")]
        [MemberData(nameof(SqlConnection), 1000000, 1000, 1.0)]
#pragma warning disable xUnit1026
        public void CheckMemoryUsageDbDestination(
            IConnectionManager connection,
            int numberOfRows,
            int batchSize,
            double _
        )
#pragma warning restore xUnit1026
        {
            //Arrange
            ReCreateDestinationTable(connection, "MemoryDestination");

            var source = new MemorySource<CsvData> { Data = GenerateWithYield(numberOfRows) };
            var dest = new DbDestination<CsvData>(connection, "MemoryDestination", batchSize);

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(numberOfRows, RowCountTask.Count(connection, "MemoryDestination"));
        }
    }
}
