using AuthenticationService.Models.Db;

namespace AuthenticationService.Data;

public interface IRoleRepo
{
    public bool SaveChanges();
    public IEnumerable<User?>? GetUsersWithRole(string roleName);
    public void AddRole(Role role);
    public bool DoesRoleExist(Role role);
}