using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace MantellaXvaCharacterEditor;

public readonly record struct VoiceModelFixPreviewRow(
    string CharacterName,
    string CurrentVoiceModel,
    string NewVoiceModel);

public partial class VoiceModelFixPreviewDialog : Window
{
    private string? _lastSortMember;
    private ListSortDirection _lastSortDirection = ListSortDirection.Ascending;

    public VoiceModelFixPreviewDialog(string summary, IReadOnlyList<VoiceModelFixPreviewRow> previewRows)
    {
        InitializeComponent();
        SummaryTextBlock.Text = summary;
        PreviewListView.ItemsSource = previewRows;
    }

    public static void Show(Window owner, string summary, IReadOnlyList<VoiceModelFixPreviewRow> previewRows)
    {
        var dialog = new VoiceModelFixPreviewDialog(summary, previewRows)
        {
            Owner = owner
        };

        _ = dialog.ShowDialog();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header)
        {
            return;
        }

        if (header.Role == GridViewColumnHeaderRole.Padding)
        {
            return;
        }

        var sortMember = header.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(sortMember))
        {
            return;
        }

        var sortDirection = ListSortDirection.Ascending;
        if (string.Equals(_lastSortMember, sortMember, StringComparison.Ordinal)
            && _lastSortDirection == ListSortDirection.Ascending)
        {
            sortDirection = ListSortDirection.Descending;
        }

        var view = CollectionViewSource.GetDefaultView(PreviewListView.ItemsSource);
        if (view is null)
        {
            return;
        }

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(sortMember, sortDirection));
        view.Refresh();
        UpdateSortHeaderIndicators(header, sortDirection);

        _lastSortMember = sortMember;
        _lastSortDirection = sortDirection;
    }

    private void UpdateSortHeaderIndicators(GridViewColumnHeader activeHeader, ListSortDirection direction)
    {
        if (PreviewListView.View is not GridView gridView)
        {
            return;
        }

        foreach (var column in gridView.Columns)
        {
            if (column.Header is not GridViewColumnHeader columnHeader)
            {
                continue;
            }

            var currentLabel = columnHeader.Content?.ToString() ?? string.Empty;
            columnHeader.Content = StripSortIndicator(currentLabel);
        }

        var activeLabel = activeHeader.Content?.ToString() ?? string.Empty;
        var arrow = direction == ListSortDirection.Ascending ? " ▲" : " ▼";
        activeHeader.Content = $"{StripSortIndicator(activeLabel)}{arrow}";
    }

    private static string StripSortIndicator(string headerLabel)
    {
        if (string.IsNullOrWhiteSpace(headerLabel))
        {
            return string.Empty;
        }

        if (headerLabel.EndsWith(" ▲", StringComparison.Ordinal)
            || headerLabel.EndsWith(" ▼", StringComparison.Ordinal))
        {
            return headerLabel[..^2];
        }

        return headerLabel;
    }
}
