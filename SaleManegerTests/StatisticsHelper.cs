using SaleManeger.Repositories;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

internal static class StatisticsHelper
{
    /// <summary>Statystyki reads its data in the background; this waits until the numbers are in.</summary>
    public static StatisticsViewModel Open(IProjectRepository repo, int timeoutMs = 60000)
    {
        var vm = new StatisticsViewModel(repo);
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (vm.IsLoading && waited.ElapsedMilliseconds < timeoutMs)
            Thread.Sleep(5);
        Assert.That(vm.IsLoading, Is.False, "Statystyki did not finish loading");
        return vm;
    }
}
