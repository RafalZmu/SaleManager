using ReactiveUI;
using SaleManeger.Services;
using System;
using System.Collections.ObjectModel;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class BackupViewModel : ViewModelBase
    {
        private readonly string _dbPath;
        private readonly string _backupDir;
        private readonly Action _beforeRestore;

        private string _statusText = "";
        private BackupInfo _pendingRestore;

        public ObservableCollection<BackupInfo> Backups { get; } = new();

        public string BackupFolder => _backupDir;

        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        // The copy waiting for the "Tak, przywróć" confirmation.
        public BackupInfo PendingRestore
        {
            get => _pendingRestore;
            private set
            {
                this.RaiseAndSetIfChanged(ref _pendingRestore, value);
                this.RaisePropertyChanged(nameof(IsRestoreConfirmationVisible));
                this.RaisePropertyChanged(nameof(RestoreQuestion));
            }
        }

        public bool IsRestoreConfirmationVisible => PendingRestore != null;

        public string RestoreQuestion => PendingRestore == null ? ""
            : $"Przywrócić dane z {PendingRestore.TimeText}? Obecne dane zostaną najpierw zapisane jako kopia \"przed przywróceniem\".";

        public bool HasBackups => Backups.Count > 0;

        public ReactiveCommand<Unit, Unit> ReturnCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateBackupCommand { get; }
        public ReactiveCommand<BackupInfo, Unit> AskRestoreCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelRestoreCommand { get; }

        // True when the database file was replaced; the caller has to reopen the database then.
        public ReactiveCommand<Unit, bool> ConfirmRestoreCommand { get; }

        public BackupViewModel(string dbPath, string backupDir, Action beforeRestore = null)
        {
            _dbPath = dbPath;
            _backupDir = backupDir;
            _beforeRestore = beforeRestore;

            ReturnCommand = ReactiveCommand.Create(() => { });
            CreateBackupCommand = ReactiveCommand.Create(CreateBackup);
            AskRestoreCommand = ReactiveCommand.Create<BackupInfo>(x => PendingRestore = x);
            CancelRestoreCommand = ReactiveCommand.Create(() => { PendingRestore = null; });
            ConfirmRestoreCommand = ReactiveCommand.Create(Restore);

            Reload();
        }

        private void Reload()
        {
            Backups.Clear();
            foreach (var backup in BackupService.List(_backupDir))
                Backups.Add(backup);
            this.RaisePropertyChanged(nameof(HasBackups));
        }

        private void CreateBackup()
        {
            try
            {
                var backup = BackupService.CreateBackup(_dbPath, _backupDir);
                StatusText = $"Zapisano kopię z {backup.TimeText}.";
            }
            catch (Exception ex)
            {
                StatusText = $"Nie udało się zapisać kopii: {ex.Message}";
            }
            Reload();
        }

        private bool Restore()
        {
            var backup = PendingRestore;
            PendingRestore = null;
            if (backup == null)
                return false;

            try
            {
                _beforeRestore?.Invoke();
                BackupService.Restore(backup.Path, _dbPath, _backupDir);
                StatusText = $"Przywrócono dane z {backup.TimeText}.";
                Reload();
                return true;
            }
            catch (Exception ex)
            {
                StatusText = $"Nie udało się przywrócić kopii: {ex.Message}";
                Reload();
                return false;
            }
        }
    }
}
