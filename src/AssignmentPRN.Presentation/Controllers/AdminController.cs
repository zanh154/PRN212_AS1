using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Admin)]
public class AdminController(IExamSessionService examSessionService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await DashboardBuilder.BuildStaffAsync(examSessionService, cancellationToken));
    }
}
