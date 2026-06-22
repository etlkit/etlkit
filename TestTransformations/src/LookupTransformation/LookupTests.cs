using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.LookupTransformation
{
    [Collection("Transformations")]
    public class LookupTests : TransformationsTestBase
    {
        public LookupTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MyLookupRow
        {
            [ColumnMap("Col1")]
            public long Key { get; set; }

            [ColumnMap("Col3")]
            public long? LookupValue1 { get; set; }

            [ColumnMap("Col4")]
            public decimal LookupValue2 { get; set; }
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MyDataRow
        {
            public long Col1 { get; set; }
            public string Col2 { get; set; }
            public long? Col3 { get; set; }
            public decimal Col4 { get; set; }
        }

        [Theory, MemberData(nameof(Connections))]
        public void InputTypeSameAsOutput(IConnectionManager connection)
        {
            //Arrange
            var source4Columns = new FourColumnsTableFixture(connection, "SourceLookupSameType");
            source4Columns.InsertTestData();
            var dest4Columns = new FourColumnsTableFixture(connection, "DestinationLookupSameType");
            var lookup4Columns = new FourColumnsTableFixture(connection, "LookupSameType");
            lookup4Columns.InsertTestData();

            var source = new DbSource<MyDataRow>(connection, "SourceLookupSameType");
            var lookupSource = new DbSource<MyLookupRow>(connection, "LookupSameType");

            var lookup = new LookupTransformation<MyDataRow, MyLookupRow>();
            lookup.TransformationFunc = row =>
            {
                row.Col3 = lookup
                    .LookupData.Where(ld => ld.Key == row.Col1)
                    .Select(ld => ld.LookupValue1)
                    .FirstOrDefault();
                row.Col4 = lookup
                    .LookupData.Where(ld => ld.Key == row.Col1)
                    .Select(ld => ld.LookupValue2)
                    .FirstOrDefault();
                return row;
            };
            lookup.Source = lookupSource;
            var dest = new DbDestination<MyDataRow>(connection, "DestinationLookupSameType");
            source.LinkTo(lookup);
            lookup.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest4Columns.AssertTestData();
        }
    }
}
