using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.RowDuplication;

public class RowDuplicationEnumTests
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public enum EnumType
    {
        Value1 = 1,
        Value2 = 2,
    }

    [Fact]
    public void NoParameter()
    {
        //Arrange
        var source = new MemorySource<MyEnumRow>();
        source.DataAsList.Add(new MyEnumRow { EnumCol = EnumType.Value2 });
        var duplication = new RowDuplication<MyEnumRow>();
        var dest = new MemoryDestination<MyEnumRow>();

        //Act
        source.LinkTo(duplication);
        duplication.LinkTo(dest);
        source.Execute();
        dest.Wait();

        //Assert
        Assert.Collection(
            dest.Data,
            d => Assert.True(d.EnumCol == EnumType.Value2),
            d => Assert.True(d.EnumCol == EnumType.Value2)
        );
    }

    public class MyEnumRow
    {
        public EnumType EnumCol { get; set; }
    }
}
