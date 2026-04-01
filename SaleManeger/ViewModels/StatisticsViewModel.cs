using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia.Media;
using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;

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
                if (PercentageDifference > 0) return Brushes.LightGreen;
                if (PercentageDifference < 0) return Brushes.LightCoral;
                return Brushes.White;
            }
        }
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

        public StatisticsViewModel(IProjectRepository db)
        {
            _dataBase = db;
            ReturnCommand = ReactiveCommand.Create(() => { });

            InitializeScopes();
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
                relevantOrders = allClientOrdersList.Where(x => x.ProductID != "Comment");
            }
            else
            {
                relevantOrders = allClientOrdersList.Where(x => x.SaleID == SelectedSaleScope.SaleID && x.ProductID != "Comment");
            }

            var culture = new System.Globalization.CultureInfo("en-US");

            foreach (var product in allProductsList.OrderBy(p => p.Code))
            {
                var productOrders = relevantOrders.Where(x => x.ProductID == product.ID).ToList();

                var reservedOrders = productOrders.Where(x => x.IsReserved).ToList();
                var soldOrders = productOrders.Where(x => !x.IsReserved).ToList();

                double totalOrdered = reservedOrders.Sum(x => 
                {
                    double.TryParse(x.Value.Split(' ')[0], System.Globalization.NumberStyles.Any, culture, out double val);
                    return val;
                });

                double totalSold = soldOrders.Sum(x => 
                {
                    double.TryParse(x.Value.Split(' ')[0], System.Globalization.NumberStyles.Any, culture, out double val);
                    return val;
                });
                
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
