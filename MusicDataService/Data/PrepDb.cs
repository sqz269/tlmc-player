namespace MusicDataService.Data;

public static class PrepDb
{
    public static void Prep(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var albumDbContext = serviceScope.ServiceProvider.GetService<AlbumRepo>();
    }

    private static void Seed(AlbumRepo repo, bool isProduction)
    {

    }
}