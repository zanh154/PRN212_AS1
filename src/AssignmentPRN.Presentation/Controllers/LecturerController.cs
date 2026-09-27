using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize("Lecturer")]
public class LecturerController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
