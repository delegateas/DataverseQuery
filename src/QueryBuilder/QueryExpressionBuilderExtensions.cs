using Microsoft.Xrm.Sdk;

namespace DataverseQuery
{
    public static class QueryExpressionBuilderExtensions
    {
        public static IReadOnlyCollection<TEntity> RetrieveAll<TEntity>(this IOrganizationService service, QueryExpressionBuilder<TEntity> builder)
            where TEntity : Entity, new()
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(builder);
            var qe = builder.Build();
            var result = service.RetrieveMultiple(qe);
            return result.Entities.Select(e => e.ToEntity<TEntity>()).ToArray();
        }
    }
}
