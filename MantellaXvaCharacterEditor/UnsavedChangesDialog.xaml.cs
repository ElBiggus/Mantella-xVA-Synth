using System.Windows;

namespace MantellaXvaCharacterEditor;

public enum UnsavedChangesDecision
{
    Save,
    Discard,
    Cancel
}

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDecision Decision { get; private set; } = UnsavedChangesDecision.Cancel;

    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    public static UnsavedChangesDecision Show(Window owner)
    {
        var dialog = new UnsavedChangesDialog
        {
            Owner = owner
        };

        _ = dialog.ShowDialog();
        return dialog.Decision;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Decision = UnsavedChangesDecision.Save;
        DialogResult = true;
    }

    private void DiscardButton_Click(object sender, RoutedEventArgs e)
    {
        Decision = UnsavedChangesDecision.Discard;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Decision = UnsavedChangesDecision.Cancel;
        DialogResult = false;
    }
}