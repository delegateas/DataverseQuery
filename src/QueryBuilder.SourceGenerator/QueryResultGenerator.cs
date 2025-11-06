using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataverseQuery.SourceGenerator
{
    [Generator]
    public class QueryResultGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new QueryBuilderSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not QueryBuilderSyntaxReceiver receiver)
                return;

            var compilation = context.Compilation;

            foreach (var queryInfo in receiver.QueryBuilders)
            {
                try
                {
                    var resultClass = GenerateResultClass(queryInfo, compilation);
                    if (resultClass != null)
                    {
                        context.AddSource($"{resultClass.ClassName}.g.cs", SourceText.From(resultClass.Code, Encoding.UTF8));
                    }
                }
                catch
                {
                    // Silently skip queries that can't be analyzed
                }
            }
        }

        private ResultClassInfo? GenerateResultClass(QueryBuilderInfo queryInfo, Compilation compilation)
        {
            var className = $"Query{queryInfo.UniqueId}Result";
            var semanticModel = compilation.GetSemanticModel(queryInfo.Syntax.SyntaxTree);

            var selectedProperties = AnalyzeSelections(queryInfo, semanticModel);
            var expandedEntities = AnalyzeExpands(queryInfo, semanticModel);

            if (selectedProperties.Count == 0 && expandedEntities.Count == 0)
                return null;

            var code = GenerateCode(className, selectedProperties, expandedEntities, queryInfo.EntityType);

            return new ResultClassInfo
            {
                ClassName = className,
                Code = code
            };
        }

        private List<PropertyInfo> AnalyzeSelections(QueryBuilderInfo queryInfo, SemanticModel semanticModel)
        {
            var properties = new List<PropertyInfo>();

            // Find all .Select() method calls in the chain
            var selectCalls = queryInfo.Syntax.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                             mae.Name.Identifier.Text == "Select");

            foreach (var selectCall in selectCalls)
            {
                // Get the lambda expressions passed to Select
                var arguments = selectCall.ArgumentList.Arguments;
                foreach (var arg in arguments)
                {
                    if (arg.Expression is SimpleLambdaExpressionSyntax lambda)
                    {
                        var propInfo = ExtractPropertyFromLambda(lambda, semanticModel);
                        if (propInfo != null)
                        {
                            properties.Add(propInfo);
                        }
                    }
                }
            }

            return properties;
        }

        private PropertyInfo? ExtractPropertyFromLambda(SimpleLambdaExpressionSyntax lambda, SemanticModel semanticModel)
        {
            // Handle lambda like: e => e.Name
            if (lambda.Body is MemberAccessExpressionSyntax memberAccess)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
                {
                    return new PropertyInfo
                    {
                        Name = propertySymbol.Name,
                        Type = propertySymbol.Type.ToDisplayString()
                    };
                }
            }

            return null;
        }

        private List<ExpandInfo> AnalyzeExpands(QueryBuilderInfo queryInfo, SemanticModel semanticModel)
        {
            var expands = new List<ExpandInfo>();

            // Find all .Expand() method calls
            var expandCalls = queryInfo.Syntax.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                             mae.Name.Identifier.Text == "Expand");

            foreach (var expandCall in expandCalls)
            {
                var expandInfo = AnalyzeExpandCall(expandCall, semanticModel);
                if (expandInfo != null)
                {
                    expands.Add(expandInfo);
                }
            }

            return expands;
        }

        private ExpandInfo? AnalyzeExpandCall(InvocationExpressionSyntax expandCall, SemanticModel semanticModel)
        {
            if (expandCall.ArgumentList.Arguments.Count < 2)
                return null;

            // First argument is the navigation property
            var navigationArg = expandCall.ArgumentList.Arguments[0].Expression;
            string? navigationName = null;
            string? targetEntityType = null;

            if (navigationArg is SimpleLambdaExpressionSyntax navLambda &&
                navLambda.Body is MemberAccessExpressionSyntax navMember)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(navMember);
                if (symbolInfo.Symbol is IPropertySymbol propSymbol)
                {
                    navigationName = propSymbol.Name;

                    // Get the target entity type
                    var propType = propSymbol.Type;
                    if (propType is INamedTypeSymbol namedType)
                    {
                        // Handle IEnumerable<T> for collection navigations
                        if (namedType.IsGenericType && namedType.TypeArguments.Length > 0)
                        {
                            targetEntityType = namedType.TypeArguments[0].ToDisplayString();
                        }
                        else
                        {
                            targetEntityType = namedType.ToDisplayString();
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(navigationName))
                return null;

            // Second argument is the configuration lambda
            var configArg = expandCall.ArgumentList.Arguments[1].Expression;
            var selectedProperties = new List<PropertyInfo>();

            if (configArg is SimpleLambdaExpressionSyntax configLambda)
            {
                // Find Select calls within the configuration lambda
                var selectCalls = configLambda.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                 mae.Name.Identifier.Text == "Select");

                foreach (var selectCall in selectCalls)
                {
                    var arguments = selectCall.ArgumentList.Arguments;
                    foreach (var arg in arguments)
                    {
                        if (arg.Expression is SimpleLambdaExpressionSyntax lambda)
                        {
                            var propInfo = ExtractPropertyFromLambda(lambda, semanticModel);
                            if (propInfo != null)
                            {
                                selectedProperties.Add(propInfo);
                            }
                        }
                    }
                }
            }

            return new ExpandInfo
            {
                NavigationName = navigationName,
                TargetEntityType = targetEntityType,
                SelectedProperties = selectedProperties
            };
        }

        private string GenerateCode(string className, List<PropertyInfo> properties, List<ExpandInfo> expands, string? entityType)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using Microsoft.Xrm.Sdk;");
            sb.AppendLine("using DataverseQuery.QueryBuilder;");
            sb.AppendLine("using DataverseQuery.QueryBuilder.Services;");
            sb.AppendLine();
            sb.AppendLine("namespace DataverseQuery.Generated");
            sb.AppendLine("{");

            // Generate the main result class
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");

            // Add properties for selected fields
            foreach (var prop in properties)
            {
                sb.AppendLine($"        public {prop.Type}? {prop.Name} {{ get; set; }}");
            }

            // Add properties for expanded entities
            foreach (var expand in expands)
            {
                var expandClassName = $"{className}_{expand.NavigationName}";
                sb.AppendLine($"        public {expandClassName}? {expand.NavigationName} {{ get; set; }}");
            }

            sb.AppendLine("    }");

            // Generate nested classes for expanded entities
            foreach (var expand in expands)
            {
                GenerateNestedClass(sb, className, expand);
            }

            sb.AppendLine();

            // Generate extension method
            GenerateExtensionMethod(sb, className, properties, expands, entityType);

            sb.AppendLine("}");

            return sb.ToString();
        }

        private void GenerateNestedClass(StringBuilder sb, string parentClassName, ExpandInfo expand)
        {
            var expandClassName = $"{parentClassName}_{expand.NavigationName}";
            sb.AppendLine();
            sb.AppendLine($"    public class {expandClassName}");
            sb.AppendLine("    {");

            foreach (var prop in expand.SelectedProperties)
            {
                sb.AppendLine($"        public {prop.Type}? {prop.Name} {{ get; set; }}");
            }

            sb.AppendLine("    }");
        }

        private void GenerateExtensionMethod(StringBuilder sb, string className, List<PropertyInfo> properties, List<ExpandInfo> expands, string? entityType)
        {
            if (string.IsNullOrEmpty(entityType))
                return;

            var methodName = $"To{className}";

            sb.AppendLine($"    public static class {className}Extensions");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static Func<Entity, {className}> {methodName}(");
            sb.AppendLine($"            this ProjectionBuilder<{entityType}> projection)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return projection.To(proj => new {className}");
            sb.AppendLine("            {");

            // Generate property assignments for main entity
            foreach (var prop in properties)
            {
                sb.AppendLine($"                {prop.Name} = proj.Get(e => e.{prop.Name}),");
            }

            // Generate property assignments for expanded entities
            for (int i = 0; i < expands.Count; i++)
            {
                var expand = expands[i];
                var expandClassName = $"{className}_{expand.NavigationName}";
                var comma = i < expands.Count - 1 || properties.Count > 0 ? "," : "";

                sb.AppendLine($"                {expand.NavigationName} = proj.GetLinked(");
                sb.AppendLine($"                    e => e.{expand.NavigationName},");
                sb.AppendLine($"                    linked => new {expandClassName}");
                sb.AppendLine("                    {");

                // Generate property assignments for linked entity
                for (int j = 0; j < expand.SelectedProperties.Count; j++)
                {
                    var linkedProp = expand.SelectedProperties[j];
                    var linkedComma = j < expand.SelectedProperties.Count - 1 ? "," : "";
                    sb.AppendLine($"                        {linkedProp.Name} = linked.Get<{expand.TargetEntityType}, {linkedProp.Type}>(x => x.{linkedProp.Name}){linkedComma}");
                }

                sb.AppendLine($"                    }}){comma}");
            }

            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
    }

    internal class QueryBuilderSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<QueryBuilderInfo> QueryBuilders { get; } = new List<QueryBuilderInfo>();
        private int nextId = 0;

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            // Look for invocations of .Project() method
            if (context.Node is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "Project")
            {
                // Get the full query builder expression chain
                var queryExpression = GetQueryBuilderExpression(invocation);
                if (queryExpression != null)
                {
                    var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
                    if (symbolInfo.Symbol != null)
                    {
                        var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
                        if (typeInfo.Type is INamedTypeSymbol namedType &&
                            namedType.Name == "QueryExpressionBuilder" &&
                            namedType.TypeArguments.Length > 0)
                        {
                            QueryBuilders.Add(new QueryBuilderInfo
                            {
                                Syntax = queryExpression,
                                EntityType = namedType.TypeArguments[0].ToDisplayString(),
                                UniqueId = nextId++
                            });
                        }
                    }
                }
            }
        }

        private ExpressionSyntax? GetQueryBuilderExpression(InvocationExpressionSyntax projectCall)
        {
            // Walk up to find the start of the query builder chain
            var current = projectCall.Expression;

            while (current is MemberAccessExpressionSyntax mae && mae.Expression != null)
            {
                current = mae.Expression;
            }

            return projectCall;
        }
    }

    internal class QueryBuilderInfo
    {
        public ExpressionSyntax Syntax { get; set; } = null!;
        public string? EntityType { get; set; }
        public int UniqueId { get; set; }
    }

    internal class PropertyInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    internal class ExpandInfo
    {
        public string NavigationName { get; set; } = string.Empty;
        public string? TargetEntityType { get; set; }
        public List<PropertyInfo> SelectedProperties { get; set; } = new List<PropertyInfo>();
    }

    internal class ResultClassInfo
    {
        public string ClassName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
