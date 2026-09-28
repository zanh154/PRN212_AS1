using AssignmentPRN.DataAccess.Models;

namespace AssignmentPRN.DataAccess.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByIdAsync(int userId);

    Task<bool> EmailExistsAsync(string email);

    Task<Role?> GetRoleByNameAsync(string roleName);

    Task<User> CreateAsync(User user);
}
