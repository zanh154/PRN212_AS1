using AssignmentPRN.DataAccess.Entities;

namespace AssignmentPRN.Business;

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password);

    Task<User?> GetUserByIdAsync(int userId);

    Task<(bool Success, string? Error, User? User)> RegisterAsync(
        string fullName,
        string email,
        string password);
}

public interface IExamSessionService
{
    Task<ServiceResponse<ExamSessionDetailResponse>> UpdateAsync(AssignmentPRN.DataAccess.Contracts.ExamSessionUpdateInput request, int? lecturerId, CancellationToken cancellationToken = default);
    Task<ServiceResponse> ChangeStatusAsync(int examId, AssignmentPRN.DataAccess.Enums.ExamSessionStatus status, int? lecturerId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<IReadOnlyList<ExamSessionListItemResponse>>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionDetailResponse>> GetAsync(
        int examId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionOptionsResponse>> GetCreationOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ClassRosterResponse>> GetClassRosterAsync(
        int classId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionDetailResponse>> CreateAsync(
        ExamSessionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionDetailResponse>> RescheduleAsync(
        ExamSessionRescheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> DeleteAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Lists student accounts, optionally filtering by name or email.</summary>
    Task<ServiceResponse<IReadOnlyList<PersonResponse>>> SearchStudentsAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the exam schedule of one student account.</summary>
    Task<ServiceResponse<StudentScheduleResponse>> GetStudentScheduleAsync(
        int studentUserId,
        CancellationToken cancellationToken = default);
}

public interface IQuestionService
{
    /// <summary>Lists questions the lecturer is allowed to see; pass a null lecturer id for an admin.</summary>
    Task<ServiceResponse<IReadOnlyList<QuestionListItemResponse>>> ListAsync(
        int? lecturerId,
        int? courseId = null,
        int? materialId = null,
        AssignmentPRN.DataAccess.Enums.QuestionDifficulty? difficulty = null,
        AssignmentPRN.DataAccess.Enums.BloomLevel? bloomLevel = null,
        string? term = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<QuestionResponse>> GetAsync(
        int questionId,
        int? lecturerId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<QuestionResponse>> SaveAsync(
        QuestionSaveRequest request,
        int lecturerId,
        int? ownerLecturerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retires a question. Once an exam holds it, the question is only archived so the
    /// exam history stays readable.
    /// </summary>
    Task<ServiceResponse> ArchiveAsync(int questionId, int? lecturerId, CancellationToken cancellationToken = default);

    /// <summary>Bulk-creates questions from rows produced by the CSV import.</summary>
    Task<ServiceResponse<QuestionImportResult>> ImportAsync(
        int courseId,
        IReadOnlyList<QuestionImportRow> rows,
        int lecturerId,
        int? ownerLecturerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deals a paper to every candidate of a freshly created exam in one call, so a
    /// question is never repeated between two students of the same session. This is what
    /// the exam scheduler calls once the session and its roster exist.
    /// </summary>
    Task<ServiceResponse<AssignmentPRN.DataAccess.Contracts.ExamQuestionAssignmentResult>> AssignToExamAsync(
        AssignmentPRN.DataAccess.Contracts.ExamQuestionAssignmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports whether the bank can supply <paramref name="count"/> distinct questions per
    /// student, so an exam is warned about a thin bank before it is created.
    /// </summary>
    Task<ServiceResponse<QuestionAvailabilityResponse>> CheckAvailabilityAsync(
        AssignmentPRN.DataAccess.Contracts.QuestionPickRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Picks one student's paper. Questions already passed in
    /// <see cref="AssignmentPRN.DataAccess.Contracts.QuestionPickRequest.TakenQuestionIds"/>
    /// are never handed out again, so two students of the same exam never share a question.
    /// </summary>
    Task<ServiceResponse<IReadOnlyList<int>>> PickAsync(
        AssignmentPRN.DataAccess.Contracts.QuestionPickRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads back the papers of an exam, one per candidate, so the lecturer can review
    /// what the randomiser handed out before the session starts.
    /// </summary>
    Task<ServiceResponse<IReadOnlyList<AssignmentPRN.DataAccess.Contracts.ExamPaperItem>>> GetExamPaperAsync(
        int examId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws away the papers of an exam so they can be dealt again with another
    /// configuration. Refused once a student has been asked a question.
    /// </summary>
    Task<ServiceResponse> ClearExamAssignmentAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a slot for the student sitting it. The paper is dealt here, on first entry,
    /// drawn from the questions nobody else in this session has been given. A student who
    /// comes back to the page keeps the paper they already hold.
    /// </summary>
    Task<ServiceResponse<ExamRoomResponse>> EnterExamAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a slot the student has already opened, without dealing anything or touching
    /// the clock, so a refresh is free of side effects.
    /// </summary>
    Task<ServiceResponse<ExamRoomResponse>> GetExamRoomAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default);
}

public interface ICourseMaterialService
{
    Task<ServiceResponse<IReadOnlyList<CourseMaterialResponse>>> ListAsync(
        int? lecturerId,
        int? courseId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a material row for a file that has already been written to disk.</summary>
    Task<ServiceResponse<CourseMaterialResponse>> CreateAsync(
        CourseMaterialCreateRequest request,
        int uploaderId,
        CancellationToken cancellationToken = default);

    /// <summary>One material by id, or null when the caller may not see it.</summary>
    Task<CourseMaterialResponse?> GetAsync(int materialId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fixes a material uploaded against the wrong course, and renames it. Moving a
    /// material that questions already point at is refused, because a question and its
    /// topic have to stay inside the same course.
    /// </summary>
    Task<ServiceResponse<CourseMaterialResponse>> UpdateAsync(
        CourseMaterialUpdateRequest request,
        int? lecturerId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> DeleteAsync(int materialId, int? lecturerId, CancellationToken cancellationToken = default);
}

public sealed class CourseMaterialUpdateRequest
{
    public int MaterialId { get; init; }

    /// <summary>Course the material should belong to after the edit.</summary>
    public int CourseId { get; init; }

    /// <summary>
    /// Display name, and the key the CSV import matches a topic by. The extension has to
    /// stay as it is: it is what <c>file_type</c> was recorded from.
    /// </summary>
    public string FileName { get; init; } = string.Empty;
}

public sealed class CourseMaterialCreateRequest
{
    public int CourseId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public AssignmentPRN.DataAccess.Enums.MaterialFileType FileType { get; init; }

    public long? FileSize { get; init; }
}
