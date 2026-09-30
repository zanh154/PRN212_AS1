using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class CoursesController(ICourseService service, ICatalogService catalog) : Controller
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await service.ListAsync(LecturerId, ct);
        if (!result.Success) TempData["Error"] = result.Error;
        return View(result.Data ?? []);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = new CourseViewModel { LecturerId = LecturerId ?? 0 };
        await Options(model, ct);
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null) { TempData["Error"] = result.Error; return RedirectToAction(nameof(Index)); }
        var course = result.Data;
        var model = new CourseViewModel { CourseId = course.CourseId, CourseCode = course.CourseCode,
            CourseName = course.CourseName, Description = course.Description, LecturerId = course.LecturerId, IsActive = course.IsActive };
        await Options(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CourseViewModel model, CancellationToken ct)
    {
        if (LecturerId.HasValue) { model.LecturerId = LecturerId.Value; ModelState.Remove(nameof(model.LecturerId)); }
        if (ModelState.IsValid)
        {
            var result = await service.SaveAsync(new(model.CourseId, model.CourseCode, model.CourseName,
                model.Description, model.LecturerId, model.IsActive), LecturerId, ct);
            if (result.Success) { TempData["Success"] = "Đã lưu môn học."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await Options(model, ct);
        return View("Edit", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã xoá môn học." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task Options(CourseViewModel model, CancellationToken ct)
    {
        var result = await catalog.ListActiveUsersInRoleAsync(RoleNames.Lecturer, ct);
        model.Lecturers = (result.Data ?? [])
            .Where(x => !LecturerId.HasValue || x.UserId == LecturerId)
            .Select(x => new SelectListItem(x.FullName, x.UserId.ToString()))
            .ToList();
    }
}
