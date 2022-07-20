using AuthenticationService.Models.Db;

namespace AuthenticationService.Data;

public interface IUserRepo
{
    public bool SaveChanges();

    public bool DoesUserExist(string username);
    public User? GetUserFromUsername(string username);
    public User? GetUserFromId(Guid userId);
    public IEnumerable<User> GetAllUsers();
    public void CreateUser(User user);
}