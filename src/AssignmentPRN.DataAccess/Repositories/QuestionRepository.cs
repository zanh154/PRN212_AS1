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
                QuestionType = QuestionType.Main,
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

    private static QuestionDetail MapDetail(Question question) => new()    {
        QuestionId = question.QuestionId,
        CourseId = question.CourseId,
        MaterialId = question.SourceMaterialId,
        QuestionText = question.QuestionText,
        ExpectedAnswer = question.ExpectedAnswer,
        BloomLevel = question.BloomLevel,
        Difficulty = question.Difficulty,
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
