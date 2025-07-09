namespace DataverseQuery
{
    public sealed class ExpandBuilder
    {
        public string RelationshipName { get; }

        public Type TargetType { get; }

        public IQueryBuilder Builder { get; }

        public bool IsCollection { get; }

        public ExpandBuilder(string relationshipName, Type targetType, IQueryBuilder builder, bool isCollection)
        {
            RelationshipName = relationshipName;
            TargetType = targetType;
            Builder = builder;
            IsCollection = isCollection;
        }
    }
}
