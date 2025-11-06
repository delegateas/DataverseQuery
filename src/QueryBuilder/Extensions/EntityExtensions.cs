using Microsoft.Xrm.Sdk;

namespace DataverseQuery.QueryBuilder.Extensions
{
    public static class EntityExtensions
    {
        /// <summary>
        /// Gets an attribute value from an aliased attribute.
        /// </summary>
        /// <typeparam name="T">The expected type of the attribute value.</typeparam>
        /// <param name="entity">The entity to extract the value from.</param>
        /// <param name="alias">The alias of the linked entity.</param>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <returns>The attribute value, or default(T) if not found.</returns>
        public static T? GetAliasedValue<T>(this Entity entity, string alias, string attributeName)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(alias);
            ArgumentNullException.ThrowIfNull(attributeName);

            var key = $"{alias}.{attributeName}";
            if (entity.Contains(key) && entity[key] is AliasedValue aliasedValue)
            {
                return aliasedValue.Value is T value ? value : default;
            }

            return default;
        }

        /// <summary>
        /// Checks if the entity has any aliased values for the given alias.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <param name="alias">The alias of the linked entity.</param>
        /// <returns>True if any aliased values exist for the given alias, otherwise false.</returns>
        public static bool HasAliasedValues(this Entity entity, string alias)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(alias);

            var prefix = $"{alias}.";
            return entity.Attributes.Keys.Any(key => key.StartsWith(prefix));
        }

        /// <summary>
        /// Gets an attribute value from the entity.
        /// </summary>
        /// <typeparam name="T">The expected type of the attribute value.</typeparam>
        /// <param name="entity">The entity to extract the value from.</param>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <returns>The attribute value, or default(T) if not found.</returns>
        public static T? GetAttributeValue<T>(this Entity entity, string attributeName)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(attributeName);

            if (entity.Contains(attributeName))
            {
                return entity.GetAttributeValue<T>(attributeName);
            }

            return default;
        }
    }
}
