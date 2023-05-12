using AuthenticationService.Models.Db;

namespace AuthenticationService.Data;

public class RoleRepo : IRoleRepo
{
    private readonly AppDbContext _context;

    public RoleRepo(AppDbContext context)
    {
        _context = context;
    }

    public bool SaveChanges()
    {
        return _context.SaveChanges() >= 1;
    }

    public IEnumerable<User?>? GetUsersWithRole(string roleName)
    {
        var role = _context.Roles.FirstOrDefault(role => role.RoleName == roleName);

        return role?.Users;
    }

    public void AddRole(Role role)
    {
        _context.Roles.Add(role);
    }

    public bool DoesRoleExist(Role role)
    {
        return _context.Roles.Any(r => r.RoleName == role.RoleName);
    }

    public Role? GetRole(string roleName)
    {
        return _context.Roles.Where(r => r.RoleName == roleName).FirstOrDefault();
    }
}