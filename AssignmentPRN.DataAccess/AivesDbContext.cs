using AssignmentPRN.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess;

public class AivesDbContext(DbContextOptions<AivesDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(role => role.RoleId);

            entity.Property(role => role.RoleId)
                .HasColumnName("role_id")
                .ValueGeneratedOnAdd();
            entity.Property(role => role.RoleName)
                .HasColumnName("role_name")
                .IsRequired();
            entity.Property(role => role.Description)
                .HasColumnName("description");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.UserId);

            entity.Property(user => user.UserId)
                .HasColumnName("user_id")
                .ValueGeneratedOnAdd();
            entity.Property(user => user.RoleId)
                .HasColumnName("role_id");
            entity.Property(user => user.FullName)
                .HasColumnName("full_name")
                .IsRequired();
            entity.Property(user => user.Email)
                .HasColumnName("email")
                .IsRequired();
            entity.Property(user => user.PasswordHash)
                .HasColumnName("password_hash")
                .IsRequired();
            entity.Property(user => user.Status)
                .HasColumnName("status")
                .IsRequired();
            entity.Property(user => user.CreatedAt)
                .HasColumnName("created_at");
            entity.Property(user => user.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.RoleId);
        });
    }
}
