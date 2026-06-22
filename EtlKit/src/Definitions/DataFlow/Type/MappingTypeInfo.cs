using EtlKit.Helper;

using EtlKit.Common;

using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    internal abstract class MappingTypeInfo<TInput, TOutput>
        : MappingTypeInfo,
            IMappingTypeInfo<TInput, TOutput>
    {
        protected MappingTypeInfo()
            : base(typeof(TInput), typeof(TOutput)) { }

        public void SetOutputValueOrThrow(
            TOutput outputRow,
            object value,
            AttributeMappingInfo attributeMapping,
            bool convertToUnderlyingType
        )
        {
            if (convertToUnderlyingType)
            {
                PropertyInfo propInfo = attributeMapping.PropInOutput;
                var conversionType = TypeInfo.TryGetUnderlyingType(propInfo.PropertyType);
                var output = Convert.ChangeType(value, conversionType);
                attributeMapping.PropInOutput.SetValueOrThrow(outputRow, output);
            }
            else
            {
                attributeMapping.PropInOutput.TrySetValue(outputRow, value);
            }
        }

        public object GetInputValue(TInput inputRow, AttributeMappingInfo attributeMapping)
        {
            return attributeMapping.PropInInput.GetValue(inputRow);
        }
    }

    internal abstract class MappingTypeInfo
    {
        private Dictionary<string, PropertyInfo> InputPropertiesByName { get; } = new();
        private bool IsArray => IsArrayInput || IsArrayOutput;
        private bool IsArrayInput { get; }
        public bool IsArrayOutput { get; }
        private bool IsDynamic { get; }

        private protected MappingTypeInfo(Type inputType, Type outputType)
        {
            IsArrayInput = inputType.IsArray;
            IsArrayOutput = outputType.IsArray;
            IsDynamic =
                typeof(IDynamicMetaObjectProvider).IsAssignableFrom(inputType)
                || typeof(IDynamicMetaObjectProvider).IsAssignableFrom(outputType);
        }

        protected void InitMappings(Type inputType, Type outputType)
        {
            if (IsArray || IsDynamic)
            {
                return;
            }

            foreach (var propInfo in outputType.GetProperties())
                AddAttributeInfoMapping(propInfo);

            foreach (var propInfo in inputType.GetProperties())
                InputPropertiesByName.Add(propInfo.Name, propInfo);

            CombineInputAndOutputMapping();
        }

        protected abstract void AddAttributeInfoMapping(PropertyInfo propInfo);

        protected abstract void CombineInputAndOutputMapping();

        protected void AssignInputProperty(IList<AttributeMappingInfo> columnList)
        {
            foreach (var attributeMappingInfo in columnList)
            {
                if (
                    !InputPropertiesByName.TryGetValue(
                        attributeMappingInfo.PropNameInInput,
                        out PropertyInfo value
                    )
                )
                    throw new EtlKitException(
                        $"Property {attributeMappingInfo.PropNameInInput} does not exists in target object!"
                    );
                attributeMappingInfo.PropInInput = value;
            }
        }
    }
}
