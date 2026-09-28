using AssignmentPRN.DataAccess.Models;

namespace AssignmentPRN.Business.Interfaces;

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password);

    Task<User?> GetUserByIdAsync(int userId);

    Task<(bool Success, string? Error, User? User)> RegisterAsync(
        string fullName,
        string email,
        string password);
}
