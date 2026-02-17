using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
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
    private const string XTtsLanguageCode = "en";
    private const string XTtsRelativeModelDirectory = "latent_speaker_folder";
    private const string XvaSynthModeSettingValue = "xva-synth";
    private const string XTtsModeSettingValue = "xtts";
    private const string SettingsFolderName = "MantellaXvaCharacterEditor";
    private const string SettingsFileName = "settings.json";
    private const string FilterAllOptionLabel = "-- ALL --";
    private const string NexusModsUrl = "https://www.nexusmods.com/skyrimspecialedition/mods/172719";
    private const string GitHubUrl = "https://github.com/ElBiggus/Mantella-xVA-Synth";
    private const int NameColumnIndex = 0;
    private const int VoiceModelColumnIndex = 1;
    private const int BioColumnIndex = 2;
    private const int RaceColumnIndex = 6;
    private const int GenderColumnIndex = 7;
    private const int SpeciesColumnIndex = 8;

    private string? _xvaSynthDirectory;
    private string? _xttsDirectory;
    private string? _csvFilePath;
    private TtsProviderMode _currentMode = TtsProviderMode.XvaSynth;
    private bool _isLoadingFields;
    private bool _isProgrammaticCharacterSelection;
    private bool _isDirty;
    private string? _selectedCharacterName;
    private CharacterFormData _lastSavedOrLoadedState = CharacterFormData.Empty;

    private List<string> _characterNames = new();
    private List<string> _voiceModelFilterValues = new();
    private List<string> _speciesValues = new();
    private List<string> _raceValues = new();
    private List<CharacterListEntry> _allCharacters = new();
    private HashSet<string> _voiceModelNames = new(StringComparer.CurrentCultureIgnoreCase);
    private HashSet<string> _voiceModelNameKeys = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        LoadInitialData();
    }

    private void LoadInitialData()
    {
        _csvFilePath = FindCsvFilePath();
        var settings = LoadSettings();
        _xvaSynthDirectory = string.IsNullOrWhiteSpace(settings.XvaSynthDirectory) ? null : settings.XvaSynthDirectory;
        _xttsDirectory = string.IsNullOrWhiteSpace(settings.XTtsDirectory) ? null : settings.XTtsDirectory;
        _currentMode = ParsePersistedMode(settings.Mode);
        UpdateModeMenuChecks();
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

    private UserSettings LoadSettings()
    {
        try
        {
            var settingsPath = GetSettingsFilePath();
            if (!File.Exists(settingsPath))
            {
                return new UserSettings();
            }

            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json);
            return settings ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    private void PersistSettings()
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
                XvaSynthDirectory = string.IsNullOrWhiteSpace(_xvaSynthDirectory) ? null : _xvaSynthDirectory,
                XTtsDirectory = string.IsNullOrWhiteSpace(_xttsDirectory) ? null : _xttsDirectory,
                Mode = _currentMode == TtsProviderMode.XTts ? XTtsModeSettingValue : XvaSynthModeSettingValue
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch
        {
        }
    }

    private TtsProviderMode ParsePersistedMode(string? persistedMode)
    {
        return string.Equals(persistedMode, XTtsModeSettingValue, StringComparison.OrdinalIgnoreCase)
            ? TtsProviderMode.XTts
            : TtsProviderMode.XvaSynth;
    }

    private string? GetCurrentProviderDirectory()
    {
        return _currentMode == TtsProviderMode.XTts ? _xttsDirectory : _xvaSynthDirectory;
    }

    private static string GetVoiceModelFallbackLabel(TtsProviderMode mode)
    {
        return mode == TtsProviderMode.XTts ? "Set xTTS Folder!" : "Set xVA Folder!";
    }

    private void UpdateModeMenuChecks()
    {
        if (XvaSynthModeMenuItem is null || XTtsModeMenuItem is null)
        {
            return;
        }

        XvaSynthModeMenuItem.IsChecked = _currentMode == TtsProviderMode.XvaSynth;
        XTtsModeMenuItem.IsChecked = _currentMode == TtsProviderMode.XTts;
    }

    private void ResetLoadedDataForModeSwitch()
    {
        _isLoadingFields = true;
        CharacterNameTextBox.Text = string.Empty;
        BioTextBox.Text = string.Empty;
        VoiceModelComboBox.SelectedItem = null;
        VoiceModelComboBox.Text = string.Empty;
        GenderComboBox.SelectedIndex = -1;
        SpeciesComboBox.SelectedIndex = -1;
        RaceComboBox.SelectedIndex = -1;
        _isLoadingFields = false;

        _selectedCharacterName = null;
        _allCharacters = new List<CharacterListEntry>();
        _characterNames = new List<string>();
        _voiceModelFilterValues = new List<string>();
        _speciesValues = new List<string>();
        _raceValues = new List<string>();
        _voiceModelNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        _voiceModelNameKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _isProgrammaticCharacterSelection = true;
        CharacterListBox.ItemsSource = Array.Empty<string>();
        CharacterListBox.SelectedItem = null;
        _isProgrammaticCharacterSelection = false;

        _lastSavedOrLoadedState = CaptureCurrentFormState();
        UpdateLoadExistingOverrideButtonState();
        SetDirtyState(false);
    }

    private void SwitchMode(TtsProviderMode nextMode)
    {
        if (_currentMode == nextMode)
        {
            UpdateModeMenuChecks();
            return;
        }

        if (!HandlePendingChanges())
        {
            UpdateModeMenuChecks();
            return;
        }

        _currentMode = nextMode;
        PersistSettings();
        UpdateModeMenuChecks();
        ResetLoadedDataForModeSwitch();
        RefreshCsvDerivedLists();
        UpdateVoiceModelList();
        UpdateLoadExistingOverrideButtonState();
    }

    private void RefreshCsvDerivedLists()
    {
        var rows = ReadCsvRows();
        var charactersByName = new Dictionary<string, CharacterListEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var name = GetColumnValue(row, NameColumnIndex);
            if (string.IsNullOrWhiteSpace(name) || charactersByName.ContainsKey(name))
            {
                continue;
            }

            charactersByName[name] = new CharacterListEntry(
                name,
                GetColumnValue(row, VoiceModelColumnIndex),
                GetColumnValue(row, BioColumnIndex),
                GetColumnValue(row, RaceColumnIndex),
                GetColumnValue(row, GenderColumnIndex),
                GetColumnValue(row, SpeciesColumnIndex));
        }

        _allCharacters = charactersByName.Values
            .OrderBy(character => character.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _characterNames = _allCharacters
            .Select(character => character.Name)
            .ToList();

        _speciesValues = rows
            .Select(row => GetColumnValue(row, SpeciesColumnIndex))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _voiceModelFilterValues = rows
            .Select(row => GetColumnValue(row, VoiceModelColumnIndex))
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

        RefreshFilterDropdownValues();
        ApplyCharacterFilters();

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
        var modelNames = GetVoiceModelNames(_currentMode, GetCurrentProviderDirectory());
        var fallbackLabel = GetVoiceModelFallbackLabel(_currentMode);
        _voiceModelNames = modelNames
            .Where(modelName => !string.IsNullOrWhiteSpace(modelName) &&
                                !string.Equals(modelName, fallbackLabel, StringComparison.CurrentCultureIgnoreCase))
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        _voiceModelNameKeys = _voiceModelNames
            .Select(NormalizeForContainsMatch)
            .Where(modelKey => !string.IsNullOrWhiteSpace(modelKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        VoiceModelComboBox.ItemsSource = modelNames;

        if (!string.IsNullOrWhiteSpace(selectedVoice) && modelNames.Contains(selectedVoice))
        {
            VoiceModelComboBox.SelectedItem = selectedVoice;
        }
        else
        {
            VoiceModelComboBox.SelectedIndex = modelNames.Count > 0 ? 0 : -1;
        }

        ApplyCharacterFilters();
    }

    private void ApplyCharacterFilters()
    {
        if (!AreFilterControlsReady())
        {
            return;
        }

        var nameFilter = NameFilterTextBox.Text.Trim();
        var hasOverrideFilter = GetRadioFilterSelection(HasOverrideAllRadioButton, HasOverrideYesRadioButton);
        var voiceModelStatusFilter = GetVoiceModelStatusFilterSelection();
        var voiceModelFilter = GetFilterDropdownValue(VoiceModelFilterComboBox);
        var genderFilter = GetGenderFilterSelection();
        var raceFilter = GetFilterDropdownValue(RaceFilterComboBox);
        var speciesFilter = GetFilterDropdownValue(SpeciesFilterComboBox);

        var filteredCharacterNames = _allCharacters
            .Where(character =>
                MatchesNameFilter(character.Name, nameFilter)
                && MatchesHasOverrideFilter(character.Name, hasOverrideFilter)
                && MatchesVoiceModelStatusFilter(character.VoiceModel, voiceModelStatusFilter)
                && MatchesExactFilter(character.VoiceModel, voiceModelFilter)
                && MatchesGenderFilter(character.Gender, genderFilter)
                && MatchesExactFilter(character.Race, raceFilter)
                && MatchesExactFilter(character.Species, speciesFilter))
            .Select(character => character.Name)
            .ToList();

        _isProgrammaticCharacterSelection = true;
        CharacterListBox.ItemsSource = filteredCharacterNames;

        if (!string.IsNullOrWhiteSpace(_selectedCharacterName))
        {
            var matchingName = filteredCharacterNames.FirstOrDefault(name =>
                string.Equals(name, _selectedCharacterName, StringComparison.OrdinalIgnoreCase));

            CharacterListBox.SelectedItem = matchingName;
        }
        else
        {
            CharacterListBox.SelectedItem = null;
        }

        _isProgrammaticCharacterSelection = false;
    }

    private bool AreFilterControlsReady()
    {
        return NameFilterTextBox is not null
            && CharacterListBox is not null
            && VoiceModelFilterComboBox is not null
            && RaceFilterComboBox is not null
            && SpeciesFilterComboBox is not null
            && HasOverrideAllRadioButton is not null
            && HasOverrideYesRadioButton is not null
            && VoiceModelStatusAllRadioButton is not null
            && VoiceModelStatusValidRadioButton is not null
            && VoiceModelStatusInvalidRadioButton is not null
            && VoiceModelStatusNoneRadioButton is not null
            && GenderFilterAllRadioButton is not null
            && GenderFilterFemaleRadioButton is not null
            && GenderFilterMaleRadioButton is not null
            && GenderFilterNoneRadioButton is not null;
    }

    private void RefreshFilterDropdownValues()
    {
        if (!AreFilterControlsReady())
        {
            return;
        }

        SetFilterDropdownValues(RaceFilterComboBox, _raceValues);
        SetFilterDropdownValues(SpeciesFilterComboBox, _speciesValues);
        SetFilterDropdownValues(VoiceModelFilterComboBox, _voiceModelFilterValues);
    }

    private static void SetFilterDropdownValues(ComboBox comboBox, IReadOnlyList<string> values)
    {
        var selectedValue = comboBox.SelectedItem?.ToString();
        var options = new List<string> { FilterAllOptionLabel };
        options.AddRange(values);

        comboBox.ItemsSource = options;

        if (!string.IsNullOrWhiteSpace(selectedValue) && options.Contains(selectedValue, StringComparer.CurrentCultureIgnoreCase))
        {
            comboBox.SelectedItem = options.First(option => string.Equals(option, selectedValue, StringComparison.CurrentCultureIgnoreCase));
            return;
        }

        comboBox.SelectedIndex = 0;
    }

    private string GetFilterDropdownValue(ComboBox comboBox)
    {
        var selectedValue = comboBox.SelectedItem?.ToString();
        return string.Equals(selectedValue, FilterAllOptionLabel, StringComparison.CurrentCultureIgnoreCase)
            ? string.Empty
            : selectedValue ?? string.Empty;
    }

    private GenderFilterSelection GetGenderFilterSelection()
    {
        if (GenderFilterFemaleRadioButton.IsChecked == true)
        {
            return GenderFilterSelection.Female;
        }

        if (GenderFilterMaleRadioButton.IsChecked == true)
        {
            return GenderFilterSelection.Male;
        }

        if (GenderFilterNoneRadioButton.IsChecked == true)
        {
            return GenderFilterSelection.None;
        }

        return GenderFilterSelection.All;
    }

    private static FilterSelection GetRadioFilterSelection(RadioButton allRadioButton, RadioButton yesRadioButton)
    {
        if (yesRadioButton.IsChecked == true)
        {
            return FilterSelection.Yes;
        }

        if (allRadioButton.IsChecked == true)
        {
            return FilterSelection.All;
        }

        return FilterSelection.No;
    }

    private static bool MatchesNameFilter(string characterName, string nameFilter)
    {
        if (string.IsNullOrWhiteSpace(nameFilter))
        {
            return true;
        }

        return characterName.Contains(nameFilter, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool MatchesHasOverrideFilter(string characterName, FilterSelection hasOverrideFilter)
    {
        if (hasOverrideFilter == FilterSelection.All)
        {
            return true;
        }

        var overrideFilePath = BuildCharacterOverrideFilePath(characterName);
        var hasOverride = !string.IsNullOrWhiteSpace(overrideFilePath) && File.Exists(overrideFilePath);
        return hasOverrideFilter == FilterSelection.Yes ? hasOverride : !hasOverride;
    }

    private VoiceModelStatusFilterSelection GetVoiceModelStatusFilterSelection()
    {
        if (VoiceModelStatusValidRadioButton.IsChecked == true)
        {
            return VoiceModelStatusFilterSelection.Valid;
        }

        if (VoiceModelStatusInvalidRadioButton.IsChecked == true)
        {
            return VoiceModelStatusFilterSelection.Invalid;
        }

        if (VoiceModelStatusNoneRadioButton.IsChecked == true)
        {
            return VoiceModelStatusFilterSelection.None;
        }

        return VoiceModelStatusFilterSelection.All;
    }

    private bool MatchesVoiceModelStatusFilter(string voiceModel, VoiceModelStatusFilterSelection voiceModelStatusFilter)
    {
        if (voiceModelStatusFilter == VoiceModelStatusFilterSelection.All)
        {
            return true;
        }

        var hasVoiceModel = !string.IsNullOrWhiteSpace(voiceModel);
        var voiceModelKey = NormalizeForContainsMatch(voiceModel);
        var hasValidVoiceModel = hasVoiceModel
            && !string.IsNullOrWhiteSpace(voiceModelKey)
            && _voiceModelNameKeys.Contains(voiceModelKey);

        return voiceModelStatusFilter switch
        {
            VoiceModelStatusFilterSelection.Valid => hasValidVoiceModel,
            VoiceModelStatusFilterSelection.Invalid => !hasValidVoiceModel,
            VoiceModelStatusFilterSelection.None => !hasVoiceModel,
            _ => true
        };
    }

    private static bool MatchesGenderFilter(string gender, GenderFilterSelection genderFilter)
    {
        return genderFilter switch
        {
            GenderFilterSelection.Female => string.Equals(gender, "Female", StringComparison.CurrentCultureIgnoreCase),
            GenderFilterSelection.Male => string.Equals(gender, "Male", StringComparison.CurrentCultureIgnoreCase),
            GenderFilterSelection.None => string.IsNullOrWhiteSpace(gender),
            _ => true
        };
    }

    private static bool MatchesExactFilter(string sourceValue, string filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterValue))
        {
            return true;
        }

        return string.Equals(sourceValue, filterValue, StringComparison.CurrentCultureIgnoreCase);
    }

    private static List<string> GetVoiceModelNames(TtsProviderMode mode, string? providerDirectory)
    {
        if (string.IsNullOrWhiteSpace(providerDirectory) || !Directory.Exists(providerDirectory))
        {
            return new List<string> { GetVoiceModelFallbackLabel(mode) };
        }

        var modelPath = GetVoiceModelDirectoryPath(mode, providerDirectory);
        if (!Directory.Exists(modelPath))
        {
            return new List<string> { GetVoiceModelFallbackLabel(mode) };
        }

        var filePaths = EnumerateVoiceModelFiles(mode, modelPath).ToList();
        var files = filePaths
            .Select(file => Path.GetFileName(file))
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .ToList();

        if (files.Count == 0)
        {
            return new List<string> { GetVoiceModelFallbackLabel(mode) };
        }

        var culture = CultureInfo.CurrentCulture;
        var textInfo = culture.TextInfo;

        var modelNames = files
            .Select(fileName => Path.GetFileNameWithoutExtension(fileName))
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => NormalizeModelToken(fileName!, mode))
            .Where(modelToken => !string.IsNullOrWhiteSpace(modelToken))
            .Select(fileName => fileName.Replace('_', ' ').Replace('-', ' '))
            .Select(fileName => textInfo.ToTitleCase(fileName.ToLower(culture)))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(fileName => fileName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return modelNames.Count == 0 ? new List<string> { GetVoiceModelFallbackLabel(mode) } : modelNames;
    }

    private static string? GetVoiceModelDirectoryPath(TtsProviderMode mode, string? providerDirectory)
    {
        if (string.IsNullOrWhiteSpace(providerDirectory) || !Directory.Exists(providerDirectory))
        {
            return null;
        }

        var modelPath = mode == TtsProviderMode.XTts
            ? Path.Combine(providerDirectory, XTtsRelativeModelDirectory, XTtsLanguageCode)
            : Path.Combine(providerDirectory, "resources", "app", "models", "Skyrim");

        return Directory.Exists(modelPath) ? modelPath : null;
    }

    private static IEnumerable<string> EnumerateVoiceModelFiles(TtsProviderMode mode, string modelDirectoryPath)
    {
        if (mode == TtsProviderMode.XTts)
        {
            return Directory.EnumerateFiles(modelDirectoryPath, "*", System.IO.SearchOption.TopDirectoryOnly);
        }

        return Directory.EnumerateFiles(modelDirectoryPath, "*.json", System.IO.SearchOption.TopDirectoryOnly);
    }

    private static string NormalizeModelToken(string fileNameWithoutExtension, TtsProviderMode mode)
    {
        if (mode == TtsProviderMode.XvaSynth
            && fileNameWithoutExtension.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
        {
            return fileNameWithoutExtension[3..];
        }

        return fileNameWithoutExtension;
    }

    private void PreviewInvalidVoiceModelsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetVoiceModelFixInputs(out var models, out var csvByName, out var visibleCharacterNames))
        {
            return;
        }

        var candidates = BuildVoiceModelFixCandidates(models, csvByName, visibleCharacterNames, out var skippedCount, out var alreadyValidOverrideCount);
        var summaryMessage = $"Would update {candidates.Count} voice models";
        if (skippedCount > 0)
        {
            summaryMessage += $" and {skippedCount} entries wouldn't be updated";
        }

        if (alreadyValidOverrideCount > 0)
        {
            summaryMessage += $"; {alreadyValidOverrideCount} already had a valid override voice model";
        }

        var previewItems = candidates
            .Select(candidate => new VoiceModelFixPreviewRow(candidate.CharacterName, candidate.CurrentVoiceModel, candidate.ResolvedVoiceModel))
            .ToList();

        VoiceModelFixPreviewDialog.Show(this, summaryMessage, previewItems);
    }

    private void FixInvalidVoiceModelsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "This action will create/amend overrides for all characters currently shown in the list.",
            "Fix invalid voice models",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.OK)
        {
            return;
        }

        if (!TryGetVoiceModelFixInputs(out var models, out var csvByName, out var visibleCharacterNames))
        {
            return;
        }

        var candidates = BuildVoiceModelFixCandidates(models, csvByName, visibleCharacterNames, out var skippedCount, out var alreadyValidOverrideCount);

        foreach (var candidate in candidates)
        {
            candidate.Payload.VoiceModel = candidate.ResolvedVoiceModel;
            SaveOverridePayload(candidate.Payload, candidate.CharacterName);
        }

        RefreshCsvDerivedLists();
        ApplyCharacterFilters();
        UpdateLoadExistingOverrideButtonState();

        var summaryMessage = $"Updated {candidates.Count} voice models";
        if (skippedCount > 0)
        {
            summaryMessage += $" and {skippedCount} entries weren't updated";
        }

        if (alreadyValidOverrideCount > 0)
        {
            summaryMessage += $"; skipped {alreadyValidOverrideCount} entries with valid override voice models";
        }

        MessageBox.Show(this, summaryMessage, "Fix invalid voice models", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool TryGetVoiceModelFixInputs(
        out List<VoiceModelCandidate> models,
        out Dictionary<string, CharacterListEntry> csvByName,
        out List<string> visibleCharacterNames)
    {
        models = new List<VoiceModelCandidate>();
        csvByName = new Dictionary<string, CharacterListEntry>(StringComparer.OrdinalIgnoreCase);
        visibleCharacterNames = new List<string>();

        var modelDirectoryPath = GetVoiceModelDirectoryPath(_currentMode, GetCurrentProviderDirectory());
        if (string.IsNullOrWhiteSpace(modelDirectoryPath))
        {
            var providerName = _currentMode == TtsProviderMode.XTts ? "xTTS" : "xVA-Synth";
            MessageBox.Show(this, $"Set a valid {providerName} folder first.", $"{providerName} folder required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        models = EnumerateVoiceModelFiles(_currentMode, modelDirectoryPath)
            .Select(filePath => BuildVoiceModelCandidate(filePath, _currentMode))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!.Value)
            .ToList();

        if (models.Count == 0)
        {
            var providerName = _currentMode == TtsProviderMode.XTts ? "xTTS" : "xVA-Synth";
            MessageBox.Show(this, $"No voice model files were found in the {providerName} models folder.", "No voice models found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var csvRows = ReadCsvRows();
        csvByName = csvRows
            .Select(row => new CharacterListEntry(
                GetColumnValue(row, NameColumnIndex),
                GetColumnValue(row, VoiceModelColumnIndex),
                GetColumnValue(row, BioColumnIndex),
                GetColumnValue(row, RaceColumnIndex),
                GetColumnValue(row, GenderColumnIndex),
                GetColumnValue(row, SpeciesColumnIndex)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        visibleCharacterNames = CharacterListBox.Items
            .Cast<object>()
            .Select(item => item?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return true;
    }

    private List<VoiceModelFixCandidate> BuildVoiceModelFixCandidates(
        IReadOnlyList<VoiceModelCandidate> models,
        IReadOnlyDictionary<string, CharacterListEntry> csvByName,
        IReadOnlyList<string> visibleCharacterNames,
        out int skippedCount,
        out int alreadyValidOverrideCount)
    {
        skippedCount = 0;
        alreadyValidOverrideCount = 0;
        var candidates = new List<VoiceModelFixCandidate>();
        var validModelKeys = models
            .Select(model => NormalizeForContainsMatch(model.VoiceModelName))
            .Where(modelKey => !string.IsNullOrWhiteSpace(modelKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var characterName in visibleCharacterNames)
        {
            if (!csvByName.TryGetValue(characterName, out var csvEntry))
            {
                continue;
            }

            CharacterJsonPayload payload;
            if (TryLoadOverridePayload(characterName, out var overridePayload))
            {
                if (IsValidVoiceModelName(overridePayload.VoiceModel, validModelKeys))
                {
                    alreadyValidOverrideCount++;
                    continue;
                }

                payload = overridePayload;
            }
            else
            {
                if (IsValidVoiceModelName(csvEntry.VoiceModel, validModelKeys))
                {
                    continue;
                }

                payload = CreatePayloadFromCsv(csvEntry);
            }

            var resolveName = string.IsNullOrWhiteSpace(payload.Name) ? csvEntry.Name : payload.Name;
            var resolveGender = string.IsNullOrWhiteSpace(payload.Gender) ? csvEntry.Gender : payload.Gender;
            var resolveRace = string.IsNullOrWhiteSpace(payload.Race) ? csvEntry.Race : payload.Race;
            var resolveSpecies = string.IsNullOrWhiteSpace(payload.Species) ? csvEntry.Species : payload.Species;

            var resolvedModel = ResolveVoiceModelName(resolveName, resolveGender, resolveRace, resolveSpecies, models);
            if (string.IsNullOrWhiteSpace(resolvedModel))
            {
                skippedCount++;
                continue;
            }

            candidates.Add(new VoiceModelFixCandidate(characterName, payload, payload.VoiceModel, resolvedModel));
        }

        return candidates;
    }

    private static bool IsValidVoiceModelName(string voiceModel, IReadOnlySet<string> validModelKeys)
    {
        if (string.IsNullOrWhiteSpace(voiceModel))
        {
            return false;
        }

        var voiceModelKey = NormalizeForContainsMatch(voiceModel);
        return !string.IsNullOrWhiteSpace(voiceModelKey)
            && validModelKeys.Contains(voiceModelKey);
    }

    private bool TryLoadOverridePayload(string characterName, out CharacterJsonPayload payload)
    {
        payload = new CharacterJsonPayload();
        var overridePath = BuildCharacterOverrideFilePath(characterName);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            try
            {
                var json = File.ReadAllText(overridePath);
                var existingPayload = JsonSerializer.Deserialize<CharacterJsonPayload>(json);
                if (existingPayload is not null)
                {
                    payload = existingPayload;
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static CharacterJsonPayload CreatePayloadFromCsv(CharacterListEntry csvEntry)
    {
        return new CharacterJsonPayload
        {
            Name = csvEntry.Name,
            VoiceModel = csvEntry.VoiceModel,
            Bio = csvEntry.Bio,
            Race = csvEntry.Race,
            Gender = csvEntry.Gender,
            Species = csvEntry.Species
        };
    }

    private static VoiceModelCandidate? BuildVoiceModelCandidate(string filePath, TtsProviderMode mode)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return null;
        }

        var modelToken = NormalizeModelToken(fileNameWithoutExtension, mode);
        if (string.IsNullOrWhiteSpace(modelToken))
        {
            return null;
        }

        var normalizedForContains = NormalizeForContainsMatch(modelToken);
        var normalizedForSegmentedPattern = NormalizeForSegmentedPattern(modelToken);
        if (string.IsNullOrWhiteSpace(normalizedForContains) || string.IsNullOrWhiteSpace(normalizedForSegmentedPattern))
        {
            return null;
        }

        var voiceModelName = ToVoiceModelDisplayName(modelToken);
        return new VoiceModelCandidate(normalizedForContains, normalizedForSegmentedPattern, voiceModelName);
    }

    private static string ToVoiceModelDisplayName(string modelToken)
    {
        var culture = CultureInfo.CurrentCulture;
        var textInfo = culture.TextInfo;
        var formatted = modelToken.Replace('_', ' ').Replace('-', ' ');
        var title = textInfo.ToTitleCase(formatted.ToLower(culture));
        return title.Replace(" ", string.Empty);
    }

    private static string? ResolveVoiceModelName(
        string characterName,
        string gender,
        string race,
        string species,
        IReadOnlyList<VoiceModelCandidate> models)
    {
        var normalizedName = NormalizeForContainsMatch(characterName);
        var normalizedGender = NormalizeForContainsMatch(gender);
        var normalizedRace = NormalizeForContainsMatch(race);
        var normalizedSpecies = NormalizeForContainsMatch(species);
        var genderToken = normalizedGender;

        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            var byName = models.FirstOrDefault(model => model.NormalizedForContains.Contains(normalizedName, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(byName.VoiceModelName))
            {
                return byName.VoiceModelName;
            }
        }

        if (!string.IsNullOrWhiteSpace(genderToken) && !string.IsNullOrWhiteSpace(normalizedRace))
        {
            var byGenderRace = models.FirstOrDefault(model =>
                ContainsInOrder(model.NormalizedForSegmentedPattern, genderToken, normalizedRace));
            if (!string.IsNullOrWhiteSpace(byGenderRace.VoiceModelName))
            {
                return byGenderRace.VoiceModelName;
            }
        }

        if (!string.IsNullOrWhiteSpace(genderToken) && !string.IsNullOrWhiteSpace(normalizedSpecies))
        {
            var byGenderSpecies = models.FirstOrDefault(model =>
                ContainsInOrder(model.NormalizedForSegmentedPattern, genderToken, normalizedSpecies));
            if (!string.IsNullOrWhiteSpace(byGenderSpecies.VoiceModelName))
            {
                return byGenderSpecies.VoiceModelName;
            }
        }

        if (!string.IsNullOrWhiteSpace(genderToken))
        {
            var byEventoned = models.FirstOrDefault(model =>
                ContainsInOrder(model.NormalizedForSegmentedPattern, genderToken, "eventoned"));
            if (!string.IsNullOrWhiteSpace(byEventoned.VoiceModelName))
            {
                return byEventoned.VoiceModelName;
            }
        }

        return null;
    }

    private static bool ContainsInOrder(string source, string first, string second)
    {
        if (string.IsNullOrWhiteSpace(source)
            || string.IsNullOrWhiteSpace(first)
            || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        var searchIndex = 0;
        while (searchIndex < source.Length)
        {
            var firstIndex = source.IndexOf(first, searchIndex, StringComparison.Ordinal);
            if (firstIndex < 0)
            {
                return false;
            }

            var hasLeftBoundary = firstIndex == 0 || source[firstIndex - 1] == '_';
            if (hasLeftBoundary)
            {
                var secondIndex = source.IndexOf(second, firstIndex + first.Length, StringComparison.Ordinal);
                if (secondIndex >= 0)
                {
                    return true;
                }
            }

            searchIndex = firstIndex + first.Length;
        }

        return false;
    }

    private static string NormalizeForContainsMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string NormalizeForSegmentedPattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedChars = value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_')
            .ToArray();

        return new string(normalizedChars);
    }

    private static void SaveOverridePayload(CharacterJsonPayload payload, string? characterNameForPath = null)
    {
        var outputPath = BuildCharacterOverrideFilePath(characterNameForPath ?? payload.Name);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        Directory.CreateDirectory(GetCharacterOverridesDirectoryPath());

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        });

        File.WriteAllText(outputPath, json);
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
        ApplyCharacterFilters();
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

    private void HasOverrideFilterRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        ApplyCharacterFilters();
    }

    private void VoiceModelStatusFilterRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        ApplyCharacterFilters();
    }

    private void VoiceModelFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyCharacterFilters();
    }

    private void NameFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyCharacterFilters();
    }

    private void GenderFilterRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        ApplyCharacterFilters();
    }

    private void RaceFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyCharacterFilters();
    }

    private void SpeciesFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyCharacterFilters();
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
        var modeLabel = _currentMode == TtsProviderMode.XTts ? "xTTS" : "xVA Synth";
        Title = isDirty
            ? $"Mantella Character Editor [{modeLabel}] *"
            : $"Mantella Character Editor [{modeLabel}]";
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
            Filter = "xVA-Synth executable|xVASynth.exe|Executable files (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            var selectedFileName = Path.GetFileName(dialog.FileName);
            if (!string.Equals(selectedFileName, "xVASynth.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Please select the xVASynth.exe file.", "Invalid file", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _xvaSynthDirectory = Path.GetDirectoryName(dialog.FileName);
            PersistSettings();
            if (_currentMode == TtsProviderMode.XvaSynth)
            {
                UpdateVoiceModelList();
            }
        }
    }

    private void SetXTtsFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select xTTS executable",
            Filter = "xTTS executable|xtts-api-server-mantella.exe|Executable files (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            var selectedFileName = Path.GetFileName(dialog.FileName);
            if (!string.Equals(selectedFileName, "xtts-api-server-mantella.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Please select the xtts-api-server-mantella.exe file.", "Invalid file", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _xttsDirectory = Path.GetDirectoryName(dialog.FileName);
            PersistSettings();
            if (_currentMode == TtsProviderMode.XTts)
            {
                UpdateVoiceModelList();
            }
        }
    }

    private void XvaSynthModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SwitchMode(TtsProviderMode.XvaSynth);
    }

    private void XTtsModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SwitchMode(TtsProviderMode.XTts);
    }

    private void QuitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ViewOnNexusModsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl(NexusModsUrl);
    }

    private void ViewOnGitHubMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl(GitHubUrl);
    }

    private void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(this, "Unable to open the requested URL.", "Open URL failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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

    private readonly record struct CharacterListEntry(
        string Name,
        string VoiceModel,
        string Bio,
        string Race,
        string Gender,
        string Species);

    private readonly record struct VoiceModelCandidate(
        string NormalizedForContains,
        string NormalizedForSegmentedPattern,
        string VoiceModelName);

    private readonly record struct VoiceModelFixCandidate(
        string CharacterName,
        CharacterJsonPayload Payload,
        string CurrentVoiceModel,
        string ResolvedVoiceModel);

    private enum FilterSelection
    {
        All,
        Yes,
        No
    }

    private enum GenderFilterSelection
    {
        All,
        Female,
        Male,
        None
    }

    private enum VoiceModelStatusFilterSelection
    {
        All,
        Valid,
        Invalid,
        None
    }

    private enum TtsProviderMode
    {
        XvaSynth,
        XTts
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
        public string? XTtsDirectory { get; set; }
        public string? Mode { get; set; }
    }
}