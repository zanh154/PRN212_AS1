using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.Models;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

/// <summary>
/// Where a student sits their own slot. The paper is dealt the moment they open it, so a
/// session never depends on the lecturer remembering to hand questions out beforehand.
/// </summary>
[SessionAuthorize(RoleNames.Student)]
public class ExamRoomController(IQuestionService questionService) : Controller
{
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    /// <summary>
    /// Opens the slot. This deals the paper and starts the clock, so it is a POST: a
    /// crawler or a refresh must not be able to trigger it.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(int id, CancellationToken ct)
    {
        var result = await questionService.EnterExamAsync(id, CurrentUserId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Mine", "StudentSchedule");
        }

        // Redirect after the write, so refreshing the room is a plain read.
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Index(int id, CancellationToken ct)
    {
        var result = await questionService.GetExamRoomAsync(id, CurrentUserId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Mine", "StudentSchedule");
        }

        // Reaching the room without a paper means the slot was never opened; send the
        // student back rather than showing an empty exam.
        if (result.Data.Questions.Count == 0)
        {
            TempData["Error"] = "Bạn chưa vào ca thi này. Hãy bấm \"Vào thi\" ở lịch thi.";
            return RedirectToAction("Mine", "StudentSchedule");
        }

        return View(new ExamRoomViewModel { Room = result.Data });
    }
}
