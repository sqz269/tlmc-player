using Microsoft.EntityFrameworkCore;

namespace PlaylistService.Data;

public static class PrepDb
{
    public static void Prep(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
        Migrate(dbContext, true);
    }

    private static void Migrate(AppDbContext context, bool isProduction)
    {
        if (isProduction)
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
}