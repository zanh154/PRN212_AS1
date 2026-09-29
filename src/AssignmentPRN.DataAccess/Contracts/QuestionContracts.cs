using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Contracts;

/// <summary>Filter applied when browsing the bank.</summary>
public sealed class QuestionQuery
{
    /// <summary>Restrict to one course. Null browses every course the caller may see.</summary>
    public int? CourseId { get; init; }

    /// <summary>Restrict to one topic, i.e. one course material.</summary>
    public int? MaterialId { get; init; }

    /// <summary>Restrict to one difficulty. Null includes every difficulty.</summary>
    public QuestionDifficulty? Difficulty { get; init; }

    /// <summary>Restrict to one Bloom level. Null includes every level.</summary>
    public BloomLevel? BloomLevel { get; init; }

    /// <summary>Free-text match against the question body and the expected answer.</summary>
    public string? Term { get; init; }

    /// <summary>Include archived questions. They are hidden by default.</summary>
    public bool IncludeArchived { get; init; }
}

/// <summary>Everything needed to write a question: the body, its metadata and its choices.</summary>
public sealed class QuestionUpsertInput
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public int? MaterialId { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public string? ExpectedAnswer { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionDifficulty Difficulty { get; init; }

    public IReadOnlyList<QuestionOptionInput> Options { get; init; } = Array.Empty<QuestionOptionInput>();
}

public sealed class QuestionOptionInput
{
    public string Text { get; init; } = string.Empty;

    public bool IsCorrect { get; init; }
}

/// <summary>
/// One exam slot for one candidate. Written when the exam is created, then read and
/// updated by the results module while the student answers.
/// </summary>
public sealed class ExamQuestionInput
{
    public int CandidateId { get; init; }

    public int QuestionId { get; init; }

    /// <summary>1-based position inside the candidate's own paper.</summary>
    public int OrderNo { get; init; }
}

/// <summary>
/// Everything the bank needs to deal a set of questions to a freshly created exam.
/// The candidates are read from the exam itself, so the caller does not have to repeat
/// the roster and cannot disagree with it.
/// </summary>
public sealed class ExamQuestionAssignmentRequest
{
    public int ExamId { get; init; }

    public int CourseId { get; init; }

    /// <summary>How many questions each candidate receives.</summary>
    public int CountPerCandidate { get; init; }

    /// <summary>Restrict the pool to these topics. Empty means every topic of the course.</summary>
    public IReadOnlyList<int> MaterialIds { get; init; } = Array.Empty<int>();

    /// <summary>Restrict the pool to these difficulties. Empty means every difficulty.</summary>
    public IReadOnlyList<QuestionDifficulty> Difficulties { get; init; } = Array.Empty<QuestionDifficulty>();
}

public sealed class ExamQuestionAssignmentResult
{
    public int ExamId { get; init; }

    public int CandidateCount { get; init; }

    /// <summary>Number of exam_questions rows written.</summary>
    public int AssignedCount { get; init; }

    /// <summary>Questions now in use by the exam; the pool must avoid these next time.</summary>
    public IReadOnlyList<int> TakenQuestionIds { get; init; } = Array.Empty<int>();
}

/// <summary>
/// The bank hands questions out per exam slot. <see cref="TakenQuestionIds"/> are the
/// questions already assigned to the exam being built, so a question is never repeated
/// between two students sitting the same session.
/// </summary>
public sealed class QuestionPickRequest
{
    public int CourseId { get; init; }

    /// <summary>How many questions each student receives.</summary>
    public int Count { get; init; }

    /// <summary>Restrict the pool to these topics. Empty means every topic of the course.</summary>
    public IReadOnlyList<int> MaterialIds { get; init; } = Array.Empty<int>();

    /// <summary>Restrict the pool to these difficulties. Empty means every difficulty.</summary>
    public IReadOnlyList<QuestionDifficulty> Difficulties { get; init; } = Array.Empty<QuestionDifficulty>();

    /// <summary>Questions already given out in this exam; they are excluded from the pool.</summary>
    public IReadOnlyCollection<int> TakenQuestionIds { get; init; } = Array.Empty<int>();
}

/// <summary>
/// Result of asking whether an exam can be built: how many questions the bank can
/// supply and how many are still missing, so the lecturer is warned before the exam
/// is created rather than at randomisation time.
/// </summary>
public sealed class QuestionAvailability
{
    public int Requested { get; init; }

    /// <summary>Approved, not-yet-taken questions currently in the pool.</summary>
    public int Available { get; init; }

    public int Missing => Math.Max(0, Requested - Available);

    public bool IsEnough => Missing == 0;
}

/// <summary>One row of the question list.</summary>
public sealed class QuestionListItem
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public int? MaterialId { get; init; }

    public string? MaterialName { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public QuestionDifficulty Difficulty { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionStatus Status { get; init; }

    public string AuthorName { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}

/// <summary>A question with its choices, used by the edit screen and the examiner preview.</summary>
public sealed class QuestionDetail
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public int? MaterialId { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public string? ExpectedAnswer { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionDifficulty Difficulty { get; init; }

    public QuestionStatus Status { get; init; }

    public IReadOnlyList<QuestionOptionDetail> Options { get; init; } = Array.Empty<QuestionOptionDetail>();
}

public sealed class QuestionOptionDetail
{
    public int OptionId { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool IsCorrect { get; init; }

    public int DisplayOrder { get; init; }
}

/// <summary>One row of the course material list.</summary>
public sealed class CourseMaterialItem
{
    public int MaterialId { get; init; }

    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public MaterialFileType FileType { get; init; }

    public long? FileSize { get; init; }

    public string UploaderName { get; init; } = string.Empty;

    public DateTime UploadedAt { get; init; }

    /// <summary>How many questions are filed under this topic.</summary>
    public int QuestionCount { get; init; }
}
