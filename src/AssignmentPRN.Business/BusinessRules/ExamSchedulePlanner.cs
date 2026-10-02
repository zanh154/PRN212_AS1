using AssignmentPRN.Business.Interfaces;
namespace AssignmentPRN.Business.BusinessRules;

public sealed record ScheduleSlot(int StudentId, DateTime StartTime, DateTime EndTime);

/// <summary>
/// Turns "who sits the exam, from when, how long each" into back-to-back slots:
/// each student starts exactly when the previous one finishes.
/// </summary>
public static class ExamSchedulePlanner
{
    public const int MinTimePerStudent = 1;

    public const int MaxTimePerStudent = 1440;

    public static IReadOnlyList<ScheduleSlot> Generate(
        DateTime firstStartTime,
        IEnumerable<int> studentIds,
        int timePerStudentMinutes)
    {
        ArgumentNullException.ThrowIfNull(studentIds);

        if (timePerStudentMinutes is < MinTimePerStudent or > MaxTimePerStudent)
        {
            throw new ArgumentException(
                $"Thời lượng mỗi sinh viên phải từ {MinTimePerStudent} đến {MaxTimePerStudent} phút.");
        }

        var ids = studentIds.ToList();
        if (ids.Count == 0)
        {
            throw new ArgumentException("Phải có ít nhất một sinh viên.");
        }

        var seen = new HashSet<int>();
        var currentStart = firstStartTime;
        var slots = new List<ScheduleSlot>(ids.Count);

        foreach (var studentId in ids)
        {
            if (studentId <= 0)
            {
                throw new ArgumentException("Mã sinh viên không hợp lệ.");
            }

            if (!seen.Add(studentId))
            {
                throw new ArgumentException("Mỗi sinh viên chỉ được xuất hiện một lần.");
            }

            var endTime = currentStart.AddMinutes(timePerStudentMinutes);
            slots.Add(new ScheduleSlot(studentId, currentStart, endTime));
            currentStart = endTime;
        }

        return slots;
    }

    /// <summary>Two half-open intervals overlap when each one starts before the other ends.</summary>
    public static bool Overlaps(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd)
    {
        if (firstEnd <= firstStart || secondEnd <= secondStart)
        {
            throw new ArgumentException("Khung giờ phải có thời điểm kết thúc sau thời điểm bắt đầu.");
        }

        return firstStart < secondEnd && firstEnd > secondStart;
    }
}
