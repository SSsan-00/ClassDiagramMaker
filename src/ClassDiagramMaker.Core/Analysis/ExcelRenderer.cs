using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ClassDiagramMaker.Analysis;

internal sealed record ExcelDiagramSheet(
    string Name,
    IReadOnlyList<DiagramType> Types,
    IReadOnlyList<DiagramRelationship> Relationships);

internal static class ExcelRenderer
{
    public static Task WriteAsync(
        string outputPath,
        IReadOnlyList<ExcelDiagramSheet> sheets,
        CancellationToken cancellationToken)
    {
        var outputSheets = sheets.Count == 0
            ? new[] { new ExcelDiagramSheet("ClassDiagram", Array.Empty<DiagramType>(), Array.Empty<DiagramRelationship>()) }
            : sheets;
        var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        using var document = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var workbookSheets = workbookPart.Workbook.AppendChild(new Sheets());

        uint sheetId = 1;
        foreach (var sheet in outputSheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = CreateWorksheet(sheet);

            workbookSheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId++,
                Name = CreateUniqueSheetName(sheet.Name, usedNames)
            });
        }

        workbookPart.Workbook.Save();
        return Task.CompletedTask;
    }

    private static Worksheet CreateWorksheet(ExcelDiagramSheet sheet)
    {
        var sheetData = new SheetData();

        AppendRow(sheetData, "Class Diagram", sheet.Name);
        AppendRow(sheetData);
        AppendRow(sheetData, "Diagram");

        foreach (var type in sheet.Types.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            AppendRow(sheetData, type.DisplayName, type.Kind.ToString(), type.Accessibility, string.Join(" ", type.Modifiers));
            foreach (var stereotype in GetStereotypes(type))
            {
                AppendRow(sheetData, string.Empty, $"<<{stereotype}>>");
            }

            foreach (var constraint in type.TypeParameterConstraints)
            {
                AppendRow(sheetData, string.Empty, constraint);
            }

            foreach (var member in type.Members)
            {
                AppendRow(sheetData, string.Empty, member.Visibility, member.Kind.ToString(), member.Type, member.Name, member.Signature);
            }

            AppendRow(sheetData);
        }

        AppendTable(
            sheetData,
            "Types",
            new[] { "Id", "FullName", "Namespace", "Kind", "Accessibility", "Modifiers", "SourceFile" },
            sheet.Types
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(type => new[]
                {
                    type.Id,
                    type.FullName,
                    type.Namespace,
                    type.Kind.ToString(),
                    type.Accessibility,
                    string.Join(" ", type.Modifiers),
                    type.SourceFile
                }));

        AppendTable(
            sheetData,
            "Members",
            new[] { "Type", "Kind", "Visibility", "MemberType", "Name", "Signature" },
            sheet.Types
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .SelectMany(type => type.Members.Select(member => new[]
                {
                    type.FullName,
                    member.Kind.ToString(),
                    member.Visibility,
                    member.Type,
                    member.Name,
                    member.Signature
                })));

        var typesById = sheet.Types.ToDictionary(type => type.Id, StringComparer.Ordinal);
        AppendTable(
            sheetData,
            "Relationships",
            new[] { "Kind", "From", "To", "Label", "Mermaid" },
            sheet.Relationships
                .OrderBy(relationship => relationship.FromTypeId, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.ToTypeId, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.Kind)
                .Select(relationship => new[]
                {
                    relationship.Kind.ToString(),
                    GetTypeName(typesById, relationship.FromTypeId),
                    GetTypeName(typesById, relationship.ToTypeId),
                    relationship.Label ?? string.Empty,
                    FormatRelationship(typesById, relationship)
                }));

        AppendRow(sheetData, "Mermaid");
        AppendRow(sheetData, MermaidRenderer.Render(sheet.Types, sheet.Relationships));

        var columns = new Columns(
            new Column { Min = 1, Max = 1, Width = 28, CustomWidth = true },
            new Column { Min = 2, Max = 2, Width = 32, CustomWidth = true },
            new Column { Min = 3, Max = 3, Width = 20, CustomWidth = true },
            new Column { Min = 4, Max = 4, Width = 24, CustomWidth = true },
            new Column { Min = 5, Max = 5, Width = 28, CustomWidth = true },
            new Column { Min = 6, Max = 6, Width = 64, CustomWidth = true });

        return new Worksheet(columns, sheetData);
    }

    private static void AppendTable(
        SheetData sheetData,
        string title,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        AppendRow(sheetData);
        AppendRow(sheetData, title);
        AppendRow(sheetData, headers);
        foreach (var row in rows)
        {
            AppendRow(sheetData, row);
        }
    }

    private static void AppendRow(SheetData sheetData, params string[] values)
    {
        AppendRow(sheetData, (IReadOnlyList<string>)values);
    }

    private static void AppendRow(SheetData sheetData, IReadOnlyList<string> values)
    {
        sheetData.AppendChild(new Row(values.Select(CreateTextCell)));
    }

    private static Cell CreateTextCell(string value)
    {
        return new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value ?? string.Empty))
        };
    }

    private static IReadOnlyList<string> GetStereotypes(DiagramType type)
    {
        var stereotypes = new List<string>();
        var kindStereotype = type.Kind switch
        {
            DiagramTypeKind.Interface => "interface",
            DiagramTypeKind.Struct => "struct",
            DiagramTypeKind.Record => "record",
            DiagramTypeKind.Enum => "enumeration",
            DiagramTypeKind.RazorPage => "razor page",
            DiagramTypeKind.Delegate => "delegate",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(kindStereotype))
        {
            stereotypes.Add(kindStereotype);
        }

        stereotypes.AddRange(type.Modifiers);
        return stereotypes;
    }

    private static string FormatRelationship(
        IReadOnlyDictionary<string, DiagramType> typesById,
        DiagramRelationship relationship)
    {
        var from = GetSimpleTypeName(typesById, relationship.FromTypeId);
        var to = GetSimpleTypeName(typesById, relationship.ToTypeId);
        var label = string.IsNullOrWhiteSpace(relationship.Label)
            ? string.Empty
            : $" : {relationship.Label}";

        return relationship.Kind switch
        {
            DiagramRelationshipKind.Inheritance => $"{to} <|-- {from}",
            DiagramRelationshipKind.Realization => $"{to} <|.. {from}",
            DiagramRelationshipKind.Association => $"{from} --> {to}{label}",
            DiagramRelationshipKind.Dependency => $"{from} ..> {to}{label}",
            _ => throw new ArgumentOutOfRangeException(nameof(relationship))
        };
    }

    private static string GetTypeName(
        IReadOnlyDictionary<string, DiagramType> typesById,
        string typeId)
    {
        return typesById.TryGetValue(typeId, out var type)
            ? type.FullName
            : typeId;
    }

    private static string GetSimpleTypeName(
        IReadOnlyDictionary<string, DiagramType> typesById,
        string typeId)
    {
        return typesById.TryGetValue(typeId, out var type)
            ? type.SimpleName
            : typeId;
    }

    private static string CreateUniqueSheetName(
        string preferredName,
        Dictionary<string, int> usedNames)
    {
        var sanitized = SanitizeSheetName(preferredName);
        var candidate = TruncateSheetName(sanitized);
        if (!usedNames.TryGetValue(candidate, out var count))
        {
            usedNames[candidate] = 1;
            return candidate;
        }

        while (true)
        {
            count++;
            var suffix = $"_{count}";
            candidate = $"{TruncateSheetName(sanitized, 31 - suffix.Length)}{suffix}";
            if (!usedNames.ContainsKey(candidate))
            {
                usedNames[TruncateSheetName(sanitized)] = count;
                usedNames[candidate] = 1;
                return candidate;
            }
        }
    }

    private static string SanitizeSheetName(string value)
    {
        var invalidCharacters = new HashSet<char> { ':', '\\', '/', '?', '*', '[', ']' };
        var sanitized = new string(value
            .Split('.')
            .Last()
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim('\'', ' ');

        return string.IsNullOrWhiteSpace(sanitized) ? "ClassDiagram" : sanitized;
    }

    private static string TruncateSheetName(string value, int maxLength = 31)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
