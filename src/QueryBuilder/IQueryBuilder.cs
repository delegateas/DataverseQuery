using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery.QueryBuilder
{
    public interface IQueryBuilder
    {
        ColumnSet GetColumns();

        FilterExpression GetCombinedFilter();

        IEnumerable<ExpandBuilder> GetExpands();

        LinkEntity BuildLinkEntity(ExpandBuilder expand);
    }
}
