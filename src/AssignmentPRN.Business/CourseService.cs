using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business;

public record CourseSaveRequest(int CourseId, string CourseCode, string CourseName,
    string? Description, int LecturerId, bool IsActive);

public class CourseService(ICourseRepository repository, ICatalogRepository catalog)
{
    public Task<ServiceResponse<List<Course>>> ListAsync(int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () => (await repository.ListAsync(ct))
            .Where(x => lecturerId == null || x.LecturerId == lecturerId).ToList(), "Không tải được môn học.");

    public Task<ServiceResponse<Course>> GetAsync(int id, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () => await OwnedAsync(id, lecturerId, ct), "Không tải được môn học.");

    public Task<ServiceResponse<Course>> SaveAsync(CourseSaveRequest request, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var course = request.CourseId == 0 ? new Course { CreatedAt = DateTime.Now }
                : await OwnedAsync(request.CourseId, lecturerId, ct);
            var code = BusinessValidation.NormalizeCode(request.CourseCode, "mã môn", 50);
            if (await repository.CodeExistsAsync(code, request.CourseId, ct))
                throw new BusinessValidationException("Mã môn học đã tồn tại.");
            var owner = lecturerId ?? BusinessValidation.PositiveId(request.LecturerId, "giảng viên");
            if (!await catalog.UserIsInRoleAsync(owner, "Lecturer", ct))
                throw new BusinessValidationException("Giảng viên không hợp lệ hoặc đã ngừng hoạt động.");
            course.CourseCode = code;
            course.CourseName = BusinessValidation.RequiredText(request.CourseName, "tên môn", 200);
            course.Description = BusinessValidation.OptionalText(request.Description, "mô tả", 1000);
            course.LecturerId = owner;
            course.IsActive = request.IsActive;
            course.UpdatedAt = request.CourseId == 0 ? null : DateTime.Now;
            await repository.SaveAsync(course, ct);
            return course;
        }, "Không thể lưu môn học.");

    public Task<ServiceResponse> DeleteAsync(int id, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            await OwnedAsync(id, lecturerId, ct);
            await repository.DeleteAsync(id, ct);
        }, "Không thể xoá môn học.");

    private async Task<Course> OwnedAsync(int id, int? lecturerId, CancellationToken ct)
    {
        var course = await repository.GetAsync(id, ct)
            ?? throw new BusinessValidationException("Không tìm thấy môn học.");
        if (lecturerId.HasValue && course.LecturerId != lecturerId)
            throw new BusinessValidationException("Bạn không có quyền quản lý môn học này.");
        return course;
    }
}
