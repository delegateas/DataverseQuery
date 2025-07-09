namespace DataverseQuery
{
    // Non-generic, top-level ExpandBuilder class
    public sealed class ExpandBuilder
    {
        public string RelationshipName { get; }

        public System.Type TargetType { get; }

        public object Builder { get; }

        public bool IsCollection { get; }

        public ExpandBuilder(string relationshipName, System.Type targetType, object builder, bool isCollection)
        {
            RelationshipName = relationshipName;
            TargetType = targetType;
            Builder = builder;
            IsCollection = isCollection;
        }
    }
}
