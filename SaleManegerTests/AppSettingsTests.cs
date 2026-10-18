using SaleManeger.Services;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>Theme and text size: the settings file, the settings screen and the colours that depend on the theme.</summary>
internal class AppSettingsTests
{
    private string _dir = null!;
    private string _path = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "salemanager-settings-" + Guid.NewGuid());
        _path = Path.Combine(_dir, "nested", "settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        ThemeService.Apply(new AppSettings());
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Test]
    public void Load_MissingFile_GivesDarkAtNormalSize()
    {
        var settings = AppSettingsStore.Load(_path);

        Assert.That(settings.Theme, Is.EqualTo(AppTheme.Dark));
        Assert.That(settings.UiScale, Is.EqualTo(1.0));
    }

    [Test]
    public void SaveThenLoad_KeepsTheChoices_AndCreatesTheFolder()
    {
        var settings = new AppSettings { Theme = AppTheme.Light, UiScale = 1.3 };

        Assert.That(AppSettingsStore.TrySave(settings, _path), Is.True);
        var loaded = AppSettingsStore.Load(_path);

        Assert.That(loaded.Theme, Is.EqualTo(AppTheme.Light));
        Assert.That(loaded.UiScale, Is.EqualTo(1.3));
    }

    [TestCase("not json at all")]
    [TestCase("")]
    [TestCase("null")]
    [TestCase("{\"Theme\": 5, \"UiScale\": \"big\"}")]
    public void Load_DamagedFile_FallsBackToDefaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, content);

        AppSettings? settings = null;
        Assert.DoesNotThrow(() => settings = AppSettingsStore.Load(_path));

        Assert.That(settings!.Theme, Is.EqualTo(AppTheme.Dark));
        Assert.That(settings.UiScale, Is.EqualTo(1.0));
    }

    [TestCase("{\"Theme\": \"Light\", \"UiScale\": 1.16}", AppTheme.Light, 1.15)]
    [TestCase("{\"Theme\": \"light\", \"UiScale\": 9}", AppTheme.Light, 1.3)]
    [TestCase("{\"Theme\": \"Neon\", \"UiScale\": 0}", AppTheme.Dark, 1.0)]
    [TestCase("{\"Theme\": \"Dark\", \"UiScale\": -2}", AppTheme.Dark, 1.0)]
    public void Load_OddValues_AreSnappedToWhatTheScreenOffers(string content, AppTheme theme, double scale)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, content);

        var settings = AppSettingsStore.Load(_path);

        Assert.That(settings.Theme, Is.EqualTo(theme));
        Assert.That(settings.UiScale, Is.EqualTo(scale));
    }

    [Test]
    public void TrySave_UnwritablePath_ReturnsFalseInsteadOfThrowing()
    {
        // a file where a folder is needed
        var blocker = Path.Combine(_dir, "blocker");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(blocker, "x");

        Assert.That(AppSettingsStore.TrySave(new AppSettings(), Path.Combine(blocker, "settings.json")), Is.False);
    }

    [Test]
    public void SettingsScreen_ChangesApplyAtOnce_AreSaved_AndTellTheWindow()
    {
        var settings = new AppSettings();
        var notified = 0;
        var vm = new SettingsViewModel(settings, _path, () => notified++);
        Assert.That(vm.ThemeIndex, Is.EqualTo(0));
        Assert.That(vm.ScaleIndex, Is.EqualTo(0));

        vm.ThemeIndex = 1;
        vm.ScaleIndex = 2;

        Assert.That(settings.Theme, Is.EqualTo(AppTheme.Light));
        Assert.That(settings.UiScale, Is.EqualTo(1.3));
        Assert.That(ThemeService.IsLight, Is.True);
        Assert.That(notified, Is.EqualTo(2));
        Assert.That(vm.StatusText, Is.Empty);
        var saved = AppSettingsStore.Load(_path);
        Assert.That(saved.Theme, Is.EqualTo(AppTheme.Light));
        Assert.That(saved.UiScale, Is.EqualTo(1.3));
    }

    [Test]
    public void SettingsScreen_IgnoresOutOfRangeAndRepeatedValues()
    {
        var notified = 0;
        var vm = new SettingsViewModel(new AppSettings(), _path, () => notified++);

        vm.ThemeIndex = 0;
        vm.ThemeIndex = -1;
        vm.ThemeIndex = 7;
        vm.ScaleIndex = 0;
        vm.ScaleIndex = 9;

        Assert.That(notified, Is.EqualTo(0));
        Assert.That(File.Exists(_path), Is.False);
    }

    [Test]
    public void SettingsScreen_WhenSavingFails_SaysSoButStillApplies()
    {
        var blocker = Path.Combine(_dir, "blocker");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(blocker, "x");
        var settings = new AppSettings();
        var vm = new SettingsViewModel(settings, Path.Combine(blocker, "settings.json"));

        vm.ThemeIndex = 1;

        Assert.That(vm.StatusText, Does.Contain("Nie udało się zapisać"));
        Assert.That(settings.Theme, Is.EqualTo(AppTheme.Light));
    }

    [Test]
    public void SettingsScreen_ReturnAndBackupCommandsExist()
    {
        var vm = new SettingsViewModel(new AppSettings(), _path);
        var returned = false;
        var backups = false;
        vm.ReturnCommand.Subscribe(_ => returned = true);
        vm.OpenBackupsCommand.Subscribe(_ => backups = true);

        vm.ReturnCommand.Execute().Subscribe();
        vm.OpenBackupsCommand.Execute().Subscribe();

        Assert.That(returned && backups, Is.True);
    }

    [Test]
    public void LightTheme_ChangesTheDefaultClientColourSoNoOrderClientsStayVisible()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        db.AddClient("Anna");

        Assert.That(new ClientSelectionViewModel(sale, db.Repo, null).Clients.Single().Color, Is.EqualTo("White"));

        ThemeService.Apply(new AppSettings { Theme = AppTheme.Light });

        Assert.That(new ClientSelectionViewModel(sale, db.Repo, null).Clients.Single().Color, Is.EqualTo("Black"));
    }
}
