using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Interfaces;
using AssignmentPRN.DataAccess.Models;

namespace AssignmentPRN.Business.Services;

public class AuthService(IUserRepository userRepository) : IAuthService
{
    public async Task<User?> LoginAsync(string email, string password)
    {
        var user = await userRepository.GetByEmailAsync(email);

        if (user is null || user.Status != "Active")
        {
            return null;
        }

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }

    public Task<User?> GetUserByIdAsync(int userId)
    {
        return userRepository.GetByIdAsync(userId);
    }
}
