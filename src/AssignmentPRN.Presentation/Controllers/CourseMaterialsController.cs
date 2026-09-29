using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Controllers;

/// <summary>
/// Course materials, which double as the topics a question can be filed under. The file
/// bytes are written into <c>wwwroot/materials</c> here; the row is created by
/// <see cref="CourseMaterialService"/>.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class CourseMaterialsController(
    ICourseMaterialService materialService,
    ICatalogRepository catalog,
    IWebHostEnvironment environment) : Controller
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;

    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    [HttpGet]
    public async Task<IActionResult> Index(int? courseId, CancellationToken ct)
    {
        var courses = await catalog.ListActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        var selected = courseId is int requested && allowed.Any(course => course.CourseId == requested)
            ? requested
            : (int?)null;

        var result = await materialService.ListAsync(LecturerId, selected, ct);
        var model = new CourseMaterialListViewModel
        {
            Materials = result.Data ?? [],
            CourseOptions = ToCourseOptions(courses),
            CourseId = selected,
            LoadError = result.Success ? null : result.Error
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Upload(int? courseId, CancellationToken ct)
    {
        var courses = await catalog.ListActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        var model = new CourseMaterialUploadViewModel
        {
            CourseId = courseId is int requested && allowed.Any(course => course.CourseId == requested)
                ? requested
                : allowed.FirstOrDefault()?.CourseId ?? 0
        };

        await OptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(MaterialFileRules.MaxBytes + (1024 * 1024))]
    public async Task<IActionResult> Upload(CourseMaterialUploadViewModel model, CancellationToken ct)
    {
        if (LecturerId.HasValue)
        {
            ModelState.Remove(nameof(model.CourseId));
        }

        var uploadedPath = (string?)null;

        if (ModelState.IsValid)
        {
            var file = model.File!;
            var fileType = MaterialFileRules.ResolveType(file.FileName);
            if (fileType is null)
            {
                ModelState.AddModelError(
                    nameof(model.File),
                    $"Chỉ nhận {string.Join(", ", MaterialFileRules.AcceptedExtensions)}.");
            }
            else if (file.Length > MaterialFileRules.MaxBytes)
            {
                ModelState.AddModelError(
                    nameof(model.File),
                    $"Tệp vượt quá {MaterialFileRules.MaxBytes / (1024 * 1024)} MB.");
            }
            else
            {
                uploadedPath = await SaveToWebRootAsync(file);
                var result = await materialService.CreateAsync(
                    new CourseMaterialCreateRequest
                    {
                        CourseId = model.CourseId,
                        // The row keeps the name the lecturer typed, because the CSV import
                        // refers to a topic by file name. Only the path on disk gets a
                        // collision-free name.
                        FileName = Path.GetFileName(file.FileName),
                        FilePath = uploadedPath,
                        FileType = fileType.Value,
                        FileSize = file.Length
                    },
                    CurrentUserId,
                    ct);

                if (result.Success)
                {
                    TempData["Success"] = "Đã tải lên tài liệu.";
                    return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
                }

                // The row failed, so the bytes on disk would be orphaned.
                DeleteFromWebRoot(uploadedPath);
                ModelState.AddModelError(string.Empty, result.Error!);
            }
        }

        await OptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await materialService.DeleteAsync(id, LecturerId, ct);
        if (result.Success)
        {
            var material = await materialService.GetAsync(id, ct);
            DeleteFromWebRoot(material?.FilePath);
        }

        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Đã xoá tài liệu."
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Opens the stored file so the lecturer can check what they uploaded.</summary>
    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var material = await materialService.GetAsync(id, ct);
        if (material is null)
        {
            TempData["Error"] = "Không tìm thấy tài liệu.";
            return RedirectToAction(nameof(Index));
        }

        var path = ResolveWebRootPath(material.FilePath);
        if (path is null || !System.IO.File.Exists(path))
        {
            TempData["Error"] = "Tệp không còn trên máy chủ.";
            return RedirectToAction(nameof(Index));
        }

        return PhysicalFile(path, ContentTypeFor(material.FileType), material.FileName);
    }

    private async Task<string> SaveToWebRootAsync(IFormFile file)
    {
        var folder = Path.Combine(environment.WebRootPath, MaterialFileRules.Folder);
        Directory.CreateDirectory(folder);

        // The caller already produced a collision-free name; Path.GetFileName is the
        // belt-and-braces guard against a crafted name escaping the folder.
        var name = Path.GetFileName($"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");
        var fullPath = Path.Combine(folder, name);

        await using var target = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(target, HttpContext.RequestAborted);

        return $"/{MaterialFileRules.Folder}/{name}";
    }

    private string? ResolveWebRootPath(string storedPath)
    {
        var relative = storedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(environment.WebRootPath);
        var full = Path.GetFullPath(Path.Combine(root, relative));

        // Refuse anything that resolves outside wwwroot, whatever is in the column.
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    private void DeleteFromWebRoot(string? storedPath)
    {
        if (string.IsNullOrEmpty(storedPath))
        {
            return;
        }

        var full = ResolveWebRootPath(storedPath);
        if (full is not null && System.IO.File.Exists(full))
        {
            System.IO.File.Delete(full);
        }
    }

    private async Task OptionsAsync(CourseMaterialUploadViewModel model, CancellationToken ct)
    {
        var courses = await catalog.ListActiveCoursesAsync(ct);
        model.CourseOptions = ToCourseOptions(courses);
    }

    private IReadOnlyList<SelectListItem> ToCourseOptions(IReadOnlyList<Course> courses) => CoursesFor(courses, LecturerId)
        .Select(course => new SelectListItem($"{course.CourseCode} · {course.CourseName}", course.CourseId.ToString()))
        .ToList();

    private IReadOnlyList<Course> CoursesFor(IReadOnlyList<Course> courses, int? lecturerId) => courses
        .Where(course => !lecturerId.HasValue || course.LecturerId == lecturerId)
        .OrderBy(course => course.CourseCode)
        .ToList();

    private static string ContentTypeFor(MaterialFileType type) => type switch
    {
        MaterialFileType.PDF => "application/pdf",
        MaterialFileType.DOCX =>
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        MaterialFileType.PPTX =>
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
    };
}
