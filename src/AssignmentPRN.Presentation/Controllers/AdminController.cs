using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize("Admin")]
public class AdminController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
