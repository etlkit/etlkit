using System.IO;
using System.Text.RegularExpressions;
using EtlKit.ControlFlow;
using EtlKit.Logging;
using EtlKit.Logging.Database;
using EtlKit.TestNonParallel.Fixtures;

namespace EtlKit.TestNonParallel.Logging.LogTable
{
    [Collection("Logging")]
    public sealed class GetLogAsJsonTests : NonParallelTestBase, IDisposable
    {
        public GetLogAsJsonTests(LoggingDatabaseFixture fixture)
            : base(fixture)
        {
            CreateLoadProcessTableTask.Create(SqlConnection);
            CreateLogTableTask.Create(SqlConnection);
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(SqlConnection);
            EtlKit.Common.ControlFlow.ControlFlow.DefaultDbConnection = SqlConnection;
        }

        public void Dispose()
        {
            DropTableTask.Drop(SqlConnection, EtlKit.Common.ControlFlow.ControlFlow.LogTable);
            DropTableTask.Drop(
                SqlConnection,
                EtlKit.Common.ControlFlow.ControlFlow.LoadProcessTable
            );
            EtlKit.Common.ControlFlow.ControlFlow.ClearSettings();
        }

        private static string RemoveHashes(string jsonresult) =>
            Regex.Replace(jsonresult, @"""taskhash"": ""[A-Za-z0-9]*""", @"""taskHash"": """"");

        private static string RemoveDates(string jsonresult) =>
            Regex.Replace(
                jsonresult,
                @"[0-9]+-[0-9]+-[0-9]([Tt]|\w)+[0-9]+:[0-9]+:[0-9]+(?:\.[0-9]+)?",
                ""
            );

        private void RunDemoProcess()
        {
            new Sequence("Test sequence 1", RunSubSequence) { TaskType = "SUBPACKAGE" }.Execute();
            SqlTask.ExecuteNonQuery("Sql #1", "Select 1 as test");
            LogTask.Info("Info message");
        }

        private static void RunSubSequence()
        {
            Sequence.Execute(
                "Test sub sequence 1.1",
                () =>
                {
                    SqlTask.ExecuteNonQuery("Sql #2", "Select 1 as test");
                    SqlTask.ExecuteNonQuery("Sql #3", "Select 1 as test");
                    LogTask.Warn("Warn message #1");
                }
            );
            Sequence.Execute(
                "Test sub sequence 1.2",
                () =>
                {
                    SqlTask.ExecuteNonQuery("Sql #4", "Select 1 as test");
                }
            );
            Sequence.Execute(
                "Test sub sequence 1.3",
                () =>
                {
                    Sequence.Execute(
                        "Test sub sequence 2.1",
                        () =>
                        {
                            Sequence.Execute(
                                "Test sub sequence 3.1",
                                () =>
                                {
                                    SqlTask.ExecuteNonQuery("Sql #5", "Select 1 as test");
                                    SqlTask.ExecuteNonQuery("Sql #6", "Select 1 as test");
                                    LogTask.Warn("Warn message #2");
                                }
                            );
                            CustomTask.Execute("Custom #1", () => { });
                            SqlTask.ExecuteNonQuery("Sql #7", "Select 1 as test");
                        }
                    );
                    Sequence.Execute(
                        "Test sub sequence 2.2",
                        () =>
                        {
                            CustomTask.Execute("Custom #2", () => { });
                            SqlTask.ExecuteNonQuery("Sql #7", "Select 1 as test");
                        }
                    );
                    Sequence.Execute(
                        "Test sub sequence 2.3",
                        () =>
                        {
                            SqlTask.ExecuteNonQuery("Sql #8", "Select 1 as test");
                            CustomTask.Execute("Custom #2", () => { });
                            Sequence.Execute(
                                "Test sub sequence 3.3",
                                () =>
                                {
                                    SqlTask.ExecuteNonQuery("Sql #9", "Select 1 as test");
                                    SqlTask.ExecuteNonQuery("Sql #10", "Select 1 as test");
                                    LogTask.Error("Error message");
                                }
                            );
                        }
                    );
                }
            );
            CustomTask.Execute("Custom #3", () => { });
        }

        [Fact]
        public void TestGetDemoLogAsJSON()
        {
            //Arrange
            RunDemoProcess();
            string jsonresult = GetLogAsJSONTask.GetJSON();

            //Act
            jsonresult = NormalizeJsonResult(jsonresult);

            //Assert
            string expectedresult = NormalizeJsonResult(
                File.ReadAllText("res/Demo/demolog_tobe.json")
            );
            Assert.Equal(expectedresult, jsonresult);
        }

        private static string NormalizeJsonResult(string jsonresult)
        {
            return RemoveLineEndings(RemoveHashes(RemoveDates(jsonresult.ToLower().Trim())));
        }

        private static string RemoveLineEndings(string originalJson)
        {
            return Regex.Replace(originalJson, "[\n\r]", "");
        }
    }
}
