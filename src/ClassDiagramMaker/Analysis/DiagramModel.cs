namespace ClassDiagramMaker.Analysis;

public enum DiagramTypeKind
{
    Class,
    Interface,
    Struct,
    Record,
    Enum
}

public enum DiagramMemberKind
{
    Field,
    Property,
    Method,
    Constructor,
    Event,
    Indexer,
    EnumValue
}

public enum DiagramRelationshipKind
{
    Inheritance,
    Realization,
    Association,
    Dependency
}

public sealed record DiagramType
{
    public required string Id { get; init; }
    public required string SimpleName { get; init; }
    public required string DisplayName { get; init; }
    public required string FullName { get; init; }
    public required string Namespace { get; init; }
    public required string SourceFile { get; init; }
    public required DiagramTypeKind Kind { get; init; }
    public required string Accessibility { get; init; }
    public IReadOnlyList<string> Modifiers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TypeParameters { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BaseTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DiagramMember> Members { get; init; } = Array.Empty<DiagramMember>();
}

public sealed record DiagramMember
{
    public required DiagramMemberKind Kind { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Visibility { get; init; }
    public required string Signature { get; init; }
    public bool IsStatic { get; init; }
    public IReadOnlyList<string> ReferencedTypes { get; init; } = Array.Empty<string>();
}

public sealed record DiagramRelationship
{
    public required DiagramRelationshipKind Kind { get; init; }
    public required string FromTypeId { get; init; }
    public required string ToTypeId { get; init; }
    public string? Label { get; init; }
}
