using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.ModpackProviders.FTB;

#region Search Response Models

/// <summary>
/// Response from FTB search and popular endpoints.
/// </summary>
public sealed record FtbSearchResponse(
    [property: JsonPropertyName("packs")] FtbPackSummary[] Packs,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("refreshed")] long Refreshed
);

/// <summary>
/// Summary information for a modpack in search results.
/// </summary>
public sealed record FtbPackSummary(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("synopsis")] string Synopsis,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("art")] FtbArt[] Art,
    [property: JsonPropertyName("authors")] FtbAuthor[] Authors,
    [property: JsonPropertyName("versions")] FtbPackVersionSummary[]? Versions,
    [property: JsonPropertyName("installs")] long Installs,
    [property: JsonPropertyName("plays")] long Plays,
    [property: JsonPropertyName("featured")] bool Featured,
    [property: JsonPropertyName("refreshed")] long Refreshed,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("tags")] FtbTag[]? Tags,
    [property: JsonPropertyName("released")] long Released,
    [property: JsonPropertyName("updated")] long Updated
);

#endregion

#region Art and Media Models

/// <summary>
/// Artwork/image for a modpack.
/// </summary>
public sealed record FtbArt(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("updated")] long Updated
);

#endregion

#region Author and Tag Models

/// <summary>
/// Author/contributor information.
/// </summary>
public sealed record FtbAuthor(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("website")] string? Website,
    [property: JsonPropertyName("updated")] long Updated
);

/// <summary>
/// Tag/category for a modpack.
/// </summary>
public sealed record FtbTag(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name
);

#endregion

#region Pack Details Models

/// <summary>
/// Full pack details response.
/// </summary>
public sealed record FtbPackDetails(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("synopsis")] string Synopsis,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("art")] FtbArt[] Art,
    [property: JsonPropertyName("authors")] FtbAuthor[] Authors,
    [property: JsonPropertyName("versions")] FtbPackVersionInfo[] Versions,
    [property: JsonPropertyName("installs")] long Installs,
    [property: JsonPropertyName("plays")] long Plays,
    [property: JsonPropertyName("featured")] bool Featured,
    [property: JsonPropertyName("refreshed")] long Refreshed,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("tags")] FtbTag[]? Tags,
    [property: JsonPropertyName("links")] FtbLink[]? Links,
    [property: JsonPropertyName("released")] long Released,
    [property: JsonPropertyName("updated")] long Updated
);

/// <summary>
/// External link for a modpack.
/// </summary>
public sealed record FtbLink(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("link")] string Link,
    [property: JsonPropertyName("type")] string Type
);

#endregion

#region Version Models

/// <summary>
/// Version summary in search results.
/// </summary>
public sealed record FtbPackVersionSummary(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("updated")] long Updated
);

/// <summary>
/// Detailed version information in pack details.
/// </summary>
public sealed record FtbPackVersionInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("updated")] long Updated,
    [property: JsonPropertyName("specs")] FtbSpecs? Specs,
    [property: JsonPropertyName("targets")] FtbTarget[] Targets
);

/// <summary>
/// Full version details response.
/// </summary>
public sealed record FtbVersionDetails(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("parent")] int Parent,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("updated")] long Updated,
    [property: JsonPropertyName("specs")] FtbSpecs? Specs,
    [property: JsonPropertyName("targets")] FtbTarget[] Targets,
    [property: JsonPropertyName("files")] FtbFile[]? Files
);

/// <summary>
/// System requirements/specifications for a version.
/// </summary>
public sealed record FtbSpecs(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("minimum")] int Minimum,
    [property: JsonPropertyName("recommended")] int Recommended
);

/// <summary>
/// Target (Minecraft version, mod loader, etc.) for a version.
/// </summary>
public sealed record FtbTarget(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("updated")] long Updated
);

/// <summary>
/// File in a version (mods, configs, etc.).
/// </summary>
public sealed record FtbFile(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("sha1")] string? Sha1,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("clientonly")] bool ClientOnly,
    [property: JsonPropertyName("serveronly")] bool ServerOnly,
    [property: JsonPropertyName("optional")] bool Optional,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("updated")] long Updated
);

#endregion

#region Server Files Models

/// <summary>
/// Server files/installer information.
/// </summary>
public sealed record FtbServerFilesResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("parent")] int Parent,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("updated")] long Updated,
    [property: JsonPropertyName("specs")] FtbSpecs? Specs,
    [property: JsonPropertyName("targets")] FtbTarget[] Targets,
    [property: JsonPropertyName("files")] FtbFile[]? Files
);

#endregion
