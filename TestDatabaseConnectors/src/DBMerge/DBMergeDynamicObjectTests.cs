using System.Dynamic;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBMerge
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbMergeDynamicObjectTests : DatabaseConnectorsTestBase
    {
        public DbMergeDynamicObjectTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleMergeWithDynamic()
        {
            //Arrange
            MemorySource source = new MemorySource();
            source.DataAsList.Add(CreateDynamicRow(1, "Test1"));
            source.DataAsList.Add(CreateDynamicRow(2, "Test2"));
            source.DataAsList.Add(CreateDynamicRow(3, "Test3"));
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                SqlConnection,
                "DBMergeDynamicDestination"
            );
            d2C.InsertTestDataSet3();

            //Act
            DbMerge dest = new DbMerge(SqlConnection, "DBMergeDynamicDestination");
            dest.MergeProperties.IdPropertyNames.Add("Col1");
            dest.MergeProperties.ComparePropertyNames.Add("Col2");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                3,
                RowCountTask.Count(
                    SqlConnection,
                    "DBMergeDynamicDestination",
                    $"{d2C.QB}Col1{d2C.QE} BETWEEN 1 AND 7 AND {d2C.QB}Col2{d2C.QE} LIKE 'Test%'"
                )
            );
            d2C.AssertTestData();
            Assert.Collection(
                dest.DeltaTable,
                row =>
                {
                    Assert.True(
                        ((dynamic)row).ChangeAction == ChangeAction.Exists
                            && ((dynamic)row).Col1 == 1
                    );
                },
                row =>
                {
                    Assert.True(
                        ((dynamic)row).ChangeAction == ChangeAction.Update
                            && ((dynamic)row).Col1 == 2
                    );
                },
                row =>
                {
                    Assert.True(
                        ((dynamic)row).ChangeAction == ChangeAction.Insert
                            && ((dynamic)row).Col1 == 3
                    );
                },
                row =>
                {
                    Assert.True(
                        ((dynamic)row).ChangeAction == ChangeAction.Delete
                            && ((dynamic)row).Col1 == 4
                    );
                },
                row =>
                {
                    Assert.True(
                        ((dynamic)row).ChangeAction == ChangeAction.Delete
                            && ((dynamic)row).Col1 == 10
                    );
                }
            );
        }

        private static dynamic CreateDynamicRow(int key, string value = "", bool delete = false)
        {
            dynamic r = new ExpandoObject();
            r.Col1 = key;
            if (!string.IsNullOrWhiteSpace(value))
                r.Col2 = value;
            if (delete)
                r.Delete = true;
            return r;
        }

        [Fact]
        public void DeltaLoadWithDeletion()
        {
            //Arrange
            MemorySource source = new MemorySource();
            source.DataAsList.Add(CreateDynamicRow(2, "Test2"));
            source.DataAsList.Add(CreateDynamicRow(3, "Test3"));
            source.DataAsList.Add(CreateDynamicRow(4, delete: true));
            source.DataAsList.Add(CreateDynamicRow(10, delete: true));
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                SqlConnection,
                "DBMergeDynamicDeltaDestination"
            );
            d2C.InsertTestDataSet3();

            //Act
            DbMerge dest = new DbMerge(SqlConnection, "DBMergeDynamicDeltaDestination")
            {
                DeltaMode = DeltaMode.Delta,
            };
            dest.MergeProperties.IdPropertyNames.Add("Col1");
            dest.MergeProperties.ComparePropertyNames.Add("Col2");
            dest.MergeProperties.DeletionProperties.Add("Delete", true);
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            d2C.AssertTestData();
            Assert.Collection(
                dest.DeltaTable,
                row =>
                {
                    dynamic r = row;
                    Assert.True(r.ChangeAction == ChangeAction.Update && r.Col1 == 2);
                },
                row =>
                {
                    dynamic r = row;
                    Assert.True(r.ChangeAction == ChangeAction.Insert && r.Col1 == 3);
                },
                row =>
                {
                    dynamic r = row;
                    Assert.True(r.ChangeAction == ChangeAction.Delete && r.Col1 == 4);
                },
                row =>
                {
                    dynamic r = row;
                    Assert.True(r.ChangeAction == ChangeAction.Delete && r.Col1 == 10);
                }
            );
        }
    }
}
