using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.ModpackProviders.Modrinth;

/// <summary>
/// Root structure of the modrinth.index.json file inside an MRPACK.
/// </summary>
public sealed record MrpackIndex(
    [property: JsonPropertyName("formatVersion")] int FormatVersion,
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("versionId")] string VersionId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("files")] MrpackFile[] Files,
    [property: JsonPropertyName("dependencies")] MrpackDependencies? Dependencies
);

/// <summary>
/// A file entry in the MRPACK index.
/// </summary>
public sealed record MrpackFile(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("hashes")] MrpackHashes Hashes,
    [property: JsonPropertyName("downloads")] string[] Downloads,
    [property: JsonPropertyName("fileSize")] long FileSize,
    [property: JsonPropertyName("env")] MrpackEnv? Env
);

/// <summary>
/// Hash values for an MRPACK file.
/// </summary>
public sealed record MrpackHashes(
    [property: JsonPropertyName("sha1")] string? Sha1,
    [property: JsonPropertyName("sha512")] string? Sha512
);

/// <summary>
/// Environment requirements for an MRPACK file.
/// Values can be "required", "optional", or "unsupported".
/// </summary>
public sealed record MrpackEnv(
    [property: JsonPropertyName("client")] string? Client,
    [property: JsonPropertyName("server")] string? Server
);

/// <summary>
/// Dependencies (mod loaders and Minecraft version) for an MRPACK.
/// </summary>
public sealed record MrpackDependencies(
    [property: JsonPropertyName("minecraft")] string? Minecraft,
    [property: JsonPropertyName("fabric-loader")] string? FabricLoader,
    [property: JsonPropertyName("quilt-loader")] string? QuiltLoader,
    [property: JsonPropertyName("forge")] string? Forge,
    [property: JsonPropertyName("neoforge")] string? NeoForge
);
