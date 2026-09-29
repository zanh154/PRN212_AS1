using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class ExamSessionsController(IExamSessionService examSessionService) : Controller
{
    private int? CurrentLecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;

    private bool CanManage(ExamSessionDetailResponse session) => !CurrentLecturerId.HasValue || session.Lecturer.UserId == CurrentLecturerId;

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var result = await examSessionService.GetAsync(id, cancellationToken);
        if (result.Data is null) return NotFound();
        if (!CanManage(result.Data)) return StatusCode(403);
        var item = result.Data;
        if (!ExamSessionRules.CanEdit(item.Status))
        {
            TempData["Error"] = "Phiên thi không còn được phép chỉnh sửa.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var model = new ExamSessionEditViewModel { ExamId = id, CourseId = item.Course.CourseId,
            ExamName = item.ExamName, Description = item.Description, StartTime = item.StartTime,
            TimePerStudent = item.TimePerStudent, MainQuestionCount = item.MainQuestionCount, MaxFollowUpCount = item.MaxFollowUpCount };
        await EditOptions(model, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ExamSessionEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.ExamId) return BadRequest();
        var current = await examSessionService.GetAsync(id, cancellationToken);
        if (current.Data is null) return NotFound();
        if (!CanManage(current.Data)) return StatusCode(403);
        if (ModelState.IsValid)
        {
            var result = await examSessionService.UpdateAsync(new ExamSessionUpdateInput {
                ExamId = id, CourseId = model.CourseId, ExamName = model.ExamName, Description = model.Description,
                StartTime = model.StartTime!.Value, TimePerStudent = model.TimePerStudent,
                MainQuestionCount = model.MainQuestionCount, MaxFollowUpCount = model.MaxFollowUpCount
            }, CurrentLecturerId, cancellationToken);
            if (result.Success)
            {
                TempData["Success"] = "Đã cập nhật phiên thi.";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await EditOptions(model, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, ExamSessionStatus status, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await examSessionService.ChangeStatusAsync(id, status, CurrentLecturerId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã cập nhật trạng thái phiên thi." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task EditOptions(ExamSessionEditViewModel model, CancellationToken cancellationToken)
    {
        var options = await examSessionService.GetCreationOptionsAsync(cancellationToken);
        model.Courses = options.Data?.Courses.Select(x => new SelectListItem(x.Label, x.Id.ToString())).ToList() ?? [];
        var current = await examSessionService.GetAsync(model.ExamId, cancellationToken);
        if (current.Data is not null && CanManage(current.Data) && !model.Courses.Any(x => x.Value == current.Data.Course.CourseId.ToString()))
            model.Courses.Add(new SelectListItem($"{current.Data.Course.CourseCode} · {current.Data.Course.CourseName} (ngừng hoạt động)", current.Data.Course.CourseId.ToString()));
        if (!options.Success) ModelState.AddModelError(string.Empty, options.Error!);
    }
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var response = await examSessionService.ListAsync(cancellationToken);

        return View(new ExamSessionListViewModel
        {
            Sessions = response.Data?.Where(x => !CurrentLecturerId.HasValue || x.LecturerId == CurrentLecturerId).ToList()
                ?? new List<ExamSessionListItemResponse>(),
            LoadError = response.Success ? null : response.Error
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var response = await examSessionService.GetAsync(id, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            TempData["Error"] = response.Error;
            return RedirectToAction(nameof(Index));
        }

        if (!CanManage(response.Data)) return StatusCode(403);
        return View(new ExamSessionDetailViewModel { Session = response.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new ExamSessionCreateViewModel();

        // A lecturer schedules their own exams, so preselect their account.
        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer)
        {
            model.LecturerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        }

        await LoadOptionsAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExamSessionCreateViewModel model, CancellationToken cancellationToken)
    {
        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer)
        {
            model.LecturerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var response = await examSessionService.CreateAsync(
            new ExamSessionCreateRequest
            {
                CourseId = model.CourseId,
                LecturerId = model.LecturerId,
                ClassId = model.ClassId,
                ExamName = model.ExamName,
                Description = model.Description,
                StartTime = model.StartTime!.Value,
                TimePerStudent = model.TimePerStudent,
                MainQuestionCount = model.MainQuestionCount,
                MaxFollowUpCount = model.MaxFollowUpCount
            },
            cancellationToken);

        if (!response.Success || response.Data is null)
        {
            AddErrors(response.Errors, response.Error);
            await LoadOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Đã tạo lịch thi và sinh khung giờ tự động.";
        return RedirectToAction(nameof(Details), new { id = response.Data.ExamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(
        ExamSessionRescheduleViewModel model,
        CancellationToken cancellationToken)
    {
        var session = await examSessionService.GetAsync(model.ExamId, cancellationToken);
        if (session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return StatusCode(403);
        if (!session.Data.Candidates.Any(x => x.CandidateId == model.CandidateId)) return BadRequest();
        if (!ModelState.IsValid || !model.ScheduledTime.HasValue)
        {
            TempData["Error"] = "Khung giờ mới không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = model.ExamId });
        }

        var response = await examSessionService.RescheduleAsync(
            new ExamSessionRescheduleRequest
            {
                CandidateId = model.CandidateId,
                ScheduledTime = model.ScheduledTime.Value
            },
            cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã đổi khung giờ và kiểm tra không trùng lịch."
            : response.Error;

        return RedirectToAction(nameof(Details), new { id = model.ExamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var session = await examSessionService.GetAsync(id, cancellationToken);
        if (session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return StatusCode(403);
        var response = await examSessionService.DeleteAsync(id, cancellationToken);
        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã xoá lịch thi."
            : response.Error;

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Feeds the student combobox. Returns at most a page of matches so the
    /// browser never has to hold the whole student list.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchStudents(string? q, CancellationToken cancellationToken)
    {
        var response = await examSessionService.SearchStudentsAsync(q ?? string.Empty, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return Json(Array.Empty<object>());
        }

        return Json(response.Data.Select(person => new
        {
            email = person.Email,
            name = person.FullName
        }));
    }

    [HttpGet]
    public async Task<IActionResult> ClassRoster(int classId, CancellationToken cancellationToken)
    {
        var response = await examSessionService.GetClassRosterAsync(classId, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(new { error = response.Error });
        }

        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
            && response.Data.LecturerId != HttpContext.Session.GetInt32(SessionKeys.UserId))
        {
            return Forbid();
        }

        return Json(new
        {
            classId = response.Data.ClassId,
            courseId = response.Data.CourseId,
            lecturerId = response.Data.LecturerId,
            classCode = response.Data.ClassCode,
            className = response.Data.ClassName,
            students = response.Data.Students.Select(student => new
            {
                id = student.UserId,
                name = student.FullName,
                email = student.Email
            })
        });
    }

    private async Task LoadOptionsAsync(ExamSessionCreateViewModel model, CancellationToken cancellationToken)
    {
        model.IsLecturerFixed = CurrentLecturerId.HasValue;
        if (CurrentLecturerId is int currentLecturerId)
        {
            model.LecturerId = currentLecturerId;
            ModelState.Remove(nameof(model.LecturerId));
        }
        var response = await examSessionService.GetCreationOptionsAsync(cancellationToken);
        if (!response.Success || response.Data is null)
        {
            model.OptionsError = response.Error;
            return;
        }

        model.CourseOptions = ToSelectList(response.Data.Courses);
        model.LecturerOptions = ToSelectList(response.Data.Lecturers
            .Where(item => !CurrentLecturerId.HasValue || item.Id == CurrentLecturerId.Value).ToList());
        var classOptions = response.Data.Classes.AsEnumerable();
        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer)
        {
            var lecturerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
            classOptions = classOptions.Where(item => item.LecturerId == lecturerId);
        }
        // The course each class belongs to rides along in Group, so the class
        // picker can hide the classes of every other course.
        model.ClassOptions = classOptions
            .Select(item => new SelectListItem(item.Label, item.ClassId.ToString())
            {
                Group = new SelectListGroup { Name = item.CourseId.ToString() }
            })
            .ToList();

        model.OptionsError = model.CanCreate
            ? null
            : "Cần có môn học, giảng viên và lớp học đang hoạt động trước khi tạo lịch thi.";
    }

    private static List<SelectListItem> ToSelectList(IReadOnlyList<LookupOption> options)
    {
        return options
            .Select(option => new SelectListItem(option.Label, option.Id.ToString()))
            .ToList();
    }

    private void AddErrors(IReadOnlyList<string> errors, string? error)
    {
        var values = errors.Count > 0 ? errors : [error ?? "Không thể tạo lịch thi."];
        foreach (var value in values)
        {
            ModelState.AddModelError(string.Empty, value);
        }
    }
}
