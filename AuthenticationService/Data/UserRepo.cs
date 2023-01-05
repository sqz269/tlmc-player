﻿using AuthenticationService.Models.Db;
using Microsoft.EntityFrameworkCore;

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

    public RefreshToken CreateToken(User user)
    {
        var token = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            IssuedTime = DateTime.UtcNow,
            UserId = user.UserId
        };

        _context.RefreshTokens.Add(token);
        return token;
    }

    public IEnumerable<RefreshToken> GetUserRefreshTokens(User user)
    {
        return _context.RefreshTokens.Where(r => r.User == user);
    }

    public User? GetUserFromToken(Guid tokenId)
    {
        var token = _context.RefreshTokens.Include(t => t.User).FirstOrDefault(token => token.TokenId == tokenId);
        if (token != null) return token.User;
        Console.WriteLine($"--> TOKEN {{{tokenId}}} DOES NOT EXIST");
        return null;

    }

    public bool RevokeRefreshToken(Guid tokenId)
    {
        var query = _context.RefreshTokens.Where(t => t.TokenId == tokenId).FirstOrDefault();
        if (query != null)
        {
            _context.RefreshTokens.Remove(query);
            return true;
        }

        return false;
    }

    public bool DoesUserExist(string username)
    {
        return _context.Users.Any(user => user.UserName == username);
    }

    public User? GetUserFromUsername(string username)
    {
        return _context.Users.Include(u => u.Roles).FirstOrDefault(user => user.UserName == username);
    }

    public User? GetUserFromId(Guid userId)
    {
        return _context.Users.Include(u => u.Roles).FirstOrDefault(user => user.UserId == userId);
    }

    public IEnumerable<User> GetAllUsers()
    {
        return _context.Users.ToList();
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