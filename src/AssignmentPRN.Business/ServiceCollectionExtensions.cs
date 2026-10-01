using AssignmentPRN.DataAccess.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace AssignmentPRN.Business;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(
        this IServiceCollection services,
        string connectionString,
        string webRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDataAccess(connectionString, webRootPath);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExamSessionService, ExamSessionService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IAcademicClassService, AcademicClassService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<ICourseMaterialService, CourseMaterialService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IExamResultService, ExamResultService>();

        return services;
    }
}
