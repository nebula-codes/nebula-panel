using System.Text.Json;
using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.ModProviders.Modtale;

/// <summary>
/// Modtale paginated search response.
/// </summary>
public sealed record ModtaleSearchResponse(
    [property: JsonPropertyName("content")] ModtaleProject[] Content,
    [property: JsonPropertyName("totalPages")] int TotalPages,
    [property: JsonPropertyName("totalElements")] int TotalElements,
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("size")] int Size,
    [property: JsonPropertyName("first")] bool First,
    [property: JsonPropertyName("last")] bool Last
);

/// <summary>
/// Modtale project summary from search results.
/// </summary>
public sealed record ModtaleProject(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("author"), JsonConverter(typeof(ModtaleAuthorConverter))] ModtaleAuthor? Author,
    [property: JsonPropertyName("classification")] string Classification,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl,
    [property: JsonPropertyName("downloadCount")] long Downloads,
    [property: JsonPropertyName("rating")] double? Rating,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt,
    [property: JsonPropertyName("tags")] string[]? Tags
);

/// <summary>
/// Modtale author information.
/// </summary>
public sealed record ModtaleAuthor(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl
);

/// <summary>
/// JSON converter that handles both string and object author formats.
/// </summary>
public sealed class ModtaleAuthorConverter : JsonConverter<ModtaleAuthor?>
{
    public override ModtaleAuthor? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            // Author is just a string username
            var username = reader.GetString();
            return new ModtaleAuthor(null, username ?? "Unknown", null);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Author is an object - parse it manually to handle missing fields
            string? id = null;
            string username = "Unknown";
            string? avatarUrl = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName?.ToLowerInvariant())
                    {
                        case "id":
                            id = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                            break;
                        case "username":
                        case "name":
                        case "displayname":
                            username = reader.GetString() ?? "Unknown";
                            break;
                        case "avatarurl":
                        case "avatar":
                        case "avatar_url":
                            avatarUrl = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            return new ModtaleAuthor(id, username, avatarUrl);
        }

        // Skip unknown token types
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ModtaleAuthor? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        if (value.Id is not null)
            writer.WriteString("id", value.Id);
        writer.WriteString("username", value.Username);
        if (value.AvatarUrl is not null)
            writer.WriteString("avatarUrl", value.AvatarUrl);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Detailed project information from Modtale.
/// </summary>
public sealed record ModtaleProjectDetails(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("about")] string? About,
    [property: JsonPropertyName("classification")] string Classification,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("author"), JsonConverter(typeof(ModtaleAuthorConverter))] ModtaleAuthor? Author,
    [property: JsonPropertyName("versions")] ModtaleVersion[] Versions,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl,
    [property: JsonPropertyName("galleryImages")] ModtaleGalleryImage[]? GalleryImages,
    [property: JsonPropertyName("license")] string? License,
    [property: JsonPropertyName("repositoryUrl")] string? RepositoryUrl,
    [property: JsonPropertyName("downloadCount")] long Downloads,
    [property: JsonPropertyName("rating")] double? Rating,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt,
    [property: JsonPropertyName("tags")] string[]? Tags
);

/// <summary>
/// Modtale version information.
/// </summary>
public sealed record ModtaleVersion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("versionNumber")] string VersionNumber,
    [property: JsonPropertyName("fileUrl")] string FileUrl,
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("fileSize")] long? FileSize,
    [property: JsonPropertyName("downloadCount")] long DownloadCount,
    [property: JsonPropertyName("gameVersions")] string[] GameVersions,
    [property: JsonPropertyName("changelog")] string? Changelog,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("releasedAt")] DateTime ReleasedAt,
    [property: JsonPropertyName("sha256")] string? Sha256
);

/// <summary>
/// Modtale gallery image.
/// </summary>
public sealed record ModtaleGalleryImage(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("featured")] bool Featured
);

/// <summary>
/// Modtale tags metadata response.
/// </summary>
public sealed record ModtaleTagsResponse(
    [property: JsonPropertyName("tags")] string[] Tags
);

/// <summary>
/// Modtale game versions metadata response.
/// </summary>
public sealed record ModtaleGameVersionsResponse(
    [property: JsonPropertyName("gameVersions")] string[] GameVersions
);

/// <summary>
/// Modtale classification constants (content types).
/// </summary>
public static class ModtaleClassification
{
    public const string Plugin = "PLUGIN";
    public const string Data = "DATA";
    public const string Art = "ART";
    public const string Save = "SAVE";
    public const string Modpack = "MODPACK";
}
