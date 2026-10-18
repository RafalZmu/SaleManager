using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
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
        private readonly IProjectRepository _dataBase;

        public ReactiveCommand<Unit, Unit> ReturnCommand { get; }

        public ObservableCollection<SaleScope> SaleScopes { get; set; }
        
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

        public ObservableCollection<SaleTrendRow> SaleTrends { get; private set; } = new();

        public PlotModel TrendPlot { get; private set; }

        public bool HasSaleTrends => SaleTrends.Count > 0;

        public string TrendSummary { get; private set; } = "";

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
            _dataBase = db;
            ReturnCommand = ReactiveCommand.Create(() => { });

            InitializeScopes();
            CalculateSaleTrends();
            SelectedPerformanceScope = PerformanceScopes[1];
        }

        private void CalculateSaleTrends()
        {
            SaleTrends = new ObservableCollection<SaleTrendRow>(SalesAnalytics.BuildSaleTrends(_dataBase));

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

        private static PlotModel CreateTrendPlot(IReadOnlyList<SaleTrendRow> rows)
        {
            var plot = new PlotModel
            {
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
                SalesAnalytics.BuildProductPerformance(_dataBase, SelectedPerformanceScope.LastSales));
        }

        private void InitializeScopes()
        {
            SaleScopes = new ObservableCollection<SaleScope>();
            SaleScopes.Add(new SaleScope { SaleID = "ALL", DisplayName = "Wszystkie zsumowane (Średnia)" });

            var sales = _dataBase.GetAll<Sale>().ToList();
            foreach (var s in sales)
            {
                SaleScopes.Add(new SaleScope { SaleID = s.SaleID, DisplayName = s.SaleName });
            }

            // Trigger calculation
            SelectedSaleScope = SaleScopes.FirstOrDefault();
        }

        private void CalculateAccuracyMetrics()
        {
            // Reset collection
            AccuracyMetrics = new ObservableCollection<OrderAccuracyMetric>();

            var allClientOrdersList = _dataBase.GetAll<ClientOrder>().ToList();
            var allProductsList = _dataBase.GetAll<Product>().ToList();
            
            // Filter by specific Sale or global aggregate
            IEnumerable<ClientOrder> relevantOrders;
            if (SelectedSaleScope.SaleID == "ALL")
            {
                relevantOrders = allClientOrdersList.Where(x => x.ProductID != OrderValue.CommentProductId);
            }
            else
            {
                relevantOrders = allClientOrdersList.Where(x => x.SaleID == SelectedSaleScope.SaleID && x.ProductID != OrderValue.CommentProductId);
            }

            var culture = new System.Globalization.CultureInfo("en-US");

            foreach (var product in allProductsList.OrderBy(p => p.Code))
            {
                var productOrders = relevantOrders.Where(x => x.ProductID == product.ID).ToList();

                var reservedOrders = productOrders.Where(x => x.IsReserved).ToList();
                var soldOrders = productOrders.Where(x => !x.IsReserved).ToList();

                double totalOrdered = reservedOrders.Sum(x => OrderValue.Parse(x.Value));

                double totalSold = soldOrders.Sum(x => OrderValue.Parse(x.Value));
                
                if (product.PricePerKg > 0)
                {
                    totalSold /= product.PricePerKg;
                }

                double percentage = 0;

                if (totalOrdered == 0 && totalSold == 0)
                {
                    continue; // Skip products not involved in this scope at all
                }
                
                if (totalOrdered == 0 && totalSold > 0)
                {
                    percentage = double.PositiveInfinity; // Infinite % increase
                }
                else
                {
                    percentage = ((totalSold - totalOrdered) / totalOrdered) * 100.0;
                }

                AccuracyMetrics.Add(new OrderAccuracyMetric
                {
                    ProductName = product.Name,
                    PercentageDifference = percentage
                });
            }
        }
    }
}
