using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace MantellaXvaCharacterEditor;

public partial class MainWindow : Window
{
    private const string CsvFileName = "skyrim_characters.csv";
    private const string VoiceModelFallbackLabel = "Set xVA Folder!";
    private const string SettingsFolderName = "MantellaXvaCharacterEditor";
    private const string SettingsFileName = "settings.json";
    private const int NameColumnIndex = 0;
    private const int VoiceModelColumnIndex = 1;
    private const int BioColumnIndex = 2;
    private const int RaceColumnIndex = 6;
    private const int GenderColumnIndex = 7;
    private const int SpeciesColumnIndex = 8;

    private string? _xvaSynthDirectory;
    private string? _csvFilePath;
    private bool _isLoadingFields;
    private bool _isProgrammaticCharacterSelection;
    private bool _isDirty;
    private string? _selectedCharacterName;
    private CharacterFormData _lastSavedOrLoadedState = CharacterFormData.Empty;

    private List<string> _characterNames = new();
    private List<string> _speciesValues = new();
    private List<string> _raceValues = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadInitialData();
    }

    private void LoadInitialData()
    {
        _csvFilePath = FindCsvFilePath();
        _xvaSynthDirectory = LoadPersistedXvaSynthDirectory();
        RefreshCsvDerivedLists();
        UpdateVoiceModelList();
        UpdateLoadExistingOverrideButtonState();
        SetDirtyState(false);
    }

    private string GetSettingsFilePath()
    {
        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDirectory = Path.Combine(appDataDirectory, SettingsFolderName);
        return Path.Combine(settingsDirectory, SettingsFileName);
    }

    private static string GetCharacterOverridesDirectoryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Mantella",
            "data",
            "Skyrim",
            "character_overrides");
    }

    private static string? BuildCharacterOverrideFilePath(string? characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        var safeName = string.Join("_", characterName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return null;
        }

        return Path.Combine(GetCharacterOverridesDirectoryPath(), $"{safeName}.json");
    }

    private void UpdateLoadExistingOverrideButtonState()
    {
        var overridePath = BuildCharacterOverrideFilePath(_selectedCharacterName);
        LoadExistingOverrideButton.IsEnabled = !string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath);
    }

    private string? LoadPersistedXvaSynthDirectory()
    {
        try
        {
            var settingsPath = GetSettingsFilePath();
            if (!File.Exists(settingsPath))
            {
                return null;
            }

            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json);
            return string.IsNullOrWhiteSpace(settings?.XvaSynthDirectory)
                ? null
                : settings!.XvaSynthDirectory;
        }
        catch
        {
            return null;
        }
    }

    private void PersistXvaSynthDirectory(string? directory)
    {
        try
        {
            var settingsPath = GetSettingsFilePath();
            var settingsDirectory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            var settings = new UserSettings
            {
                XvaSynthDirectory = string.IsNullOrWhiteSpace(directory) ? null : directory
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch
        {
        }
    }

    private void RefreshCsvDerivedLists()
    {
        var rows = ReadCsvRows();
        _characterNames = rows
            .Select(row => GetColumnValue(row, NameColumnIndex))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _speciesValues = rows
            .Select(row => GetColumnValue(row, SpeciesColumnIndex))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _raceValues = rows
            .Select(row => GetColumnValue(row, RaceColumnIndex))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _isProgrammaticCharacterSelection = true;
        CharacterListBox.ItemsSource = _characterNames;
        _isProgrammaticCharacterSelection = false;

        SpeciesComboBox.ItemsSource = _speciesValues;
        RaceComboBox.ItemsSource = _raceValues;
    }

    private List<string[]> ReadCsvRows()
    {
        var rows = new List<string[]>();
        if (string.IsNullOrWhiteSpace(_csvFilePath) || !File.Exists(_csvFilePath))
        {
            return rows;
        }

        using var parser = new TextFieldParser(_csvFilePath)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };

        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            return rows;
        }

        _ = parser.ReadFields();

        while (!parser.EndOfData)
        {
            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                continue;
            }

            if (fields is null || fields.Length == 0)
            {
                continue;
            }

            var hasAnyValue = fields.Any(field => !string.IsNullOrWhiteSpace(field));
            if (!hasAnyValue)
            {
                continue;
            }

            rows.Add(fields);
        }

        return rows;
    }

    private static string GetColumnValue(IReadOnlyList<string> row, int index)
    {
        if (index < 0 || index >= row.Count)
        {
            return string.Empty;
        }

        return row[index].Trim();
    }

    private string? FindCsvFilePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, CsvFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        var workspaceCandidate = Path.Combine(Environment.CurrentDirectory, CsvFileName);
        return File.Exists(workspaceCandidate) ? workspaceCandidate : null;
    }

    private void UpdateVoiceModelList()
    {
        var selectedVoice = VoiceModelComboBox.SelectedItem?.ToString();
        var modelNames = GetVoiceModelNames(_xvaSynthDirectory);
        VoiceModelComboBox.ItemsSource = modelNames;

        if (!string.IsNullOrWhiteSpace(selectedVoice) && modelNames.Contains(selectedVoice))
        {
            VoiceModelComboBox.SelectedItem = selectedVoice;
        }
        else
        {
            VoiceModelComboBox.SelectedIndex = modelNames.Count > 0 ? 0 : -1;
        }
    }

    private static List<string> GetVoiceModelNames(string? xvaDirectory)
    {
        if (string.IsNullOrWhiteSpace(xvaDirectory) || !Directory.Exists(xvaDirectory))
        {
            return new List<string> { VoiceModelFallbackLabel };
        }

        var modelPath = Path.Combine(xvaDirectory, "resources", "app", "models", "Skyrim");
        if (!Directory.Exists(modelPath))
        {
            return new List<string> { VoiceModelFallbackLabel };
        }

        var files = Directory
            .EnumerateFiles(modelPath, "*.json", System.IO.SearchOption.TopDirectoryOnly)
            .Select(file => Path.GetFileName(file))
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .ToList();

        if (files.Count == 0)
        {
            return new List<string> { VoiceModelFallbackLabel };
        }

        var culture = CultureInfo.CurrentCulture;
        var textInfo = culture.TextInfo;

        var modelNames = files
            .Select(fileName => Path.GetFileNameWithoutExtension(fileName))
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!.StartsWith("sk_", StringComparison.OrdinalIgnoreCase)
                ? fileName[3..]
                : fileName)
            .Select(fileName => fileName.Replace('_', ' ').Replace('-', ' '))
            .Select(fileName => textInfo.ToTitleCase(fileName.ToLower(culture)))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(fileName => fileName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return modelNames.Count == 0 ? new List<string> { VoiceModelFallbackLabel } : modelNames;
    }

    private void CharacterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isProgrammaticCharacterSelection)
        {
            return;
        }

        var nextSelection = CharacterListBox.SelectedItem?.ToString();
        if (string.Equals(nextSelection, _selectedCharacterName, StringComparison.Ordinal))
        {
            return;
        }

        if (!HandlePendingChanges())
        {
            _isProgrammaticCharacterSelection = true;
            CharacterListBox.SelectedItem = _selectedCharacterName;
            _isProgrammaticCharacterSelection = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(nextSelection))
        {
            LoadCharacter(nextSelection);
        }
    }

    private void LoadCharacter(string characterName)
    {
        var rows = ReadCsvRows();
        var row = rows.FirstOrDefault(item =>
            string.Equals(GetColumnValue(item, NameColumnIndex), characterName, StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            return;
        }

        _isLoadingFields = true;

        CharacterNameTextBox.Text = GetColumnValue(row, NameColumnIndex);
        BioTextBox.Text = GetColumnValue(row, BioColumnIndex);

        SetComboValue(VoiceModelComboBox, GetColumnValue(row, VoiceModelColumnIndex));
        SetComboValue(GenderComboBox, GetColumnValue(row, GenderColumnIndex));
        SetComboValue(SpeciesComboBox, GetColumnValue(row, SpeciesColumnIndex));
        SetComboValue(RaceComboBox, GetColumnValue(row, RaceColumnIndex));

        _isLoadingFields = false;
        _selectedCharacterName = characterName;
        _lastSavedOrLoadedState = CaptureCurrentFormState();
        UpdateLoadExistingOverrideButtonState();
        SetDirtyState(false);
    }

    private static void SetComboValue(ComboBox comboBox, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            comboBox.SelectedItem = null;
            comboBox.Text = string.Empty;
            return;
        }

        var item = comboBox.Items.Cast<object>().FirstOrDefault(existing =>
        {
            var existingText = existing is ComboBoxItem comboBoxItem
                ? comboBoxItem.Content?.ToString()
                : existing.ToString();

            return string.Equals(existingText, value, StringComparison.CurrentCultureIgnoreCase);
        });

        if (item is null)
        {
            comboBox.SelectedItem = null;
            comboBox.Text = string.Empty;
        }
        else
        {
            comboBox.SelectedItem = item;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentCharacter();
    }

    private bool SaveCurrentCharacter()
    {
        var name = CharacterNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Character name is required.", "Cannot save", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var payload = new CharacterJsonPayload
        {
            Name = name,
            VoiceModel = VoiceModelComboBox.SelectedItem?.ToString() ?? string.Empty,
            Bio = BioTextBox.Text,
            Race = RaceComboBox.SelectedItem?.ToString() ?? string.Empty,
            Gender = GetComboBoxDisplayValue(GenderComboBox),
            Species = SpeciesComboBox.SelectedItem?.ToString() ?? string.Empty
        };

        var outputDirectory = GetCharacterOverridesDirectoryPath();

        Directory.CreateDirectory(outputDirectory);

        var outputPath = BuildCharacterOverrideFilePath(name);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.Show(this, "Character name contains only invalid filename characters.", "Cannot save", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        });

        File.WriteAllText(outputPath, json);

        _lastSavedOrLoadedState = CaptureCurrentFormState();
        SetDirtyState(false);
        _selectedCharacterName = name;
        UpdateLoadExistingOverrideButtonState();
        return true;
    }

    private static string GetComboBoxDisplayValue(ComboBox comboBox)
    {
        if (comboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Content?.ToString() ?? string.Empty;
        }

        return comboBox.SelectedItem?.ToString() ?? string.Empty;
    }

    private void FieldControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingFields)
        {
            return;
        }

        SetDirtyState(!CaptureCurrentFormState().Equals(_lastSavedOrLoadedState));
    }

    private CharacterFormData CaptureCurrentFormState()
    {
        return new CharacterFormData(
            CharacterNameTextBox.Text.Trim(),
            VoiceModelComboBox.SelectedItem?.ToString() ?? string.Empty,
            GetComboBoxDisplayValue(GenderComboBox),
            SpeciesComboBox.SelectedItem?.ToString() ?? string.Empty,
            RaceComboBox.SelectedItem?.ToString() ?? string.Empty,
            BioTextBox.Text);
    }

    private void SetDirtyState(bool isDirty)
    {
        _isDirty = isDirty;
        Title = isDirty ? "Mantella Character Editor *" : "Mantella Character Editor";
    }

    private bool HandlePendingChanges()
    {
        if (!_isDirty)
        {
            return true;
        }

        var decision = UnsavedChangesDialog.Show(this);
        return decision switch
        {
            UnsavedChangesDecision.Save => SaveCurrentCharacter(),
            UnsavedChangesDecision.Discard => true,
            _ => false
        };
    }

    private void SetXvaSynthFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select xVA-Synth executable",
            Filter = "xVA-Synth executable|xVA-Synth.exe|Executable files (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            var selectedFileName = Path.GetFileName(dialog.FileName);
            if (!string.Equals(selectedFileName, "xVA-Synth.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Please select the xVA-Synth.exe file.", "Invalid file", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _xvaSynthDirectory = Path.GetDirectoryName(dialog.FileName);
            PersistXvaSynthDirectory(_xvaSynthDirectory);
            UpdateVoiceModelList();
        }
    }

    private void QuitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (HandlePendingChanges())
        {
            return;
        }

        e.Cancel = true;
    }

    private void NewCharacterButton_Click(object sender, RoutedEventArgs e)
    {
        if (!HandlePendingChanges())
        {
            return;
        }

        _isLoadingFields = true;
        CharacterNameTextBox.Text = string.Empty;
        BioTextBox.Text = string.Empty;
        VoiceModelComboBox.SelectedIndex = VoiceModelComboBox.Items.Count > 0 ? 0 : -1;
        GenderComboBox.SelectedIndex = -1;
        SpeciesComboBox.SelectedIndex = -1;
        RaceComboBox.SelectedIndex = -1;
        _isLoadingFields = false;

        _isProgrammaticCharacterSelection = true;
        CharacterListBox.SelectedItem = null;
        _isProgrammaticCharacterSelection = false;

        _selectedCharacterName = null;
        _lastSavedOrLoadedState = CaptureCurrentFormState();
        UpdateLoadExistingOverrideButtonState();
        SetDirtyState(false);
    }

    private void LoadExistingOverrideButton_Click(object sender, RoutedEventArgs e)
    {
        if (!HandlePendingChanges())
        {
            return;
        }

        var overridePath = BuildCharacterOverrideFilePath(_selectedCharacterName);
        if (string.IsNullOrWhiteSpace(overridePath) || !File.Exists(overridePath))
        {
            UpdateLoadExistingOverrideButtonState();
            return;
        }

        CharacterJsonPayload? payload;
        try
        {
            var json = File.ReadAllText(overridePath);
            payload = JsonSerializer.Deserialize<CharacterJsonPayload>(json);
        }
        catch
        {
            MessageBox.Show(this, "Unable to load the existing override file.", "Load failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (payload is null)
        {
            MessageBox.Show(this, "Unable to load the existing override file.", "Load failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isLoadingFields = true;
        CharacterNameTextBox.Text = payload.Name;
        BioTextBox.Text = payload.Bio;
        SetComboValue(VoiceModelComboBox, payload.VoiceModel);
        SetComboValue(GenderComboBox, payload.Gender);
        SetComboValue(SpeciesComboBox, payload.Species);
        SetComboValue(RaceComboBox, payload.Race);
        _isLoadingFields = false;

        _lastSavedOrLoadedState = CaptureCurrentFormState();
        SetDirtyState(false);
    }

    private readonly record struct CharacterFormData(
        string Name,
        string VoiceModel,
        string Gender,
        string Species,
        string Race,
        string Bio)
    {
        public static CharacterFormData Empty => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private sealed class CharacterJsonPayload
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("voice_model")]
        public string VoiceModel { get; set; } = string.Empty;

        [JsonPropertyName("bio")]
        public string Bio { get; set; } = string.Empty;

        [JsonPropertyName("race")]
        public string Race { get; set; } = string.Empty;

        [JsonPropertyName("gender")]
        public string Gender { get; set; } = string.Empty;

        [JsonPropertyName("species")]
        public string Species { get; set; } = string.Empty;
    }

    private sealed class UserSettings
    {
        public string? XvaSynthDirectory { get; set; }
    }
}