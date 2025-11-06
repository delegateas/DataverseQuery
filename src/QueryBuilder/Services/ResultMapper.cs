using System.Dynamic;
using Microsoft.Xrm.Sdk;
using DataverseQuery.QueryBuilder.Extensions;

namespace DataverseQuery.QueryBuilder.Services
{
    /// <summary>
    /// Creates mapping functions that transform Entity objects into dynamic objects
    /// containing only the selected columns and linked entities.
    /// </summary>
    public class ResultMapper
    {
        private readonly List<string> columns;
        private readonly List<ExpandMapping> expandMappings;

        internal ResultMapper(List<string> columns, List<ExpandMapping> expandMappings)
        {
            this.columns = columns;
            this.expandMappings = expandMappings;
        }

        /// <summary>
        /// Creates a mapping function that transforms an Entity into a dynamic object.
        /// </summary>
        /// <returns>A function that maps Entity to dynamic object with selected columns.</returns>
        public Func<Entity, dynamic> CreateMapper()
        {
            return entity =>
            {
                dynamic result = new ExpandoObject();
                var resultDict = (IDictionary<string, object?>)result;

                // Map selected columns from the main entity
                foreach (var column in columns)
                {
                    if (entity.Contains(column))
                    {
                        resultDict[column] = entity[column];
                    }
                    else
                    {
                        resultDict[column] = null;
                    }
                }

                // Map linked entities
                foreach (var expandMapping in expandMappings)
                {
                    resultDict[expandMapping.PropertyName] = MapLinkedEntity(entity, expandMapping);
                }

                return result;
            };
        }

        private static dynamic? MapLinkedEntity(Entity entity, ExpandMapping mapping)
        {
            if (mapping.Alias == null)
            {
                return null;
            }

            // Check if any aliased values exist for this linked entity
            var hasAnyValue = mapping.Columns.Any(col =>
                entity.Contains($"{mapping.Alias}.{col}"));

            if (!hasAnyValue)
            {
                return null;
            }

            dynamic linkedResult = new ExpandoObject();
            var linkedDict = (IDictionary<string, object?>)linkedResult;

            // Map columns from the linked entity
            foreach (var column in mapping.Columns)
            {
                linkedDict[column] = entity.GetAliasedValue<object>(mapping.Alias, column);
            }

            // Map nested linked entities
            foreach (var nestedMapping in mapping.NestedExpands)
            {
                linkedDict[nestedMapping.PropertyName] = MapLinkedEntity(entity, nestedMapping);
            }

            return linkedResult;
        }
    }

    /// <summary>
    /// Represents the mapping configuration for a linked entity.
    /// </summary>
    public class ExpandMapping
    {
        public string PropertyName { get; set; } = string.Empty;
        public string? Alias { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<ExpandMapping> NestedExpands { get; set; } = new();
    }
}
