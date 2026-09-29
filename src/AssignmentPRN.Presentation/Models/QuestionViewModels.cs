using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AssignmentPRN.Presentation.Models;

public class QuestionListViewModel
{
    public IReadOnlyList<QuestionListItemResponse> Questions { get; init; } =
        Array.Empty<QuestionListItemResponse>();

    public IReadOnlyList<SelectListItem> CourseOptions { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> MaterialOptions { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> DifficultyOptions { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> BloomOptions { get; init; } = Array.Empty<SelectListItem>();

    public int? CourseId { get; init; }

    public int? MaterialId { get; init; }

    public QuestionDifficulty? Difficulty { get; init; }

    public BloomLevel? BloomLevel { get; init; }

    public string? Term { get; init; }

    public bool IncludeArchived { get; init; }

    public string? LoadError { get; init; }
}

/// <summary>One answer choice row on the question editor.</summary>
public sealed class QuestionOptionViewModel
{
    [Display(Name = "Phương án")]
    [Required(ErrorMessage = "Vui lòng nhập phương án.")]
    [StringLength(500, ErrorMessage = "Phương án không được vượt quá 500 ký tự.")]
    public string Text { get; set; } = string.Empty;

    [Display(Name = "Đáp án đúng")]
    public bool IsCorrect { get; set; }
}

public class QuestionEditViewModel : IValidatableObject
{
    /// <summary>Fixed number of choice rows the editor renders.</summary>
    public const int OptionRows = 4;

    public int QuestionId { get; set; }

    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "Chủ đề (tài liệu)")]
    public int? MaterialId { get; set; }

    [Display(Name = "Nội dung câu hỏi")]
    [Required(ErrorMessage = "Vui lòng nhập nội dung câu hỏi.")]
    [StringLength(4000, ErrorMessage = "Nội dung câu hỏi không được vượt quá 4000 ký tự.")]
    public string QuestionText { get; set; } = string.Empty;

    [Display(Name = "Đáp án mong đợi")]
    [StringLength(4000, ErrorMessage = "Đáp án mong đợi không được vượt quá 4000 ký tự.")]
    public string? ExpectedAnswer { get; set; }

    [Display(Name = "Mức độ khó")]
    public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

    [Display(Name = "Mức độ Bloom")]
    public BloomLevel BloomLevel { get; set; } = BloomLevel.Understand;

    public List<QuestionOptionViewModel> Options { get; set; } = [];

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> MaterialOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> DifficultyOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> BloomOptions { get; set; } = Array.Empty<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CourseId <= 0)
        {
            yield return new ValidationResult("Vui lòng chọn môn học.", [nameof(CourseId)]);
        }

        var filled = Options.Count(option => !string.IsNullOrWhiteSpace(option.Text));
        if (filled < 2)
        {
            yield return new ValidationResult("Câu hỏi cần ít nhất 2 phương án trả lời.", [nameof(Options)]);
        }

        var correct = Options.Count(option => option.IsCorrect && !string.IsNullOrWhiteSpace(option.Text));
        if (filled >= 2 && correct != 1)
        {
            yield return new ValidationResult("Câu hỏi phải có đúng một phương án đúng.", [nameof(Options)]);
        }
    }

    /// <summary>Expands the option list to the fixed number of rows the form binds to.</summary>
    public static List<QuestionOptionViewModel> BuildOptions(int count = OptionRows)
    {
        var options = new List<QuestionOptionViewModel>(count);
        for (var index = 0; index < count; index++)
        {
            options.Add(new QuestionOptionViewModel());
        }

        return options;
    }
}

public class CourseMaterialListViewModel
{
    public IReadOnlyList<CourseMaterialResponse> Materials { get; init; } =
        Array.Empty<CourseMaterialResponse>();

    public IReadOnlyList<SelectListItem> CourseOptions { get; init; } = Array.Empty<SelectListItem>();

    public int? CourseId { get; init; }

    public string? LoadError { get; init; }
}

public class CourseMaterialUploadViewModel
{
    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "Tệp tài liệu")]
    [Required(ErrorMessage = "Vui lòng chọn tệp tài liệu.")]
    public IFormFile? File { get; set; }

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class QuestionImportViewModel
{
    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "File CSV")]
    [Required(ErrorMessage = "Vui lòng chọn file CSV.")]
    public IFormFile? File { get; set; }

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();

    public QuestionImportResult? Result { get; set; }
}

public static class QuestionText
{
    public static string Difficulty(QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "Dễ",
        QuestionDifficulty.Medium => "Trung bình",
        QuestionDifficulty.Hard => "Khó",
        _ => difficulty.ToString()
    };

    public static string Bloom(BloomLevel level) => level switch
    {
        BloomLevel.Remember => "Nhớ",
        BloomLevel.Understand => "Hiểu",
        BloomLevel.Apply => "Áp dụng",
        BloomLevel.Analyze => "Phân tích",
        _ => level.ToString()
    };

    public static string Status(QuestionStatus status) => status switch
    {
        QuestionStatus.Approved => "Sẵn sàng",
        QuestionStatus.Archived => "Đã ẩn",
        QuestionStatus.Draft => "Nháp",
        QuestionStatus.PendingReview => "Chờ duyệt",
        QuestionStatus.Rejected => "Bị từ chối",
        _ => status.ToString()
    };

    public static string DifficultyChip(QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "status-chip--success",
        QuestionDifficulty.Hard => "status-chip--danger",
        _ => "status-chip--warning"
    };

    public static string FileType(AssignmentPRN.DataAccess.Enums.MaterialFileType type) => type switch
    {
        AssignmentPRN.DataAccess.Enums.MaterialFileType.PDF => "PDF",
        AssignmentPRN.DataAccess.Enums.MaterialFileType.DOCX => "Word",
        AssignmentPRN.DataAccess.Enums.MaterialFileType.PPTX => "PowerPoint",
        _ => type.ToString()
    };

    public static string FileSize(long? bytes) => bytes switch
    {
        null or 0 => "—",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / (1024d * 1024d):0.#} MB"
    };
}
