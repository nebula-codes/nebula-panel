using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.ModProviders.Modrinth;

/// <summary>
/// Modrinth API search response.
/// </summary>
public sealed record ModrinthSearchResponse(
    [property: JsonPropertyName("hits")] ModrinthSearchHit[] Hits,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("total_hits")] int TotalHits
);

/// <summary>
/// Individual search result hit from Modrinth.
/// </summary>
public sealed record ModrinthSearchHit(
    [property: JsonPropertyName("project_id")] string ProjectId,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("categories")] string[]? Categories,
    [property: JsonPropertyName("client_side")] string? ClientSide,
    [property: JsonPropertyName("server_side")] string? ServerSide,
    [property: JsonPropertyName("project_type")] string? ProjectType,
    [property: JsonPropertyName("downloads")] long Downloads,
    [property: JsonPropertyName("icon_url")] string? IconUrl,
    [property: JsonPropertyName("color")] int? Color,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("versions")] string[]? Versions,
    [property: JsonPropertyName("date_created")] string? DateCreated,
    [property: JsonPropertyName("date_modified")] string? DateModified,
    [property: JsonPropertyName("latest_version")] string? LatestVersion,
    [property: JsonPropertyName("license")] string? License,
    [property: JsonPropertyName("gallery")] string[]? Gallery,
    [property: JsonPropertyName("featured_gallery")] string? FeaturedGallery
);

/// <summary>
/// Full project details from Modrinth.
/// </summary>
public sealed record ModrinthProject(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("project_type")] string ProjectType,
    [property: JsonPropertyName("team")] string Team,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("body_url")] string? BodyUrl,
    [property: JsonPropertyName("published")] string Published,
    [property: JsonPropertyName("updated")] string Updated,
    [property: JsonPropertyName("approved")] string? Approved,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("license")] ModrinthLicense? License,
    [property: JsonPropertyName("client_side")] string? ClientSide,
    [property: JsonPropertyName("server_side")] string? ServerSide,
    [property: JsonPropertyName("downloads")] long Downloads,
    [property: JsonPropertyName("followers")] int Followers,
    [property: JsonPropertyName("categories")] string[] Categories,
    [property: JsonPropertyName("additional_categories")] string[]? AdditionalCategories,
    [property: JsonPropertyName("game_versions")] string[] GameVersions,
    [property: JsonPropertyName("loaders")] string[] Loaders,
    [property: JsonPropertyName("versions")] string[] Versions,
    [property: JsonPropertyName("icon_url")] string? IconUrl,
    [property: JsonPropertyName("issues_url")] string? IssuesUrl,
    [property: JsonPropertyName("source_url")] string? SourceUrl,
    [property: JsonPropertyName("wiki_url")] string? WikiUrl,
    [property: JsonPropertyName("discord_url")] string? DiscordUrl,
    [property: JsonPropertyName("donation_urls")] ModrinthDonationUrl[]? DonationUrls,
    [property: JsonPropertyName("gallery")] ModrinthGalleryImage[]? Gallery
);

/// <summary>
/// License information for a Modrinth project.
/// </summary>
public sealed record ModrinthLicense(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string? Url
);

/// <summary>
/// Donation URL for a Modrinth project.
/// </summary>
public sealed record ModrinthDonationUrl(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("url")] string Url
);

/// <summary>
/// Gallery image for a Modrinth project.
/// </summary>
public sealed record ModrinthGalleryImage(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("featured")] bool Featured,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("created")] string Created,
    [property: JsonPropertyName("ordering")] int Ordering
);

/// <summary>
/// Version information for a Modrinth project.
/// </summary>
public sealed record ModrinthVersion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("project_id")] string ProjectId,
    [property: JsonPropertyName("author_id")] string AuthorId,
    [property: JsonPropertyName("featured")] bool Featured,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version_number")] string VersionNumber,
    [property: JsonPropertyName("changelog")] string? Changelog,
    [property: JsonPropertyName("changelog_url")] string? ChangelogUrl,
    [property: JsonPropertyName("date_published")] string DatePublished,
    [property: JsonPropertyName("downloads")] long Downloads,
    [property: JsonPropertyName("version_type")] string VersionType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("game_versions")] string[] GameVersions,
    [property: JsonPropertyName("loaders")] string[] Loaders,
    [property: JsonPropertyName("files")] ModrinthFile[] Files,
    [property: JsonPropertyName("dependencies")] ModrinthDependency[] Dependencies
);

/// <summary>
/// File information for a Modrinth version.
/// </summary>
public sealed record ModrinthFile(
    [property: JsonPropertyName("hashes")] ModrinthHashes Hashes,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("primary")] bool Primary,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("file_type")] string? FileType
);

/// <summary>
/// Hash values for a Modrinth file.
/// </summary>
public sealed record ModrinthHashes(
    [property: JsonPropertyName("sha512")] string? Sha512,
    [property: JsonPropertyName("sha1")] string? Sha1
);

/// <summary>
/// Dependency information for a Modrinth version.
/// </summary>
public sealed record ModrinthDependency(
    [property: JsonPropertyName("version_id")] string? VersionId,
    [property: JsonPropertyName("project_id")] string? ProjectId,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("dependency_type")] string DependencyType
);

/// <summary>
/// Team member information from Modrinth.
/// </summary>
public sealed record ModrinthTeamMember(
    [property: JsonPropertyName("team_id")] string TeamId,
    [property: JsonPropertyName("user")] ModrinthUser User,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("permissions")] int? Permissions,
    [property: JsonPropertyName("accepted")] bool Accepted,
    [property: JsonPropertyName("payouts_split")] int? PayoutsSplit,
    [property: JsonPropertyName("ordering")] int Ordering
);

/// <summary>
/// User information from Modrinth.
/// </summary>
public sealed record ModrinthUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("bio")] string? Bio,
    [property: JsonPropertyName("created")] string Created,
    [property: JsonPropertyName("role")] string Role
);
