﻿using Microsoft.EntityFrameworkCore;

namespace TlmcPlayerBackend.Data;

public static class PrepDb
{
    public static void Prep(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
        Seed(dbContext, true);
    }

    private static void Seed(AppDbContext context, bool isProduction)
    {
        if (isProduction)
        {
            Console.WriteLine("Migrating Database");

            try
            {
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