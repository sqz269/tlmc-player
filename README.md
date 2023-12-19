# TLMC Player Backend

This repository hosts the backend service for the TLMC (Touhou Lossless Music Collection) Player, a comprehensive solution for streaming and managing Touhou music.

## Linked Projects

- [TLMC Player Flutter](https://github.com/sqz269/tlmc-player-flutter): Mobile application for this backend service
- [TLMC Player Vue](https://github.com/sqz269/tlmc-player-vue): Web app frontend for this backend service
- [TLMC Info Provider](https://github.com/sqz269/TlmcInfoProvider): Script collection for transforming TLMC metadata into structured data compatible with this backend's DB schema (Rework in progress)

## Features

### Extensive Metadata

- **Album Metadata**: Release date, artist, release convention, cover art, etc.
- **Track Metadata**: Staff, arrangements, original track, and more.

### High-Quality Streaming

- **HTTP Live Streaming (HLS) Support**: Different bandwidths and quality options (128k, 192k, 320k).

### Advanced Search

- **ElasticSearch Integration**: Perform comprehensive search queries on albums and tracks.
- **Advanced Query Support** (TODO): Enhanced search flexibility using Query DSL.

### Playlist Management

- **Personalized Playlists**: Create, delete, and manage playlists with various visibility options (TODO: Discoverability of public playlists).
- **Track Management**: Add or remove tracks from playlists.

### User Profile Management

- **Extended User Profiles**: Customize displayed username and profile messages.

## Getting Started

TODO: See Wiki section of the repository

## Project Structure

TODO: See Wiki section of the repository

## API Documentation

To explore and test our API endpoints interactively, visit the Swagger UI for each service:

- [Music Data Service API](https://api-music.marisad.me/swagger/music-data/index.html)
- [Playlist Service API](https://api-music.marisad.me/swagger/playlist/index.html)
- [Search Service API](https://api-music.marisad.me/swagger/search-api/index.html)
- [User Profile Service API](https://api-music.marisad.me/swagger/user-profile/index.html)

These interfaces provide detailed information about each endpoint, including available operations, request parameters, and response formats. You can try out different requests directly in the Swagger UI to see how our API responds in real-time.

## Acknowledgements

Special thanks to Connor_CZ and the team behind [Touhou Lossless Music Collection](https://nyaa.si/view/1714682) for their invaluable contributions.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/sqz269/tlmc-player/blob/master/LICENSE) file for details.
