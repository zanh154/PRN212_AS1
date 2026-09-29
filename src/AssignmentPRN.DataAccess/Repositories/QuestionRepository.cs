using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public interface IQuestionRepository
{
    Task<IReadOnlyList<QuestionListItem>> ListAsync(
        QuestionQuery query,
        CancellationToken cancellationToken = default);

    Task<QuestionDetail?> GetDetailAsync(int questionId, CancellationToken cancellationToken = default);

    /// <summary>Creates the question when <see cref="QuestionUpsertInput.QuestionId"/> is 0, otherwise replaces its body and choices.</summary>
    Task<QuestionDetail> SaveAsync(QuestionUpsertInput input, int authorId, CancellationToken cancellationToken = default);

    /// <summary>Hides a question without touching exams that already reference it.</summary>
    Task ArchiveAsync(int questionId, CancellationToken cancellationToken = default);

    /// <summary>True when at least one exam slot already holds this question.</summary>
    Task<bool> IsAssignedToExamAsync(int questionId, CancellationToken cancellationToken = default);

    /// <summary>Identifiers of every approved question still free for the exam being built.</summary>
    Task<IReadOnlyList<int>> ListPoolIdsAsync(QuestionPickRequest request, CancellationToken cancellationToken = default);

    /// <summary>Identifiers of the candidates registered for the exam.</summary>
    Task<IReadOnlyList<int>> ListCandidateIdsAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Every question id already written to the exam, across all its candidates.</summary>
    Task<IReadOnlyList<int>> ListAssignedQuestionIdsAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Writes the dealt questions in one transaction, so a failure leaves the exam empty.</summary>
    Task AddExamQuestionsAsync(
        int examId,
        IReadOnlyList<ExamQuestionInput> rows,
        CancellationToken cancellationToken = default);

    /// <summary>Every candidate of the exam with the paper the bank dealt them.</summary>
    Task<IReadOnlyList<ExamPaperItem>> ListExamPaperAsync(
        int examId,
        CancellationToken cancellationToken = default);

    /// <summary>True once any slot of the exam has been asked or answered.</summary>
    Task<bool> HasStartedExamQuestionsAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Drops the exam's papers so they can be dealt again. Returns how many rows went.</summary>
    Task<int> ClearExamQuestionsAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>One slot with its session, for the exam room to check before letting a student in.</summary>
    Task<ExamRoomCandidate?> GetExamRoomCandidateAsync(
        int candidateId,
        CancellationToken cancellationToken = default);

    /// <summary>One candidate's paper as the student sees it, without the answer key.</summary>
    Task<IReadOnlyList<ExamRoomQuestion>> ListCandidateQuestionsAsync(
        int candidateId,
        CancellationToken cancellationToken = default);

    /// <summary>One completed paper with the student's choices and answer key.</summary>
    Task<IReadOnlyList<ExamResultQuestion>> ListCandidateResultsAsync(
        int candidateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the slot as being sat. Only the first entry records the clock, so refreshing
    /// the page does not restart the exam.
    /// </summary>
    Task StartCandidateAsync(int candidateId, DateTime startedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the candidate's choices and closes the slot. Answers are written per exam
    /// question, one row each, so a resubmit overwrites rather than piles up.
    /// </summary>
    Task SubmitAnswersAsync(
        int candidateId,
        IReadOnlyDictionary<int, int?> selectedOptionByExamQuestion,
        DateTime finishedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Which option ids the caller may legally pick, keyed by exam question id.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> ListAllowedOptionsAsync(
        int candidateId,
        CancellationToken cancellationToken = default);
}

public class QuestionRepository(AivesDbContext context) : IQuestionRepository
{
    public async Task<IReadOnlyList<QuestionListItem>> ListAsync(
        QuestionQuery query,
        CancellationToken cancellationToken = default)
    {
        var questions = context.Questions.AsNoTracking().AsQueryable();

        if (query.CourseId is int courseId)
        {
            questions = questions.Where(question => question.CourseId == courseId);
        }

        if (query.MaterialId is int materialId)
        {
            questions = questions.Where(question => question.SourceMaterialId == materialId);
        }

        if (query.Difficulty is QuestionDifficulty difficulty)
        {
            questions = questions.Where(question => question.Difficulty == difficulty);
        }

        if (query.BloomLevel is BloomLevel bloomLevel)
        {
            questions = questions.Where(question => question.BloomLevel == bloomLevel);
        }

        if (!query.IncludeArchived)
        {
            questions = questions.Where(question => question.Status != QuestionStatus.Archived);
        }

        var term = query.Term?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            questions = questions.Where(question =>
                question.QuestionText.Contains(term) || (question.ExpectedAnswer != null && question.ExpectedAnswer.Contains(term)));
        }

        return await questions
            .OrderBy(question => question.Course.CourseCode)
            .ThenByDescending(question => question.CreatedAt)
            .Select(question => new QuestionListItem
            {
                QuestionId = question.QuestionId,
                CourseId = question.CourseId,
                CourseCode = question.Course.CourseCode,
                CourseName = question.Course.CourseName,
                MaterialId = question.SourceMaterialId,
                MaterialName = question.SourceMaterial == null ? null : question.SourceMaterial.FileName,
                QuestionText = question.QuestionText,
                Difficulty = question.Difficulty,
                BloomLevel = question.BloomLevel,
                QuestionType = question.QuestionType,
                Status = question.Status,
                AuthorName = question.CreatedByUser.FullName,
                CreatedAt = question.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<QuestionDetail?> GetDetailAsync(int questionId, CancellationToken cancellationToken = default)
    {
        if (questionId <= 0)
        {
            return null;
        }

        var question = await context.Questions
            .AsNoTracking()
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.QuestionId == questionId, cancellationToken);

        return question is null ? null : MapDetail(question);
    }

    public async Task<QuestionDetail> SaveAsync(
        QuestionUpsertInput input,
        int authorId,
        CancellationToken cancellationToken = default)
    {
        var options = input.Options
            .Select((option, index) => new QuestionOption
            {
                OptionText = option.Text,
                IsCorrect = option.IsCorrect,
                DisplayOrder = index + 1
            })
            .ToList();

        if (input.QuestionId == 0)
        {
            var created = new Question
            {
                CourseId = input.CourseId,
                SourceMaterialId = input.MaterialId,
                CreatedBy = authorId,
                QuestionText = input.QuestionText,
                ExpectedAnswer = input.ExpectedAnswer,
                BloomLevel = input.BloomLevel,
                Difficulty = input.Difficulty,
                QuestionType = input.QuestionType,
                Status = QuestionStatus.Approved,
                CreatedAt = DateTime.Now,
                Options = options
            };

            context.Questions.Add(created);
            await context.SaveChangesAsync(cancellationToken);
            return (await GetDetailAsync(created.QuestionId, cancellationToken))!;
        }

        var existing = await context.Questions
            .Include(question => question.Options)
            .FirstOrDefaultAsync(question => question.QuestionId == input.QuestionId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

        // Replacing the collection removes the old rows and inserts the new ones, so
        // option identifiers are never reused for a different choice.
        context.QuestionOptions.RemoveRange(existing.Options);
        existing.CourseId = input.CourseId;
        existing.SourceMaterialId = input.MaterialId;
        existing.QuestionText = input.QuestionText;
        existing.ExpectedAnswer = input.ExpectedAnswer;
        existing.BloomLevel = input.BloomLevel;
        existing.Difficulty = input.Difficulty;
        existing.QuestionType = input.QuestionType;
        existing.UpdatedAt = DateTime.Now;
        existing.Options = options;

        await context.SaveChangesAsync(cancellationToken);
        return (await GetDetailAsync(existing.QuestionId, cancellationToken))!;
    }

    public async Task ArchiveAsync(int questionId, CancellationToken cancellationToken = default)
    {
        var question = await context.Questions.FindAsync([questionId], cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

        question.Status = QuestionStatus.Archived;
        question.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsAssignedToExamAsync(int questionId, CancellationToken cancellationToken = default) =>
        context.ExamQuestions.AnyAsync(item => item.QuestionId == questionId, cancellationToken);

    public async Task<IReadOnlyList<int>> ListPoolIdsAsync(
        QuestionPickRequest request,
        CancellationToken cancellationToken = default)
    {
        var pool = context.Questions
            .AsNoTracking()
            .Where(question => question.CourseId == request.CourseId
                && question.QuestionType == QuestionType.Main
                && question.Status == QuestionStatus.Approved);

        if (request.MaterialIds.Count > 0)
        {
            var materialIds = request.MaterialIds;
            pool = pool.Where(question => question.SourceMaterialId.HasValue
                && materialIds.Contains(question.SourceMaterialId.Value));
        }

        if (request.Difficulties.Count > 0)
        {
            var difficulties = request.Difficulties;
            pool = pool.Where(question => difficulties.Contains(question.Difficulty));
        }

        if (request.TakenQuestionIds.Count > 0)
        {
            var taken = request.TakenQuestionIds;
            pool = pool.Where(question => !taken.Contains(question.QuestionId));
        }

        return await pool
            .OrderBy(question => question.QuestionId)
            .Select(question => question.QuestionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListCandidateIdsAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await context.ExamCandidates
            .AsNoTracking()
            .Where(candidate => candidate.ExamId == examId)
            .OrderBy(candidate => candidate.CandidateId)
            .Select(candidate => candidate.CandidateId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> ListAssignedQuestionIdsAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.ExamId == examId)
            .Select(item => item.QuestionId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task AddExamQuestionsAsync(
        int examId,
        IReadOnlyList<ExamQuestionInput> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return;
        }

        // One SaveChanges is the transaction: either every exam slot lands or none does,
        // which keeps a candidate from ending up with half a paper.
        context.ExamQuestions.AddRange(rows.Select(row => new ExamQuestion
        {
            ExamId = examId,
            CandidateId = row.CandidateId,
            QuestionId = row.QuestionId,
            OrderNo = row.OrderNo,
            AskedAt = null,
            IsCompleted = false
        }));

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExamPaperItem>> ListExamPaperAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        // Every candidate is listed, dealt or not, so a half-finished assignment is visible
        // instead of silently looking like an exam with fewer students.
        var candidates = await context.ExamCandidates
            .AsNoTracking()
            .Where(candidate => candidate.ExamId == examId)
            .OrderBy(candidate => candidate.ScheduledTime)
            .ThenBy(candidate => candidate.CandidateId)
            .Select(candidate => new
            {
                candidate.CandidateId,
                candidate.Student.FullName,
                candidate.Student.Email,
                candidate.ScheduledTime
            })
            .ToListAsync(cancellationToken);

        var papers = await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.ExamId == examId)
            .OrderBy(item => item.OrderNo)
            .Select(item => new
            {
                item.CandidateId,
                Question = new ExamPaperQuestion
                {
                    ExamQuestionId = item.ExamQuestionId,
                    QuestionId = item.QuestionId,
                    OrderNo = item.OrderNo,
                    QuestionText = item.Question.QuestionText,
                    Difficulty = item.Question.Difficulty,
                    MaterialName = item.Question.SourceMaterial == null
                        ? null
                        : item.Question.SourceMaterial.FileName,
                    IsCompleted = item.IsCompleted
                }
            })
            .ToListAsync(cancellationToken);

        var byCandidate = papers
            .GroupBy(item => item.CandidateId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Question).ToList());

        return candidates
            .Select(candidate => new ExamPaperItem
            {
                CandidateId = candidate.CandidateId,
                StudentName = candidate.FullName,
                StudentEmail = candidate.Email,
                ScheduledTime = candidate.ScheduledTime,
                Questions = byCandidate.TryGetValue(candidate.CandidateId, out var questions)
                    ? questions
                    : []
            })
            .ToList();
    }

    public Task<bool> HasStartedExamQuestionsAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        context.ExamQuestions.AnyAsync(
            item => item.ExamId == examId && (item.IsCompleted || item.AskedAt != null),
            cancellationToken);

    public async Task<int> ClearExamQuestionsAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ExamQuestions
            .Where(item => item.ExamId == examId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return 0;
        }

        context.ExamQuestions.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    public Task<ExamRoomCandidate?> GetExamRoomCandidateAsync(
        int candidateId,
        CancellationToken cancellationToken = default) =>
        candidateId <= 0
            ? Task.FromResult<ExamRoomCandidate?>(null)
            : context.ExamCandidates
                .AsNoTracking()
                .Where(candidate => candidate.CandidateId == candidateId)
                .Select(candidate => new ExamRoomCandidate
                {
                    CandidateId = candidate.CandidateId,
                    ExamId = candidate.ExamId,
                    StudentId = candidate.StudentId,
                    ExamName = candidate.Session.ExamName,
                    CourseId = candidate.Session.CourseId,
                    CourseCode = candidate.Session.Course.CourseCode,
                    CourseName = candidate.Session.Course.CourseName,
                    LecturerName = candidate.Session.Lecturer.FullName,
                    ScheduledTime = candidate.ScheduledTime,
                    TimePerStudent = candidate.Session.TimePerStudent,
                    MainQuestionCount = candidate.Session.MainQuestionCount,
                    CandidateStatus = candidate.Status,
                    SessionStatus = candidate.Session.Status,
                    StartedAt = candidate.StartedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ExamRoomQuestion>> ListCandidateQuestionsAsync(
        int candidateId,
        CancellationToken cancellationToken = default) =>
        await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.CandidateId == candidateId)
            .OrderBy(item => item.OrderNo)
            .Select(item => new ExamRoomQuestion
            {
                ExamQuestionId = item.ExamQuestionId,
                OrderNo = item.OrderNo,
                QuestionText = item.Question.QuestionText,
                Difficulty = item.Question.Difficulty,
                // Only the id and the text of each choice are projected, so is_correct
                // never leaves the database on this path.
                Options = item.Question.Options
                    .OrderBy(option => option.DisplayOrder)
                    .Select(option => new ExamRoomOption
                    {
                        OptionId = option.OptionId,
                        Text = option.OptionText
                    })
                    .ToList(),
                SelectedOptionId = context.Answers
                    .Where(answer => answer.ExamQuestionId == item.ExamQuestionId)
                    .Select(answer => answer.SelectedOptionId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExamResultQuestion>> ListCandidateResultsAsync(
        int candidateId,
        CancellationToken cancellationToken = default) =>
        await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.CandidateId == candidateId)
            .OrderBy(item => item.OrderNo)
            .Select(item => new ExamResultQuestion
            {
                ExamQuestionId = item.ExamQuestionId,
                OrderNo = item.OrderNo,
                QuestionText = item.Question.QuestionText,
                Difficulty = item.Question.Difficulty,
                Options = item.Question.Options
                    .OrderBy(option => option.DisplayOrder)
                    .Select(option => new ExamResultOption
                    {
                        OptionId = option.OptionId,
                        Text = option.OptionText,
                        IsCorrect = option.IsCorrect
                    })
                    .ToList(),
                SelectedOptionId = context.Answers
                    .Where(answer => answer.ExamQuestionId == item.ExamQuestionId)
                    .Select(answer => answer.SelectedOptionId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

    public async Task SubmitAnswersAsync(
        int candidateId,
        IReadOnlyDictionary<int, int?> selectedOptionByExamQuestion,
        DateTime finishedAt,
        CancellationToken cancellationToken = default)
    {
        var slots = await context.ExamQuestions
            .Where(item => item.CandidateId == candidateId)
            .ToListAsync(cancellationToken);

        var existing = await context.Answers
            .Where(answer => answer.CandidateId == candidateId)
            .ToListAsync(cancellationToken);

        foreach (var slot in slots)
        {
            if (!selectedOptionByExamQuestion.TryGetValue(slot.ExamQuestionId, out var optionId))
            {
                continue;
            }

            var answer = existing.FirstOrDefault(item => item.ExamQuestionId == slot.ExamQuestionId);
            if (answer is null)
            {
                answer = new Answer
                {
                    ExamQuestionId = slot.ExamQuestionId,
                    CandidateId = candidateId,
                    CreatedAt = finishedAt,
                    StartedAt = slot.AskedAt
                };
                context.Answers.Add(answer);
            }

            answer.SelectedOptionId = optionId;
            answer.FinishedAt = finishedAt;

            slot.IsCompleted = optionId.HasValue;
            slot.AskedAt ??= finishedAt;
        }

        var candidate = await context.ExamCandidates
            .FirstOrDefaultAsync(item => item.CandidateId == candidateId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lượt thi.");

        candidate.Status = CandidateStatus.Completed;
        candidate.FinishedAt = finishedAt;

        // One SaveChanges: the paper, the answers and the slot close together or not at all.
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> ListAllowedOptionsAsync(
        int candidateId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.CandidateId == candidateId)
            .Select(item => new
            {
                item.ExamQuestionId,
                OptionIds = item.Question.Options.Select(option => option.OptionId).ToList()
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.ExamQuestionId,
            row => (IReadOnlyList<int>)row.OptionIds);
    }

    public async Task StartCandidateAsync(
        int candidateId,
        DateTime startedAt,
        CancellationToken cancellationToken = default)
    {
        var candidate = await context.ExamCandidates
            .FirstOrDefaultAsync(item => item.CandidateId == candidateId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lượt thi.");

        if (candidate.Status == CandidateStatus.Waiting)
        {
            candidate.Status = CandidateStatus.InProgress;
        }

        // Keep the first timestamp: a refresh must not look like a fresh start.
        candidate.StartedAt ??= startedAt;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static QuestionDetail MapDetail(Question question) => new()    {
        QuestionId = question.QuestionId,
        CourseId = question.CourseId,
        MaterialId = question.SourceMaterialId,
        QuestionText = question.QuestionText,
        ExpectedAnswer = question.ExpectedAnswer,
        BloomLevel = question.BloomLevel,
        Difficulty = question.Difficulty,
        QuestionType = question.QuestionType,
        Status = question.Status,
        Options = question.Options
            .OrderBy(option => option.DisplayOrder)
            .Select(option => new QuestionOptionDetail
            {
                OptionId = option.OptionId,
                Text = option.OptionText,
                IsCorrect = option.IsCorrect,
                DisplayOrder = option.DisplayOrder
            })
            .ToList()
    };
}
