using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace ClassDiagramMaker.Analysis;

internal sealed record ExcelDiagramSheet(
    string Name,
    IReadOnlyList<DiagramType> Types,
    IReadOnlyList<DiagramRelationship> Relationships);

internal static class ExcelRenderer
{
    private const int ClassColumnGap = 2;
    private const int ClassRowGap = 4;
    private const int ClassMarginColumn = 1;
    private const int ClassMarginRow = 1;
    private const int MinimumClassColumns = 5;
    private const int MaximumClassColumns = 14;
    private const int MinimumClassRows = 5;
    private const int CharactersPerColumn = 11;
    private const double WorksheetColumnWidth = 12;
    private const double WorksheetRowHeight = 18;
    private const int OutlineWidth = 12700;
    private const int RelationshipWidth = 19050;
    private const string ClassFillColor = "F8FBFF";
    private const string ClassOutlineColor = "4472C4";
    private const string RelationshipColor = "595959";

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
        workbookPart.Workbook = new S.Workbook();
        var workbookSheets = workbookPart.Workbook.AppendChild(new S.Sheets());

        uint sheetId = 1;
        foreach (var sheet in outputSheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = CreateWorksheet(worksheetPart, sheet);

            workbookSheets.Append(new S.Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId++,
                Name = CreateUniqueSheetName(sheet.Name, usedNames)
            });
        }

        workbookPart.Workbook.Save();
        return Task.CompletedTask;
    }

    private static S.Worksheet CreateWorksheet(WorksheetPart worksheetPart, ExcelDiagramSheet sheet)
    {
        var layout = CreateLayout(sheet.Types, sheet.Relationships);
        var worksheet = new S.Worksheet(
            CreateColumns(layout.ColumnCount),
            CreateSheetData(layout.RowCount));

        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var worksheetDrawing = new Xdr.WorksheetDrawing();
        var nextShapeId = 1U;

        if (layout.Boxes.Count == 0)
        {
            worksheetDrawing.Append(CreateMessageShape("No classes found", nextShapeId++));
        }
        else
        {
            var boxesByTypeId = layout.Boxes.ToDictionary(box => box.Type.Id, StringComparer.Ordinal);
            foreach (var relationship in sheet.Relationships
                         .OrderBy(relationship => relationship.FromTypeId, StringComparer.Ordinal)
                         .ThenBy(relationship => relationship.ToTypeId, StringComparer.Ordinal)
                         .ThenBy(relationship => relationship.Kind))
            {
                if (!boxesByTypeId.TryGetValue(relationship.FromTypeId, out var fromBox) ||
                    !boxesByTypeId.TryGetValue(relationship.ToTypeId, out var toBox))
                {
                    continue;
                }

                var connector = CreateRelationshipConnector(relationship, fromBox, toBox, nextShapeId++);
                worksheetDrawing.Append(connector.Anchor);

                if (!string.IsNullOrWhiteSpace(relationship.Label))
                {
                    worksheetDrawing.Append(CreateLabelShape(
                        relationship.Label,
                        connector.LabelColumn,
                        connector.LabelRow,
                        nextShapeId++));
                }
            }

            foreach (var box in layout.Boxes.OrderBy(box => box.Row).ThenBy(box => box.Column))
            {
                worksheetDrawing.Append(CreateClassShape(box, nextShapeId++));
            }
        }

        drawingsPart.WorksheetDrawing = worksheetDrawing;
        drawingsPart.WorksheetDrawing.Save();
        worksheet.Append(new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
        return worksheet;
    }

    private static S.Columns CreateColumns(int columnCount)
    {
        return new S.Columns(new S.Column
        {
            Min = 1,
            Max = (uint)Math.Max(1, columnCount),
            Width = WorksheetColumnWidth,
            CustomWidth = true
        });
    }

    private static S.SheetData CreateSheetData(int rowCount)
    {
        var sheetData = new S.SheetData();
        for (var row = 1; row <= Math.Max(1, rowCount); row++)
        {
            sheetData.Append(new S.Row
            {
                RowIndex = (uint)row,
                Height = WorksheetRowHeight,
                CustomHeight = true
            });
        }

        return sheetData;
    }

    private static DiagramLayout CreateLayout(
        IReadOnlyList<DiagramType> types,
        IReadOnlyList<DiagramRelationship> relationships)
    {
        var orderedTypes = types
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        if (orderedTypes.Length == 0)
        {
            return new DiagramLayout(Array.Empty<DiagramBox>(), 12, 6);
        }

        var ranks = CalculateRanks(orderedTypes, relationships);
        var boxes = new List<DiagramBox>();
        var currentRow = ClassMarginRow;
        var maxColumn = ClassMarginColumn;

        foreach (var layer in orderedTypes
                     .GroupBy(type => ranks[type.Id])
                     .OrderBy(group => group.Key))
        {
            var layerBoxes = layer
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(CreateUnplacedBox)
                .ToArray();

            var currentColumn = ClassMarginColumn;
            var layerHeight = layerBoxes.Max(box => box.RowSpan);
            foreach (var box in layerBoxes)
            {
                var placed = box with
                {
                    Column = currentColumn,
                    Row = currentRow
                };
                boxes.Add(placed);
                currentColumn += placed.ColumnSpan + ClassColumnGap;
                maxColumn = Math.Max(maxColumn, placed.Column + placed.ColumnSpan + ClassMarginColumn);
            }

            currentRow += layerHeight + ClassRowGap;
        }

        return new DiagramLayout(boxes, maxColumn, currentRow + ClassMarginRow);
    }

    private static DiagramBox CreateUnplacedBox(DiagramType type)
    {
        var lines = CreateClassLines(type);
        var maxLineLength = lines
            .Select(line => line.Length)
            .DefaultIfEmpty(type.DisplayName.Length)
            .Max();
        var columnSpan = Math.Clamp(
            (int)Math.Ceiling((maxLineLength + 4) / (double)CharactersPerColumn),
            MinimumClassColumns,
            MaximumClassColumns);
        var rowSpan = Math.Max(MinimumClassRows, lines.Count + 2);

        return new DiagramBox(type, 0, 0, columnSpan, rowSpan, lines);
    }

    private static IReadOnlyList<string> CreateClassLines(DiagramType type)
    {
        var lines = new List<string>
        {
            type.DisplayName
        };

        var metadata = new List<string> { type.Kind.ToString() };
        if (!string.IsNullOrWhiteSpace(type.Accessibility))
        {
            metadata.Add(type.Accessibility);
        }

        metadata.AddRange(type.Modifiers);
        lines.Add(string.Join(" ", metadata.Where(value => !string.IsNullOrWhiteSpace(value))));

        foreach (var stereotype in GetStereotypes(type))
        {
            lines.Add($"<<{stereotype}>>");
        }

        lines.Add("----------------");
        lines.AddRange(type.Members.Select(member => member.Signature));
        lines.AddRange(type.TypeParameterConstraints);
        return lines;
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

    private static IReadOnlyDictionary<string, int> CalculateRanks(
        IReadOnlyList<DiagramType> types,
        IReadOnlyList<DiagramRelationship> relationships)
    {
        var typeIds = types
            .Select(type => type.Id)
            .ToHashSet(StringComparer.Ordinal);
        var adjacency = typeIds.ToDictionary(
            id => id,
            _ => new SortedSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var relationship in relationships)
        {
            if (typeIds.Contains(relationship.FromTypeId) &&
                typeIds.Contains(relationship.ToTypeId) &&
                !string.Equals(relationship.FromTypeId, relationship.ToTypeId, StringComparison.Ordinal))
            {
                adjacency[relationship.ToTypeId].Add(relationship.FromTypeId);
            }
        }

        var components = FindStronglyConnectedComponents(types, adjacency);
        var componentByTypeId = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
        {
            foreach (var typeId in components[componentIndex])
            {
                componentByTypeId[typeId] = componentIndex;
            }
        }

        var componentGraph = Enumerable.Range(0, components.Count)
            .ToDictionary(index => index, _ => new SortedSet<int>());
        var indegrees = Enumerable.Range(0, components.Count)
            .ToDictionary(index => index, _ => 0);

        foreach (var (fromTypeId, targets) in adjacency)
        {
            var fromComponent = componentByTypeId[fromTypeId];
            foreach (var targetTypeId in targets)
            {
                var targetComponent = componentByTypeId[targetTypeId];
                if (fromComponent == targetComponent || !componentGraph[fromComponent].Add(targetComponent))
                {
                    continue;
                }

                indegrees[targetComponent]++;
            }
        }

        var componentSortKeys = components
            .Select((component, index) => new
            {
                Index = index,
                Key = types
                    .Where(type => component.Contains(type.Id))
                    .Select(type => type.FullName)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .First()
            })
            .ToDictionary(value => value.Index, value => value.Key);
        var ready = indegrees
            .Where(pair => pair.Value == 0)
            .Select(pair => pair.Key)
            .OrderBy(index => componentSortKeys[index], StringComparer.Ordinal)
            .ToList();
        var ranks = Enumerable.Range(0, components.Count)
            .ToDictionary(index => index, _ => 0);

        while (ready.Count > 0)
        {
            var component = ready[0];
            ready.RemoveAt(0);

            foreach (var targetComponent in componentGraph[component])
            {
                ranks[targetComponent] = Math.Max(ranks[targetComponent], ranks[component] + 1);
                indegrees[targetComponent]--;
                if (indegrees[targetComponent] == 0)
                {
                    ready.Add(targetComponent);
                    ready.Sort((left, right) => string.Compare(
                        componentSortKeys[left],
                        componentSortKeys[right],
                        StringComparison.Ordinal));
                }
            }
        }

        return typeIds.ToDictionary(
            id => id,
            id => ranks[componentByTypeId[id]],
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<IReadOnlyList<string>> FindStronglyConnectedComponents(
        IReadOnlyList<DiagramType> types,
        IReadOnlyDictionary<string, SortedSet<string>> adjacency)
    {
        var index = 0;
        var stack = new Stack<string>();
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<IReadOnlyList<string>>();

        foreach (var type in types.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (!indices.ContainsKey(type.Id))
            {
                StrongConnect(type.Id);
            }
        }

        return components;

        void StrongConnect(string typeId)
        {
            indices[typeId] = index;
            lowLinks[typeId] = index;
            index++;
            stack.Push(typeId);
            onStack.Add(typeId);

            foreach (var targetTypeId in adjacency[typeId])
            {
                if (!indices.ContainsKey(targetTypeId))
                {
                    StrongConnect(targetTypeId);
                    lowLinks[typeId] = Math.Min(lowLinks[typeId], lowLinks[targetTypeId]);
                }
                else if (onStack.Contains(targetTypeId))
                {
                    lowLinks[typeId] = Math.Min(lowLinks[typeId], indices[targetTypeId]);
                }
            }

            if (lowLinks[typeId] != indices[typeId])
            {
                return;
            }

            var component = new List<string>();
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                onStack.Remove(current);
                component.Add(current);

                if (string.Equals(current, typeId, StringComparison.Ordinal))
                {
                    break;
                }
            }

            components.Add(component);
        }
    }

    private static Xdr.TwoCellAnchor CreateClassShape(DiagramBox box, uint shapeId)
    {
        return new Xdr.TwoCellAnchor(
            CreateFromMarker(box.Column, box.Row),
            CreateToMarker(box.Column + box.ColumnSpan, box.Row + box.RowSpan),
            new Xdr.Shape(
                new Xdr.NonVisualShapeProperties(
                    new Xdr.NonVisualDrawingProperties
                    {
                        Id = shapeId,
                        Name = $"Class {box.Type.DisplayName}"
                    },
                    new Xdr.NonVisualShapeDrawingProperties { TextBox = true }),
                new Xdr.ShapeProperties(
                    CreateRectangleGeometry(),
                    CreateSolidFill(ClassFillColor),
                    CreateOutline(ClassOutlineColor, OutlineWidth)),
                CreateTextBody(box.Lines, boldFirstLine: true, centered: false)),
            new Xdr.ClientData())
        {
            EditAs = Xdr.EditAsValues.OneCell
        };
    }

    private static RelationshipConnector CreateRelationshipConnector(
        DiagramRelationship relationship,
        DiagramBox fromBox,
        DiagramBox toBox,
        uint shapeId)
    {
        var targetPoint = GetTargetConnectionPoint(toBox, fromBox);
        var sourcePoint = GetSourceConnectionPoint(fromBox, toBox);
        var anchorColumn = Math.Min(targetPoint.Column, sourcePoint.Column);
        var anchorRow = Math.Min(targetPoint.Row, sourcePoint.Row);
        var toColumn = Math.Max(targetPoint.Column, sourcePoint.Column);
        var toRow = Math.Max(targetPoint.Row, sourcePoint.Row);
        if (toColumn == anchorColumn)
        {
            toColumn++;
        }

        if (toRow == anchorRow)
        {
            toRow++;
        }

        var transform = new A.Transform2D
        {
            HorizontalFlip = targetPoint.Column > sourcePoint.Column,
            VerticalFlip = targetPoint.Row > sourcePoint.Row
        };

        var anchor = new Xdr.TwoCellAnchor(
            CreateFromMarker(anchorColumn, anchorRow),
            CreateToMarker(toColumn, toRow),
            new Xdr.ConnectionShape(
                new Xdr.NonVisualConnectionShapeProperties(
                    new Xdr.NonVisualDrawingProperties
                    {
                        Id = shapeId,
                        Name = $"{relationship.Kind} {relationship.FromTypeId} to {relationship.ToTypeId}"
                    },
                    new Xdr.NonVisualConnectorShapeDrawingProperties()),
                new Xdr.ShapeProperties(
                    transform,
                    CreateStraightConnectorGeometry(),
                    CreateRelationshipOutline(relationship))),
            new Xdr.ClientData())
        {
            EditAs = Xdr.EditAsValues.OneCell
        };

        var labelColumn = Math.Max(ClassMarginColumn, (targetPoint.Column + sourcePoint.Column) / 2);
        var labelRow = Math.Max(ClassMarginRow, (targetPoint.Row + sourcePoint.Row) / 2);
        return new RelationshipConnector(anchor, labelColumn, labelRow);
    }

    private static MarkerPoint GetTargetConnectionPoint(DiagramBox targetBox, DiagramBox sourceBox)
    {
        if (targetBox.Row < sourceBox.Row)
        {
            return new MarkerPoint(targetBox.Column + targetBox.ColumnSpan / 2, targetBox.Row + targetBox.RowSpan);
        }

        if (targetBox.Row > sourceBox.Row)
        {
            return new MarkerPoint(targetBox.Column + targetBox.ColumnSpan / 2, targetBox.Row);
        }

        return targetBox.Column < sourceBox.Column
            ? new MarkerPoint(targetBox.Column + targetBox.ColumnSpan, targetBox.Row + targetBox.RowSpan / 2)
            : new MarkerPoint(targetBox.Column, targetBox.Row + targetBox.RowSpan / 2);
    }

    private static MarkerPoint GetSourceConnectionPoint(DiagramBox sourceBox, DiagramBox targetBox)
    {
        if (targetBox.Row < sourceBox.Row)
        {
            return new MarkerPoint(sourceBox.Column + sourceBox.ColumnSpan / 2, sourceBox.Row);
        }

        if (targetBox.Row > sourceBox.Row)
        {
            return new MarkerPoint(sourceBox.Column + sourceBox.ColumnSpan / 2, sourceBox.Row + sourceBox.RowSpan);
        }

        return targetBox.Column < sourceBox.Column
            ? new MarkerPoint(sourceBox.Column, sourceBox.Row + sourceBox.RowSpan / 2)
            : new MarkerPoint(sourceBox.Column + sourceBox.ColumnSpan, sourceBox.Row + sourceBox.RowSpan / 2);
    }

    private static Xdr.TwoCellAnchor CreateLabelShape(
        string label,
        int column,
        int row,
        uint shapeId)
    {
        return new Xdr.TwoCellAnchor(
            CreateFromMarker(column, row),
            CreateToMarker(column + 4, row + 2),
            new Xdr.Shape(
                new Xdr.NonVisualShapeProperties(
                    new Xdr.NonVisualDrawingProperties
                    {
                        Id = shapeId,
                        Name = $"Label {label}"
                    },
                    new Xdr.NonVisualShapeDrawingProperties { TextBox = true }),
                new Xdr.ShapeProperties(
                    CreateRectangleGeometry(),
                    new A.NoFill(),
                    new A.Outline(new A.NoFill())),
                CreateTextBody(new[] { label }, boldFirstLine: false, centered: true)),
            new Xdr.ClientData())
        {
            EditAs = Xdr.EditAsValues.OneCell
        };
    }

    private static Xdr.TwoCellAnchor CreateMessageShape(string message, uint shapeId)
    {
        return new Xdr.TwoCellAnchor(
            CreateFromMarker(1, 1),
            CreateToMarker(9, 4),
            new Xdr.Shape(
                new Xdr.NonVisualShapeProperties(
                    new Xdr.NonVisualDrawingProperties
                    {
                        Id = shapeId,
                        Name = "No classes found"
                    },
                    new Xdr.NonVisualShapeDrawingProperties { TextBox = true }),
                new Xdr.ShapeProperties(
                    CreateRectangleGeometry(),
                    CreateSolidFill("FFF2CC"),
                    CreateOutline("D6B656", OutlineWidth)),
                CreateTextBody(new[] { message }, boldFirstLine: true, centered: true)),
            new Xdr.ClientData())
        {
            EditAs = Xdr.EditAsValues.OneCell
        };
    }

    private static Xdr.FromMarker CreateFromMarker(int column, int row)
    {
        return new Xdr.FromMarker(
            new Xdr.ColumnId(column.ToString(CultureInfo.InvariantCulture)),
            new Xdr.ColumnOffset("0"),
            new Xdr.RowId(row.ToString(CultureInfo.InvariantCulture)),
            new Xdr.RowOffset("0"));
    }

    private static Xdr.ToMarker CreateToMarker(int column, int row)
    {
        return new Xdr.ToMarker(
            new Xdr.ColumnId(column.ToString(CultureInfo.InvariantCulture)),
            new Xdr.ColumnOffset("0"),
            new Xdr.RowId(row.ToString(CultureInfo.InvariantCulture)),
            new Xdr.RowOffset("0"));
    }

    private static A.PresetGeometry CreateRectangleGeometry()
    {
        return new A.PresetGeometry(new A.AdjustValueList())
        {
            Preset = A.ShapeTypeValues.Rectangle
        };
    }

    private static A.PresetGeometry CreateStraightConnectorGeometry()
    {
        return new A.PresetGeometry(new A.AdjustValueList())
        {
            Preset = A.ShapeTypeValues.StraightConnector1
        };
    }

    private static A.SolidFill CreateSolidFill(string rgb)
    {
        return new A.SolidFill(new A.RgbColorModelHex { Val = rgb });
    }

    private static A.Outline CreateOutline(string rgb, int width)
    {
        return new A.Outline(CreateSolidFill(rgb))
        {
            Width = width
        };
    }

    private static A.Outline CreateRelationshipOutline(DiagramRelationship relationship)
    {
        var outline = CreateOutline(RelationshipColor, RelationshipWidth);
        if (relationship.Kind is DiagramRelationshipKind.Realization or DiagramRelationshipKind.Dependency)
        {
            outline.Append(new A.PresetDash { Val = A.PresetLineDashValues.Dash });
        }

        outline.Append(new A.HeadEnd
        {
            Type = relationship.Kind is DiagramRelationshipKind.Inheritance or DiagramRelationshipKind.Realization
                ? A.LineEndValues.Triangle
                : A.LineEndValues.Arrow
        });
        return outline;
    }

    private static Xdr.TextBody CreateTextBody(
        IReadOnlyList<string> lines,
        bool boldFirstLine,
        bool centered)
    {
        IReadOnlyList<string> textLines = lines.Count == 0 ? new[] { string.Empty } : lines;
        var textBody = new Xdr.TextBody(
            new A.BodyProperties
            {
                Wrap = A.TextWrappingValues.Square,
                VerticalOverflow = A.TextVerticalOverflowValues.Clip,
                HorizontalOverflow = A.TextHorizontalOverflowValues.Clip,
                Anchor = centered ? A.TextAnchoringTypeValues.Center : A.TextAnchoringTypeValues.Top
            },
            new A.ListStyle());

        for (var index = 0; index < textLines.Count; index++)
        {
            textBody.Append(CreateParagraph(
                textLines[index],
                bold: boldFirstLine && index == 0,
                centered: centered,
                fontSize: index == 0 ? 1100 : 900));
        }

        return textBody;
    }

    private static A.Paragraph CreateParagraph(
        string text,
        bool bold,
        bool centered,
        int fontSize)
    {
        var paragraph = new A.Paragraph();
        if (centered)
        {
            paragraph.Append(new A.ParagraphProperties
            {
                Alignment = A.TextAlignmentTypeValues.Center
            });
        }

        paragraph.Append(new A.Run(
            new A.RunProperties
            {
                FontSize = fontSize,
                Bold = bold
            },
            new A.Text(text ?? string.Empty)));
        return paragraph;
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

    private sealed record DiagramLayout(
        IReadOnlyList<DiagramBox> Boxes,
        int ColumnCount,
        int RowCount);

    private sealed record DiagramBox(
        DiagramType Type,
        int Column,
        int Row,
        int ColumnSpan,
        int RowSpan,
        IReadOnlyList<string> Lines);

    private sealed record MarkerPoint(int Column, int Row);

    private sealed record RelationshipConnector(
        Xdr.TwoCellAnchor Anchor,
        int LabelColumn,
        int LabelRow);
}
