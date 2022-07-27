using AuthenticationService.Data;
using AuthenticationService.Models.Db;
using AuthServiceClientApi;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationService.Controllers;

[Route("api/user")]
[ApiController]
public class UserController : Controller
{
    private readonly IUserRepo _userRepo;

    public UserController(IUserRepo userRepo)
    {
        _userRepo = userRepo;
    }

    [HttpGet("all")]
    [RoleRequired(KnownRoles.Default)]
    public IEnumerable<User> GetAllUsers()
    {
        return _userRepo.GetAllUsers();
    }
}