namespace ClassDiagramMaker.Analysis;

internal static class RelationshipBuilder
{
    public static IReadOnlyList<DiagramRelationship> Build(
        IReadOnlyList<DiagramType> types,
        DiagramGenerationOptions options)
    {
        var index = TypeIndex.Create(types);
        var relationships = new List<DiagramRelationship>();

        foreach (var type in types)
        {
            foreach (var baseTypeName in type.BaseTypes)
            {
                var target = index.Resolve(baseTypeName, type);
                if (target is null || target.Id == type.Id)
                {
                    continue;
                }

                var kind = target.Kind == DiagramTypeKind.Interface && type.Kind != DiagramTypeKind.Interface
                    ? DiagramRelationshipKind.Realization
                    : DiagramRelationshipKind.Inheritance;

                if (!ShouldInclude(kind, options))
                {
                    continue;
                }

                relationships.Add(new DiagramRelationship
                {
                    Kind = kind,
                    FromTypeId = type.Id,
                    ToTypeId = target.Id
                });
            }

            foreach (var member in type.Members)
            {
                foreach (var referencedTypeName in member.ReferencedTypes)
                {
                    var target = index.Resolve(referencedTypeName, type);
                    if (target is null || target.Id == type.Id)
                    {
                        continue;
                    }

                    var kind = member.Kind is DiagramMemberKind.Field or DiagramMemberKind.Property or DiagramMemberKind.Event
                        ? DiagramRelationshipKind.Association
                        : DiagramRelationshipKind.Dependency;

                    if (!ShouldInclude(kind, options))
                    {
                        continue;
                    }

                    relationships.Add(new DiagramRelationship
                    {
                        Kind = kind,
                        FromTypeId = type.Id,
                        ToTypeId = target.Id,
                        Label = member.Name
                    });
                }
            }
        }

        return relationships
            .DistinctBy(relationship => $"{relationship.Kind}:{relationship.FromTypeId}:{relationship.ToTypeId}:{relationship.Label}")
            .OrderBy(relationship => relationship.Kind)
            .ThenBy(relationship => relationship.FromTypeId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.ToTypeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ShouldInclude(DiagramRelationshipKind kind, DiagramGenerationOptions options)
    {
        return kind switch
        {
            DiagramRelationshipKind.Inheritance => options.IncludeInheritance,
            DiagramRelationshipKind.Realization => options.IncludeRealization,
            DiagramRelationshipKind.Association => options.IncludeAssociation,
            DiagramRelationshipKind.Dependency => options.IncludeDependency,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private sealed class TypeIndex
    {
        private readonly Dictionary<string, DiagramType> _byFullName;
        private readonly ILookup<string, DiagramType> _bySimpleName;

        private TypeIndex(IReadOnlyList<DiagramType> types)
        {
            _byFullName = types
                .GroupBy(type => NormalizeTypeName(type.FullName), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            _bySimpleName = types.ToLookup(type => type.SimpleName, StringComparer.Ordinal);
        }

        public static TypeIndex Create(IReadOnlyList<DiagramType> types)
        {
            return new TypeIndex(types);
        }

        public DiagramType? Resolve(string typeName, DiagramType context)
        {
            var normalized = NormalizeTypeName(typeName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (_byFullName.TryGetValue(normalized, out var exact))
            {
                return exact;
            }

            if (normalized.Contains('.', StringComparison.Ordinal))
            {
                var suffixMatch = _byFullName.Values
                    .Where(type => NormalizeTypeName(type.FullName).EndsWith($".{normalized}", StringComparison.Ordinal))
                    .ToArray();
                if (suffixMatch.Length == 1)
                {
                    return suffixMatch[0];
                }
            }

            var simpleName = normalized.Split('.').Last();
            var matches = _bySimpleName[simpleName].ToArray();
            if (matches.Length == 0)
            {
                return null;
            }

            var sameNamespace = matches
                .Where(type => string.Equals(type.Namespace, context.Namespace, StringComparison.Ordinal))
                .ToArray();
            if (sameNamespace.Length == 1)
            {
                return sameNamespace[0];
            }

            return matches.Length == 1 ? matches[0] : null;
        }

        private static string NormalizeTypeName(string typeName)
        {
            var value = typeName
                .Replace("global::", string.Empty, StringComparison.Ordinal)
                .Replace("?", string.Empty, StringComparison.Ordinal)
                .Trim();

            var genericStart = value.IndexOf('<', StringComparison.Ordinal);
            if (genericStart >= 0)
            {
                value = value[..genericStart];
            }

            return value;
        }
    }
}
