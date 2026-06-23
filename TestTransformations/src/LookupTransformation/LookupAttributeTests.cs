using EtlKit.Common;
using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.LookupTransformation
{
    public class LookupAttributeTests
    {
        [Serializable]
        public class LookupData
        {
            [MatchColumn("LookupId")]
            public int Id { get; set; }

            [RetrieveColumn("LookupValue")]
            public string Value { get; set; }
        }

        [Serializable]
        public class InputDataRow
        {
            public int LookupId { get; set; }
            public string LookupValue { get; set; }
        }

        [Fact]
        public void OneMatchOneRetrieveColumn()
        {
            //Arrange
            var source = new MemorySource<InputDataRow>();
            source.DataAsList.Add(new InputDataRow { LookupId = 1 });
            source.DataAsList.Add(new InputDataRow { LookupId = 2 });
            source.DataAsList.Add(new InputDataRow { LookupId = 4 });
            source.DataAsList.Add(new InputDataRow { LookupId = 3 });
            var lookupSource = new MemorySource<LookupData>();
            lookupSource.DataAsList.Add(new LookupData { Id = 1, Value = "Test1" });
            lookupSource.DataAsList.Add(new LookupData { Id = 2, Value = "Test2" });
            lookupSource.DataAsList.Add(new LookupData { Id = 3, Value = "Test3" });

            var lookup = new LookupTransformation<InputDataRow, LookupData>
            {
                Source = lookupSource,
            };
            var dest = new MemoryDestination<InputDataRow>();
            source.LinkTo(lookup);
            lookup.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                r => Assert.True(r.LookupId == 1 && r.LookupValue == "Test1"),
                r => Assert.True(r.LookupId == 2 && r.LookupValue == "Test2"),
                r => Assert.True(r.LookupId == 4 && r.LookupValue == null),
                r => Assert.True(r.LookupId == 3 && r.LookupValue == "Test3")
            );
        }

        [Serializable]
        public class LookupDataMultiple
        {
            [MatchColumn("LookupId1")]
            public int Id1 { get; set; }

            [MatchColumn("LookupId2")]
            public string Id2 { get; set; }

            [RetrieveColumn("LookupValue1")]
            public string Value1 { get; set; }

            [RetrieveColumn("LookupValue2")]
            public int Value2 { get; set; }
        }

        [Serializable]
        public class InputDataMultiple
        {
            public int LookupId1 { get; set; }
            public string LookupId2 { get; set; }
            public string LookupValue1 { get; set; }
            public int LookupValue2 { get; set; }
        }

        [Fact]
        public void MultipleMatchAndRetrieveColumns()
        {
            //Arrange
            var source = new MemorySource<InputDataMultiple>();
            source.DataAsList.Add(new InputDataMultiple { LookupId1 = 1, LookupId2 = "T1" });
            source.DataAsList.Add(new InputDataMultiple { LookupId1 = 2, LookupId2 = "TX" });
            source.DataAsList.Add(new InputDataMultiple { LookupId1 = 4, LookupId2 = "T2" });
            source.DataAsList.Add(new InputDataMultiple { LookupId1 = 3, LookupId2 = "T3" });
            var lookupSource = new MemorySource<LookupDataMultiple>();
            lookupSource.DataAsList.Add(
                new LookupDataMultiple
                {
                    Id1 = 1,
                    Id2 = "T1",
                    Value1 = "Test1",
                    Value2 = 100,
                }
            );
            lookupSource.DataAsList.Add(
                new LookupDataMultiple
                {
                    Id1 = 2,
                    Value1 = "Test2",
                    Value2 = 200,
                }
            );
            lookupSource.DataAsList.Add(
                new LookupDataMultiple
                {
                    Id1 = 3,
                    Id2 = "T3",
                    Value2 = 300,
                }
            );

            var lookup = new LookupTransformation<InputDataMultiple, LookupDataMultiple>
            {
                Source = lookupSource,
            };
            var dest = new MemoryDestination<InputDataMultiple>();
            source.LinkTo(lookup);
            lookup.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                r =>
                    Assert.True(
                        r.LookupId1 == 1 && r.LookupValue1 == "Test1" && r.LookupValue2 == 100
                    ),
                r => Assert.True(r.LookupId1 == 2 && r.LookupValue1 == null),
                r => Assert.True(r.LookupId1 == 4 && r.LookupValue1 == null),
                r =>
                    Assert.True(r.LookupId1 == 3 && r.LookupValue1 == null && r.LookupValue2 == 300)
            );
        }

        public class LookupDataError1
        {
            [MatchColumn("Id")]
            public int LookupId { get; set; }
        }

        public class LookupDataError2
        {
            [MatchColumn("XXX")]
            public int LookupId { get; set; }

            [RetrieveColumn("Value")]
            public string LookupValue { get; set; }
        }

        public class LookupDataError3
        {
            [MatchColumn("Id")]
            public int LookupId { get; set; }

            [RetrieveColumn("XXX")]
            public string LookupValue { get; set; }
        }

        [Fact]
        public void TestExceptions()
        {
            RunExceptionFlowWithType<LookupDataError1>();
            RunExceptionFlowWithType<LookupDataError2>();
            RunExceptionFlowWithType<LookupDataError3>();
        }

        private static void RunExceptionFlowWithType<T>()
        {
            //Arrange
            var source = new MemorySource<InputDataRow>();
            source.DataAsList.Add(new InputDataRow { LookupId = 1 });
            var lookupSource = new MemorySource<T>();
            var lookup = new LookupTransformation<InputDataRow, T>(lookupSource);
            var dest = new MemoryDestination<InputDataRow>();

            source.LinkTo(lookup);
            lookup.LinkTo(dest);

            //Act && Assert
            Assert.Throws<EtlKitException>(() =>
            {
                try
                {
                    source.Execute();
                    dest.Wait();
                }
                catch (AggregateException e)
                {
                    throw e.InnerException!;
                }
            });
        }
    }
}
