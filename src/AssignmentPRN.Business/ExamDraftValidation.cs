using AssignmentPRN.DataAccess.Contracts;

namespace AssignmentPRN.Business;

/// <summary>Draft authorization and round rules; runs on the transaction's current snapshot.</summary>
internal static class ExamDraftValidation
{
    internal static IReadOnlyDictionary<int, int?> Validate(
        ExamDraftState? state, int studentId, IReadOnlyDictionary<int, int?> answers, DateTime now)
    {
        if (state is null) throw new BusinessValidationException("Không tìm thấy lượt thi.");
        if (state.StudentId != studentId || state.CandidateStatus.ToBusiness() != CandidateStatus.InProgress
            || !ExamSessionRules.CanSit(state.SessionStatus.ToBusiness()))
            throw new BusinessValidationException("Bạn không được lưu đáp án cho lượt thi này.");
        if (state.ScheduledTime is not DateTime start || !ExamSessionRules.IsSlotOpen(now, start, state.TimePerStudent))
            throw new BusinessValidationException("Đã hết giờ làm bài, không thể lưu tạm.");

        var followUp = state.Questions.Any(x => x.IsFollowUp);
        var open = state.Questions.Where(x => x.IsFollowUp == followUp).ToDictionary(x => x.ExamQuestionId);
        foreach (var (id, option) in answers)
        {
            if (!open.TryGetValue(id, out var question) || (option.HasValue && !question.OptionIds.Contains(option.Value)))
                throw new BusinessValidationException("Câu hỏi hoặc đáp án không thuộc vòng thi hiện tại.");
            if (question.IsSubmitted)
                throw new BusinessValidationException("Vòng đã nộp không được sửa.");
        }
        return answers;
    }
}
