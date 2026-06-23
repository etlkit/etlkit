using System.Dynamic;
using EtlKit.Common.DataFlow;
using JetBrains.Annotations;

namespace EtlKit.Serialization.Tests
{
    [PublicAPI]
    public sealed class BrokenTransformation : RowTransformation<ExpandoObject>
    {
        public BrokenTransformation()
        {
            TransformationFunc = source =>
            {
                try
                {
                    throw new InvalidDataException("test");
                }
                catch (Exception e)
                {
                    if (!ErrorHandler.HasErrorBuffer)
                        throw;
                    ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(source));
                }

                return new ExpandoObject();
            };
        }
    }
}
