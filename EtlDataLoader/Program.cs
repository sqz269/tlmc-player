using EtlDataLoader.Operations;
using EtlDataLoader.UserOptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sharprompt;
using TlmcPlayerBackend.Data;

NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();

var postgresConnectionString = Prompt.Input<string>("Enter PostgresDB connection string: ",
    defaultValue: "Host=localhost;Port=30001;Username=postgres;Password=postgrespw;Database=postgres");

Console.WriteLine("Initializing DB Connection");

AppDbContext appDbContext;
try
{
    // Initialize AppDbContext
    var dbContext = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(postgresConnectionString)
        .LogTo(Console.WriteLine, LogLevel.Warning);

    appDbContext = new AppDbContext(dbContext.Options);

    Console.WriteLine("DB Connection Initialized");
    appDbContext.Database.EnsureCreated();
    Console.WriteLine("Migrating Database");
    appDbContext.Database.Migrate();
    Console.WriteLine("Migration complete, schema updated");
}
catch (Exception e)
{
    Console.WriteLine("Fatal: Error initializing DB Connection");
    Console.WriteLine(e);

    Console.WriteLine("Press any key to exit");
    Console.ReadKey();
    return;
}


var opt = Prompt.Select<UserOptionDataOptions>("Select the data you want to push to the database (Use Arrow keys to select)", pageSize: 5);

switch (opt)
{
    case UserOptionDataOptions.AlbumTrackBasicMetadata:
        AlbumTrackMetadataProcessor.PushBasicAlbumAndTrackData(appDbContext);
        break;
    case UserOptionDataOptions.CircleBasicMetadata:
        CircleMetadataProcessor.PushBasicCircleData(appDbContext);
        break;
    case UserOptionDataOptions.MpegDashPlaylists:
        MpegDashPlaylistProcessor.PushMpegDashPlaylists(appDbContext);
        break;
    // case UserOptionDataOptions.ThwikiExtendedArtistCircleMetadata:
    //     UserThwikiExtendedArtistCircleMetadataOption.GetAndInvokeThwikiExtendedArtistCircleMetadataOption();
    //     break;
    // case UserOptionDataOptions.ThwikiExtendedAlbumTrackMetadata:
    //     UserThwikiExtendedAlbumTrackMetadataOption.GetAndInvokeThwikiExtendedAlbumTrackMetadataOption();
    //     break;
    // case UserOptionDataOptions.ThwikiLyricsData:
    //     UserThwikiLyricsDataOption.GetAndInvokeThwikiLyricsDataOption();
    //     break;
    default:
        throw new ArgumentOutOfRangeException();
}
