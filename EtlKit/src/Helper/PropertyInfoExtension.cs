using EtlKit.Common;

namespace EtlKit.Helper
{
    /// <summary>
    /// Extension methods for <see cref="PropertyInfo"/>.
    /// </summary>
    public static class PropertyInfoExtension
    {
        /// <summary>
        /// Sets <paramref name="pi"/>'s value on <paramref name="obj"/>, throwing if the property has
        /// no setter.
        /// </summary>
        /// <param name="pi">The property to set.</param>
        /// <param name="obj">The object instance to set the property on.</param>
        /// <param name="value">The value to set.</param>
        /// <exception cref="EtlKitException"><paramref name="pi"/> has no setter.</exception>
        public static void SetValueOrThrow(this PropertyInfo pi, object obj, object value)
        {
            if (pi.CanWrite)
                pi.SetValue(obj, value);
            else
                throw new EtlKitException(
                    $"Can't write into property {pi.Name} - property has no setter definition."
                );
        }

        /// <summary>
        /// Sets <paramref name="pi"/>'s value on <paramref name="obj"/> if it has a setter; does
        /// nothing otherwise. When <paramref name="enumType"/> is given and is an enum, <paramref
        /// name="value"/> is parsed as that enum's string representation before being set.
        /// </summary>
        /// <param name="pi">The property to set.</param>
        /// <param name="obj">The object instance to set the property on.</param>
        /// <param name="value">The value to set, or the enum member name when <paramref name="enumType"/> is given.</param>
        /// <param name="enumType">Enum type to parse <paramref name="value"/> as, or <see langword="null"/> to set it directly.</param>
        public static void TrySetValue(
            this PropertyInfo pi,
            object obj,
            object value,
            Type enumType = null
        )
        {
            if (!pi.CanWrite)
            {
                return;
            }

            if (enumType != null && value != null && enumType.IsEnum)
            {
                pi.SetValue(obj, Enum.Parse(enumType, value.ToString()));
            }
            else
            {
                pi.SetValue(obj, value);
            }
        }
    }
}
