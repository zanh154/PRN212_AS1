namespace AssignmentPRN.Business;

public interface IAuthService
{
    Task<AuthenticatedUserResponse?> LoginAsync(string email, string password);

    Task<AuthenticatedUserResponse?> GetUserByIdAsync(int userId);

    Task<(bool Success, string? Error, AuthenticatedUserResponse? User)> RegisterAsync(
        string fullName,
        string email,
        string password);
}

public interface IExamSessionService
{
    Task<ServiceResponse<ExamSessionDetailResponse>> UpdateAsync(AssignmentPRN.DataAccess.Contracts.ExamSessionUpdateInput request, int? lecturerId, CancellationToken cancellationToken = default);
    Task<ServiceResponse> ChangeStatusAsync(int examId, AssignmentPRN.DataAccess.Enums.ExamSessionStatus status, int? lecturerId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<AssignmentPRN.DataAccess.Contracts.ExamStudentSearchResult>> SearchExamStudentsAsync(
        AssignmentPRN.DataAccess.Contracts.ExamStudentSearch filter, int? lecturerId,
        CancellationToken cancellationToken = default);
    Task<ServiceResponse> RemoveStudentAsync(int examId, int candidateId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionDetailResponse>> AddStudentAsync(int examId, string email,
        DateTime scheduledTime, CancellationToken cancellationToken = default);

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

    /// <summary>Grades and returns a completed paper. The answer key is never exposed before completion.</summary>
    Task<ServiceResponse<ExamResultResponse>> GetExamResultAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the student's choices for the round being sat. Handing in the main round may
    /// deal a follow-up round (the slot stays In progress); otherwise, and after the
    /// follow-up round, the slot moves to Completed and the room becomes read-only.
    /// A handed-in round is final either way.
    /// </summary>
    Task<ServiceResponse<ExamRoomResponse>> SubmitExamAsync(
        int candidateId,
        int studentUserId,
        IReadOnlyDictionary<int, int?> selectedOptionByExamQuestion,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Results of a session for the examiner. A lecturer only sees the sessions they run;
/// pass a null lecturer id for an admin.
/// </summary>
public interface IExamResultService
{
    /// <summary>The score sheet of a session: one row per candidate plus a summary.</summary>
    Task<ServiceResponse<SessionResultResponse>> GetSessionResultsAsync(
        int examId,
        int? lecturerId,
        CancellationToken cancellationToken = default);

    /// <summary>One candidate's paper with the answer key and the examiner notes.</summary>
    Task<ServiceResponse<CandidateResultResponse>> GetCandidateResultAsync(
        int candidateId,
        int? lecturerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes every slot of the session whose time is over: no-shows become Absent, papers
    /// left open are completed as saved. See <see cref="OverdueSlotRules"/>.
    /// </summary>
    Task<ServiceResponse<OverdueSlotsResult>> CloseOverdueSlotsAsync(
        int examId,
        int? lecturerId,
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

public interface ICatalogService
{
    Task<ServiceResponse<IReadOnlyList<CourseResponse>>> ListActiveCoursesAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<PersonResponse>>> ListActiveUsersInRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default);
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
