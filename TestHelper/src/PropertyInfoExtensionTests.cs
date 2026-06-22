using System;
using System.Reflection;
using EtlKit.Helper;

namespace EtlKit.TestHelper
{
    public class PropertyInfoExtensionsTests
    {
        public class TestClass
        {
            public int TestProp1 { get; set; }
        }

        [Fact]
        public void TestTrySetValue()
        {
            var t = new TestClass { TestProp1 = 1 };
            Assert.Equal(1, t.TestProp1);
            PropertyInfo propInfo1 = t.GetType().GetProperty("TestProp1");
            propInfo1.TrySetValue(t, 5);
            Assert.Equal(5, t.TestProp1);
        }

        public class TestEnumClass
        {
            public EnumType TestEnumProp { get; set; }
        }

        public enum EnumType
        {
            Value1 = 1,
            Value2 = 2,
        }

        [Fact]
        public void TestTrySetEnumValue()
        {
            var t = new TestEnumClass { TestEnumProp = EnumType.Value2 };
            Assert.Equal(EnumType.Value2, t.TestEnumProp);
            PropertyInfo propInfo1 = t.GetType().GetProperty("TestEnumProp");
            propInfo1.TrySetValue(t, 1);
            Assert.Equal(EnumType.Value1, t.TestEnumProp);
        }

        public class TestNullableEnumClass
        {
            public EnumType? TestEnumProp { get; set; }
        }

        [Fact]
        public void TestTrySetNullableEnumValue()
        {
            var t = new TestNullableEnumClass { TestEnumProp = EnumType.Value2 };
            Assert.Equal(EnumType.Value2, t.TestEnumProp);
            PropertyInfo propInfo1 = t.GetType().GetProperty("TestEnumProp");
            propInfo1.TrySetValue(t, 1, Nullable.GetUnderlyingType(propInfo1!.PropertyType));
            Assert.Equal(EnumType.Value1, t.TestEnumProp);
        }
    }
}
