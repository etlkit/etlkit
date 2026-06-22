using EtlKit.Common;
using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.LookupTransformation
{
    public class LookupExceptionTests
    {
        [Serializable]
        public class MyDataRow
        {
            [MatchColumn("Key")]
            public long Col1 { get; set; }

            [RetrieveColumn("LookupValue")]
            public string Col2 { get; set; }
        }

        public class MyLookupRow
        {
            public long Key { get; set; }
            public long? LookupValue { get; set; }
        }

        [Fact]
        public void NoLookupSource()
        {
            //Arrange
            var source = new MemorySource<MyDataRow>();
            source.DataAsList.Add(new MyDataRow { Col1 = 1, Col2 = "Test1" });

            //Act
            var lookup = new LookupTransformation<MyDataRow, MyLookupRow>();
            var dest = new MemoryDestination<MyDataRow>();

            //Assert
            Assert.Throws<EtlKitException>(() =>
            {
                try
                {
                    source.LinkTo(lookup);
                    lookup.LinkTo(dest);
                    source.Execute();
                    dest.Wait();
                }
                catch (AggregateException e)
                {
                    throw e.InnerException!;
                }
            });
            //Assert
        }
    }
}
