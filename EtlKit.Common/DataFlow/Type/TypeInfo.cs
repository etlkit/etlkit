using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;

namespace EtlKit.Common.DataFlow
{
    internal class TypeInfo
    {
        internal PropertyInfo[] Properties { get; private set; }
        protected Dictionary<string, int> PropertyIndex { get; } = new();
        internal int PropertyLength { get; private set; }
        internal bool IsArray { get; private set; } = true;
        internal bool IsDynamic { get; private set; }
        internal int ArrayLength { get; set; }

        private Type InternalType { get; set; }

        internal TypeInfo(Type type)
        {
            InternalType = type;
        }

        internal TypeInfo GatherTypeInfo()
        {
            IsArray = InternalType.IsArray;
            if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(InternalType))
                IsDynamic = true;
            switch (IsArray, IsDynamic)
            {
                case (false, false):
                {
                    Properties = InternalType.GetProperties();
                    PropertyLength = Properties.Length;
                    var index = 0;
                    foreach (var propInfo in Properties)
                    {
                        PropertyIndex.Add(propInfo.Name, index);
                        RetrieveAdditionalTypeInfo(propInfo, index);
                        index++;
                    }

                    break;
                }
                case (true, _):
                    ArrayLength = InternalType.GetArrayRank();
                    break;
            }
            return this;
        }

        internal static Type TryGetUnderlyingType(Type propertyType)
        {
            return Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        }

        protected virtual void RetrieveAdditionalTypeInfo(
            PropertyInfo propInfo,
            int currentIndex
        ) { }

        internal enum TypeInfoGroup
        {
            Array,
            Dynamic,
            Object
        }

        public TypeInfoGroup GetTypeInfoGroup() =>
            IsDynamic switch
            {
                true => TypeInfoGroup.Dynamic,
                false when IsArray => TypeInfoGroup.Array,
                _ => TypeInfoGroup.Object
            };
    }
}
