using AssignmentPRN.DataAccess.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace AssignmentPRN.Business;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDataAccess(connectionString);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExamSessionService, ExamSessionService>();
        services.AddScoped<CourseService>();
        services.AddScoped<AcademicClassService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<ICourseMaterialService, CourseMaterialService>();
        services.AddScoped<ICatalogService, CatalogService>();

        return services;
    }
}
