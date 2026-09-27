using AssignmentPRN.DataAccess.Interfaces;
using AssignmentPRN.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public class UserRepository(AivesDbContext context) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email)
    {
        return context.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.Email == email);
    }

    public Task<User?> GetByIdAsync(int userId)
    {
        return context.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.UserId == userId);
    }
}
