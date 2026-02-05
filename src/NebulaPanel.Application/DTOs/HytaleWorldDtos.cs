namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Represents a Hytale world with its configuration
/// </summary>
public record HytaleWorldDto(
    string Name,
    string FolderPath,
    long SizeBytes,
    DateTime? CreatedAt,
    DateTime LastModified,
    bool HasConfig,
    HytaleWorldConfigDto? Config
);

/// <summary>
/// Configuration for a Hytale world (matches config.json schema)
/// </summary>
public record HytaleWorldConfigDto
{
    public int Version { get; init; }
    public long Seed { get; init; }
    public HytaleWorldGenConfig? WorldGen { get; init; }
    public HytaleWorldMapConfig? WorldMap { get; init; }
    public HytaleStorageConfig? ChunkStorage { get; init; }
    public HytaleStorageConfig? ResourceStorage { get; init; }

    // Gameplay toggles
    public bool IsTicking { get; init; } = true;
    public bool IsBlockTicking { get; init; } = true;
    public bool IsPvpEnabled { get; init; }
    public bool IsFallDamageEnabled { get; init; } = true;
    public bool IsGameTimePaused { get; init; }
    public bool IsSpawningNPC { get; init; } = true;
    public bool IsSpawnMarkersEnabled { get; init; } = true;
    public bool IsAllNPCFrozen { get; init; }
    public bool IsCompassUpdating { get; init; } = true;
    public bool IsSavingPlayers { get; init; } = true;
    public bool IsSavingChunks { get; init; } = true;
    public bool SaveNewChunks { get; init; } = true;
    public bool IsUnloadingChunks { get; init; } = true;
    public bool IsObjectiveMarkersEnabled { get; init; } = true;
    public bool DeleteOnUniverseStart { get; init; }
    public bool DeleteOnRemove { get; init; }

    public string? GameTime { get; init; }
    public string? GameplayConfig { get; init; }

    public HytaleClientEffectsConfig? ClientEffects { get; init; }
}

public record HytaleWorldGenConfig
{
    public string Type { get; init; } = "Hytale";
    public string Name { get; init; } = "Default";
}

public record HytaleWorldMapConfig
{
    public string Type { get; init; } = "WorldGen";
}

public record HytaleStorageConfig
{
    public string Type { get; init; } = "Hytale";
}

public record HytaleClientEffectsConfig
{
    public double SunHeightPercent { get; init; } = 100.0;
    public double SunAngleDegrees { get; init; }
    public double BloomIntensity { get; init; } = 0.3;
    public double BloomPower { get; init; } = 8.0;
    public double SunIntensity { get; init; } = 0.25;
    public double SunshaftIntensity { get; init; } = 0.3;
    public double SunshaftScaleFactor { get; init; } = 4.0;
}

/// <summary>
/// Request to update a world's configuration
/// </summary>
public record UpdateHytaleWorldConfigRequest(
    string WorldName,
    // Gameplay toggles
    bool? IsPvpEnabled,
    bool? IsFallDamageEnabled,
    bool? IsSpawningNPC,
    bool? IsAllNPCFrozen,
    bool? IsGameTimePaused,
    bool? IsTicking,
    bool? IsBlockTicking,
    bool? IsSpawnMarkersEnabled,
    bool? IsObjectiveMarkersEnabled,
    bool? IsCompassUpdating,
    bool? IsSavingPlayers,
    bool? IsSavingChunks,
    bool? SaveNewChunks,
    bool? IsUnloadingChunks,
    bool? DeleteOnUniverseStart,
    bool? DeleteOnRemove,
    string? GameplayConfig
);

/// <summary>
/// Request to create a new world
/// </summary>
public record CreateHytaleWorldRequest(
    string Name,
    long? Seed,
    string? WorldGenType,
    string? WorldGenName
);

/// <summary>
/// Request to duplicate a world
/// </summary>
public record DuplicateHytaleWorldRequest(
    string SourceWorldName,
    string NewWorldName
);
