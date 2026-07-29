using Microsoft.EntityFrameworkCore;

namespace TlmcPlayerBackend.Data;

public static class PrepDb
{
    public static void Prep(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Seed(dbContext, true);
    }

    private static void Seed(AppDbContext context, bool isProduction)
    {
        if (isProduction)
        {
            Console.WriteLine("Migrating Database");

            try
            {
                // The context's global CommandTimeout is 5 seconds, which is fine for
                // request queries and hopeless for schema work: the halfvec conversion
                // rewrites TrackEmbeddings and the two HNSW index builds take minutes
                // on a populated table. Inheriting 5s meant Migrate() threw, this
                // rethrew, and the app crash-looped mid-migration.
                context.Database.SetCommandTimeout(TimeSpan.FromMinutes(30));
                context.Database.Migrate();
                System.Console.WriteLine("Database Migrated");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}