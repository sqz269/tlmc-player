using AuthenticationService.Models.Db;

namespace AuthenticationService.Data;

public class UserRepo : IUserRepo
{
    private readonly AppDbContext _context;

    public UserRepo(AppDbContext context)
    {
        _context = context;
    }

    public bool SaveChanges()
    {
        return _context.SaveChanges() >= 1;
    }

    public bool DoesUserExist(string username)
    {
        return _context.Users.Any(user => user.UserName == username);
    }

    public User? GetUserFromUsername(string username)
    {
        return _context.Users.FirstOrDefault(user => user.UserName == username);
    }

    public User? GetUserFromId(Guid userId)
    {
        return _context.Users.FirstOrDefault(user => user.UserId == userId);
    }

    public IEnumerable<User> GetAllUsers()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<User> GetUsers(int amount, int start)
    {
        return _context.Users.Skip(start).Take(amount).ToList();
    }

    public void CreateUser(User user)
    {
        user.UserId = new Guid();
        _context.Users.Add(user);
    }
}