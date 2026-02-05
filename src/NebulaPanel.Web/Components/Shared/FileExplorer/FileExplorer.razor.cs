using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Web.Components.Shared.FileExplorer;

public partial class FileExplorer : ComponentBase
{
    [Inject] private IFileExplorerService FileService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid ServerId { get; set; }

    [Parameter]
    public string InitialPath { get; set; } = "";

    [Parameter]
    public EventCallback<string> OnPathChanged { get; set; }

    // State
    private List<FileSystemEntryDto> _entries = [];
    private HashSet<string> _selectedItems = [];
    private List<ClipboardItem> _clipboard = [];
    private string _currentPath = "";
    private Stack<string> _navigationHistory = new();
    private FileViewMode _viewMode = FileViewMode.Grid;
    private string _searchQuery = "";

    // Loading states
    private bool _loading;
    private bool _creating;
    private bool _renaming;
    private bool _saving;
    private bool _deleting;
    private bool _uploading;
    private string? _error;

    // Drag & Drop
    private bool _isDragging;

    // Modals
    private bool _showUploadModal;
    private bool _showCreateFolderModal;
    private bool _showRenameModal;
    private bool _showDeleteModal;
    private bool _showEditor;
    private bool _showImagePreview;

    // Image preview state
    private string _previewImageUrl = "";
    private string _previewImageName = "";

    // Context menu
    private FileContextMenu? _contextMenu;
    private FileSystemEntryDto? _contextMenuEntry;

    // Editor state
    private CodeEditor? _codeEditor;
    private string _editingFileName = "";
    private string _editingContent = "";
    private string _editingLanguage = "plaintext";
    private string _editingPath = "";
    private bool _editorReadOnly;

    // Form values
    private string _newFolderName = "";
    private string _newName = "";
    private string _renameItemPath = "";
    private List<string> _itemsToDelete = [];

    // Upload progress
    private List<UploadProgressItem> _uploadProgress = [];

    public bool CanGoBack => _navigationHistory.Count > 0;

    private IEnumerable<FileSystemEntryDto> FilteredEntries => string.IsNullOrWhiteSpace(_searchQuery)
        ? _entries
        : _entries.Where(e => e.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

    private string? _lastInitialPath;

    protected override async Task OnInitializedAsync()
    {
        _currentPath = InitialPath;
        _lastInitialPath = InitialPath;
        await RefreshAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Navigate when InitialPath changes from external source
        if (InitialPath != _lastInitialPath && !string.IsNullOrEmpty(InitialPath))
        {
            _lastInitialPath = InitialPath;
            await NavigateToAsync(InitialPath);
        }
    }

    private async Task NavigateToAsync(string path)
    {
        if (path != _currentPath && !string.IsNullOrEmpty(_currentPath))
        {
            _navigationHistory.Push(_currentPath);
        }

        _currentPath = path;
        _searchQuery = "";
        _selectedItems.Clear();
        await RefreshAsync();
        await OnPathChanged.InvokeAsync(path);
    }

    private async Task NavigateBackAsync()
    {
        if (_navigationHistory.Count > 0)
        {
            var previousPath = _navigationHistory.Pop();
            _currentPath = previousPath;
            _searchQuery = "";
            _selectedItems.Clear();
            await RefreshAsync();
            await OnPathChanged.InvokeAsync(previousPath);
        }
    }

    private async Task RefreshAsync()
    {
        _loading = true;
        _error = null;
        StateHasChanged();

        try
        {
            var result = await FileService.GetDirectoryContentsAsync(ServerId, _currentPath);
            if (result.IsFailure)
            {
                _error = result.Error;
            }
            else
            {
                _entries = result.Value?.Children?.ToList() ?? [];
            }
        }
        catch (Exception ex)
        {
            _error = $"Failed to load directory: {ex.Message}";
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private void SetViewMode(FileViewMode mode)
    {
        _viewMode = mode;
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _searchQuery = e.Value?.ToString() ?? "";
    }

    private void ClearSearch()
    {
        _searchQuery = "";
    }

    private void SelectItem(string path)
    {
        _selectedItems.Clear();
        _selectedItems.Add(path);
    }

    private void MultiSelectItem(string path)
    {
        if (_selectedItems.Contains(path))
        {
            _selectedItems.Remove(path);
        }
        else
        {
            _selectedItems.Add(path);
        }
    }

    private static readonly HashSet<string> ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".ico", ".bmp", ".webp", ".svg"];

    private async Task OpenItemAsync(FileSystemEntryDto entry)
    {
        if (entry.Type == "directory")
        {
            await NavigateToAsync(entry.Path);
        }
        else if (IsImageFile(entry.Extension))
        {
            ShowImagePreview(entry);
        }
        else if (entry.IsEditable)
        {
            await EditItemAsync(entry);
        }
        else
        {
            await DownloadItemAsync(entry.Path);
        }
    }

    private static bool IsImageFile(string? extension)
    {
        return !string.IsNullOrEmpty(extension) &&
               ImageExtensions.Contains(extension.ToLowerInvariant());
    }

    private void ShowImagePreview(FileSystemEntryDto entry)
    {
        _previewImageUrl = $"/api/servers/{ServerId}/files/download?path={Uri.EscapeDataString(entry.Path)}";
        _previewImageName = entry.Name;
        _showImagePreview = true;
    }

    private async Task EditItemAsync(FileSystemEntryDto entry)
    {
        try
        {
            _editingFileName = entry.Name;
            _editingPath = entry.Path;
            _editingLanguage = GetLanguageFromExtension(entry.Extension);
            _editorReadOnly = !entry.IsEditable;

            var result = await FileService.ReadFileAsync(ServerId, entry.Path);
            if (result.IsFailure)
            {
                _error = $"Failed to open file: {result.Error}";
                return;
            }

            _editingContent = result.Value?.Content ?? "";
            _showEditor = true;
        }
        catch (Exception ex)
        {
            _error = $"Failed to open file: {ex.Message}";
        }
    }

    private async Task SaveFileAsync()
    {
        if (_codeEditor is null) return;

        _saving = true;
        StateHasChanged();

        try
        {
            var content = await _codeEditor.GetContentAsync();
            var request = new WriteFileRequest
            {
                Path = _editingPath,
                Content = content
            };

            var result = await FileService.WriteFileAsync(ServerId, request);
            if (result.IsFailure)
            {
                _error = $"Failed to save file: {result.Error}";
                return;
            }

            CloseEditor();
        }
        catch (Exception ex)
        {
            _error = $"Failed to save file: {ex.Message}";
        }
        finally
        {
            _saving = false;
            StateHasChanged();
        }
    }

    private void CloseEditor()
    {
        _showEditor = false;
        _editingContent = "";
        _editingFileName = "";
        _editingPath = "";
    }

    private void ShowCreateFolderModal()
    {
        _newFolderName = "";
        _showCreateFolderModal = true;
        _contextMenu?.Hide();
    }

    private async Task CreateFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(_newFolderName)) return;

        _creating = true;
        StateHasChanged();

        try
        {
            var path = string.IsNullOrEmpty(_currentPath)
                ? _newFolderName
                : $"{_currentPath}/{_newFolderName}";

            var request = new CreateDirectoryRequest { Path = path };
            var result = await FileService.CreateDirectoryAsync(ServerId, request);
            if (result.IsFailure)
            {
                _error = $"Failed to create folder: {result.Error}";
                return;
            }

            _showCreateFolderModal = false;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _error = $"Failed to create folder: {ex.Message}";
        }
        finally
        {
            _creating = false;
            StateHasChanged();
        }
    }

    private void ShowRenameModal()
    {
        if (_contextMenuEntry is null) return;

        _renameItemPath = _contextMenuEntry.Path;
        _newName = _contextMenuEntry.Name;
        _showRenameModal = true;
        _contextMenu?.Hide();
    }

    private async Task RenameAsync()
    {
        if (string.IsNullOrWhiteSpace(_newName)) return;

        _renaming = true;
        StateHasChanged();

        try
        {
            var request = new RenameRequest
            {
                Path = _renameItemPath,
                NewName = _newName
            };

            var result = await FileService.RenameAsync(ServerId, request);
            if (result.IsFailure)
            {
                _error = $"Failed to rename: {result.Error}";
                return;
            }

            _showRenameModal = false;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _error = $"Failed to rename: {ex.Message}";
        }
        finally
        {
            _renaming = false;
            StateHasChanged();
        }
    }

    private void ShowUploadModal()
    {
        _uploadProgress.Clear();
        _showUploadModal = true;
        _contextMenu?.Hide();
    }

    private void CloseUploadModal()
    {
        _showUploadModal = false;
        _uploadProgress.Clear();
    }

    private async Task HandleFilesSelectedAsync(IReadOnlyList<IBrowserFile> files)
    {
        _uploading = true;
        _uploadProgress = files.Select(f => new UploadProgressItem
        {
            Name = f.Name,
            Size = f.Size,
            Status = UploadStatus.Pending
        }).ToList();

        // IMPORTANT: Read all files into memory BEFORE any StateHasChanged calls
        // StateHasChanged can cause re-renders that invalidate the JS file references
        var fileDataList = new List<(string Name, long Size, byte[] Data)>();

        try
        {
            foreach (var file in files)
            {
                await using var browserStream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024);
                using var memoryStream = new MemoryStream();
                await browserStream.CopyToAsync(memoryStream);
                fileDataList.Add((file.Name, file.Size, memoryStream.ToArray()));
            }
        }
        catch (Exception ex)
        {
            foreach (var item in _uploadProgress)
            {
                item.Status = UploadStatus.Error;
                item.Error = $"Failed to read file: {ex.Message}";
            }
            _uploading = false;
            StateHasChanged();
            return;
        }

        // Now safe to update UI and upload
        StateHasChanged();

        try
        {
            foreach (var (name, size, data) in fileDataList)
            {
                var progressItem = _uploadProgress.FirstOrDefault(p => p.Name == name);
                if (progressItem is not null)
                {
                    progressItem.Status = UploadStatus.Uploading;
                    StateHasChanged();
                }

                try
                {
                    using var memoryStream = new MemoryStream(data);

                    var result = await FileService.UploadFileAsync(
                        ServerId,
                        _currentPath,
                        name,
                        memoryStream,
                        size);

                    if (progressItem is not null)
                    {
                        if (result.IsFailure)
                        {
                            progressItem.Status = UploadStatus.Error;
                            progressItem.Error = result.Error ?? "Unknown error";
                        }
                        else if (result.Value is null)
                        {
                            progressItem.Status = UploadStatus.Error;
                            progressItem.Error = "No result returned";
                        }
                        else if (!result.Value.Success)
                        {
                            progressItem.Status = UploadStatus.Error;
                            progressItem.Error = result.Value.Error ?? "Upload failed";
                        }
                        else
                        {
                            progressItem.Status = UploadStatus.Complete;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (progressItem is not null)
                    {
                        progressItem.Status = UploadStatus.Error;
                        progressItem.Error = ex.Message;
                    }
                }

                StateHasChanged();
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            foreach (var item in _uploadProgress.Where(p => p.Status == UploadStatus.Uploading || p.Status == UploadStatus.Pending))
            {
                item.Status = UploadStatus.Error;
                item.Error = ex.Message;
            }
        }
        finally
        {
            _uploading = false;
            StateHasChanged();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        _itemsToDelete = _selectedItems.ToList();
        _showDeleteModal = true;
    }

    private async Task DeleteContextItemAsync()
    {
        if (_contextMenuEntry is null) return;

        _itemsToDelete = _selectedItems.Count > 0 && _selectedItems.Contains(_contextMenuEntry.Path)
            ? _selectedItems.ToList()
            : [_contextMenuEntry.Path];

        _showDeleteModal = true;
        _contextMenu?.Hide();
    }

    private async Task ConfirmDeleteAsync()
    {
        _deleting = true;
        StateHasChanged();

        try
        {
            foreach (var path in _itemsToDelete)
            {
                var result = await FileService.DeleteAsync(ServerId, path);
                if (result.IsFailure)
                {
                    _error = $"Failed to delete: {result.Error}";
                    return;
                }
            }

            _showDeleteModal = false;
            _selectedItems.Clear();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _error = $"Failed to delete: {ex.Message}";
        }
        finally
        {
            _deleting = false;
            _itemsToDelete.Clear();
            StateHasChanged();
        }
    }

    private async Task DownloadSelectedAsync()
    {
        foreach (var path in _selectedItems)
        {
            await DownloadItemAsync(path);
        }
    }

    private async Task DownloadContextItemAsync()
    {
        if (_contextMenuEntry is null) return;

        if (_selectedItems.Count > 0 && _selectedItems.Contains(_contextMenuEntry.Path))
        {
            await DownloadSelectedAsync();
        }
        else
        {
            await DownloadItemAsync(_contextMenuEntry.Path);
        }
        _contextMenu?.Hide();
    }

    private async Task DownloadItemAsync(string path)
    {
        try
        {
            var entry = _entries.FirstOrDefault(e => e.Path == path);
            var isDirectory = entry?.Type == "directory";

            var result = isDirectory
                ? await FileService.CreateArchiveAsync(ServerId, path)
                : await FileService.DownloadFileAsync(ServerId, path);

            if (result.IsFailure)
            {
                _error = $"Download failed: {result.Error}";
                return;
            }

            var download = result.Value!;

            // Read stream into byte array
            using var memoryStream = new MemoryStream();
            await download.Content.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();

            // Convert to base64 and trigger download via JS
            var base64 = Convert.ToBase64String(bytes);
            var dataUrl = $"data:{download.MimeType};base64,{base64}";

            await JS.InvokeVoidAsync("eval", $@"
                (function() {{
                    var a = document.createElement('a');
                    a.href = '{dataUrl}';
                    a.download = '{download.FileName.Replace("'", "\\'")}';
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                }})();
            ");
        }
        catch (Exception ex)
        {
            _error = $"Download failed: {ex.Message}";
        }
    }

    private void CopyItems()
    {
        var items = _selectedItems.Count > 0 && _contextMenuEntry is not null && _selectedItems.Contains(_contextMenuEntry.Path)
            ? _selectedItems.ToList()
            : _contextMenuEntry is not null ? [_contextMenuEntry.Path] : [];

        _clipboard = items.Select(p => new ClipboardItem(p, false)).ToList();
        _contextMenu?.Hide();
    }

    private void CutItems()
    {
        var items = _selectedItems.Count > 0 && _contextMenuEntry is not null && _selectedItems.Contains(_contextMenuEntry.Path)
            ? _selectedItems.ToList()
            : _contextMenuEntry is not null ? [_contextMenuEntry.Path] : [];

        _clipboard = items.Select(p => new ClipboardItem(p, true)).ToList();
        _contextMenu?.Hide();
    }

    private async Task PasteAsync()
    {
        if (_clipboard.Count == 0) return;
        _contextMenu?.Hide();

        try
        {
            foreach (var item in _clipboard)
            {
                var fileName = Path.GetFileName(item.Path);
                var destination = string.IsNullOrEmpty(_currentPath)
                    ? fileName
                    : $"{_currentPath}/{fileName}";

                Result result;
                if (item.IsCut)
                    result = await FileService.MoveAsync(ServerId, item.Path, destination);
                else
                    result = await FileService.CopyAsync(ServerId, item.Path, destination);

                if (result.IsFailure)
                {
                    _error = $"Failed to paste: {result.Error}";
                    return;
                }
            }

            if (_clipboard.Any(c => c.IsCut))
                _clipboard.Clear();
        }
        catch (Exception ex)
        {
            _error = $"Failed to paste: {ex.Message}";
        }

        await RefreshAsync();
    }

    private async Task ArchiveContextItemAsync()
    {
        if (_contextMenuEntry is null) return;
        await DownloadItemAsync(_contextMenuEntry.Path);
        _contextMenu?.Hide();
    }

    private async Task ExtractContextItemAsync()
    {
        if (_contextMenuEntry is null) return;
        _contextMenu?.Hide();

        try
        {
            var result = await FileService.ExtractArchiveInPlaceAsync(
                ServerId, _contextMenuEntry.Path, _currentPath);

            if (result.IsFailure)
            {
                _error = $"Failed to extract: {result.Error}";
                return;
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _error = $"Failed to extract: {ex.Message}";
        }
    }

    private void ShowItemContextMenu((FileSystemEntryDto Entry, MouseEventArgs Args) args)
    {
        _contextMenuEntry = args.Entry;
        if (!_selectedItems.Contains(args.Entry.Path))
        {
            _selectedItems.Clear();
            _selectedItems.Add(args.Entry.Path);
        }
        _contextMenu?.Show(args.Args);
    }

    private void HandleBackgroundContextMenu(MouseEventArgs args)
    {
        _contextMenuEntry = null;
        _selectedItems.Clear();
        _contextMenu?.Show(args);
    }

    private void HandleDragOver()
    {
        _isDragging = true;
    }

    private void HandleDragLeave()
    {
        _isDragging = false;
    }

    private async Task HandleDropAsync()
    {
        _isDragging = false;
        ShowUploadModal();
    }

    private async Task HandleCreateFolderKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_newFolderName))
        {
            await CreateFolderAsync();
        }
    }

    private async Task HandleRenameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_newName))
        {
            await RenameAsync();
        }
    }

    private static string GetLanguageFromExtension(string? extension) => extension?.ToLowerInvariant() switch
    {
        ".json" => "json",
        ".xml" => "xml",
        ".yaml" or ".yml" => "yaml",
        ".md" => "markdown",
        ".js" => "javascript",
        ".ts" => "typescript",
        ".py" => "python",
        ".java" => "java",
        ".cs" => "csharp",
        ".lua" => "lua",
        ".sh" => "shell",
        ".bat" or ".cmd" => "bat",
        ".ps1" => "powershell",
        ".properties" or ".cfg" or ".conf" or ".ini" => "ini",
        ".html" or ".htm" => "html",
        ".css" => "css",
        ".sql" => "sql",
        _ => "plaintext"
    };

    public record ClipboardItem(string Path, bool IsCut);

    public class UploadProgressItem
    {
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public UploadStatus Status { get; set; }
        public string? Error { get; set; }
    }

    public enum UploadStatus
    {
        Pending,
        Uploading,
        Complete,
        Error
    }

    public enum FileViewMode
    {
        Grid,
        List
    }
}
