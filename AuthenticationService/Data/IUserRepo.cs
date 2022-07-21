using AuthenticationService.Models.Db;

namespace AuthenticationService.Data;

public interface IUserRepo
{
    public bool SaveChanges();

    public RefreshToken CreateToken(User user);
    public IEnumerable<RefreshToken> GetUserRefreshTokens(User user);
    public User? GetUserFromToken(Guid tokenId);
    public void DeleteToken(Guid token);

    public bool DoesUserExist(string username);
    public User? GetUserFromUsername(string username);
    public User? GetUserFromId(Guid userId);
    public IEnumerable<User> GetAllUsers();
    public void CreateUser(User user);
}