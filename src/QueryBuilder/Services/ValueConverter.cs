using System.Globalization;
using DataverseQuery.QueryBuilder.Interfaces;
using Microsoft.Xrm.Sdk;

namespace DataverseQuery.QueryBuilder.Services
{
    /// <summary>
    /// Default implementation of value conversion for Dataverse queries.
    /// </summary>
    public sealed class ValueConverter : IValueConverter
    {
        /// <summary>
        /// Converts an array of values to their primitive representations for use in Dataverse queries.
        /// </summary>
        /// <typeparam name="TValue">The type of values to convert.</typeparam>
        /// <param name="values">The values to convert.</param>
        /// <returns>An array of converted values suitable for Dataverse queries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when values is null.</exception>
        /// <exception cref="ArgumentException">Thrown when any value in the array is null.</exception>
        public object[] ConvertValues<TValue>(TValue[] values)
        {
            ArgumentNullException.ThrowIfNull(values);

            return values
                .Cast<object>()
                .Select(ConvertValue)
                .ToArray();
        }

        private static object ConvertValue(object value)
        {
            return value switch
            {
                null => throw new ArgumentException("Null values are not supported in filter conditions.", nameof(value)),
                Enum enumValue => Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType()), CultureInfo.InvariantCulture),
                EntityReference entityRef => entityRef.Id,
                _ => value,
            };
        }
    }
}