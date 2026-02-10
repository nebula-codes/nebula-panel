using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace NebulaPanel.Web.Components.Shared.DataGrid;

public partial class DataGrid<TItem> : ComponentBase, IDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    #region Parameters

    /// <summary>
    /// The items to display in the grid.
    /// </summary>
    [Parameter]
    public IEnumerable<TItem> Items { get; set; } = [];

    /// <summary>
    /// Column definitions.
    /// </summary>
    [Parameter]
    public RenderFragment? Columns { get; set; }

    /// <summary>
    /// Additional actions to display in the header.
    /// </summary>
    [Parameter]
    public RenderFragment? HeaderActions { get; set; }

    /// <summary>
    /// Content to display in the empty state.
    /// </summary>
    [Parameter]
    public RenderFragment? EmptyAction { get; set; }

    /// <summary>
    /// Template for bulk actions bar when items are selected.
    /// </summary>
    [Parameter]
    public RenderFragment<IEnumerable<TItem>>? BulkActions { get; set; }

    /// <summary>
    /// Whether the grid is in a loading state.
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// Whether to show the search box.
    /// </summary>
    [Parameter]
    public bool Searchable { get; set; }

    /// <summary>
    /// Placeholder text for the search box.
    /// </summary>
    [Parameter]
    public string SearchPlaceholder { get; set; } = "Search...";

    /// <summary>
    /// Message to display when no items are found.
    /// </summary>
    [Parameter]
    public string EmptyMessage { get; set; } = "No items found.";

    /// <summary>
    /// Whether to enable pagination.
    /// </summary>
    [Parameter]
    public bool Pageable { get; set; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    [Parameter]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Available page size options.
    /// </summary>
    [Parameter]
    public int[] PageSizeOptions { get; set; } = [10, 25, 50, 100];

    /// <summary>
    /// Function to extract searchable text from an item.
    /// </summary>
    [Parameter]
    public Func<TItem, string>? SearchFields { get; set; }

    /// <summary>
    /// Event callback when a row is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<TItem> OnRowClick { get; set; }

    /// <summary>
    /// Event callback when a row is double-clicked.
    /// </summary>
    [Parameter]
    public EventCallback<TItem> OnRowDoubleClick { get; set; }

    /// <summary>
    /// Function to determine if a row is clickable.
    /// </summary>
    [Parameter]
    public Func<TItem, bool>? RowClickable { get; set; }

    /// <summary>
    /// Additional CSS classes.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    // Selection parameters
    /// <summary>
    /// Whether row selection is enabled.
    /// </summary>
    [Parameter]
    public bool Selectable { get; set; }

    /// <summary>
    /// Selection mode (None, Single, Multiple).
    /// </summary>
    [Parameter]
    public SelectionMode SelectionMode { get; set; } = SelectionMode.Multiple;

    /// <summary>
    /// Currently selected items.
    /// </summary>
    [Parameter]
    public IEnumerable<TItem> SelectedItems { get; set; } = [];

    /// <summary>
    /// Event callback when selected items change.
    /// </summary>
    [Parameter]
    public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    /// <summary>
    /// Function to extract a unique key from an item.
    /// </summary>
    [Parameter]
    public Func<TItem, object>? ItemKey { get; set; }

    // Column customization parameters
    /// <summary>
    /// Whether to allow column customization (hide/show).
    /// </summary>
    [Parameter]
    public bool AllowColumnCustomization { get; set; }

    /// <summary>
    /// localStorage key for saving column preferences.
    /// </summary>
    [Parameter]
    public string? ColumnPreferenceKey { get; set; }

    // Keyboard navigation parameters
    /// <summary>
    /// Whether keyboard navigation is enabled.
    /// </summary>
    [Parameter]
    public bool KeyboardNavigation { get; set; }

    // Visual parameters
    /// <summary>
    /// Whether to show alternating row colors.
    /// </summary>
    [Parameter]
    public bool Striped { get; set; } = true;

    /// <summary>
    /// Whether rows highlight on hover.
    /// </summary>
    [Parameter]
    public bool Hoverable { get; set; } = true;

    /// <summary>
    /// Whether the header is sticky on scroll.
    /// </summary>
    [Parameter]
    public bool StickyHeader { get; set; } = true;

    /// <summary>
    /// Size variant for the grid.
    /// </summary>
    [Parameter]
    public DataGridSize Size { get; set; } = DataGridSize.Medium;

    #endregion

    #region State

    private List<DataGridColumn<TItem>> _columns = [];
    private string _searchTerm = "";
    private DataGridColumn<TItem>? _sortColumn;
    private SortDirection _sortDirection = SortDirection.None;
    private int _currentPage = 1;
    private int _pageSize;
    private HashSet<object> _selectedKeys = [];
    private int? _focusedRowIndex;
    private int? _lastSelectedIndex;
    private Dictionary<string, object?> _columnFilters = [];
    private HashSet<string> _hiddenColumns = [];
    private bool _showColumnPicker;
    private string? _activeFilterColumn;
    private ElementReference _gridRef;
    private bool _isInitialized;
    private IReadOnlyList<TItem>? _cachedFilteredItems;
    private IEnumerable<TItem>? _previousItems;

    #endregion

    #region Computed Properties

    private IReadOnlyList<TItem> FilteredItems
    {
        get
        {
            if (_cachedFilteredItems is not null)
                return _cachedFilteredItems;

            var items = Items ?? Enumerable.Empty<TItem>();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(_searchTerm) && SearchFields != null)
            {
                var term = _searchTerm.ToLowerInvariant();
                items = items.Where(item =>
                {
                    var searchText = SearchFields(item);
                    return searchText?.ToLowerInvariant().Contains(term) == true;
                });
            }

            // Apply column filters
            foreach (var (columnId, filterValue) in _columnFilters)
            {
                if (filterValue == null) continue;

                var column = _columns.FirstOrDefault(c => c.ColumnId == columnId);
                if (column?.EffectiveFilterField == null) continue;

                items = ApplyColumnFilter(items, column, filterValue);
            }

            // Apply sorting
            if (_sortColumn?.EffectiveSortField != null && _sortDirection != SortDirection.None)
            {
                items = _sortDirection == SortDirection.Descending
                    ? items.OrderByDescending(i => _sortColumn.EffectiveSortField(i))
                    : items.OrderBy(i => _sortColumn.EffectiveSortField(i));
            }

            _cachedFilteredItems = items.ToList();
            return _cachedFilteredItems;
        }
    }

    private void InvalidateFilterCache()
    {
        _cachedFilteredItems = null;
    }

    private IEnumerable<TItem> PagedItems
    {
        get
        {
            IEnumerable<TItem> items = FilteredItems;

            if (Pageable)
            {
                items = items.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);
            }

            return items;
        }
    }

    private int TotalPages => Pageable && FilteredItems.Any()
        ? (int)Math.Ceiling(FilteredItems.Count() / (double)_pageSize)
        : 1;

    private int TotalFilteredCount => FilteredItems.Count();

    private int StartIndex => (_currentPage - 1) * _pageSize + 1;

    private int EndIndex => Math.Min(_currentPage * _pageSize, TotalFilteredCount);

    private IEnumerable<DataGridColumn<TItem>> VisibleColumns =>
        _columns.Where(c => c.Visible && !_hiddenColumns.Contains(c.ColumnId));

    private IEnumerable<TItem> CurrentSelectedItems =>
        FilteredItems.Where(item => IsItemSelected(item));

    private bool AllCurrentPageSelected =>
        PagedItems.Any() && PagedItems.All(item => IsItemSelected(item));

    private bool SomeCurrentPageSelected =>
        PagedItems.Any(item => IsItemSelected(item)) && !AllCurrentPageSelected;

    private bool HasActiveFilters =>
        _columnFilters.Any(f => f.Value != null) || !string.IsNullOrWhiteSpace(_searchTerm);

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        _pageSize = PageSize;
    }

    protected override void OnParametersSet()
    {
        // Invalidate cache when items reference changes
        if (!ReferenceEquals(Items, _previousItems))
        {
            _previousItems = Items;
            InvalidateFilterCache();
        }

        // Sync selected items from parameter
        if (SelectedItems != null && ItemKey != null)
        {
            _selectedKeys = SelectedItems.Select(item => ItemKey(item)).ToHashSet();
        }

        // Reset to first page when items change significantly
        if (_currentPage > TotalPages && TotalPages > 0)
        {
            _currentPage = 1;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isInitialized = true;

            // Load column preferences from localStorage
            if (AllowColumnCustomization && !string.IsNullOrEmpty(ColumnPreferenceKey))
            {
                await LoadColumnPreferencesAsync();
            }

            // Apply default sorts
            var defaultSortColumn = _columns.FirstOrDefault(c => c.DefaultSort.HasValue);
            if (defaultSortColumn != null && _sortColumn == null)
            {
                _sortColumn = defaultSortColumn;
                _sortDirection = defaultSortColumn.DefaultSort!.Value;
                StateHasChanged();
            }
        }
    }

    public void Dispose()
    {
        _columns.Clear();
    }

    #endregion

    #region Column Management

    public void AddColumn(DataGridColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            StateHasChanged();
        }
    }

    public void RemoveColumn(DataGridColumn<TItem> column)
    {
        _columns.Remove(column);
        StateHasChanged();
    }

    #endregion

    #region Search

    private void OnSearchInput(ChangeEventArgs e)
    {
        _searchTerm = e.Value?.ToString() ?? "";
        _currentPage = 1;
        InvalidateFilterCache();
        ClearSelection();
    }

    private void ClearSearch()
    {
        _searchTerm = "";
        _currentPage = 1;
        InvalidateFilterCache();
    }

    #endregion

    #region Sorting

    private void OnHeaderClick(DataGridColumn<TItem> column)
    {
        if (!column.Sortable) return;

        if (_sortColumn == column)
        {
            // Cycle: None -> Ascending -> Descending -> None
            _sortDirection = _sortDirection switch
            {
                SortDirection.None => SortDirection.Ascending,
                SortDirection.Ascending => SortDirection.Descending,
                SortDirection.Descending => SortDirection.None
            };

            if (_sortDirection == SortDirection.None)
            {
                _sortColumn = null;
            }
        }
        else
        {
            _sortColumn = column;
            _sortDirection = SortDirection.Ascending;
        }

        InvalidateFilterCache();
    }

    #endregion

    #region Pagination

    private void GoToPage(int page)
    {
        _currentPage = Math.Clamp(page, 1, TotalPages);
        _focusedRowIndex = null;
    }

    private void PreviousPage()
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            _focusedRowIndex = null;
        }
    }

    private void NextPage()
    {
        if (_currentPage < TotalPages)
        {
            _currentPage++;
            _focusedRowIndex = null;
        }
    }

    private void OnPageSizeChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var newSize))
        {
            _pageSize = newSize;
            _currentPage = 1;
            _focusedRowIndex = null;
        }
    }

    private IEnumerable<int> GetPageNumbers()
    {
        const int maxVisible = 5;
        var total = TotalPages;

        if (total <= maxVisible)
        {
            return Enumerable.Range(1, total);
        }

        var start = Math.Max(1, _currentPage - maxVisible / 2);
        var end = Math.Min(total, start + maxVisible - 1);

        if (end - start < maxVisible - 1)
        {
            start = Math.Max(1, end - maxVisible + 1);
        }

        return Enumerable.Range(start, end - start + 1);
    }

    #endregion

    #region Selection

    private object GetItemKey(TItem item)
    {
        return ItemKey?.Invoke(item) ?? item!;
    }

    private bool IsItemSelected(TItem item)
    {
        var key = GetItemKey(item);
        return _selectedKeys.Contains(key);
    }

    private void ToggleSelection(TItem item, int index, bool shiftKey = false, bool ctrlKey = false)
    {
        if (!Selectable || SelectionMode == SelectionMode.None) return;

        var key = GetItemKey(item);

        if (SelectionMode == SelectionMode.Single)
        {
            _selectedKeys.Clear();
            _selectedKeys.Add(key);
        }
        else // Multiple
        {
            if (shiftKey && _lastSelectedIndex.HasValue)
            {
                // Range selection
                var start = Math.Min(_lastSelectedIndex.Value, index);
                var end = Math.Max(_lastSelectedIndex.Value, index);
                var items = PagedItems.ToList();

                for (var i = start; i <= end; i++)
                {
                    if (i < items.Count)
                    {
                        _selectedKeys.Add(GetItemKey(items[i]));
                    }
                }
            }
            else if (ctrlKey || _selectedKeys.Contains(key))
            {
                // Toggle single item
                if (_selectedKeys.Contains(key))
                {
                    _selectedKeys.Remove(key);
                }
                else
                {
                    _selectedKeys.Add(key);
                }
            }
            else
            {
                // Replace selection
                _selectedKeys.Clear();
                _selectedKeys.Add(key);
            }
        }

        _lastSelectedIndex = index;
        _ = NotifySelectionChangedAsync();
    }

    private void ToggleRowSelection(TItem item, int index)
    {
        var key = GetItemKey(item);

        if (_selectedKeys.Contains(key))
        {
            _selectedKeys.Remove(key);
        }
        else
        {
            if (SelectionMode == SelectionMode.Single)
            {
                _selectedKeys.Clear();
            }
            _selectedKeys.Add(key);
        }

        _lastSelectedIndex = index;
        _ = NotifySelectionChangedAsync();
    }

    private void ToggleSelectAll()
    {
        if (AllCurrentPageSelected)
        {
            // Deselect all on current page
            foreach (var item in PagedItems)
            {
                _selectedKeys.Remove(GetItemKey(item));
            }
        }
        else
        {
            // Select all on current page
            foreach (var item in PagedItems)
            {
                _selectedKeys.Add(GetItemKey(item));
            }
        }

        _ = NotifySelectionChangedAsync();
    }

    private void SelectAll()
    {
        foreach (var item in FilteredItems)
        {
            _selectedKeys.Add(GetItemKey(item));
        }
        _ = NotifySelectionChangedAsync();
    }

    private void ClearSelection()
    {
        _selectedKeys.Clear();
        _lastSelectedIndex = null;
        _ = NotifySelectionChangedAsync();
    }

    private async Task NotifySelectionChangedAsync()
    {
        try
        {
            if (SelectedItemsChanged.HasDelegate)
            {
                var selectedItems = FilteredItems.Where(IsItemSelected).ToList();
                await SelectedItemsChanged.InvokeAsync(selectedItems);
            }
        }
        catch (Exception)
        {
            // Swallow exceptions during selection notification to prevent crash
        }
    }

    #endregion

    #region Column Filtering

    private IEnumerable<TItem> ApplyColumnFilter(IEnumerable<TItem> items, DataGridColumn<TItem> column, object filterValue)
    {
        if (column.EffectiveFilterField == null) return items;

        return column.FilterType switch
        {
            ColumnFilterType.Text when filterValue is string textFilter =>
                items.Where(item =>
                {
                    var value = column.EffectiveFilterField(item)?.ToString();
                    return value?.Contains(textFilter, StringComparison.OrdinalIgnoreCase) == true;
                }),

            ColumnFilterType.Select =>
                items.Where(item =>
                {
                    var value = column.EffectiveFilterField(item);
                    return Equals(value, filterValue);
                }),

            ColumnFilterType.MultiSelect when filterValue is IEnumerable<object> multiFilter =>
                items.Where(item =>
                {
                    var value = column.EffectiveFilterField(item);
                    return multiFilter.Contains(value);
                }),

            _ => items
        };
    }

    private void SetColumnFilter(string columnId, object? value)
    {
        if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
        {
            _columnFilters.Remove(columnId);
        }
        else
        {
            _columnFilters[columnId] = value;
        }

        _currentPage = 1;
        _activeFilterColumn = null;
        InvalidateFilterCache();
        ClearSelection();
    }

    private void ClearColumnFilter(string columnId)
    {
        _columnFilters.Remove(columnId);
        _currentPage = 1;
        InvalidateFilterCache();
    }

    private void ClearAllFilters()
    {
        _columnFilters.Clear();
        _searchTerm = "";
        _currentPage = 1;
        InvalidateFilterCache();
    }

    private void ToggleFilterPopover(string columnId)
    {
        _activeFilterColumn = _activeFilterColumn == columnId ? null : columnId;
    }

    private bool IsColumnFiltered(string columnId)
    {
        return _columnFilters.TryGetValue(columnId, out var value) && value != null;
    }

    private object? GetColumnFilterValue(string columnId)
    {
        return _columnFilters.TryGetValue(columnId, out var value) ? value : null;
    }

    #endregion

    #region Column Visibility

    private void ToggleColumnVisibility(string columnId)
    {
        if (_hiddenColumns.Contains(columnId))
        {
            _hiddenColumns.Remove(columnId);
        }
        else
        {
            _hiddenColumns.Add(columnId);
        }

        _ = SaveColumnPreferencesAsync();
    }

    private bool IsColumnHidden(string columnId)
    {
        return _hiddenColumns.Contains(columnId);
    }

    private void ToggleColumnPicker()
    {
        _showColumnPicker = !_showColumnPicker;
    }

    private async Task LoadColumnPreferencesAsync()
    {
        try
        {
            var key = $"nebula-datagrid-{ColumnPreferenceKey}";
            var stored = await JS.InvokeAsync<string?>("localStorage.getItem", key);

            if (!string.IsNullOrEmpty(stored))
            {
                var hidden = JsonSerializer.Deserialize<HashSet<string>>(stored);
                if (hidden != null)
                {
                    _hiddenColumns = hidden;
                    StateHasChanged();
                }
            }
        }
        catch
        {
            // Ignore localStorage errors
        }
    }

    private async Task SaveColumnPreferencesAsync()
    {
        if (string.IsNullOrEmpty(ColumnPreferenceKey)) return;

        try
        {
            var key = $"nebula-datagrid-{ColumnPreferenceKey}";
            var json = JsonSerializer.Serialize(_hiddenColumns);
            await JS.InvokeVoidAsync("localStorage.setItem", key, json);
        }
        catch
        {
            // Ignore localStorage errors
        }
    }

    #endregion

    #region Keyboard Navigation

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!KeyboardNavigation) return;

        var items = PagedItems.ToList();
        if (!items.Any()) return;

        switch (e.Key)
        {
            case "ArrowDown":
                MoveFocus(1, items.Count);
                break;

            case "ArrowUp":
                MoveFocus(-1, items.Count);
                break;

            case "Enter":
                if (_focusedRowIndex.HasValue && _focusedRowIndex.Value < items.Count)
                {
                    var item = items[_focusedRowIndex.Value];
                    if (OnRowClick.HasDelegate && (RowClickable?.Invoke(item) ?? true))
                    {
                        await OnRowClick.InvokeAsync(item);
                    }
                }
                break;

            case " ": // Space
                if (Selectable && _focusedRowIndex.HasValue && _focusedRowIndex.Value < items.Count)
                {
                    ToggleRowSelection(items[_focusedRowIndex.Value], _focusedRowIndex.Value);
                }
                break;

            case "a" when e.CtrlKey:
                if (Selectable && SelectionMode == SelectionMode.Multiple)
                {
                    SelectAll();
                }
                break;

            case "Escape":
                if (Selectable && _selectedKeys.Any())
                {
                    ClearSelection();
                }
                else
                {
                    _focusedRowIndex = null;
                }
                break;

            case "Home":
                _focusedRowIndex = 0;
                break;

            case "End":
                _focusedRowIndex = items.Count - 1;
                break;

            case "PageDown":
                if (_currentPage < TotalPages)
                {
                    NextPage();
                }
                break;

            case "PageUp":
                if (_currentPage > 1)
                {
                    PreviousPage();
                }
                break;
        }
    }

    private void MoveFocus(int delta, int itemCount)
    {
        var current = _focusedRowIndex ?? -1;
        var newIndex = Math.Clamp(current + delta, 0, itemCount - 1);
        _focusedRowIndex = newIndex;
    }

    #endregion

    #region Row Handling

    private async Task HandleRowClick(TItem item, int index, MouseEventArgs e)
    {
        _focusedRowIndex = index;

        if (Selectable && SelectionMode != SelectionMode.None)
        {
            ToggleSelection(item, index, e.ShiftKey, e.CtrlKey);
        }

        if (OnRowClick.HasDelegate && (RowClickable?.Invoke(item) ?? true))
        {
            await OnRowClick.InvokeAsync(item);
        }
    }

    private async Task HandleRowDoubleClick(TItem item)
    {
        if (OnRowDoubleClick.HasDelegate)
        {
            await OnRowDoubleClick.InvokeAsync(item);
        }
    }

    private void HandleCheckboxClick(TItem item, int index)
    {
        ToggleRowSelection(item, index);
    }

    #endregion

    #region CSS Helpers

    private string GetGridClasses()
    {
        var classes = new List<string> { "datagrid" };

        if (StickyHeader) classes.Add("datagrid-sticky");
        if (Striped) classes.Add("datagrid-striped");

        classes.Add(Size switch
        {
            DataGridSize.Compact => "datagrid-compact",
            DataGridSize.Comfortable => "datagrid-comfortable",
            _ => "datagrid-medium"
        });

        if (!string.IsNullOrEmpty(Class))
        {
            classes.Add(Class);
        }

        return string.Join(" ", classes);
    }

    private string GetHeaderClasses(DataGridColumn<TItem> column)
    {
        var classes = new List<string>();

        if (column.Sortable) classes.Add("sortable");
        if (_sortColumn == column) classes.Add("sorted");
        if (IsColumnFiltered(column.ColumnId)) classes.Add("datagrid-th-filtered");

        if (!string.IsNullOrEmpty(column.HeaderClass))
        {
            classes.Add(column.HeaderClass);
        }

        if (!string.IsNullOrEmpty(column.ResponsiveClasses))
        {
            classes.Add(column.ResponsiveClasses);
        }

        classes.Add(column.AlignClass);

        return string.Join(" ", classes);
    }

    private string GetRowClasses(TItem item, int index)
    {
        var classes = new List<string>();

        if (OnRowClick.HasDelegate && (RowClickable?.Invoke(item) ?? true))
        {
            classes.Add("clickable");
        }

        if (IsItemSelected(item))
        {
            classes.Add("datagrid-row-selected");
        }

        if (_focusedRowIndex == index)
        {
            classes.Add("datagrid-row-focused");
        }

        return string.Join(" ", classes);
    }

    private string GetCellClasses(DataGridColumn<TItem> column)
    {
        var classes = new List<string>();

        if (!string.IsNullOrEmpty(column.Class))
        {
            classes.Add(column.Class);
        }

        if (!string.IsNullOrEmpty(column.ResponsiveClasses))
        {
            classes.Add(column.ResponsiveClasses);
        }

        classes.Add(column.AlignClass);

        return string.Join(" ", classes);
    }

    private string GetColumnStyle(DataGridColumn<TItem> column)
    {
        var styles = new List<string>();

        if (!string.IsNullOrEmpty(column.Width))
        {
            styles.Add($"width: {column.Width}");
        }

        if (!string.IsNullOrEmpty(column.MinWidth))
        {
            styles.Add($"min-width: {column.MinWidth}");
        }

        return string.Join("; ", styles);
    }

    #endregion
}
