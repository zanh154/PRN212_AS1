namespace AssignmentPRN.DataAccess.Common;

/// <summary>Raised when a slot overlaps another slot of the same student or lecturer.</summary>
public sealed class ExamScheduleConflictException(string message) : Exception(message);
