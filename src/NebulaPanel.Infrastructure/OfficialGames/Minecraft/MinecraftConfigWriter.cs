using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Implementation of IMinecraftConfigWriter for writing Minecraft server configuration files.
/// </summary>
public class MinecraftConfigWriter : IMinecraftConfigWriter
{
    public Task WriteServerPropertiesAsync(
        string installPath,
        MinecraftServerPropertiesSettings settings,
        CancellationToken cancellationToken = default)
    {
        return MinecraftConfigFiles.CreateServerPropertiesWithSettingsAsync(
            installPath, settings, cancellationToken);
    }
}
