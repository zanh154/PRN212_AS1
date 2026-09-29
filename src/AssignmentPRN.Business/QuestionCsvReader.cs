using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Business;

/// <summary>
/// Reads the CSV the bank accepts for bulk import. Parsing lives here, apart from the
/// database, so the column mapping and the error messages can be checked on their own.
/// </summary>
/// <remarks>
/// Expected header row, one column each, separated by <c>;</c> or <c>,</c>:
/// <c>question_text;difficulty;bloom;material;expected_answer;option_a;option_b;option_c;option_d;correct</c>.
/// <c>material</c> matches the stored file name of a course material, <c>correct</c> is the
/// letter of the right choice (<c>A</c>..<c>D</c>, or <c>1</c>..<c>4</c>).
/// </remarks>
public static class QuestionCsvReader
{
    public const string QuestionTextColumn = "question_text";

    public const string DifficultyColumn = "difficulty";

    public const string BloomColumn = "bloom";

    public const string MaterialColumn = "material";

    public const string ExpectedAnswerColumn = "expected_answer";

    public const string CorrectColumn = "correct";

    private const int MaxOptionColumns = 8;

    /// <summary>
    /// Turns the file body into rows. A row that cannot be read is reported in
    /// <paramref name="errors"/> and skipped, so one bad line does not void the file.
    /// </summary>
    public static IReadOnlyList<QuestionImportRow> Parse(
        string content,
        IReadOnlyDictionary<string, int> materialIdsByFileName,
        out IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(content);

        var messages = new List<string>();
        var rows = new List<QuestionImportRow>();

        var lines = content
            .Replace("\r\n", "\n")
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            errors = ["File CSV rỗng."];
            return rows;
        }

        var separator = DetectSeparator(lines[0]);
        var header = SplitRow(lines[0], separator)
            .Select(column => NormaliseHeader(column))
            .ToList();

        if (!header.Contains(QuestionTextColumn))
        {
            errors = [$"File CSV thiếu cột bắt buộc {QuestionTextColumn}."];
            return rows;
        }

        var optionColumns = Enumerable.Range(0, MaxOptionColumns)
            .Select(index => $"option_{(char)('a' + index)}")
            .Where(header.Contains)
            .ToList();

        if (optionColumns.Count < 2)
        {
            errors = ["File CSV cần ít nhất 2 cột phương án (option_a, option_b)."];
            return rows;
        }

        for (var index = 1; index < lines.Count; index++)
        {
            var lineNumber = index + 1;
            var cells = SplitRow(lines[index], separator);

            try
            {
                var row = BuildRow(header, cells, optionColumns, materialIdsByFileName, lineNumber);
                if (row is not null)
                {
                    rows.Add(row);
                }
            }
            catch (Exception exception) when (exception is BusinessValidationException or ArgumentException)
            {
                messages.Add($"Dòng {lineNumber}: {exception.Message}");
            }
        }

        errors = messages;
        return rows;
    }

    /// <summary>Finds a material by its stored file name, ignoring case.</summary>
    public static int? ResolveMaterialId(
        string? fileName,
        IReadOnlyDictionary<string, int> materialIdsByFileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var key = fileName.Trim();

        if (materialIdsByFileName.TryGetValue(key, out var materialId))
        {
            return materialId;
        }

        foreach (var pair in materialIdsByFileName)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static QuestionImportRow? BuildRow(
        IReadOnlyList<string> header,
        IReadOnlyList<string> cells,
        IReadOnlyList<string> optionColumns,
        IReadOnlyDictionary<string, int> materialIdsByFileName,
        int lineNumber)
    {
        var text = Value(header, cells, QuestionTextColumn);
        if (string.IsNullOrWhiteSpace(text))
        {
            // A trailing blank line that survived the filter: silently skip it.
            return null;
        }

        var options = optionColumns
            .Select(column => new DataAccess.Contracts.QuestionOptionInput
            {
                Text = Value(header, cells, column) ?? string.Empty
            })
            .Where(option => !string.IsNullOrWhiteSpace(option.Text))
            .ToList();

        if (options.Count < 2)
        {
            throw new BusinessValidationException("Cần ít nhất 2 phương án trả lời.");
        }

        var correctIndex = ResolveCorrectIndex(Value(header, cells, CorrectColumn), options.Count);
        var marked = new List<DataAccess.Contracts.QuestionOptionInput>(options.Count);
        for (var index = 0; index < options.Count; index++)
        {
            marked.Add(new DataAccess.Contracts.QuestionOptionInput
            {
                Text = options[index].Text,
                IsCorrect = index == correctIndex
            });
        }

        var materialId = ResolveMaterialId(Value(header, cells, MaterialColumn), materialIdsByFileName);

        return new QuestionImportRow
        {
            SourceLine = lineNumber,
            QuestionText = text,
            ExpectedAnswer = Value(header, cells, ExpectedAnswerColumn),
            MaterialId = materialId,
            Difficulty = ParseDifficulty(Value(header, cells, DifficultyColumn)),
            BloomLevel = ParseBloom(Value(header, cells, BloomColumn)),
            Options = marked
        };
    }

    private static int ResolveCorrectIndex(string? raw, int optionCount)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new BusinessValidationException(
                "Thiếu cột correct, cần chỉ ra phương án đúng (A..D hoặc 1..4).");
        }

        if (value.Length == 1 && char.IsLetter(value[0]))
        {
            var byLetter = char.ToUpperInvariant(value[0]) - 'A';
            if (byLetter >= 0 && byLetter < optionCount)
            {
                return byLetter;
            }
        }

        if (int.TryParse(value, out var byPosition) && byPosition >= 1 && byPosition <= optionCount)
        {
            return byPosition - 1;
        }

        throw new BusinessValidationException($"Phương án đúng \"{value}\" không hợp lệ.");
    }

    private static QuestionDifficulty ParseDifficulty(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return QuestionDifficulty.Medium;
        }

        return value.ToLowerInvariant() switch
        {
            "easy" or "de" or "dễ" or "de_easy" => QuestionDifficulty.Easy,
            "hard" or "kho" or "khó" => QuestionDifficulty.Hard,
            _ => QuestionDifficulty.Medium
        };
    }

    private static BloomLevel ParseBloom(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return BloomLevel.Understand;
        }

        return value.ToLowerInvariant() switch
        {
            "remember" or "nho" or "nhớ" => BloomLevel.Remember,
            "apply" or "ap_dung" or "áp dụng" => BloomLevel.Apply,
            "analyze" or "analyse" or "phan_tich" or "phân tích" => BloomLevel.Analyze,
            _ => BloomLevel.Understand
        };
    }

    private static string? Value(IReadOnlyList<string> header, IReadOnlyList<string> cells, string column)
    {
        var index = header.ToList().IndexOf(column);
        if (index < 0 || index >= cells.Count)
        {
            return null;
        }

        var value = cells[index].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static char DetectSeparator(string headerLine) =>
        headerLine.Count(item => item == ';') >= headerLine.Count(item => item == ',') ? ';' : ',';

    /// <summary>
    /// Splits one line, honouring double quotes so a question containing a comma or a
    /// semicolon still lands in a single cell.
    /// </summary>
    private static List<string> SplitRow(string line, char separator)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                // A doubled quote inside a quoted cell is one literal quote.
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (character == separator && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static string NormaliseHeader(string column) => column
        .Trim()
        .Trim('﻿')
        .ToLowerInvariant()
        .Replace(' ', '_');
}
