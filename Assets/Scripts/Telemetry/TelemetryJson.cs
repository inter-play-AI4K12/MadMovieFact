using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace MadFact.Telemetry
{
    /// <summary>Small AOT-friendly JSON writer for telemetry DTOs and anonymous objects.</summary>
    public static class TelemetryJson
    {
        const int MaxDepth = 8;
        const int MaxCollectionItems = 128;

        public static string Serialize(object value)
        {
            var builder = new StringBuilder(256);
            Write(value, builder, 0);
            return builder.ToString();
        }

        public static string Quote(string value)
        {
            var builder = new StringBuilder((value?.Length ?? 0) + 2);
            WriteString(value, builder);
            return builder.ToString();
        }

        static void Write(object value, StringBuilder builder, int depth)
        {
            if (value == null) { builder.Append("null"); return; }
            if (depth > MaxDepth) { WriteString("[depth_limit]", builder); return; }

            switch (value)
            {
                case string text: WriteString(text, builder); return;
                case char character: WriteString(character.ToString(), builder); return;
                case bool boolean: builder.Append(boolean ? "true" : "false"); return;
                case DateTime dateTime: WriteString(dateTime.ToUniversalTime().ToString("O"), builder); return;
                case DateTimeOffset offset: WriteString(offset.ToUniversalTime().ToString("O"), builder); return;
                case Guid guid: WriteString(guid.ToString("D"), builder); return;
                case Enum enumeration: WriteString(ToSnakeCase(enumeration.ToString()), builder); return;
            }

            if (IsNumber(value))
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dictionary)
            {
                WriteDictionary(dictionary, builder, depth + 1);
                return;
            }

            if (value is IEnumerable enumerable)
            {
                WriteEnumerable(enumerable, builder, depth + 1);
                return;
            }

            WriteObject(value, builder, depth + 1);
        }

        static void WriteDictionary(IDictionary dictionary, StringBuilder builder, int depth)
        {
            builder.Append('{');
            bool first = true;
            int count = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (count++ >= MaxCollectionItems) break;
                if (!first) builder.Append(',');
                first = false;
                WriteString(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), builder);
                builder.Append(':');
                Write(entry.Value, builder, depth);
            }
            builder.Append('}');
        }

        static void WriteEnumerable(IEnumerable enumerable, StringBuilder builder, int depth)
        {
            builder.Append('[');
            bool first = true;
            int count = 0;
            foreach (object item in enumerable)
            {
                if (count++ >= MaxCollectionItems) break;
                if (!first) builder.Append(',');
                first = false;
                Write(item, builder, depth);
            }
            builder.Append(']');
        }

        static void WriteObject(object value, StringBuilder builder, int depth)
        {
            builder.Append('{');
            bool first = true;
            var type = value.GetType();

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!first) builder.Append(',');
                first = false;
                WriteString(field.Name, builder);
                builder.Append(':');
                Write(field.GetValue(value), builder, depth);
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                object propertyValue;
                try { propertyValue = property.GetValue(value); }
                catch { continue; }
                if (!first) builder.Append(',');
                first = false;
                WriteString(property.Name, builder);
                builder.Append(':');
                Write(propertyValue, builder, depth);
            }
            builder.Append('}');
        }

        static bool IsNumber(object value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        static void WriteString(string value, StringBuilder builder)
        {
            if (value == null) { builder.Append("null"); return; }
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 32)
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }

        public static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            var builder = new StringBuilder(value.Length + 8);
            bool previousUnderscore = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsLetterOrDigit(character))
                {
                    if (char.IsUpper(character) && index > 0 && !previousUnderscore &&
                        (char.IsLower(value[index - 1]) || char.IsDigit(value[index - 1])))
                        builder.Append('_');
                    builder.Append(char.ToLowerInvariant(character));
                    previousUnderscore = false;
                }
                else if (!previousUnderscore && builder.Length > 0)
                {
                    builder.Append('_');
                    previousUnderscore = true;
                }
            }
            return builder.ToString().Trim('_');
        }
    }
}
