using ReactiveUI;
using SaleManeger.Services;
using System;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    /// <summary>Look of the program: dark or light theme and the size of text and buttons. Changes apply at once and are remembered.</summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly string _path;
        private readonly Action _changed;
        private string _statusText = "";

        public AppSettings Settings { get; }

        public string[] ThemeNames { get; } = { "Ciemny", "Jasny" };

        public string[] ScaleNames { get; } = { "Normalny (100%)", "Większy (115%)", "Największy (130%)" };

        public int ThemeIndex
        {
            get => (int)Settings.Theme;
            set
            {
                if (value < 0 || value >= ThemeNames.Length || value == ThemeIndex) return;
                Settings.Theme = (AppTheme)value;
                this.RaisePropertyChanged();
                Changed();
            }
        }

        public int ScaleIndex
        {
            get => Array.IndexOf(AppSettings.Scales, Settings.UiScale);
            set
            {
                if (value < 0 || value >= AppSettings.Scales.Length || value == ScaleIndex) return;
                Settings.UiScale = AppSettings.Scales[value];
                this.RaisePropertyChanged();
                Changed();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        public ReactiveCommand<Unit, Unit> ReturnCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenBackupsCommand { get; }

        public SettingsViewModel(AppSettings settings, string path, Action changed = null)
        {
            Settings = settings;
            _path = path;
            _changed = changed;
            ReturnCommand = ReactiveCommand.Create(() => { });
            OpenBackupsCommand = ReactiveCommand.Create(() => { });
        }

        private void Changed()
        {
            ThemeService.Apply(Settings);
            StatusText = AppSettingsStore.TrySave(Settings, _path) ? "" : "Nie udało się zapisać ustawień, obowiązują do zamknięcia programu.";
            _changed?.Invoke();
        }
    }
}
