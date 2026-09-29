using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AssignmentPRN.Presentation.Models;

/// <summary>
/// The "phát đề" screen of an exam: how many questions each student gets and which
/// slice of the bank they are drawn from. The same model carries the papers back so
/// the lecturer sees the result of the draw without a second round-trip.
/// </summary>
public class ExamQuestionAssignViewModel
{
    [Range(1, int.MaxValue)]
    public int ExamId { get; set; }

    public string ExamName { get; set; } = string.Empty;

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public int CourseId { get; set; }

    [Display(Name = "Số câu hỏi chính mỗi sinh viên")]
    [Range(1, 50, ErrorMessage = "Số câu hỏi chính phải từ 1 đến 50.")]
    public int CountPerCandidate { get; set; } = 3;

    [Display(Name = "Chủ đề (tài liệu)")]
    public List<int> MaterialIds { get; set; } = [];

    [Display(Name = "Độ khó")]
    public List<QuestionDifficulty> Difficulties { get; set; } = [];

    public IReadOnlyList<SelectListItem> MaterialOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> DifficultyOptions { get; set; } = Array.Empty<SelectListItem>();

    /// <summary>Candidates with the paper they already hold; empty papers mean "chưa phát".</summary>
    public IReadOnlyList<ExamPaperItem> Papers { get; set; } = Array.Empty<ExamPaperItem>();

    /// <summary>Number of distinct, still-free questions matching the current filter.</summary>
    public int AvailableCount { get; set; }

    public int CandidateCount => Papers.Count;

    public int AssignedCandidateCount => Papers.Count(paper => paper.Questions.Count > 0);

    /// <summary>Questions the draw needs for the students that do not have a paper yet.</summary>
    public int RequiredCount => (CandidateCount - AssignedCandidateCount) * CountPerCandidate;

    public bool IsEnough => AvailableCount >= RequiredCount;

    public bool IsFullyAssigned => CandidateCount > 0 && AssignedCandidateCount == CandidateCount;

    /// <summary>False once a student has started answering: the draw is then frozen.</summary>
    public bool CanRedeal { get; set; }

    /// <summary>True while the exam is still editable; a closed exam is read-only here.</summary>
    public bool CanAssign { get; set; }

    public string? LoadError { get; set; }
}
