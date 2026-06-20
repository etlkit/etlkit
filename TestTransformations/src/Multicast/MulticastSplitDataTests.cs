using System.Diagnostics.CodeAnalysis;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.Multicast
{
    [Collection("Transformations")]
    public class MulticastSplitDataTests : TransformationsTestBase
    {
        public MulticastSplitDataTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Serializable]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        internal class CSVPoco
        {
            public int CSVCol1 { get; set; }
            public string CSVCol2 { get; set; }
            public long? CSVCol3 { get; set; }
            public decimal CSVCol4 { get; set; }
        }

        [Serializable]
        internal class Entity1
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Serializable]
        internal class Entity2
        {
            public string Col2 { get; set; }
            public long? Col3 { get; set; }
            public decimal Col4 { get; set; }
        }

        [Fact(Skip = "TODO: Fix in under Gitlab/Kubernetes runner")]
        public void SplitCsvSourceIn2Tables()
        {
            //Arrange
            var dest1Table = new TwoColumnsTableFixture("SplitDataDestination1");
            var dest2Table = new FourColumnsTableFixture("SplitDataDestination2");

            var source = new CsvSource<CSVPoco>("res/Multicast/CsvSourceToSplit.csv")
            {
                Configuration = { Delimiter = ";" },
            };

            var multicast = new Multicast<CSVPoco>();

            var row1 = new RowTransformation<CSVPoco, Entity1>(input => new Entity1
            {
                Col1 = input.CSVCol1,
                Col2 = input.CSVCol2,
            });
            var row2 = new RowTransformation<CSVPoco, Entity2>(input => new Entity2
            {
                Col2 = input.CSVCol2,
                Col3 = input.CSVCol3,
                Col4 = input.CSVCol4,
            });

            var destination1 = new DbDestination<Entity1>(SqlConnection, "SplitDataDestination1");
            var destination2 = new DbDestination<Entity2>(SqlConnection, "SplitDataDestination2");

            //Act
            source.LinkTo(multicast);
            multicast.LinkTo(row1);
            multicast.LinkTo(row2);

            row1.LinkTo(destination1);
            row2.LinkTo(destination2);

            source.Execute();
            destination1.Wait();
            destination2.Wait();

            //Assert
            dest1Table.AssertTestData();
            dest2Table.AssertTestData();
        }
    }
}
