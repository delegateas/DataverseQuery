namespace DataverseQuery.QueryBuilder.Interfaces
{
    /// <summary>
    /// Converts values for use in Dataverse queries.
    /// </summary>
    public interface IValueConverter
    {
        /// <summary>
        /// Converts an array of values to their primitive representations for use in Dataverse queries.
        /// </summary>
        /// <typeparam name="TValue">The type of values to convert.</typeparam>
        /// <param name="values">The values to convert.</param>
        /// <returns>An array of converted values suitable for Dataverse queries.</returns>
        object[] ConvertValues<TValue>(TValue[] values);
    }
}
