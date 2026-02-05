using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.ModProviders.CurseForge;

/// <summary>
/// CurseForge API search response wrapper.
/// </summary>
public sealed record CurseForgeSearchResponse(
    [property: JsonPropertyName("data")] CurseForgeMod[] Data,
    [property: JsonPropertyName("pagination")] CurseForgePagination Pagination
);

/// <summary>
/// CurseForge API single mod response wrapper.
/// </summary>
public sealed record CurseForgeModResponse(
    [property: JsonPropertyName("data")] CurseForgeMod Data
);

/// <summary>
/// CurseForge API files list response wrapper.
/// </summary>
public sealed record CurseForgeFilesResponse(
    [property: JsonPropertyName("data")] CurseForgeFile[] Data,
    [property: JsonPropertyName("pagination")] CurseForgePagination Pagination
);

/// <summary>
/// CurseForge API single file response wrapper.
/// </summary>
public sealed record CurseForgeFileResponse(
    [property: JsonPropertyName("data")] CurseForgeFile Data
);

/// <summary>
/// CurseForge API download URL response wrapper.
/// </summary>
public sealed record CurseForgeDownloadUrlResponse(
    [property: JsonPropertyName("data")] string Data
);

/// <summary>
/// CurseForge pagination information.
/// </summary>
public sealed record CurseForgePagination(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("resultCount")] int ResultCount,
    [property: JsonPropertyName("totalCount")] int TotalCount
);

/// <summary>
/// CurseForge mod information.
/// </summary>
public sealed record CurseForgeMod(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("gameId")] int GameId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("links")] CurseForgeLinks? Links,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("downloadCount")] long DownloadCount,
    [property: JsonPropertyName("isFeatured")] bool IsFeatured,
    [property: JsonPropertyName("primaryCategoryId")] int PrimaryCategoryId,
    [property: JsonPropertyName("categories")] CurseForgeCategory[] Categories,
    [property: JsonPropertyName("classId")] int? ClassId,
    [property: JsonPropertyName("authors")] CurseForgeAuthor[] Authors,
    [property: JsonPropertyName("logo")] CurseForgeAsset? Logo,
    [property: JsonPropertyName("screenshots")] CurseForgeAsset[]? Screenshots,
    [property: JsonPropertyName("mainFileId")] int MainFileId,
    [property: JsonPropertyName("latestFiles")] CurseForgeFile[] LatestFiles,
    [property: JsonPropertyName("latestFilesIndexes")] CurseForgeFileIndex[]? LatestFilesIndexes,
    [property: JsonPropertyName("latestEarlyAccessFilesIndexes")] CurseForgeFileIndex[]? LatestEarlyAccessFilesIndexes,
    [property: JsonPropertyName("dateCreated")] DateTime DateCreated,
    [property: JsonPropertyName("dateModified")] DateTime DateModified,
    [property: JsonPropertyName("dateReleased")] DateTime DateReleased,
    [property: JsonPropertyName("allowModDistribution")] bool? AllowModDistribution,
    [property: JsonPropertyName("gamePopularityRank")] int GamePopularityRank,
    [property: JsonPropertyName("isAvailable")] bool IsAvailable,
    [property: JsonPropertyName("thumbsUpCount")] int ThumbsUpCount
);

/// <summary>
/// CurseForge mod links.
/// </summary>
public sealed record CurseForgeLinks(
    [property: JsonPropertyName("websiteUrl")] string? WebsiteUrl,
    [property: JsonPropertyName("wikiUrl")] string? WikiUrl,
    [property: JsonPropertyName("issuesUrl")] string? IssuesUrl,
    [property: JsonPropertyName("sourceUrl")] string? SourceUrl
);

/// <summary>
/// CurseForge category information.
/// </summary>
public sealed record CurseForgeCategory(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("gameId")] int GameId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("iconUrl")] string? IconUrl,
    [property: JsonPropertyName("dateModified")] DateTime DateModified,
    [property: JsonPropertyName("isClass")] bool? IsClass,
    [property: JsonPropertyName("classId")] int? ClassId,
    [property: JsonPropertyName("parentCategoryId")] int? ParentCategoryId,
    [property: JsonPropertyName("displayIndex")] int? DisplayIndex
);

/// <summary>
/// CurseForge author information.
/// </summary>
public sealed record CurseForgeAuthor(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string? Url
);

/// <summary>
/// CurseForge asset (logo, screenshot, etc).
/// </summary>
public sealed record CurseForgeAsset(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("modId")] int ModId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("thumbnailUrl")] string? ThumbnailUrl,
    [property: JsonPropertyName("url")] string Url
);

/// <summary>
/// CurseForge file (version) information.
/// </summary>
public sealed record CurseForgeFile(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("gameId")] int GameId,
    [property: JsonPropertyName("modId")] int ModId,
    [property: JsonPropertyName("isAvailable")] bool IsAvailable,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("releaseType")] int ReleaseType,
    [property: JsonPropertyName("fileStatus")] int FileStatus,
    [property: JsonPropertyName("hashes")] CurseForgeHash[] Hashes,
    [property: JsonPropertyName("fileDate")] DateTime FileDate,
    [property: JsonPropertyName("fileLength")] long FileLength,
    [property: JsonPropertyName("downloadCount")] long DownloadCount,
    [property: JsonPropertyName("fileSizeOnDisk")] long? FileSizeOnDisk,
    [property: JsonPropertyName("downloadUrl")] string? DownloadUrl,
    [property: JsonPropertyName("gameVersions")] string[] GameVersions,
    [property: JsonPropertyName("sortableGameVersions")] CurseForgeSortableGameVersion[]? SortableGameVersions,
    [property: JsonPropertyName("dependencies")] CurseForgeDependency[]? Dependencies,
    [property: JsonPropertyName("exposeAsAlternative")] bool? ExposeAsAlternative,
    [property: JsonPropertyName("parentProjectFileId")] int? ParentProjectFileId,
    [property: JsonPropertyName("alternateFileId")] int? AlternateFileId,
    [property: JsonPropertyName("isServerPack")] bool? IsServerPack,
    [property: JsonPropertyName("serverPackFileId")] int? ServerPackFileId,
    [property: JsonPropertyName("isEarlyAccessContent")] bool? IsEarlyAccessContent,
    [property: JsonPropertyName("earlyAccessEndDate")] DateTime? EarlyAccessEndDate,
    [property: JsonPropertyName("fileFingerprint")] long FileFingerprint,
    [property: JsonPropertyName("modules")] CurseForgeModule[]? Modules
);

/// <summary>
/// CurseForge file index for quick version lookup.
/// </summary>
public sealed record CurseForgeFileIndex(
    [property: JsonPropertyName("gameVersion")] string GameVersion,
    [property: JsonPropertyName("fileId")] int FileId,
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("releaseType")] int ReleaseType,
    [property: JsonPropertyName("gameVersionTypeId")] int? GameVersionTypeId,
    [property: JsonPropertyName("modLoader")] int? ModLoader
);

/// <summary>
/// CurseForge file hash.
/// </summary>
public sealed record CurseForgeHash(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("algo")] int Algo
);

/// <summary>
/// CurseForge sortable game version information.
/// </summary>
public sealed record CurseForgeSortableGameVersion(
    [property: JsonPropertyName("gameVersionName")] string GameVersionName,
    [property: JsonPropertyName("gameVersionPadded")] string GameVersionPadded,
    [property: JsonPropertyName("gameVersion")] string GameVersion,
    [property: JsonPropertyName("gameVersionReleaseDate")] DateTime GameVersionReleaseDate,
    [property: JsonPropertyName("gameVersionTypeId")] int? GameVersionTypeId
);

/// <summary>
/// CurseForge file dependency.
/// </summary>
public sealed record CurseForgeDependency(
    [property: JsonPropertyName("modId")] int ModId,
    [property: JsonPropertyName("relationType")] int RelationType
);

/// <summary>
/// CurseForge file module (for fingerprinting).
/// </summary>
public sealed record CurseForgeModule(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("fingerprint")] long Fingerprint
);

/// <summary>
/// CurseForge hash algorithm types.
/// </summary>
public static class CurseForgeHashAlgo
{
    public const int Sha1 = 1;
    public const int Md5 = 2;
}

/// <summary>
/// CurseForge file release types.
/// </summary>
public static class CurseForgeReleaseType
{
    public const int Release = 1;
    public const int Beta = 2;
    public const int Alpha = 3;
}

/// <summary>
/// CurseForge file status types.
/// </summary>
public static class CurseForgeFileStatus
{
    public const int Processing = 1;
    public const int ChangesRequired = 2;
    public const int UnderReview = 3;
    public const int Approved = 4;
    public const int Rejected = 5;
    public const int MalwareDetected = 6;
    public const int Deleted = 7;
    public const int Archived = 8;
    public const int Testing = 9;
    public const int Released = 10;
    public const int ReadyForReview = 11;
    public const int Deprecated = 12;
    public const int Baking = 13;
    public const int AwaitingPublishing = 14;
    public const int FailedPublishing = 15;
}

/// <summary>
/// CurseForge dependency relation types.
/// </summary>
public static class CurseForgeDependencyType
{
    public const int EmbeddedLibrary = 1;
    public const int OptionalDependency = 2;
    public const int RequiredDependency = 3;
    public const int Tool = 4;
    public const int Incompatible = 5;
    public const int Include = 6;
}

/// <summary>
/// CurseForge mod loader types for Minecraft.
/// </summary>
public static class CurseForgeModLoaderType
{
    public const int Any = 0;
    public const int Forge = 1;
    public const int Cauldron = 2;
    public const int LiteLoader = 3;
    public const int Fabric = 4;
    public const int Quilt = 5;
    public const int NeoForge = 6;
}

/// <summary>
/// CurseForge sort field types.
/// </summary>
public static class CurseForgeSortField
{
    public const int Featured = 1;
    public const int Popularity = 2;
    public const int LastUpdated = 3;
    public const int Name = 4;
    public const int Author = 5;
    public const int TotalDownloads = 6;
    public const int Category = 7;
    public const int GameVersion = 8;
    public const int EarlyAccess = 9;
    public const int FeaturedReleased = 10;
    public const int ReleasedDate = 11;
    public const int Rating = 12;
}

/// <summary>
/// Request body for POST /v1/mods (batch get mods).
/// </summary>
public sealed record CurseForgeGetModsRequest(
    [property: JsonPropertyName("modIds")] int[] ModIds
);

/// <summary>
/// Response from POST /v1/mods (batch get mods).
/// </summary>
public sealed record CurseForgeGetModsResponse(
    [property: JsonPropertyName("data")] CurseForgeMod[] Data
);

/// <summary>
/// Request body for POST /v1/mods/files (batch get files).
/// </summary>
public sealed record CurseForgeGetFilesRequest(
    [property: JsonPropertyName("fileIds")] int[] FileIds
);

/// <summary>
/// Response from POST /v1/mods/files (batch get files).
/// </summary>
public sealed record CurseForgeGetFilesResponse(
    [property: JsonPropertyName("data")] CurseForgeFile[] Data
);
