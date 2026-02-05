using Microsoft.AspNetCore.Components;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Web.Components.Shared;
using NebulaPanel.Web.Components.Shared.DataGrid;
using NebulaPanel.Web.Services;

namespace NebulaPanel.Web.Components.Pages.Servers;

public partial class Index : ComponentBase
{
    [Inject]
    private IGameServerService ServerService { get; set; } = default!;

    [Inject]
    private IGameService GameService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IToastService ToastService { get; set; } = default!;

    // Data state
    private List<GameServerListItemDto> _servers = [];
    private List<GameListItemDto> _games = [];
    private bool _loading = true;
    private string? _error;

    // View state
    private ViewMode _viewMode = ViewMode.Table;

    // Filter state
    private ServerStatus? _statusFilter;
    private Guid? _gameFilter;
    private string? _tagFilter;

    // Selection state
    private IEnumerable<GameServerListItemDto> _selectedServers = [];
    private bool _bulkStarting;
    private bool _bulkStopping;
    private bool _bulkDeleting;

    // Delete confirmation
    private bool _showDeleteConfirm;
    private bool _deleting;
    private GameServerListItemDto? _serverToDelete;
    private bool _isBulkDelete;
    private bool _deleteFiles;
    private bool _deleteContainer;

    // Status filter options
    private readonly FilterPillOption<ServerStatus?>[] _statusOptions =
    [
        new(ServerStatus.Running, "Running", "play", null),
        new(ServerStatus.Stopped, "Stopped", "square", null),
        new(ServerStatus.Crashed, "Error", "alert-triangle", null)
    ];

    // Computed properties
    private IEnumerable<GameServerListItemDto> FilteredServers => _servers
        .Where(s => !_statusFilter.HasValue || s.Status == _statusFilter.Value)
        .Where(s => !_gameFilter.HasValue || s.GameId == _gameFilter.Value)
        .Where(s => string.IsNullOrEmpty(_tagFilter) || s.Tags.Contains(_tagFilter))
        .OrderByDescending(s => s.IsPinned)
        .ThenBy(s => s.Name);

    private bool HasActiveFilters => _statusFilter.HasValue || _gameFilter.HasValue || !string.IsNullOrEmpty(_tagFilter);

    private IEnumerable<string> AllTags => _servers
        .SelectMany(s => s.Tags)
        .Distinct()
        .OrderBy(t => t);

    private int RunningCount => _servers.Count(s => s.Status == ServerStatus.Running);
    private int StoppedCount => _servers.Count(s => s.Status == ServerStatus.Stopped);
    private int ErrorCount => _servers.Count(s => s.Status == ServerStatus.Crashed);

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _loading = true;
            _error = null;

            var serversTask = ServerService.GetAllServersAsync();
            var gamesTask = GameService.GetAllGamesAsync();

            await Task.WhenAll(serversTask, gamesTask);

            _servers = (await serversTask).ToList();
            _games = (await gamesTask).ToList();

            UpdateStatusCounts();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private void UpdateStatusCounts()
    {
        // Update counts in status options
        // Note: Since _statusOptions is readonly, we create new instances
    }

    private void ClearFilters()
    {
        _statusFilter = null;
        _gameFilter = null;
        _tagFilter = null;
    }

    private void ViewServer(GameServerListItemDto server)
    {
        Navigation.NavigateTo($"/servers/{server.Id}");
    }

    private void EditServer(GameServerListItemDto server)
    {
        Navigation.NavigateTo($"/servers/{server.Id}/edit");
    }

    private void NavigateToCreate()
    {
        Navigation.NavigateTo("/servers/create");
    }

    #region Single Server Actions

    private async Task StartServer(GameServerListItemDto server)
    {
        try
        {
            // Optimistic update
            UpdateServerStatus(server.Id, ServerStatus.Starting);
            StateHasChanged();

            ToastService.ShowInfo($"Starting {server.Name}...", "Server");
            var result = await ServerService.StartServerAsync(server.Id);
            if (result.IsSuccess)
            {
                ToastService.ShowSuccess($"{server.Name} started successfully", "Server");
            }
            else
            {
                // Revert on failure
                UpdateServerStatus(server.Id, ServerStatus.Stopped);
                ToastService.ShowError(result.Error ?? "Failed to start server", "Server Error");
                _error = result.Error;
            }
        }
        catch (Exception ex)
        {
            UpdateServerStatus(server.Id, ServerStatus.Stopped);
            ToastService.ShowError(ex.Message, "Server Error");
            _error = ex.Message;
        }
    }

    private async Task StopServer(GameServerListItemDto server)
    {
        try
        {
            // Optimistic update
            UpdateServerStatus(server.Id, ServerStatus.Stopping);
            StateHasChanged();

            ToastService.ShowInfo($"Stopping {server.Name}...", "Server");
            var result = await ServerService.StopServerAsync(server.Id);
            if (result.IsSuccess)
            {
                ToastService.ShowSuccess($"{server.Name} stopped successfully", "Server");
            }
            else
            {
                // Revert on failure
                UpdateServerStatus(server.Id, ServerStatus.Running);
                ToastService.ShowError(result.Error ?? "Failed to stop server", "Server Error");
                _error = result.Error;
            }
        }
        catch (Exception ex)
        {
            UpdateServerStatus(server.Id, ServerStatus.Running);
            ToastService.ShowError(ex.Message, "Server Error");
            _error = ex.Message;
        }
    }

    private void UpdateServerStatus(Guid serverId, ServerStatus newStatus)
    {
        var index = _servers.FindIndex(s => s.Id == serverId);
        if (index >= 0)
        {
            _servers[index] = _servers[index] with { Status = newStatus };
        }
    }

    private async Task TogglePin(GameServerListItemDto server)
    {
        try
        {
            // Optimistic update
            var index = _servers.FindIndex(s => s.Id == server.Id);
            if (index >= 0)
            {
                _servers[index] = _servers[index] with { IsPinned = !_servers[index].IsPinned };
                StateHasChanged();
            }

            var result = await ServerService.TogglePinAsync(server.Id);
            if (!result.IsSuccess)
            {
                // Revert on failure
                if (index >= 0)
                {
                    _servers[index] = _servers[index] with { IsPinned = !_servers[index].IsPinned };
                }
                ToastService.ShowError(result.Error ?? "Failed to toggle pin", "Pin Error");
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message, "Pin Error");
        }
    }

    #endregion

    #region Bulk Actions

    private async Task BulkStart()
    {
        if (!_selectedServers.Any()) return;

        try
        {
            _bulkStarting = true;
            var stoppedServers = _selectedServers
                .Where(s => s.Status == ServerStatus.Stopped || s.Status == ServerStatus.Crashed)
                .ToList();

            ToastService.ShowInfo($"Starting {stoppedServers.Count} server(s)...", "Bulk Start");

            var successCount = 0;
            foreach (var server in stoppedServers)
            {
                var result = await ServerService.StartServerAsync(server.Id);
                if (result.IsSuccess) successCount++;
            }

            if (successCount == stoppedServers.Count)
                ToastService.ShowSuccess($"Started {successCount} server(s)", "Bulk Start");
            else
                ToastService.ShowWarning($"Started {successCount} of {stoppedServers.Count} server(s)", "Bulk Start");

            _selectedServers = [];
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message, "Bulk Start Error");
            _error = ex.Message;
        }
        finally
        {
            _bulkStarting = false;
        }
    }

    private async Task BulkStop()
    {
        if (!_selectedServers.Any()) return;

        try
        {
            _bulkStopping = true;
            var runningServers = _selectedServers
                .Where(s => s.Status == ServerStatus.Running)
                .ToList();

            ToastService.ShowInfo($"Stopping {runningServers.Count} server(s)...", "Bulk Stop");

            var successCount = 0;
            foreach (var server in runningServers)
            {
                var result = await ServerService.StopServerAsync(server.Id);
                if (result.IsSuccess) successCount++;
            }

            if (successCount == runningServers.Count)
                ToastService.ShowSuccess($"Stopped {successCount} server(s)", "Bulk Stop");
            else
                ToastService.ShowWarning($"Stopped {successCount} of {runningServers.Count} server(s)", "Bulk Stop");

            _selectedServers = [];
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message, "Bulk Stop Error");
            _error = ex.Message;
        }
        finally
        {
            _bulkStopping = false;
        }
    }

    private void ConfirmBulkDelete()
    {
        _isBulkDelete = true;
        _showDeleteConfirm = true;
    }

    #endregion

    #region Delete Actions

    private void ConfirmDelete(GameServerListItemDto server)
    {
        _serverToDelete = server;
        _isBulkDelete = false;
        _showDeleteConfirm = true;
    }

    private void CancelDelete()
    {
        _serverToDelete = null;
        _showDeleteConfirm = false;
        _isBulkDelete = false;
    }

    private async Task DeleteServer()
    {
        try
        {
            _deleting = true;

            if (_isBulkDelete)
            {
                await BulkDeleteServers();
            }
            else if (_serverToDelete != null)
            {
                var result = await ServerService.DeleteServerAsync(_serverToDelete.Id, _deleteFiles, _deleteContainer);
                if (result.IsSuccess)
                {
                    ToastService.ShowSuccess($"{_serverToDelete.Name} deleted", "Server Deleted");
                    _servers.Remove(_serverToDelete);
                }
                else
                {
                    ToastService.ShowError(result.Error ?? "Failed to delete server", "Delete Error");
                    _error = result.Error;
                }
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message, "Delete Error");
            _error = ex.Message;
        }
        finally
        {
            _deleting = false;
            _showDeleteConfirm = false;
            _serverToDelete = null;
            _isBulkDelete = false;
            _deleteFiles = false;
            _deleteContainer = false;
        }
    }

    private async Task BulkDeleteServers()
    {
        var deletableServers = _selectedServers
            .Where(s => s.Status == ServerStatus.Stopped || s.Status == ServerStatus.Unknown || s.Status == ServerStatus.Crashed)
            .ToList();

        var successCount = 0;
        foreach (var server in deletableServers)
        {
            var result = await ServerService.DeleteServerAsync(server.Id, _deleteFiles, _deleteContainer);
            if (result.IsSuccess)
            {
                _servers.Remove(server);
                successCount++;
            }
        }

        if (successCount == deletableServers.Count)
            ToastService.ShowSuccess($"Deleted {successCount} server(s)", "Bulk Delete");
        else
            ToastService.ShowWarning($"Deleted {successCount} of {deletableServers.Count} server(s)", "Bulk Delete");

        _selectedServers = [];
    }

    #endregion

    #region Helpers

    private static StatusBadge.StatusType GetStatusType(ServerStatus status) => status switch
    {
        ServerStatus.Running => StatusBadge.StatusType.Success,
        ServerStatus.Starting => StatusBadge.StatusType.Warning,
        ServerStatus.Stopping => StatusBadge.StatusType.Warning,
        ServerStatus.Stopped => StatusBadge.StatusType.Neutral,
        ServerStatus.Crashed => StatusBadge.StatusType.Error,
        ServerStatus.Updating => StatusBadge.StatusType.Info,
        ServerStatus.Installing => StatusBadge.StatusType.Info,
        _ => StatusBadge.StatusType.Neutral
    };

    private int DeletableCount => _selectedServers
        .Count(s => s.Status == ServerStatus.Stopped || s.Status == ServerStatus.Unknown || s.Status == ServerStatus.Crashed);

    private int StartableCount => _selectedServers
        .Count(s => s.Status == ServerStatus.Stopped || s.Status == ServerStatus.Crashed);

    private int StoppableCount => _selectedServers
        .Count(s => s.Status == ServerStatus.Running);

    #endregion
}
