using System.Globalization;
using EtlKit.Serialization.DataFlow;

namespace EtlKit.Serialization.Tests
{
    public sealed class TypeExtensionsTests
    {
        [Theory]
        [InlineData(typeof(string), "1")]
        [InlineData(typeof(char), '1')]
        [InlineData(typeof(byte), (byte)1)]
        [InlineData(typeof(short), (short)1)]
        [InlineData(typeof(ushort?), (ushort)1)]
        [InlineData(typeof(int), 1)]
        [InlineData(typeof(int?), 1)]
        [InlineData(typeof(uint), (uint)1)]
        [InlineData(typeof(uint?), (uint)1)]
        [InlineData(typeof(long), (long)1)]
        [InlineData(typeof(long?), (long)1)]
        [InlineData(typeof(ulong), (ulong)1)]
        [InlineData(typeof(ulong?), (ulong)1)]
        [InlineData(typeof(double), (double)1)]
        public void GetValue_ObjectValueShouldBeReturned(Type type, object? value)
        {
            var result = "1".TryParse(type, CultureInfo.InvariantCulture, out var obj);
            Assert.True(result);
            Assert.Equal(value, obj);
        }

        [Theory]
        [InlineData(typeof(Guid))]
        [InlineData(typeof(Guid?))]
        public void GetValue_WithGuid_ObjectValueShouldBeReturned(Type type)
        {
            var id = Guid.Parse("db31477f-1b02-4e82-a14e-fa26a35a27da");

            var result = "db31477f-1b02-4e82-a14e-fa26a35a27da".TryParse(
                type,
                CultureInfo.InvariantCulture,
                out var obj
            );

            Assert.True(result);
            Assert.Equal(id, obj);
        }

        [Theory]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(DateTime?))]
        public void GetValue_WithDateTime_ObjectValueShouldBeReturned(Type type)
        {
            var now = new DateTime(2024, 1, 2, 3, 4, 0, DateTimeKind.Local);
            var date = now.ToString(CultureInfo.InvariantCulture);

            var result = date.TryParse(type, CultureInfo.InvariantCulture, out var obj);

            Assert.True(result);
            Assert.Equal(now, obj);
        }

        [Fact]
        public void GetValue_NullValueShouldBeReturned()
        {
            var result = "1".TryParse(
                typeof(UnsupportedType),
                CultureInfo.InvariantCulture,
                out var obj
            );

            Assert.False(result);
            Assert.Null(obj);
        }

        public struct UnsupportedType
        {
            // test
        }
    }
}
