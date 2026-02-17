using System.Windows;

namespace MantellaXvaCharacterEditor;

public readonly record struct VoiceModelFixPreviewRow(
    string CharacterName,
    string CurrentVoiceModel,
    string NewVoiceModel);

public partial class VoiceModelFixPreviewDialog : Window
{
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
}
