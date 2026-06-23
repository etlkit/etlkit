using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.LookupTransformation
{
    [Collection("Transformations")]
    public sealed class LookupDynamicObjectTests : TransformationsTestBase, IDisposable
    {
        private readonly CultureInfo _culture;

        public LookupDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture)
        {
            _culture = CultureInfo.CurrentCulture;
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void SimpleLookupWithDynamicObject(IConnectionManager connection)
        {
            //Arrange
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var source2Columns = new TwoColumnsTableFixture(
                connection,
                "SourceLookupDynamicObject"
            );
            source2Columns.InsertTestData();
            var dest4Columns = new FourColumnsTableFixture(
                connection,
                "DestinationLookupDynamicObject",
                -1
            );

            var source = new DbSource<ExpandoObject>(connection, "SourceLookupDynamicObject");
            var dest = new DbDestination<ExpandoObject>(
                connection,
                "DestinationLookupDynamicObject"
            );

            //Act
            var lookupList = new List<ExpandoObject>();

            var lookupSource = new CsvSource<ExpandoObject>("res/Lookup/LookupSource.csv");

            var lookup = new LookupTransformation<ExpandoObject, ExpandoObject>(
                lookupSource,
                row =>
                {
                    dynamic r = row;
                    r.Col3 = lookupList
                        .Where(lkupRow =>
                        {
                            dynamic lk = lkupRow;
                            return int.Parse(lk.Key) == r.Col1;
                        })
                        .Select(lkupRow =>
                        {
                            dynamic lk = lkupRow;
                            return lk.Column3 == string.Empty ? null : long.Parse(lk.Column3);
                        })
                        .FirstOrDefault();
                    r.Col4 = lookupList
                        .Where(lkupRow =>
                        {
                            dynamic lk = lkupRow;
                            return int.Parse(lk.Key) == r.Col1;
                        })
                        .Select(lkupRow =>
                        {
                            dynamic lk = lkupRow;
                            return double.Parse(lk.Column4);
                        })
                        .FirstOrDefault();
                    return row;
                },
                lookupList
            );

            source.LinkTo(lookup);
            lookup.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest4Columns.AssertTestData();
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
        }
    }
}
