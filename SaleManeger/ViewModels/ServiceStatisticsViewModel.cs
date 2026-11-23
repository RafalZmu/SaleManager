using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaleManeger.ViewModels
{
    public class ServiceMetricOption
    {
        public string DisplayName { get; init; }
        public string AxisTitle { get; init; }
        public ServiceMetric Metric { get; init; }
    }

    /// <summary>
    /// The "Czas obsługi" statistics: how long serving clients takes, in four groups (summary, correlations,
    /// products, hours and clients). Everything is calculated in memory from the already loaded snapshot.
    /// </summary>
    public class ServiceStatisticsViewModel : ReactiveObject
    {
        private readonly SalesSnapshot _data;
        private readonly List<ServiceRecord> _allRecords;
        private List<ServiceRecord> _records = new();

        public ServiceStatisticsViewModel(SalesSnapshot data)
        {
            _data = data;
            _allRecords = ServiceAnalytics.BuildRecords(data);
            _selectedMetric = MetricOptions[0];
            _selectedScope = Scopes[3];
            Recalculate();
        }

        public List<PerformanceScope> Scopes { get; } = new()
        {
            new PerformanceScope { DisplayName = "Ostatnie 3 sprzedaże", LastSales = 3 },
            new PerformanceScope { DisplayName = "Ostatnie 5 sprzedaży", LastSales = 5 },
            new PerformanceScope { DisplayName = "Ostatnie 10 sprzedaży", LastSales = 10 },
            new PerformanceScope { DisplayName = "Wszystkie sprzedaże", LastSales = 0 },
        };

        private PerformanceScope _selectedScope;
        public PerformanceScope SelectedScope
        {
            get => _selectedScope;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedScope, value);
                if (value != null)
                    Recalculate();
            }
        }

        public List<ServiceMetricOption> MetricOptions { get; } = new()
        {
            new ServiceMetricOption { DisplayName = "Waga kupionych produktów (kg)", AxisTitle = "Waga (kg)", Metric = ServiceMetric.Kg },
            new ServiceMetricOption { DisplayName = "Liczba kupionych pozycji", AxisTitle = "Pozycje", Metric = ServiceMetric.Items },
            new ServiceMetricOption { DisplayName = "Wartość zakupów (zł)", AxisTitle = "Wartość (zł)", Metric = ServiceMetric.Value },
        };

        private ServiceMetricOption _selectedMetric;
        public ServiceMetricOption SelectedMetric
        {
            get => _selectedMetric;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedMetric, value);
                if (value != null)
                    BuildCorrelation();
            }
        }

        // ---- Overall ----

        private bool _hasData;
        public bool HasData
        {
            get => _hasData;
            private set
            {
                this.RaiseAndSetIfChanged(ref _hasData, value);
                this.RaisePropertyChanged(nameof(HasNoData));
            }
        }

        public bool HasNoData => !HasData;

        public string EmptyText => "Brak danych o czasie obsługi. Czas liczy się od wpisania pierwszego kupionego produktu z wagą do zamknięcia klienta, " +
                                   "więc pojawi się po obsłużeniu pierwszych klientów w tej wersji programu.";

        private string _countText = "";
        public string CountText { get => _countText; private set => this.RaiseAndSetIfChanged(ref _countText, value); }

        private string _averageText = "";
        public string AverageText { get => _averageText; private set => this.RaiseAndSetIfChanged(ref _averageText, value); }

        private string _medianText = "";
        public string MedianText { get => _medianText; private set => this.RaiseAndSetIfChanged(ref _medianText, value); }

        private string _fastestText = "";
        public string FastestText { get => _fastestText; private set => this.RaiseAndSetIfChanged(ref _fastestText, value); }

        private string _slowestText = "";
        public string SlowestText { get => _slowestText; private set => this.RaiseAndSetIfChanged(ref _slowestText, value); }

        private string _percentile90Text = "";
        public string Percentile90Text { get => _percentile90Text; private set => this.RaiseAndSetIfChanged(ref _percentile90Text, value); }

        private string _totalText = "";
        public string TotalText { get => _totalText; private set => this.RaiseAndSetIfChanged(ref _totalText, value); }

        private string _perKgText = "";
        public string PerKgText { get => _perKgText; private set => this.RaiseAndSetIfChanged(ref _perKgText, value); }

        private string _perItemText = "";
        public string PerItemText { get => _perItemText; private set => this.RaiseAndSetIfChanged(ref _perItemText, value); }

        private string _perHundredText = "";
        public string PerHundredText { get => _perHundredText; private set => this.RaiseAndSetIfChanged(ref _perHundredText, value); }

        private string _clientsPerHourText = "";
        public string ClientsPerHourText { get => _clientsPerHourText; private set => this.RaiseAndSetIfChanged(ref _clientsPerHourText, value); }

        private PlotModel _distributionPlot;
        public PlotModel DistributionPlot { get => _distributionPlot; private set => this.RaiseAndSetIfChanged(ref _distributionPlot, value); }

        private PlotModel _salePlot;
        public PlotModel SalePlot { get => _salePlot; private set => this.RaiseAndSetIfChanged(ref _salePlot, value); }

        private ObservableCollection<ServiceSaleRow> _saleRows = new();
        public ObservableCollection<ServiceSaleRow> SaleRows { get => _saleRows; private set => this.RaiseAndSetIfChanged(ref _saleRows, value); }

        // ---- Correlations ----

        private PlotModel _correlationPlot;
        public PlotModel CorrelationPlot { get => _correlationPlot; private set => this.RaiseAndSetIfChanged(ref _correlationPlot, value); }

        private string _correlationText = "";
        public string CorrelationText { get => _correlationText; private set => this.RaiseAndSetIfChanged(ref _correlationText, value); }

        private ObservableCollection<string> _correlationOverview = new();

        // One line per metric, so the three correlations can be compared without switching.
        public ObservableCollection<string> CorrelationOverview { get => _correlationOverview; private set => this.RaiseAndSetIfChanged(ref _correlationOverview, value); }

        // ---- Products ----

        private ObservableCollection<ServiceProductRow> _productRows = new();
        public ObservableCollection<ServiceProductRow> ProductRows { get => _productRows; private set => this.RaiseAndSetIfChanged(ref _productRows, value); }

        // ---- Hours and clients ----

        private PlotModel _hourPlot;
        public PlotModel HourPlot { get => _hourPlot; private set => this.RaiseAndSetIfChanged(ref _hourPlot, value); }

        private ObservableCollection<ServiceClientRow> _clientRows = new();
        public ObservableCollection<ServiceClientRow> ClientRows { get => _clientRows; private set => this.RaiseAndSetIfChanged(ref _clientRows, value); }

        private void Recalculate()
        {
            _records = FilterByScope(_allRecords, _selectedScope?.LastSales ?? 0);
            HasData = _records.Count > 0;

            var summary = ServiceAnalytics.Summarize(_records);
            CountText = summary.Count.ToString();
            AverageText = Show(summary.Count, summary.AverageSeconds);
            MedianText = Show(summary.Count, summary.MedianSeconds);
            FastestText = Show(summary.Count, summary.FastestSeconds);
            SlowestText = Show(summary.Count, summary.SlowestSeconds);
            Percentile90Text = Show(summary.Count, summary.Percentile90Seconds);
            TotalText = Show(summary.Count, summary.TotalSeconds);
            PerKgText = summary.SecondsPerKg == null ? "-" : ServiceAnalytics.Duration(summary.SecondsPerKg.Value);
            PerItemText = summary.SecondsPerItem == null ? "-" : ServiceAnalytics.Duration(summary.SecondsPerItem.Value);
            PerHundredText = summary.SecondsPerHundredZl == null ? "-" : ServiceAnalytics.Duration(summary.SecondsPerHundredZl.Value);
            ClientsPerHourText = summary.ClientsPerHour == null ? "-" : ServiceAnalytics.Num(summary.ClientsPerHour.Value);

            DistributionPlot = _records.Count == 0 ? null : BuildDistributionPlot();
            var sales = ServiceAnalytics.BySale(_records);
            SaleRows = new ObservableCollection<ServiceSaleRow>(sales);
            SalePlot = sales.Count == 0 ? null : BuildSalePlot(sales);

            BuildCorrelation();
            ProductRows = new ObservableCollection<ServiceProductRow>(ServiceAnalytics.ByProduct(_records, _data.ProductsById));
            HourPlot = _records.Count == 0 ? null : BuildHourPlot(ServiceAnalytics.ByHour(_records));
            ClientRows = new ObservableCollection<ServiceClientRow>(ServiceAnalytics.ByClient(_records));
        }

        private static string Show(int count, double seconds) => count == 0 ? "-" : ServiceAnalytics.Duration(seconds);

        // The newest sales are the ones whose timed clients started last.
        private static List<ServiceRecord> FilterByScope(List<ServiceRecord> all, int lastSales)
        {
            if (lastSales <= 0)
                return all;
            var keep = all.GroupBy(x => x.SaleID)
                .OrderByDescending(g => g.Min(x => x.Start))
                .Take(lastSales)
                .Select(g => g.Key)
                .ToHashSet();
            return all.Where(x => keep.Contains(x.SaleID)).ToList();
        }

        private void BuildCorrelation()
        {
            if (_records.Count == 0)
            {
                CorrelationPlot = null;
                CorrelationText = "";
                CorrelationOverview = new ObservableCollection<string>();
                return;
            }

            var option = _selectedMetric ?? MetricOptions[0];
            var correlation = ServiceAnalytics.Correlate(_records, option.Metric);
            CorrelationText = $"{correlation.Description} (obsłużonych klientów: {correlation.Count})";

            var plot = NewPlot();
            plot.Axes.Add(StyledAxis(AxisPosition.Bottom, option.AxisTitle));
            plot.Axes.Add(StyledAxis(AxisPosition.Left, "Czas obsługi (min)"));

            var points = new ScatterSeries { Title = "Klient", MarkerType = MarkerType.Circle, MarkerSize = 5, MarkerFill = ThemeService.PlotAccent };
            foreach (var r in _records)
                points.Points.Add(new ScatterPoint(ServiceAnalytics.ValueOf(r, option.Metric), r.Seconds / 60.0));
            plot.Series.Add(points);

            if (correlation.R != null)
            {
                var xs = _records.Select(r => ServiceAnalytics.ValueOf(r, option.Metric)).ToList();
                var line = new LineSeries { Title = "Trend", Color = ThemeService.PlotGood, StrokeThickness = 2 };
                foreach (var x in new[] { xs.Min(), xs.Max() })
                    line.Points.Add(new DataPoint(x, (correlation.Intercept + correlation.Slope * x) / 60.0));
                plot.Series.Add(line);
            }
            plot.Axes[1].Minimum = 0;
            plot.Legends.Add(new Legend { LegendPosition = LegendPosition.TopLeft, LegendTextColor = ThemeService.PlotText });
            CorrelationPlot = plot;

            CorrelationOverview = new ObservableCollection<string>(MetricOptions.Select(o =>
            {
                var c = ServiceAnalytics.Correlate(_records, o.Metric);
                return c.R == null
                    ? $"{o.DisplayName}: za mało danych"
                    : $"{o.DisplayName}: r = {c.R.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";
            }));
        }

        private PlotModel BuildDistributionPlot()
        {
            var buckets = ServiceAnalytics.Distribution(_records);
            var plot = NewPlot();
            var categories = new CategoryAxis { Position = AxisPosition.Left, StartPosition = 1, EndPosition = 0, TextColor = ThemeService.PlotText, TicklineColor = ThemeService.PlotText, Title = "Czas obsługi", TitleColor = ThemeService.PlotText };
            categories.Labels.AddRange(buckets.Select(x => x.Label));
            plot.Axes.Add(categories);
            var counts = StyledAxis(AxisPosition.Bottom, "Klienci");
            counts.Minimum = 0;
            counts.MinimumRange = 3;
            plot.Axes.Add(counts);
            var bars = new BarSeries { FillColor = ThemeService.PlotAccent, LabelPlacement = LabelPlacement.Outside, LabelFormatString = "{0}", TextColor = ThemeService.PlotText };
            foreach (var b in buckets)
                bars.Items.Add(new BarItem(b.Count));
            plot.Series.Add(bars);
            return plot;
        }

        private static PlotModel BuildSalePlot(IReadOnlyList<ServiceSaleRow> sales)
        {
            var rows = sales.Count > 24 ? sales.Skip(sales.Count - 24).ToList() : sales;
            var plot = NewPlot();
            var categories = new CategoryAxis { Position = AxisPosition.Bottom, TextColor = ThemeService.PlotText, TicklineColor = ThemeService.PlotText, Angle = rows.Count > 6 ? -35 : 0 };
            categories.Labels.AddRange(rows.Select(x => x.SaleName));
            plot.Axes.Add(categories);
            var minutes = StyledAxis(AxisPosition.Left, "Średni czas klienta (min)");
            minutes.Key = "minutes";
            minutes.Minimum = 0;
            plot.Axes.Add(minutes);
            plot.Series.Add(new LineSeries
            {
                Title = "Średni czas obsługi",
                Color = ThemeService.PlotGood,
                MarkerType = MarkerType.Circle,
                YAxisKey = "minutes",
                ItemsSource = rows.Select((x, i) => new DataPoint(i, x.AverageSeconds / 60.0)).ToList()
            });
            plot.Legends.Add(new Legend { LegendPosition = LegendPosition.TopLeft, LegendTextColor = ThemeService.PlotText });
            return plot;
        }

        private static PlotModel BuildHourPlot(IReadOnlyList<ServiceHourRow> hours)
        {
            var plot = NewPlot();
            var categories = new CategoryAxis { Position = AxisPosition.Left, StartPosition = 1, EndPosition = 0, TextColor = ThemeService.PlotText, TicklineColor = ThemeService.PlotText, Title = "Godzina rozpoczęcia obsługi", TitleColor = ThemeService.PlotText };
            categories.Labels.AddRange(hours.Select(x => $"{x.HourText} ({x.Clients})"));
            plot.Axes.Add(categories);
            var minutes = StyledAxis(AxisPosition.Bottom, "Średni czas klienta (min)");
            minutes.Minimum = 0;
            plot.Axes.Add(minutes);
            var bars = new BarSeries { FillColor = ThemeService.PlotAccent, LabelPlacement = LabelPlacement.Outside, LabelFormatString = "{0:0.0}", TextColor = ThemeService.PlotText };
            foreach (var h in hours)
                bars.Items.Add(new BarItem(h.AverageSeconds / 60.0));
            plot.Series.Add(bars);
            return plot;
        }

        private static PlotModel NewPlot() => new()
        {
            TextColor = ThemeService.PlotText,
            PlotAreaBorderColor = ThemeService.PlotText,
            TitleColor = ThemeService.PlotText,
        };

        private static LinearAxis StyledAxis(AxisPosition position, string title) => new()
        {
            Position = position,
            Title = title,
            TextColor = ThemeService.PlotText,
            TitleColor = ThemeService.PlotText,
            TicklineColor = ThemeService.PlotText,
        };
    }
}
