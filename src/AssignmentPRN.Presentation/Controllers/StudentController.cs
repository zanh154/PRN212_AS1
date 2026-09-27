using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize("Student")]
public class StudentController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
