using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business;

/// <summary>
/// Owns the question bank. A lecturer may only touch the questions of the courses they
/// teach, which is checked here rather than in the controller.
/// </summary>
public class QuestionService(
    IQuestionRepository questionRepository,
    ICatalogRepository catalogRepository,
    ICourseMaterialRepository courseMaterialRepository) : IQuestionService
{
    /// <summary>Longest question body, comfortably above the 65 000-byte TEXT limit is checked separately.</summary>
    public const int MaxQuestionTextLength = 4000;

    public const int MaxExpectedAnswerLength = 4000;

    public const int MaxOptionTextLength = 500;

    public const int MinOptions = 2;

    public const int MaxOptions = 8;

    public Task<ServiceResponse<IReadOnlyList<QuestionListItemResponse>>> ListAsync(
        int? lecturerId,
        int? courseId = null,
        int? materialId = null,
        QuestionDifficulty? difficulty = null,
        BloomLevel? bloomLevel = null,
        string? term = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<QuestionListItemResponse>>(
            async () =>
            {
                var allowedCourseIds = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
                if (allowedCourseIds.Count == 0)
                {
                    return (IReadOnlyList<QuestionListItemResponse>)Array.Empty<QuestionListItemResponse>();
                }

                if (courseId is int requested && !allowedCourseIds.Contains(requested))
                {
                    throw new BusinessValidationException("Bạn không có quyền xem câu hỏi của môn học này.");
                }

                var effectiveCourseId = courseId ?? (allowedCourseIds.Count == 1 ? allowedCourseIds[0] : null);

                // An admin sees every course, so no course filter is applied unless one was chosen.
                var items = await questionRepository.ListAsync(
                    new QuestionQuery
                    {
                        CourseId = effectiveCourseId,
                        MaterialId = materialId,
                        Difficulty = difficulty,
                        BloomLevel = bloomLevel,
                        Term = term,
                        IncludeArchived = includeArchived
                    },
                    cancellationToken);

                return items
                    .Where(item => allowedCourseIds.Contains(item.CourseId))
                    .Select(item => new QuestionListItemResponse
                    {
                        QuestionId = item.QuestionId,
                        CourseId = item.CourseId,
                        CourseCode = item.CourseCode,
                        MaterialId = item.MaterialId,
                        MaterialName = item.MaterialName,
                        QuestionText = item.QuestionText,
                        Difficulty = item.Difficulty,
                        BloomLevel = item.BloomLevel,
                        Status = item.Status,
                        AuthorName = item.AuthorName,
                        CreatedAt = item.CreatedAt
                    })
                    .ToList();
            },
            "Không thể tải ngân hàng câu hỏi.");
    }

    public Task<ServiceResponse<QuestionResponse>> GetAsync(
        int questionId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var detail = await questionRepository.GetDetailAsync(questionId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");

                await EnsureCourseVisibleAsync(detail.CourseId, lecturerId, cancellationToken);
                return MapQuestion(detail);
            },
            "Không thể tải câu hỏi.");
    }

    public Task<ServiceResponse<QuestionResponse>> SaveAsync(
        QuestionSaveRequest request,
        int lecturerId,
        int? ownerLecturerId,
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

                var body = BusinessValidation.RequiredText(request.QuestionText, "nội dung câu hỏi", MaxQuestionTextLength);
                var expectedAnswer = BusinessValidation.OptionalText(
                    request.ExpectedAnswer,
                    "đáp án mong đợi",
                    MaxExpectedAnswerLength);
                var options = NormaliseOptions(request.Options);

                if (request.QuestionId != 0)
                {
                    var existing = await questionRepository.GetDetailAsync(request.QuestionId, cancellationToken)
                        ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");
                    await EnsureCourseVisibleAsync(existing.CourseId, ownerLecturerId, cancellationToken);
                }

                if (request.MaterialId is int materialId)
                {
                    await EnsureMaterialBelongsToCourseAsync(materialId, courseId, cancellationToken);
                }

                var saved = await questionRepository.SaveAsync(
                    new QuestionUpsertInput
                    {
                        QuestionId = request.QuestionId,
                        CourseId = courseId,
                        MaterialId = request.MaterialId,
                        QuestionText = body,
                        ExpectedAnswer = expectedAnswer,
                        BloomLevel = request.BloomLevel,
                        Difficulty = request.Difficulty,
                        Options = options
                    },
                    lecturerId,
                    cancellationToken);

                return MapQuestion(saved);
            },
            "Không thể lưu câu hỏi.");
    }

    public Task<ServiceResponse> ArchiveAsync(
        int questionId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var detail = await questionRepository.GetDetailAsync(questionId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");
                await EnsureCourseVisibleAsync(detail.CourseId, lecturerId, cancellationToken);
                await questionRepository.ArchiveAsync(questionId, cancellationToken);
            },
            "Không thể ẩn câu hỏi.");
    }

    public Task<ServiceResponse<QuestionImportResult>> ImportAsync(
        int courseId,
        IReadOnlyList<QuestionImportRow> rows,
        int lecturerId,
        int? ownerLecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(rows);

                var targetCourseId = BusinessValidation.PositiveId(courseId, "môn học");
                await EnsureCourseVisibleAsync(targetCourseId, ownerLecturerId, cancellationToken);

                if (!await catalogRepository.CourseExistsAsync(targetCourseId, cancellationToken))
                {
                    throw new BusinessValidationException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
                }

                if (rows.Count == 0)
                {
                    throw new BusinessValidationException("File không có câu hỏi nào để nhập.");
                }

                var errors = new List<string>();
                var imported = 0;

                for (var index = 0; index < rows.Count; index++)
                {
                    var row = rows[index];

                    try
                    {
                        var body = BusinessValidation.RequiredText(
                            row.QuestionText,
                            "nội dung câu hỏi",
                            MaxQuestionTextLength);
                        var expectedAnswer = BusinessValidation.OptionalText(
                            row.ExpectedAnswer,
                            "đáp án mong đợi",
                            MaxExpectedAnswerLength);
                        var options = NormaliseOptions(row.Options);

                        if (row.MaterialId is int materialId)
                        {
                            await EnsureMaterialBelongsToCourseAsync(materialId, targetCourseId, cancellationToken);
                        }

                        await questionRepository.SaveAsync(
                            new QuestionUpsertInput
                            {
                                CourseId = targetCourseId,
                                MaterialId = row.MaterialId,
                                QuestionText = body,
                                ExpectedAnswer = expectedAnswer,
                                BloomLevel = row.BloomLevel,
                                Difficulty = row.Difficulty,
                                Options = options
                            },
                            lecturerId,
                            cancellationToken);

                        imported++;
                    }
                    catch (Exception exception) when (exception is BusinessValidationException or ArgumentException
                        or KeyNotFoundException)
                    {
                        // The row carries the file line it came from, so the message points
                        // at the source even when the failure happens here rather than
                        // while parsing.
                        errors.Add($"Dòng {row.SourceLine}: {exception.Message}");
                    }
                }

                return new QuestionImportResult
                {
                    Imported = imported,
                    Errors = errors
                };
            },
            "Không thể nhập câu hỏi từ file.");
    }

    public Task<ServiceResponse<QuestionAvailabilityResponse>> CheckAvailabilityAsync(
        QuestionPickRequest request,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var count = BusinessValidation.InRange(request.Count, 1, 50, "Số câu hỏi mỗi sinh viên");
                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                var pool = await questionRepository.ListPoolIdsAsync(
                    BuildPoolRequest(courseId, count, request),
                    cancellationToken);

                return new QuestionAvailabilityResponse
                {
                    Requested = count,
                    Available = QuestionPicker.CountAvailable(pool, request.TakenQuestionIds)
                };
            },
            "Không thể kiểm tra số câu hỏi của ngân hàng.");
    }

    public Task<ServiceResponse<IReadOnlyList<int>>> PickAsync(
        QuestionPickRequest request,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<int>>(
            async () =>
            {
                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                var count = BusinessValidation.InRange(request.Count, 1, 50, "Số câu hỏi mỗi sinh viên");
                var pool = await questionRepository.ListPoolIdsAsync(
                    BuildPoolRequest(courseId, count, request),
                    cancellationToken);

                var picked = QuestionPicker.Pick(pool, count, request.TakenQuestionIds);
                if (picked.Count < count)
                {
                    throw new BusinessValidationException(
                        $"Ngân hàng câu hỏi của môn này chỉ còn {picked.Count}/{count} câu chưa dùng. "
                        + "Hãy bổ sung câu hỏi trước khi tạo lịch thi.");
                }

                return picked;
            },
            "Không thể rút câu hỏi cho sinh viên.");
    }

    public Task<ServiceResponse<ExamQuestionAssignmentResult>> AssignToExamAsync(
        ExamQuestionAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);

                var examId = BusinessValidation.PositiveId(request.ExamId, "lịch thi");
                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                var countPerCandidate = BusinessValidation.InRange(
                    request.CountPerCandidate,
                    1,
                    50,
                    "Số câu hỏi mỗi sinh viên");

                var papers = await questionRepository.ListExamPaperAsync(examId, cancellationToken);
                if (papers.Count == 0)
                {
                    throw new BusinessValidationException(
                        "Lịch thi này chưa có sinh viên nào để phân câu hỏi.");
                }

                // The exam may already hold rows if the scheduler retried. Candidates that
                // already have a paper are skipped and their questions seed `taken`, so a
                // rerun tops up the missing students instead of doubling anyone's paper.
                var candidateIds = papers
                    .Where(paper => paper.Questions.Count == 0)
                    .Select(paper => paper.CandidateId)
                    .ToList();
                if (candidateIds.Count == 0)
                {
                    throw new BusinessValidationException(
                        "Mọi sinh viên của lịch thi này đã có đề. Hãy huỷ đề hiện tại trước khi phát lại.");
                }

                var taken = (await questionRepository.ListAssignedQuestionIdsAsync(examId, cancellationToken))
                    .ToHashSet();

                var needed = candidateIds.Count * countPerCandidate;
                var wholePool = await questionRepository.ListPoolIdsAsync(
                    BuildPoolRequest(courseId, countPerCandidate, request),
                    cancellationToken);
                if (QuestionPicker.CountAvailable(wholePool, taken) < needed)
                {
                    throw new BusinessValidationException(
                        $"Ngân hàng câu hỏi của môn này không đủ {needed} câu khác nhau cho "
                        + $"{candidateIds.Count} sinh viên. Hãy bổ sung câu hỏi trước khi phát đề.");
                }

                // Walking the candidates one by one and growing `taken` as we go is what
                // stops the same question reaching two students of this exam.
                var rows = new List<ExamQuestionInput>();
                foreach (var candidateId in candidateIds)
                {
                    var pool = await questionRepository.ListPoolIdsAsync(
                        new QuestionPickRequest
                        {
                            CourseId = courseId,
                            Count = countPerCandidate,
                            MaterialIds = request.MaterialIds,
                            Difficulties = request.Difficulties,
                            TakenQuestionIds = taken
                        },
                        cancellationToken);

                    var picked = QuestionPicker.Pick(pool, countPerCandidate, taken);
                    if (picked.Count < countPerCandidate)
                    {
                        throw new BusinessValidationException(
                            $"Ngân hàng câu hỏi đã cạn: sinh viên thứ {rows.Count / countPerCandidate + 1} "
                            + "không đủ câu chưa dùng. Hãy bổ sung câu hỏi trước khi phát đề.");
                    }

                    for (var index = 0; index < picked.Count; index++)
                    {
                        taken.Add(picked[index]);
                        rows.Add(new ExamQuestionInput
                        {
                            CandidateId = candidateId,
                            QuestionId = picked[index],
                            OrderNo = index + 1
                        });
                    }
                }

                await questionRepository.AddExamQuestionsAsync(examId, rows, cancellationToken);

                return new ExamQuestionAssignmentResult
                {
                    ExamId = examId,
                    CandidateCount = candidateIds.Count,
                    AssignedCount = rows.Count,
                    TakenQuestionIds = taken.OrderBy(id => id).ToList()
                };
            },
            "Không thể phân câu hỏi cho lịch thi.");
    }

    public Task<ServiceResponse<ExamRoomResponse>> EnterExamAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var candidate = await LoadSittableCandidateAsync(candidateId, studentUserId, cancellationToken);

                var existing = await questionRepository.ListCandidateQuestionsAsync(candidateId, cancellationToken);
                if (existing.Count == 0)
                {
                    await DealToCandidateAsync(candidate, cancellationToken);
                }

                await questionRepository.StartCandidateAsync(candidateId, DateTime.Now, cancellationToken);

                // Re-read: the slot now carries its paper and its start time.
                var opened = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

                return MapRoom(
                    opened,
                    await questionRepository.ListCandidateQuestionsAsync(candidateId, cancellationToken));
            },
            "Không thể vào phòng thi.");
    }

    public Task<ServiceResponse<ExamRoomResponse>> GetExamRoomAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var candidate = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

                if (candidate.StudentId != studentUserId)
                {
                    throw new BusinessValidationException("Đây không phải lượt thi của bạn.");
                }

                return MapRoom(
                    candidate,
                    await questionRepository.ListCandidateQuestionsAsync(candidateId, cancellationToken));
            },
            "Không thể mở phòng thi.");
    }

    public Task<ServiceResponse<ExamResultResponse>> GetExamResultAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var candidate = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

                if (candidate.StudentId != studentUserId)
                {
                    throw new BusinessValidationException("Đây không phải lượt thi của bạn.");
                }

                if (candidate.CandidateStatus != CandidateStatus.Completed)
                {
                    throw new BusinessValidationException("Kết quả chỉ hiển thị sau khi bạn đã nộp bài.");
                }

                return new ExamResultResponse
                {
                    CandidateId = candidate.CandidateId,
                    ExamName = candidate.ExamName,
                    CourseCode = candidate.CourseCode,
                    CourseName = candidate.CourseName,
                    LecturerName = candidate.LecturerName,
                    Questions = await questionRepository.ListCandidateResultsAsync(candidateId, cancellationToken)
                };
            },
            "Không thể tải kết quả bài thi.");
    }

    public Task<ServiceResponse<ExamRoomResponse>> SubmitExamAsync(
        int candidateId,
        int studentUserId,
        IReadOnlyDictionary<int, int?> selectedOptionByExamQuestion,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(selectedOptionByExamQuestion);

                await LoadSubmittableCandidateAsync(candidateId, studentUserId, cancellationToken);

                var allowed = await questionRepository.ListAllowedOptionsAsync(candidateId, cancellationToken);
                if (allowed.Count == 0)
                {
                    throw new BusinessValidationException("Bạn chưa vào ca thi này.");
                }

                // Each pick has to be a choice of the very question it was posted for, so a
                // tampered form cannot attach someone else's option to a slot.
                var cleaned = new Dictionary<int, int?>(allowed.Count);
                foreach (var (examQuestionId, optionIds) in allowed)
                {
                    if (!selectedOptionByExamQuestion.TryGetValue(examQuestionId, out var optionId))
                    {
                        cleaned[examQuestionId] = null;
                        continue;
                    }

                    if (optionId is int picked && !optionIds.Contains(picked))
                    {
                        throw new BusinessValidationException(
                            "Đáp án gửi lên không thuộc câu hỏi tương ứng.");
                    }

                    cleaned[examQuestionId] = optionId;
                }

                await questionRepository.SubmitAnswersAsync(
                    candidateId, cleaned, DateTime.Now, cancellationToken);

                var submitted = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

                return MapRoom(
                    submitted,
                    await questionRepository.ListCandidateQuestionsAsync(candidateId, cancellationToken));
            },
            "Không thể nộp bài.");
    }

    /// <summary>
    /// The gate in front of handing a paper in. Deliberately not the same as the gate in
    /// front of starting one: a student who ran out of time while answering still gets to
    /// hand in what they did, within <see cref="ExamSessionRules.SubmitGrace"/>.
    /// </summary>
    private async Task<ExamRoomCandidate> LoadSubmittableCandidateAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
            ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

        if (candidate.StudentId != studentUserId)
        {
            throw new BusinessValidationException("Đây không phải lượt thi của bạn.");
        }

        if (candidate.CandidateStatus == CandidateStatus.Completed)
        {
            throw new BusinessValidationException("Bạn đã nộp bài cho lượt thi này.");
        }

        if (candidate.CandidateStatus != CandidateStatus.InProgress)
        {
            throw new BusinessValidationException("Bạn chưa vào ca thi này.");
        }

        if (candidate.ScheduledTime is not DateTime scheduled)
        {
            throw new BusinessValidationException("Lượt thi này chưa được xếp giờ.");
        }

        var endTime = scheduled.AddMinutes(candidate.TimePerStudent);
        if (!ExamSessionRules.CanSubmit(DateTime.Now, scheduled, endTime))
        {
            throw new BusinessValidationException(
                $"Ca thi của bạn đã kết thúc lúc {endTime:HH:mm}, không nộp bài được nữa.");
        }

        return candidate;
    }

    /// <summary>
    /// The gate in front of the exam room: the slot has to belong to the caller, the
    /// session has to be running, and the clock has to be inside the slot.
    /// </summary>
    private async Task<ExamRoomCandidate> LoadSittableCandidateAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
            ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

        if (candidate.StudentId != studentUserId)
        {
            throw new BusinessValidationException("Đây không phải lượt thi của bạn.");
        }

        if (candidate.CandidateStatus is CandidateStatus.Cancelled or CandidateStatus.Absent)
        {
            throw new BusinessValidationException("Lượt thi này đã bị huỷ hoặc bạn được ghi nhận vắng thi.");
        }

        if (candidate.CandidateStatus == CandidateStatus.Completed)
        {
            throw new BusinessValidationException("Bạn đã hoàn thành lượt thi này.");
        }

        if (!ExamSessionRules.CanSit(candidate.SessionStatus))
        {
            throw new BusinessValidationException(
                "Phiên thi chưa mở hoặc đã kết thúc, chưa thể vào thi.");
        }

        if (candidate.ScheduledTime is not DateTime scheduled)
        {
            throw new BusinessValidationException("Lượt thi này chưa được xếp giờ.");
        }

        var now = DateTime.Now;
        if (!ExamSessionRules.IsSlotOpen(now, scheduled, candidate.TimePerStudent))
        {
            throw new BusinessValidationException(
                now < scheduled
                    ? $"Chưa đến giờ thi. Ca của bạn bắt đầu lúc {scheduled:HH:mm} ngày {scheduled:dd/MM}."
                    : $"Ca thi của bạn đã kết thúc lúc {scheduled.AddMinutes(candidate.TimePerStudent):HH:mm}.");
        }

        return candidate;
    }

    /// <summary>
    /// Deals one student's paper. Everything already handed out inside this session is
    /// excluded, which is what keeps two students of one session off the same question.
    /// </summary>
    private async Task DealToCandidateAsync(
        ExamRoomCandidate candidate,
        CancellationToken cancellationToken)
    {
        var count = BusinessValidation.InRange(
            candidate.MainQuestionCount, 1, 50, "Số câu hỏi mỗi sinh viên");

        var taken = (await questionRepository.ListAssignedQuestionIdsAsync(candidate.ExamId, cancellationToken))
            .ToHashSet();

        var pool = await questionRepository.ListPoolIdsAsync(
            new QuestionPickRequest
            {
                CourseId = candidate.CourseId,
                Count = count,
                TakenQuestionIds = taken
            },
            cancellationToken);

        var picked = QuestionPicker.Pick(pool, count, taken);
        if (picked.Count < count)
        {
            throw new BusinessValidationException(
                $"Ngân hàng câu hỏi của môn {candidate.CourseCode} không còn đủ {count} câu chưa dùng "
                + "cho lượt thi này. Hãy báo giảng viên bổ sung câu hỏi.");
        }

        await questionRepository.AddExamQuestionsAsync(
            candidate.ExamId,
            picked
                .Select((questionId, index) => new ExamQuestionInput
                {
                    CandidateId = candidate.CandidateId,
                    QuestionId = questionId,
                    OrderNo = index + 1
                })
                .ToList(),
            cancellationToken);
    }

    private static ExamRoomResponse MapRoom(
        ExamRoomCandidate candidate,
        IReadOnlyList<ExamRoomQuestion> questions) => new()
    {
        CandidateId = candidate.CandidateId,
        ExamId = candidate.ExamId,
        ExamName = candidate.ExamName,
        CourseCode = candidate.CourseCode,
        CourseName = candidate.CourseName,
        LecturerName = candidate.LecturerName,
        ScheduledTime = candidate.ScheduledTime ?? default,
        EndTime = (candidate.ScheduledTime ?? default).AddMinutes(candidate.TimePerStudent),
        TimePerStudent = candidate.TimePerStudent,
        CandidateStatus = candidate.CandidateStatus,
        StartedAt = candidate.StartedAt,
        Questions = questions,
        CanAnswer = candidate.CandidateStatus == CandidateStatus.InProgress
            && candidate.ScheduledTime is DateTime slot
            && ExamSessionRules.IsSlotOpen(DateTime.Now, slot, candidate.TimePerStudent),
        SecondsRemaining = RemainingSeconds(candidate)
    };

    /// <summary>Whole seconds left in the slot, never negative.</summary>
    private static int RemainingSeconds(ExamRoomCandidate candidate)
    {
        if (candidate.ScheduledTime is not DateTime scheduled)
        {
            return 0;
        }

        var left = scheduled.AddMinutes(candidate.TimePerStudent) - DateTime.Now;
        return left <= TimeSpan.Zero ? 0 : (int)left.TotalSeconds;
    }

    public Task<ServiceResponse<IReadOnlyList<ExamPaperItem>>> GetExamPaperAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<ExamPaperItem>>(
            async () =>
            {
                var id = BusinessValidation.PositiveId(examId, "lịch thi");
                return await questionRepository.ListExamPaperAsync(id, cancellationToken);
            },
            "Không thể tải đề thi đã phát.");
    }

    public Task<ServiceResponse> ClearExamAssignmentAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var id = BusinessValidation.PositiveId(examId, "lịch thi");

                // Once a question has been put to a student the paper is part of the exam
                // record, so it is frozen rather than reshuffled underneath them.
                if (await questionRepository.HasStartedExamQuestionsAsync(id, cancellationToken))
                {
                    throw new BusinessValidationException(
                        "Đã có sinh viên bắt đầu trả lời, không thể huỷ đề của lịch thi này.");
                }

                await questionRepository.ClearExamQuestionsAsync(id, cancellationToken);
            },
            "Không thể huỷ đề đã phát.");
    }

    /// <summary>
    /// Copies the filter across and pins the count to the range a request may carry,
    /// so <see cref="QuestionPickRequest.Count"/> cannot smuggle in a negative value.
    /// </summary>
    private static QuestionPickRequest BuildPoolRequest(
        int courseId,
        int count,
        QuestionPickRequest request) => new()
    {
        CourseId = courseId,
        Count = count,
        MaterialIds = request.MaterialIds,
        Difficulties = request.Difficulties,
        TakenQuestionIds = request.TakenQuestionIds
    };

    /// <summary>Same filter, applied to the exam assignment request.</summary>
    private static QuestionPickRequest BuildPoolRequest(
        int courseId,
        int count,
        ExamQuestionAssignmentRequest request) => new()
    {
        CourseId = courseId,
        Count = count,
        MaterialIds = request.MaterialIds,
        Difficulties = request.Difficulties
    };

    /// <summary>
    /// Trims the choices, drops the blank ones and enforces the multiple-choice shape:
    /// at least two choices and exactly one correct answer.
    /// </summary>
    private static IReadOnlyList<QuestionOptionInput> NormaliseOptions(
        IReadOnlyList<QuestionOptionInput> options)
    {
        if (options is null || options.Count == 0)
        {
            throw new BusinessValidationException("Câu hỏi cần ít nhất 2 phương án trả lời.");
        }

        if (options.Count > MaxOptions)
        {
            throw new BusinessValidationException($"Câu hỏi chỉ được có tối đa {MaxOptions} phương án.");
        }

        var cleaned = new List<QuestionOptionInput>(options.Count);
        foreach (var option in options)
        {
            var text = option.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (text.Length > MaxOptionTextLength)
            {
                throw new BusinessValidationException(
                    $"Phương án không được vượt quá {MaxOptionTextLength} ký tự.");
            }

            cleaned.Add(new QuestionOptionInput { Text = text, IsCorrect = option.IsCorrect });
        }

        if (cleaned.Count < MinOptions)
        {
            throw new BusinessValidationException($"Câu hỏi cần ít nhất {MinOptions} phương án trả lời.");
        }

        if (cleaned.Count(item => item.IsCorrect) != 1)
        {
            throw new BusinessValidationException("Câu hỏi phải có đúng một phương án đúng.");
        }

        return cleaned;
    }

    private async Task EnsureMaterialBelongsToCourseAsync(
        int materialId,
        int courseId,
        CancellationToken cancellationToken)
    {
        var material = await courseMaterialRepository.GetAsync(materialId, cancellationToken);
        if (material is null || material.CourseId != courseId)
        {
            throw new BusinessValidationException("Tài liệu được chọn không thuộc môn học này.");
        }
    }

    /// <summary>Course ids the caller may act on: all of them for an admin, their own for a lecturer.</summary>
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
            throw new BusinessValidationException("Bạn không có quyền thao tác câu hỏi của môn học này.");
        }
    }

    private static QuestionResponse MapQuestion(QuestionDetail detail) => new()
    {
        QuestionId = detail.QuestionId,
        CourseId = detail.CourseId,
        MaterialId = detail.MaterialId,
        QuestionText = detail.QuestionText,
        ExpectedAnswer = detail.ExpectedAnswer,
        BloomLevel = detail.BloomLevel,
        Difficulty = detail.Difficulty,
        Status = detail.Status,
        Options = detail.Options
            .Select(option => new QuestionOptionResponse
            {
                Text = option.Text,
                IsCorrect = option.IsCorrect
            })
            .ToList()
    };
}
