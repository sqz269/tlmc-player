using Microsoft.EntityFrameworkCore;
using MusicDataService.Data.Api;
using MusicDataService.Models;

namespace MusicDataService.Data.Impl;

public class CircleRepo : ICircleRepo
{
    private readonly AppDbContext _context;

    public CircleRepo(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<IEnumerable<Circle>> GetCircles(int start, int limit)
    {
        return await _context.Circles.Skip(start).Take(limit).OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Circle?> GetCircleByName(string name)
    {
        return await _context.Circles.Where(c => 
                EF.Functions.ILike(name, c.Name) || 
                c.Alias.Any(a => EF.Functions.ILike(name, a)))
            .FirstOrDefaultAsync();
    }

    public async Task<Circle?> GetCircleById(Guid id)
    {
        return await _context.Circles.Where(c => c.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Guid> AddCircle(Circle circle)
    {
        if (circle.Id == Guid.Empty)
        {
            circle.Id = Guid.NewGuid();
        }

        var added = await _context.Circles.AddAsync(circle);
        return added.Entity.Id;
    }
}