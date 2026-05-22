using System.Globalization;

namespace scp.filestorage.Common
{
    public static class SystemSettingValueConverter
    {
        public static object? ConvertTo(string value, Type targetType)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            var realType = nullableType ?? targetType;

            if (realType == typeof(string))
                return value;

            if (realType == typeof(int))
                return int.Parse(value, CultureInfo.InvariantCulture);

            if (realType == typeof(long))
                return long.Parse(value, CultureInfo.InvariantCulture);

            if (realType == typeof(decimal))
                return decimal.Parse(value, CultureInfo.InvariantCulture);

            if (realType == typeof(double))
                return double.Parse(value, CultureInfo.InvariantCulture);

            if (realType == typeof(float))
                return float.Parse(value, CultureInfo.InvariantCulture);

            if (realType == typeof(bool))
                return bool.Parse(value);

            if (realType == typeof(Guid))
                return Guid.Parse(value);

            if (realType == typeof(DateTime))
                return DateTime.Parse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

            if (realType == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

            if (realType.IsEnum)
                return Enum.Parse(realType, value, ignoreCase: true);

            return System.Convert.ChangeType(value, realType, CultureInfo.InvariantCulture);
        }

        public static string? ConvertFrom<T>(T? value)
        {
            if (value is null)
                return null;

            var type = value.GetType();

            if (type.IsEnum)
                return value.ToString();

            return value switch
            {
                DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        public static string GetValueTypeName(Type type)
        {
            var realType = Nullable.GetUnderlyingType(type) ?? type;

            if (realType.IsEnum)
                return $"enum:{realType.FullName}";

            if (realType == typeof(string)) return "string";
            if (realType == typeof(int)) return "int";
            if (realType == typeof(long)) return "long";
            if (realType == typeof(decimal)) return "decimal";
            if (realType == typeof(double)) return "double";
            if (realType == typeof(float)) return "float";
            if (realType == typeof(bool)) return "bool";
            if (realType == typeof(Guid)) return "guid";
            if (realType == typeof(DateTime)) return "datetime";
            if (realType == typeof(DateTimeOffset)) return "datetimeoffset";

            return realType.FullName ?? realType.Name;
        }
    }
}
