using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.CsvSource
{
    [Collection("FlatFilesToDatabase")]
    public class CsvSourceWithClassMapsTests : FlatFileConnectorsTestBase
    {
        public CsvSourceWithClassMapsTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MySimpleRow
        {
            public string Col2 { get; set; }
            public int Col1 { get; set; }
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public sealed class ModelClassMap : ClassMap<MySimpleRow>
        {
            public ModelClassMap()
            {
                Map(m => m.Col1).Name("Header1");
                Map(m => m.Col2).Name("Header2");
            }
        }

        [Fact]
        public void SimpleFlowUsingClassMap()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("CsvDestination2ColumnsClassMap");
            var dest = new DbDestination<MySimpleRow>(
                SqlConnection,
                "CsvDestination2ColumnsClassMap"
            );

            //Act
            var source = new CsvSource<MySimpleRow>("res/CsvSource/TwoColumns.csv")
            {
                ClassMapType = typeof(ModelClassMap),
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Serializable]
        private class MyExtendedRow
        {
            public string Col2 { get; set; }
            public long? Col3 { get; set; }
            public decimal Col4 { get; set; }
        }

        private sealed class ExtendedClassMap : ClassMap<MyExtendedRow>
        {
            public ExtendedClassMap()
            {
                Map(m => m.Col2).Index(0);
                Map(m => m.Col3).Index(1);
                Map(m => m.Col4).Index(2);
            }
        }

        [Fact]
        public void UsingClassMapAndNoHeader()
        {
            //Arrange
            var fourColumnsTableFixture = new FourColumnsTableFixture(
                "CsvDestination4ColumnsClassMap"
            );
            var dest = new DbDestination<MyExtendedRow>(
                SqlConnection,
                "CsvDestination4ColumnsClassMap"
            );

            //Act
            var source = new CsvSource<MyExtendedRow>("res/CsvSource/FourColumnsInvalidHeader.csv")
            {
                ClassMapType = typeof(ExtendedClassMap),
                Configuration =
                {
                    HasHeaderRecord = false,
                    ShouldSkipRecord = ShouldSkipRecordDelegate,
                },
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            fourColumnsTableFixture.AssertTestData();
        }

        private static bool ShouldSkipRecordDelegate(ShouldSkipRecordArgs args)
        {
            return args.Row.TryGetField<string>(0, out var field) && field!.Contains(".csv");
        }
    }
}
