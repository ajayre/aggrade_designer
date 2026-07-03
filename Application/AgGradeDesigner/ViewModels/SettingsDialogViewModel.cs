using System.Collections.ObjectModel;

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// View model for the settings dialog.
    /// </summary>
    public sealed partial class SettingsDialogViewModel : ViewModelBase
    {
        /// <summary>User settings service supplying persisted AI configuration.</summary>
        private readonly UserSettingsService _SettingsService;

        /// <summary>Working copy of AI settings edited in the dialog session.</summary>
        private AiSettings _SessionAiSettings;

        /// <summary>Working copy of display unit settings edited in the dialog session.</summary>
        private DisplayUnitsSettings _SessionDisplayUnitsSettings;

        /// <summary>
        /// Identifies a settings dialog content section.
        /// </summary>
        public enum SettingsSectionKind
        {
            /// <summary>AI provider and model settings section.</summary>
            Ai,

            /// <summary>Display unit preferences section.</summary>
            Units
        }

        /// <summary>
        /// Initializes sidebar sections, provider options, and loads persisted AI settings.
        /// </summary>
        /// <param name="SettingsService">User settings service; defaults to the app singleton.</param>
        public SettingsDialogViewModel
            (
            UserSettingsService? SettingsService = null
            )
        {
            _SettingsService = SettingsService ?? UserSettingsService.Instance;
            SettingsSectionItemViewModel AiSection = new SettingsSectionItemViewModel(this, SettingsSectionKind.Ai, "AI");
            SettingsSectionItemViewModel UnitsSection = new SettingsSectionItemViewModel(this, SettingsSectionKind.Units, "Units", ShowNavIcon: false);
            Sections = new ReadOnlyCollection<SettingsSectionItemViewModel>([AiSection, UnitsSection]);
            SelectedSection = SettingsSectionKind.Ai;
            AiSection.IsSelected = true;

            ProviderOptions = new ReadOnlyCollection<AiProviderOptionViewModel>(
            [
                new AiProviderOptionViewModel(AiProviderKind.GoogleGemini, "Google Gemini"),
                new AiProviderOptionViewModel(AiProviderKind.OpenAi, "OpenAI"),
                new AiProviderOptionViewModel(AiProviderKind.OpenRouter, "OpenRouter"),
                new AiProviderOptionViewModel(AiProviderKind.CustomOpenAiCompatible, "Custom (OpenAI-compatible)")
            ]);

            DisplayUnitOptions = new ReadOnlyCollection<DisplayUnitOptionViewModel>(
            [
                new DisplayUnitOptionViewModel(DisplayUnitSystem.Imperial, "Imperial"),
                new DisplayUnitOptionViewModel(DisplayUnitSystem.Metric, "Metric")
            ]);

            _SessionAiSettings = _SettingsService.LoadAiSettingsForDialog();
            _SessionDisplayUnitsSettings = _SettingsService.LoadDisplayUnitsSettingsForDialog();
            ApplySessionToBindings();
        }

        /// <summary>
        /// Sidebar entries for choosing a settings section.
        /// </summary>
        public ReadOnlyCollection<SettingsSectionItemViewModel> Sections { get; }

        /// <summary>
        /// Phase 1 provider choices shown in the AI section combo box.
        /// </summary>
        public ReadOnlyCollection<AiProviderOptionViewModel> ProviderOptions { get; }

        /// <summary>
        /// Display unit choices shown in the Units section combo box.
        /// </summary>
        public ReadOnlyCollection<DisplayUnitOptionViewModel> DisplayUnitOptions { get; }

        /// <summary>
        /// Currently displayed settings section.
        /// </summary>
        [ObservableProperty]
        private SettingsSectionKind _SelectedSection;

        /// <summary>
        /// Whether the AI section is currently displayed.
        /// </summary>
        public bool IsAiSectionSelected => SelectedSection == SettingsSectionKind.Ai;

        /// <summary>
        /// Whether the Units section is currently displayed.
        /// </summary>
        public bool IsUnitsSectionSelected => SelectedSection == SettingsSectionKind.Units;

        /// <summary>
        /// Selected display unit option row for the combo box.
        /// </summary>
        [ObservableProperty]
        private DisplayUnitOptionViewModel? _SelectedDisplayUnitOption;

        /// <summary>
        /// Selected display unit system.
        /// </summary>
        [ObservableProperty]
        private DisplayUnitSystem _SelectedDisplayUnitSystem;

        /// <summary>
        /// Selected provider option row for the combo box.
        /// </summary>
        [ObservableProperty]
        private AiProviderOptionViewModel? _SelectedProviderOption;

        /// <summary>
        /// Selected AI provider kind.
        /// </summary>
        [ObservableProperty]
        private AiProviderKind _SelectedProvider;

        /// <summary>
        /// Optional model override for named remote providers.
        /// </summary>
        [ObservableProperty]
        private string _ModelOverride = string.Empty;

        /// <summary>
        /// API key for remote providers.
        /// </summary>
        [ObservableProperty]
        private string _ApiKey = string.Empty;

        /// <summary>
        /// Base URL for custom OpenAI-compatible endpoints.
        /// </summary>
        [ObservableProperty]
        private string _CustomBaseUrl = string.Empty;

        /// <summary>
        /// Model identifier for custom OpenAI-compatible endpoints.
        /// </summary>
        [ObservableProperty]
        private string _CustomModel = string.Empty;

        /// <summary>
        /// Whether the optional model field is visible for the current provider.
        /// </summary>
        public bool ShowNamedProviderModel =>
            SelectedProvider == AiProviderKind.GoogleGemini ||
            SelectedProvider == AiProviderKind.OpenAi ||
            SelectedProvider == AiProviderKind.OpenRouter;

        /// <summary>
        /// Whether the API key field is visible for the current provider.
        /// </summary>
        public bool ShowApiKeyField => SelectedProvider != AiProviderKind.None;

        /// <summary>
        /// Whether custom endpoint fields are visible.
        /// </summary>
        public bool ShowCustomFields => SelectedProvider == AiProviderKind.CustomOpenAiCompatible;

        /// <summary>
        /// Whether the user accepted the dialog.
        /// </summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>
        /// Incremented to signal the view to close the dialog.
        /// </summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>
        /// Switches the visible settings section and updates sidebar selection.
        /// </summary>
        /// <param name="Kind">Section to display.</param>
        internal void SelectSection
            (
            SettingsSectionKind Kind
            )
        {
            SelectedSection = Kind;
            foreach (SettingsSectionItemViewModel Section in Sections)
                Section.IsSelected = Section.Kind == Kind;
            OnPropertyChanged(nameof(IsAiSectionSelected));
            OnPropertyChanged(nameof(IsUnitsSectionSelected));
        }

        /// <summary>
        /// Accepts session edits, persists AI settings, and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Ok
            (
            )
        {
            CopyBindingsToSession();
            _SettingsService.SaveAiSettings(_SessionAiSettings);
            _SettingsService.SaveDisplayUnitsSettings(_SessionDisplayUnitsSettings);
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Cancels session edits without persisting and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Cancel
            (
            )
        {
            Confirmed = false;
            CloseToken++;
        }

        /// <summary>
        /// Copies persisted settings into dialog bindings when the provider option changes.
        /// </summary>
        /// <param name="Value">New provider option.</param>
        partial void OnSelectedProviderOptionChanged
            (
            AiProviderOptionViewModel? Value
            )
        {
            if (Value != null)
                SelectedProvider = Value.Kind;
            NotifyProviderFieldVisibility();
        }

        /// <summary>
        /// Copies persisted settings into dialog bindings when the provider changes.
        /// </summary>
        /// <param name="Value">New provider selection.</param>
        partial void OnSelectedProviderChanged
            (
            AiProviderKind Value
            )
        {
            NotifyProviderFieldVisibility();
        }

        /// <summary>
        /// Copies persisted settings into dialog bindings when the display unit option changes.
        /// </summary>
        /// <param name="Value">New display unit option.</param>
        partial void OnSelectedDisplayUnitOptionChanged
            (
            DisplayUnitOptionViewModel? Value
            )
        {
            if (Value != null)
                SelectedDisplayUnitSystem = Value.System;
        }

        /// <summary>
        /// Applies the session AI settings snapshot to dialog bindings.
        /// </summary>
        private void ApplySessionToBindings
            (
            )
        {
            SelectedProvider = _SessionAiSettings.ResolvePhase12Provider();
            SelectedProviderOption = ProviderOptions.First(O => O.Kind == SelectedProvider);
            ModelOverride = _SessionAiSettings.ModelOverride;
            ApiKey = _SessionAiSettings.ApiKey;
            CustomBaseUrl = _SessionAiSettings.CustomBaseUrl;
            CustomModel = _SessionAiSettings.CustomModel;
            NotifyProviderFieldVisibility();

            SelectedDisplayUnitSystem = _SessionDisplayUnitsSettings.System;
            SelectedDisplayUnitOption = DisplayUnitOptions.First(O => O.System == SelectedDisplayUnitSystem);
        }

        /// <summary>
        /// Copies dialog bindings into the session AI settings snapshot.
        /// </summary>
        private void CopyBindingsToSession
            (
            )
        {
            _SessionAiSettings.Provider = SelectedProvider;
            _SessionAiSettings.ModelOverride = ModelOverride;
            _SessionAiSettings.ApiKey = ApiKey;
            _SessionAiSettings.CustomBaseUrl = CustomBaseUrl;
            _SessionAiSettings.CustomModel = CustomModel;

            _SessionDisplayUnitsSettings.System = SelectedDisplayUnitSystem;
        }

        /// <summary>
        /// Raises visibility change notifications for provider-dependent fields.
        /// </summary>
        private void NotifyProviderFieldVisibility
            (
            )
        {
            OnPropertyChanged(nameof(ShowNamedProviderModel));
            OnPropertyChanged(nameof(ShowApiKeyField));
            OnPropertyChanged(nameof(ShowCustomFields));
        }
    }
}
