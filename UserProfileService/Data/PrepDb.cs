using Microsoft.EntityFrameworkCore;

namespace UserProfileService.Data;

public static class PrepDb
{
    public static void Prep(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
        InitDb(dbContext);
    }

    public static void InitDb(AppDbContext context)
    {
        Console.WriteLine("Migrating Database");
        try
        {
            context.Database.Migrate();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }   
}