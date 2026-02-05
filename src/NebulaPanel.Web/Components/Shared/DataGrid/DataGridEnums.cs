namespace NebulaPanel.Web.Components.Shared.DataGrid;

/// <summary>
/// Selection mode for the DataGrid component.
/// </summary>
public enum SelectionMode
{
    /// <summary>
    /// No selection allowed.
    /// </summary>
    None,

    /// <summary>
    /// Only one row can be selected at a time.
    /// </summary>
    Single,

    /// <summary>
    /// Multiple rows can be selected.
    /// </summary>
    Multiple
}

/// <summary>
/// Filter type for DataGrid columns.
/// </summary>
public enum ColumnFilterType
{
    /// <summary>
    /// Free text search filter.
    /// </summary>
    Text,

    /// <summary>
    /// Single-select dropdown filter.
    /// </summary>
    Select,

    /// <summary>
    /// Multi-select checkbox filter.
    /// </summary>
    MultiSelect,

    /// <summary>
    /// Date range filter.
    /// </summary>
    DateRange,

    /// <summary>
    /// Number range filter.
    /// </summary>
    NumberRange
}

/// <summary>
/// Sort direction for DataGrid columns.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// No sorting applied.
    /// </summary>
    None,

    /// <summary>
    /// Ascending order (A-Z, 0-9).
    /// </summary>
    Ascending,

    /// <summary>
    /// Descending order (Z-A, 9-0).
    /// </summary>
    Descending
}

/// <summary>
/// Text alignment for DataGrid columns.
/// </summary>
public enum TextAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// Size variants for the DataGrid.
/// </summary>
public enum DataGridSize
{
    /// <summary>
    /// Compact sizing with reduced padding.
    /// </summary>
    Compact,

    /// <summary>
    /// Default medium sizing.
    /// </summary>
    Medium,

    /// <summary>
    /// Comfortable sizing with increased padding.
    /// </summary>
    Comfortable
}

/// <summary>
/// View mode for list/grid toggle.
/// </summary>
public enum ViewMode
{
    /// <summary>
    /// Table/list view.
    /// </summary>
    Table,

    /// <summary>
    /// Card grid view.
    /// </summary>
    Grid
}

/// <summary>
/// Represents a filter option for Select/MultiSelect column filters.
/// </summary>
/// <param name="Value">The value to filter by.</param>
/// <param name="Label">Display label for the option.</param>
/// <param name="Icon">Optional icon name.</param>
public record FilterOption(object Value, string Label, string? Icon = null);

/// <summary>
/// Represents a filter pill option for status filtering.
/// </summary>
/// <typeparam name="T">The type of the filter value.</typeparam>
/// <param name="Value">The value to filter by.</param>
/// <param name="Label">Display label for the pill.</param>
/// <param name="Icon">Optional icon name.</param>
/// <param name="Count">Optional count to display.</param>
public record FilterPillOption<T>(T Value, string Label, string? Icon = null, int? Count = null);
