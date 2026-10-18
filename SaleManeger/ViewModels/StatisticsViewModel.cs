using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using SaleManeger.Services;

namespace SaleManeger.ViewModels
{
    public class SaleScope
    {
        public string SaleID { get; set; }
        public string DisplayName { get; set; }
    }

    public class OrderAccuracyMetric
    {
        public string ProductName { get; set; }
        public double PercentageDifference { get; set; }
        
        public string PercentageString 
        { 
            get 
            {
                if (double.IsNaN(PercentageDifference) || double.IsInfinity(PercentageDifference))
                    return "Brak danych";
                var modifier = PercentageDifference > 0 ? "+" : "";
                return $"{modifier}{PercentageDifference:0.0}%";
            }
        }
        
        public IBrush PercentageColor
        {
            get
            {
                if (double.IsNaN(PercentageDifference)) return Brushes.Gray;
                if (PercentageDifference > 0) return ThemeService.GoodBrush;
                if (PercentageDifference < 0) return ThemeService.BadBrush;
                return ThemeService.TextBrush;
            }
        }
    }

    public class PerformanceScope
    {
        public string DisplayName { get; set; }

        // How many of the newest sales are looked at; 0 = all.
        public int LastSales { get; set; }
    }

    public class StatisticsViewModel : ViewModelBase
    {
        // Loaded once, off the UI thread, when the tab opens; switching sale or range then only recalculates in memory.
        private SalesSnapshot _data;
        private List<SaleTrendRow> _trends;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        // "Ładowanie..." while the data is read; the reason when reading failed; empty otherwise.
        private string _loadMessage = "";
        public string LoadMessage
        {
            get => _loadMessage;
            private set => this.RaiseAndSetIfChanged(ref _loadMessage, value);
        }

        public ReactiveCommand<Unit, Unit> ReturnCommand { get; }

        public ObservableCollection<SaleScope> SaleScopes { get; } = new();
        
        private SaleScope _selectedSaleScope;
        public SaleScope SelectedSaleScope
        {
            get => _selectedSaleScope;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSaleScope, value);
                if (value != null)
                {
                    CalculateAccuracyMetrics();
                }
            }
        }

        private ObservableCollection<OrderAccuracyMetric> _accuracyMetrics;
        public ObservableCollection<OrderAccuracyMetric> AccuracyMetrics
        {
            get => _accuracyMetrics;
            set => this.RaiseAndSetIfChanged(ref _accuracyMetrics, value);
        }

        // ---- Przychód i trendy ----

        private ObservableCollection<SaleTrendRow> _saleTrends = new();
        public ObservableCollection<SaleTrendRow> SaleTrends
        {
            get => _saleTrends;
            private set
            {
                this.RaiseAndSetIfChanged(ref _saleTrends, value);
                this.RaisePropertyChanged(nameof(HasSaleTrends));
            }
        }

        private PlotModel _trendPlot;
        public PlotModel TrendPlot
        {
            get => _trendPlot;
            private set => this.RaiseAndSetIfChanged(ref _trendPlot, value);
        }

        public bool HasSaleTrends => SaleTrends.Count > 0;

        private string _trendSummary = "";
        public string TrendSummary
        {
            get => _trendSummary;
            private set => this.RaiseAndSetIfChanged(ref _trendSummary, value);
        }

        // ---- Wyniki produktów ----

        public List<PerformanceScope> PerformanceScopes { get; } = new()
        {
            new PerformanceScope { DisplayName = "Ostatnie 3 sprzedaże", LastSales = 3 },
            new PerformanceScope { DisplayName = "Ostatnie 5 sprzedaży", LastSales = 5 },
            new PerformanceScope { DisplayName = "Ostatnie 10 sprzedaży", LastSales = 10 },
            new PerformanceScope { DisplayName = "Wszystkie sprzedaże", LastSales = 0 },
        };

        private PerformanceScope _selectedPerformanceScope;
        public PerformanceScope SelectedPerformanceScope
        {
            get => _selectedPerformanceScope;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedPerformanceScope, value);
                if (value != null)
                    CalculateProductPerformance();
            }
        }

        private ObservableCollection<ProductPerformanceRow> _productPerformance = new();
        public ObservableCollection<ProductPerformanceRow> ProductPerformance
        {
            get => _productPerformance;
            private set
            {
                this.RaiseAndSetIfChanged(ref _productPerformance, value);
                this.RaisePropertyChanged(nameof(HasProductPerformance));
            }
        }

        public bool HasProductPerformance => ProductPerformance.Count > 0;

        public StatisticsViewModel(IProjectRepository db)
        {
            // Going back is held until the read ends: the database context must not be used from two places at once.
            ReturnCommand = ReactiveCommand.Create(() => { }, this.WhenAnyValue(x => x.IsLoading, loading => !loading));

            IsLoading = true;
            LoadMessage = "Ładowanie statystyk...";
            Observable.Start(() => SalesSnapshot.Load(db), RxApp.TaskpoolScheduler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(OnLoaded, OnLoadFailed);
        }

        private void OnLoaded(SalesSnapshot data)
        {
            _data = data;
            InitializeScopes();
            CalculateSaleTrends();
            SelectedPerformanceScope = PerformanceScopes[1];
            LoadMessage = "";
            IsLoading = false;
        }

        private void OnLoadFailed(Exception error)
        {
            LoadMessage = "Nie udało się wczytać statystyk: " + error.Message;
            IsLoading = false;
        }

        private void CalculateSaleTrends()
        {
            _trends = SalesAnalytics.BuildSaleTrends(_data);
            SaleTrends = new ObservableCollection<SaleTrendRow>(_trends);

            if (SaleTrends.Count == 0)
            {
                TrendSummary = "";
                TrendPlot = null;
                return;
            }

            var best = SaleTrends.OrderByDescending(x => x.Revenue).First();
            TrendSummary = $"Razem {SaleTrends.Count} sprzedaży, przychód {SaleTrendRow.Money(SaleTrends.Sum(x => x.Revenue))}. " +
                           $"Najlepsza: {best.SaleName} ({best.RevenueText}).";
            TrendPlot = CreateTrendPlot(SaleTrends);
        }

        // More points than this and the sale names on the axis turn into an unreadable smear.
        private const int MaxChartSales = 24;

        private static PlotModel CreateTrendPlot(IReadOnlyList<SaleTrendRow> allRows)
        {
            var rows = allRows.Count > MaxChartSales ? allRows.Skip(allRows.Count - MaxChartSales).ToList() : allRows;
            var plot = new PlotModel
            {
                Subtitle = rows.Count < allRows.Count ? $"Wykres: ostatnie {rows.Count} z {allRows.Count} sprzedaży (pełna lista poniżej)" : null,
                SubtitleColor = ThemeService.PlotText,
                TextColor = ThemeService.PlotText,
                PlotAreaBorderColor = ThemeService.PlotText,
                TitleColor = ThemeService.PlotText,
            };
            var salesAxis = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                TextColor = ThemeService.PlotText,
                TicklineColor = ThemeService.PlotText,
                Angle = rows.Count > 6 ? -35 : 0,
            };
            salesAxis.Labels.AddRange(rows.Select(x => x.SaleName));
            plot.Axes.Add(salesAxis);
            plot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Key = "revenue",
                Title = "Przychód (zł)",
                Minimum = 0,
                TextColor = ThemeService.PlotGood,
                TitleColor = ThemeService.PlotGood,
                TicklineColor = ThemeService.PlotGood,
            });
            plot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Right,
                Key = "clients",
                Title = "Klienci",
                Minimum = 0,
                TextColor = ThemeService.PlotAccent,
                TitleColor = ThemeService.PlotAccent,
                TicklineColor = ThemeService.PlotAccent,
            });
            plot.Series.Add(new LineSeries
            {
                Title = "Przychód",
                Color = ThemeService.PlotGood,
                YAxisKey = "revenue",
                MarkerType = MarkerType.Circle,
                ItemsSource = rows.Select((x, i) => new DataPoint(i, x.Revenue)).ToList(),
            });
            plot.Series.Add(new LineSeries
            {
                Title = "Klienci, którzy kupili",
                Color = ThemeService.PlotAccent,
                YAxisKey = "clients",
                MarkerType = MarkerType.Diamond,
                ItemsSource = rows.Select((x, i) => new DataPoint(i, x.ClientsBought)).ToList(),
            });
            plot.Legends.Add(new Legend { LegendPosition = LegendPosition.TopLeft, LegendTextColor = ThemeService.PlotText });
            return plot;
        }

        private void CalculateProductPerformance()
        {
            ProductPerformance = new ObservableCollection<ProductPerformanceRow>(
                SalesAnalytics.BuildProductPerformance(_data, _trends, SelectedPerformanceScope.LastSales));
        }

        private void InitializeScopes()
        {
            SaleScopes.Clear();
            SaleScopes.Add(new SaleScope { SaleID = "ALL", DisplayName = "Wszystkie zsumowane (Średnia)" });

            foreach (var s in _data.Sales)
            {
                SaleScopes.Add(new SaleScope { SaleID = s.SaleID, DisplayName = s.SaleName });
            }

            // Trigger calculation
            SelectedSaleScope = SaleScopes.FirstOrDefault();
        }

        private void CalculateAccuracyMetrics()
        {
            // Ordered kg and money paid per product, summed in one pass over the lines in scope.
            IEnumerable<OrderLine> lines = SelectedSaleScope.SaleID == "ALL"
                ? _data.Lines
                : _data.LinesBySale.GetValueOrDefault(SelectedSaleScope.SaleID) ?? Enumerable.Empty<OrderLine>();

            var sums = new Dictionary<string, (double ordered, double paid)>();
            foreach (var line in lines)
            {
                if (line.ProductID == null)
                    continue;
                sums.TryGetValue(line.ProductID, out var s);
                sums[line.ProductID] = line.IsReserved ? (s.ordered + line.Amount, s.paid) : (s.ordered, s.paid + line.Amount);
            }

            var metrics = new List<OrderAccuracyMetric>();
            foreach (var product in _data.Products.OrderBy(p => p.Code))
            {
                sums.TryGetValue(product.ID, out var s);
                double totalOrdered = s.ordered;
                double totalSold = s.paid;
                if (product.PricePerKg > 0)
                {
                    totalSold /= product.PricePerKg;
                }

                if (totalOrdered == 0 && totalSold == 0)
                {
                    continue; // Skip products not involved in this scope at all
                }

                // Sold without any order is an infinite increase.
                double percentage = totalOrdered == 0 && totalSold > 0
                    ? double.PositiveInfinity
                    : (totalSold - totalOrdered) / totalOrdered * 100.0;

                metrics.Add(new OrderAccuracyMetric
                {
                    ProductName = product.Name,
                    PercentageDifference = percentage
                });
            }
            AccuracyMetrics = new ObservableCollection<OrderAccuracyMetric>(metrics);
        }
    }
}
