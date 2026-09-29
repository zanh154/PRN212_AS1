using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business;

/// <summary>
/// Owns the course material list, which doubles as the topic a question is filed under.
/// The file itself is written to disk by the controller before this service records it.
/// </summary>
public class CourseMaterialService(
    ICourseMaterialRepository materialRepository,
    ICatalogRepository catalogRepository) : ICourseMaterialService
{
    /// <summary>Longest stored file name, matching course_materials.file_name.</summary>
    public const int MaxFileNameLength = 255;

    public Task<ServiceResponse<IReadOnlyList<CourseMaterialResponse>>> ListAsync(
        int? lecturerId,
        int? courseId = null,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<CourseMaterialResponse>>(
            async () =>
            {
                var visibleCourseIds = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
                if (visibleCourseIds.Count == 0)
                {
                    return (IReadOnlyList<CourseMaterialResponse>)Array.Empty<CourseMaterialResponse>();
                }

                if (courseId is int requested && !visibleCourseIds.Contains(requested))
                {
                    throw new BusinessValidationException("Bạn không có quyền xem tài liệu của môn học này.");
                }

                // Listing by course is only worth the round trip when the caller picked one.
                var items = courseId.HasValue
                    ? await materialRepository.ListAsync(courseId, cancellationToken)
                    : await materialRepository.ListForCoursesAsync(visibleCourseIds, cancellationToken);

                return items
                    .Where(item => visibleCourseIds.Contains(item.CourseId))
                    .Select(MapMaterial)
                    .ToList();
            },
            "Không thể tải danh sách tài liệu.");
    }

    public Task<ServiceResponse<CourseMaterialResponse>> CreateAsync(
        CourseMaterialCreateRequest request,
        int uploaderId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);

                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                if (!await catalogRepository.CourseExistsAsync(courseId, cancellationToken))
                {
                    throw new BusinessValidationException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
                }

                var fileName = BusinessValidation.RequiredText(request.FileName, "tên tệp", MaxFileNameLength);
                var filePath = BusinessValidation.RequiredText(request.FilePath, "đường dẫn tệp", 1000);

                if (await materialRepository.FileNameExistsAsync(courseId, fileName, 0, cancellationToken))
                {
                    throw new BusinessValidationException(
                        $"Môn học này đã có tệp tên {fileName}. Hãy đổi tên hoặc xoá tệp cũ.");
                }

                var created = await materialRepository.SaveAsync(
                    new CourseMaterial
                    {
                        CourseId = courseId,
                        FileName = fileName,
                        FilePath = filePath,
                        FileType = request.FileType,
                        FileSize = request.FileSize,
                        UploadedBy = uploaderId,
                        ProcessingStatus = MaterialProcessingStatus.Completed,
                        UploadedAt = DateTime.Now
                    },
                    cancellationToken);

                // Re-read so the response carries the course code and uploader name the
                // project query fills in, instead of guessing them here.
                var saved = await materialRepository.GetAsync(created.MaterialId, cancellationToken);
                return saved is null
                    ? throw new BusinessValidationException("Không thể đọc lại tài liệu vừa lưu.")
                    : MapMaterial(saved);
            },
            "Không thể lưu tài liệu.");
    }

    public async Task<CourseMaterialResponse?> GetAsync(
        int materialId,
        CancellationToken cancellationToken = default)
    {
        if (materialId <= 0)
        {
            return null;
        }

        var material = await materialRepository.GetAsync(materialId, cancellationToken);
        return material is null ? null : MapMaterial(material);
    }

    public Task<ServiceResponse> DeleteAsync(
        int materialId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var material = await materialRepository.GetAsync(materialId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy tài liệu.");
                await EnsureCourseVisibleAsync(material.CourseId, lecturerId, cancellationToken);

                var questionCount = await materialRepository.CountQuestionsAsync(materialId, cancellationToken);
                if (questionCount > 0)
                {
                    throw new BusinessValidationException(
                        $"Tài liệu này đang được {questionCount} câu hỏi sử dụng. "
                        + "Hãy chuyển hoặc ẩn các câu hỏi đó trước khi xoá tài liệu.");
                }

                await materialRepository.DeleteAsync(materialId, cancellationToken);
            },
            "Không thể xoá tài liệu.");
    }

    private async Task<IReadOnlyList<int>> VisibleCourseIdsAsync(
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        var courses = await catalogRepository.ListActiveCoursesAsync(cancellationToken);
        return courses
            .Where(course => !lecturerId.HasValue || course.LecturerId == lecturerId)
            .Select(course => course.CourseId)
            .ToList();
    }

    private async Task EnsureCourseVisibleAsync(
        int courseId,
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        if (!lecturerId.HasValue)
        {
            return;
        }

        var allowed = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
        if (!allowed.Contains(courseId))
        {
            throw new BusinessValidationException("Bạn không có quyền thao tác tài liệu của môn học này.");
        }
    }

    private static CourseMaterialResponse MapMaterial(DataAccess.Contracts.CourseMaterialItem item) => new()
    {
        MaterialId = item.MaterialId,
        CourseId = item.CourseId,
        CourseCode = item.CourseCode,
        CourseName = item.CourseName,
        FileName = item.FileName,
        FilePath = item.FilePath,
        FileType = item.FileType,
        FileSize = item.FileSize,
        UploaderName = item.UploaderName,
        UploadedAt = item.UploadedAt,
        QuestionCount = item.QuestionCount
    };
}
