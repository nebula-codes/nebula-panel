namespace NebulaPanel.Infrastructure.Services;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Repositories;

/// <summary>
/// Service for managing Hytale server worlds
/// </summary>
public class HytaleWorldService(
    IGameServerRepository serverRepository,
    ILogger<HytaleWorldService> logger) : IHytaleWorldService
{
    private const string WorldsRelativePath = "Server/universe/worlds";
    private const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<IEnumerable<HytaleWorldDto>> GetWorldsAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            logger.LogWarning("Server {ServerId} not found", serverId);
            return [];
        }

        var worldsPath = Path.Combine(server.InstallPath, WorldsRelativePath);
        if (!Directory.Exists(worldsPath))
        {
            logger.LogDebug("Worlds directory does not exist: {WorldsPath}", worldsPath);
            return [];
        }

        var worlds = new List<HytaleWorldDto>();

        foreach (var worldDir in Directory.GetDirectories(worldsPath))
        {
            try
            {
                var worldDto = await GetWorldInfoAsync(worldDir, cancellationToken).ConfigureAwait(false);
                if (worldDto is not null)
                {
                    worlds.Add(worldDto);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read world from {WorldDir}", worldDir);
            }
        }

        return worlds.OrderBy(w => w.Name);
    }

    public async Task<HytaleWorldConfigDto?> GetWorldConfigAsync(
        Guid serverId,
        string worldName,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null) return null;

        var configPath = Path.Combine(server.InstallPath, WorldsRelativePath, worldName, ConfigFileName);
        if (!File.Exists(configPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            return ParseWorldConfig(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read world config: {ConfigPath}", configPath);
            return null;
        }
    }

    public async Task<bool> UpdateWorldConfigAsync(
        Guid serverId,
        UpdateHytaleWorldConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null) return false;

        var configPath = Path.Combine(server.InstallPath, WorldsRelativePath, request.WorldName, ConfigFileName);

        if (!File.Exists(configPath))
        {
            logger.LogWarning("World config does not exist: {ConfigPath}", configPath);
            return false;
        }

        try
        {
            // Read existing config
            var existingJson = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            var configObj = JsonNode.Parse(existingJson)?.AsObject();
            if (configObj is null) return false;

            // Update only the gameplay toggles that are provided
            if (request.IsPvpEnabled.HasValue) configObj["IsPvpEnabled"] = request.IsPvpEnabled.Value;
            if (request.IsFallDamageEnabled.HasValue) configObj["IsFallDamageEnabled"] = request.IsFallDamageEnabled.Value;
            if (request.IsSpawningNPC.HasValue) configObj["IsSpawningNPC"] = request.IsSpawningNPC.Value;
            if (request.IsAllNPCFrozen.HasValue) configObj["IsAllNPCFrozen"] = request.IsAllNPCFrozen.Value;
            if (request.IsGameTimePaused.HasValue) configObj["IsGameTimePaused"] = request.IsGameTimePaused.Value;
            if (request.IsTicking.HasValue) configObj["IsTicking"] = request.IsTicking.Value;
            if (request.IsBlockTicking.HasValue) configObj["IsBlockTicking"] = request.IsBlockTicking.Value;
            if (request.IsSpawnMarkersEnabled.HasValue) configObj["IsSpawnMarkersEnabled"] = request.IsSpawnMarkersEnabled.Value;
            if (request.IsObjectiveMarkersEnabled.HasValue) configObj["IsObjectiveMarkersEnabled"] = request.IsObjectiveMarkersEnabled.Value;
            if (request.IsCompassUpdating.HasValue) configObj["IsCompassUpdating"] = request.IsCompassUpdating.Value;
            if (request.IsSavingPlayers.HasValue) configObj["IsSavingPlayers"] = request.IsSavingPlayers.Value;
            if (request.IsSavingChunks.HasValue) configObj["IsSavingChunks"] = request.IsSavingChunks.Value;
            if (request.SaveNewChunks.HasValue) configObj["SaveNewChunks"] = request.SaveNewChunks.Value;
            if (request.IsUnloadingChunks.HasValue) configObj["IsUnloadingChunks"] = request.IsUnloadingChunks.Value;
            if (request.DeleteOnUniverseStart.HasValue) configObj["DeleteOnUniverseStart"] = request.DeleteOnUniverseStart.Value;
            if (request.DeleteOnRemove.HasValue) configObj["DeleteOnRemove"] = request.DeleteOnRemove.Value;
            if (request.GameplayConfig is not null) configObj["GameplayConfig"] = request.GameplayConfig;

            var json = configObj.ToJsonString(JsonOptions);
            await File.WriteAllTextAsync(configPath, json, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Updated world config for {WorldName}", request.WorldName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update world config: {ConfigPath}", configPath);
            return false;
        }
    }

    public async Task<HytaleWorldDto?> CreateWorldAsync(
        Guid serverId,
        CreateHytaleWorldRequest request,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null) return null;

        var worldPath = Path.Combine(server.InstallPath, WorldsRelativePath, request.Name);

        if (Directory.Exists(worldPath))
        {
            logger.LogWarning("World already exists: {WorldPath}", worldPath);
            return null;
        }

        try
        {
            Directory.CreateDirectory(worldPath);

            // Create config.json with initial settings
            var seed = request.Seed ?? Random.Shared.NextInt64();
            var config = new JsonObject
            {
                ["Version"] = 4,
                ["Seed"] = seed,
                ["WorldGen"] = new JsonObject
                {
                    ["Type"] = request.WorldGenType ?? "Hytale",
                    ["Name"] = request.WorldGenName ?? "Default"
                },
                ["WorldMap"] = new JsonObject { ["Type"] = "WorldGen" },
                ["ChunkStorage"] = new JsonObject { ["Type"] = "Hytale" },
                ["ChunkConfig"] = new JsonObject(),
                ["IsTicking"] = true,
                ["IsBlockTicking"] = true,
                ["IsPvpEnabled"] = false,
                ["IsFallDamageEnabled"] = true,
                ["IsGameTimePaused"] = false,
                ["IsSpawningNPC"] = true,
                ["IsSpawnMarkersEnabled"] = true,
                ["IsAllNPCFrozen"] = false,
                ["GameplayConfig"] = "Default",
                ["IsCompassUpdating"] = true,
                ["IsSavingPlayers"] = true,
                ["IsSavingChunks"] = true,
                ["SaveNewChunks"] = true,
                ["IsUnloadingChunks"] = true,
                ["IsObjectiveMarkersEnabled"] = true,
                ["DeleteOnUniverseStart"] = false,
                ["DeleteOnRemove"] = false,
                ["ResourceStorage"] = new JsonObject { ["Type"] = "Hytale" },
                ["ClientEffects"] = new JsonObject
                {
                    ["SunHeightPercent"] = 100.0,
                    ["SunAngleDegrees"] = 0.0,
                    ["BloomIntensity"] = 0.3,
                    ["BloomPower"] = 8.0,
                    ["SunIntensity"] = 0.25,
                    ["SunshaftIntensity"] = 0.3,
                    ["SunshaftScaleFactor"] = 4.0
                },
                ["RequiredPlugins"] = new JsonObject(),
                ["Plugin"] = new JsonObject()
            };

            var configPath = Path.Combine(worldPath, ConfigFileName);
            var json = config.ToJsonString(JsonOptions);
            await File.WriteAllTextAsync(configPath, json, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Created new world: {WorldName} with seed {Seed}", request.Name, seed);

            return await GetWorldInfoAsync(worldPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create world: {WorldPath}", worldPath);
            // Clean up on failure
            if (Directory.Exists(worldPath))
            {
                try { Directory.Delete(worldPath, true); }
                catch { /* Ignore cleanup errors */ }
            }
            return null;
        }
    }

    public async Task<HytaleWorldDto?> DuplicateWorldAsync(
        Guid serverId,
        DuplicateHytaleWorldRequest request,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null) return null;

        var sourceWorldPath = Path.Combine(server.InstallPath, WorldsRelativePath, request.SourceWorldName);
        var destWorldPath = Path.Combine(server.InstallPath, WorldsRelativePath, request.NewWorldName);

        if (!Directory.Exists(sourceWorldPath))
        {
            logger.LogWarning("Source world does not exist: {SourceWorldPath}", sourceWorldPath);
            return null;
        }

        if (Directory.Exists(destWorldPath))
        {
            logger.LogWarning("Destination world already exists: {DestWorldPath}", destWorldPath);
            return null;
        }

        try
        {
            await CopyDirectoryAsync(sourceWorldPath, destWorldPath, cancellationToken).ConfigureAwait(false);

            // Generate new UUID and optionally new seed for the duplicated world
            var configPath = Path.Combine(destWorldPath, ConfigFileName);
            if (File.Exists(configPath))
            {
                var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
                var config = JsonNode.Parse(json)?.AsObject();
                if (config is not null)
                {
                    // Generate new UUID
                    var newUuid = Guid.NewGuid().ToByteArray();
                    config["UUID"] = new JsonObject
                    {
                        ["$binary"] = Convert.ToBase64String(newUuid),
                        ["$type"] = "04"
                    };

                    await File.WriteAllTextAsync(configPath, config.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
                }
            }

            logger.LogInformation("Duplicated world {Source} to {Dest}", request.SourceWorldName, request.NewWorldName);

            return await GetWorldInfoAsync(destWorldPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to duplicate world from {Source} to {Dest}", request.SourceWorldName, request.NewWorldName);
            // Clean up on failure
            if (Directory.Exists(destWorldPath))
            {
                try { Directory.Delete(destWorldPath, true); }
                catch { /* Ignore cleanup errors */ }
            }
            return null;
        }
    }

    public async Task<bool> DeleteWorldAsync(
        Guid serverId,
        string worldName,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null) return false;

        var worldPath = Path.Combine(server.InstallPath, WorldsRelativePath, worldName);

        if (!Directory.Exists(worldPath))
        {
            logger.LogWarning("World does not exist: {WorldPath}", worldPath);
            return false;
        }

        try
        {
            Directory.Delete(worldPath, true);
            logger.LogInformation("Deleted world: {WorldName}", worldName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete world: {WorldPath}", worldPath);
            return false;
        }
    }

    public async Task<string?> GetWorldConfigRawAsync(
        Guid serverId,
        string worldName,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null) return null;

        var configPath = Path.Combine(server.InstallPath, WorldsRelativePath, worldName, ConfigFileName);
        if (!File.Exists(configPath)) return null;

        return await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SaveWorldConfigRawAsync(
        Guid serverId,
        string worldName,
        string content,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null) return false;

        var configPath = Path.Combine(server.InstallPath, WorldsRelativePath, worldName, ConfigFileName);
        var worldDir = Path.GetDirectoryName(configPath)!;

        if (!Directory.Exists(worldDir))
        {
            logger.LogWarning("World directory does not exist: {WorldDir}", worldDir);
            return false;
        }

        try
        {
            // Validate JSON
            JsonNode.Parse(content);

            await File.WriteAllTextAsync(configPath, content, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Saved raw config for world {WorldName}", worldName);
            return true;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON content for world config: {WorldName}", worldName);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save world config: {ConfigPath}", configPath);
            return false;
        }
    }

    private async Task<HytaleWorldDto?> GetWorldInfoAsync(string worldDir, CancellationToken cancellationToken)
    {
        var worldName = Path.GetFileName(worldDir);
        var configPath = Path.Combine(worldDir, ConfigFileName);
        var hasConfig = File.Exists(configPath);

        HytaleWorldConfigDto? config = null;
        if (hasConfig)
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
                config = ParseWorldConfig(json);
            }
            catch
            {
                // Ignore config parsing errors, still return basic world info
            }
        }

        var dirInfo = new DirectoryInfo(worldDir);
        var sizeBytes = GetDirectorySize(dirInfo);
        var createdAt = dirInfo.CreationTimeUtc;
        var lastModified = GetLatestModificationTime(dirInfo);

        return new HytaleWorldDto(
            Name: worldName,
            FolderPath: worldDir,
            SizeBytes: sizeBytes,
            CreatedAt: createdAt,
            LastModified: lastModified,
            HasConfig: hasConfig,
            Config: config
        );
    }

    private static HytaleWorldConfigDto ParseWorldConfig(string json)
    {
        var node = JsonNode.Parse(json);
        if (node is null) return new HytaleWorldConfigDto();

        var obj = node.AsObject();

        HytaleWorldGenConfig? worldGen = null;
        if (obj["WorldGen"] is JsonObject wgObj)
        {
            worldGen = new HytaleWorldGenConfig
            {
                Type = wgObj["Type"]?.GetValue<string>() ?? "Hytale",
                Name = wgObj["Name"]?.GetValue<string>() ?? "Default"
            };
        }

        HytaleWorldMapConfig? worldMap = null;
        if (obj["WorldMap"] is JsonObject wmObj)
        {
            worldMap = new HytaleWorldMapConfig
            {
                Type = wmObj["Type"]?.GetValue<string>() ?? "WorldGen"
            };
        }

        HytaleStorageConfig? chunkStorage = null;
        if (obj["ChunkStorage"] is JsonObject csObj)
        {
            chunkStorage = new HytaleStorageConfig
            {
                Type = csObj["Type"]?.GetValue<string>() ?? "Hytale"
            };
        }

        HytaleStorageConfig? resourceStorage = null;
        if (obj["ResourceStorage"] is JsonObject rsObj)
        {
            resourceStorage = new HytaleStorageConfig
            {
                Type = rsObj["Type"]?.GetValue<string>() ?? "Hytale"
            };
        }

        HytaleClientEffectsConfig? clientEffects = null;
        if (obj["ClientEffects"] is JsonObject ceObj)
        {
            clientEffects = new HytaleClientEffectsConfig
            {
                SunHeightPercent = ceObj["SunHeightPercent"]?.GetValue<double>() ?? 100.0,
                SunAngleDegrees = ceObj["SunAngleDegrees"]?.GetValue<double>() ?? 0.0,
                BloomIntensity = ceObj["BloomIntensity"]?.GetValue<double>() ?? 0.3,
                BloomPower = ceObj["BloomPower"]?.GetValue<double>() ?? 8.0,
                SunIntensity = ceObj["SunIntensity"]?.GetValue<double>() ?? 0.25,
                SunshaftIntensity = ceObj["SunshaftIntensity"]?.GetValue<double>() ?? 0.3,
                SunshaftScaleFactor = ceObj["SunshaftScaleFactor"]?.GetValue<double>() ?? 4.0
            };
        }

        return new HytaleWorldConfigDto
        {
            Version = obj["Version"]?.GetValue<int>() ?? 0,
            Seed = obj["Seed"]?.GetValue<long>() ?? 0,
            WorldGen = worldGen,
            WorldMap = worldMap,
            ChunkStorage = chunkStorage,
            ResourceStorage = resourceStorage,
            IsTicking = obj["IsTicking"]?.GetValue<bool>() ?? true,
            IsBlockTicking = obj["IsBlockTicking"]?.GetValue<bool>() ?? true,
            IsPvpEnabled = obj["IsPvpEnabled"]?.GetValue<bool>() ?? false,
            IsFallDamageEnabled = obj["IsFallDamageEnabled"]?.GetValue<bool>() ?? true,
            IsGameTimePaused = obj["IsGameTimePaused"]?.GetValue<bool>() ?? false,
            IsSpawningNPC = obj["IsSpawningNPC"]?.GetValue<bool>() ?? true,
            IsSpawnMarkersEnabled = obj["IsSpawnMarkersEnabled"]?.GetValue<bool>() ?? true,
            IsAllNPCFrozen = obj["IsAllNPCFrozen"]?.GetValue<bool>() ?? false,
            IsCompassUpdating = obj["IsCompassUpdating"]?.GetValue<bool>() ?? true,
            IsSavingPlayers = obj["IsSavingPlayers"]?.GetValue<bool>() ?? true,
            IsSavingChunks = obj["IsSavingChunks"]?.GetValue<bool>() ?? true,
            SaveNewChunks = obj["SaveNewChunks"]?.GetValue<bool>() ?? true,
            IsUnloadingChunks = obj["IsUnloadingChunks"]?.GetValue<bool>() ?? true,
            IsObjectiveMarkersEnabled = obj["IsObjectiveMarkersEnabled"]?.GetValue<bool>() ?? true,
            DeleteOnUniverseStart = obj["DeleteOnUniverseStart"]?.GetValue<bool>() ?? false,
            DeleteOnRemove = obj["DeleteOnRemove"]?.GetValue<bool>() ?? false,
            GameTime = obj["GameTime"]?.GetValue<string>(),
            GameplayConfig = obj["GameplayConfig"]?.GetValue<string>(),
            ClientEffects = clientEffects
        };
    }

    private static long GetDirectorySize(DirectoryInfo dir)
    {
        long size = 0;
        try
        {
            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }
        }
        catch
        {
            // Ignore errors when calculating size
        }
        return size;
    }

    private static DateTime GetLatestModificationTime(DirectoryInfo dir)
    {
        var latest = dir.LastWriteTimeUtc;
        try
        {
            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (file.LastWriteTimeUtc > latest)
                {
                    latest = file.LastWriteTimeUtc;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return latest;
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            await using var sourceStream = File.OpenRead(file);
            await using var destStream = File.Create(destFile);
            await sourceStream.CopyToAsync(destStream, cancellationToken).ConfigureAwait(false);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destSubDir, cancellationToken).ConfigureAwait(false);
        }
    }
}
