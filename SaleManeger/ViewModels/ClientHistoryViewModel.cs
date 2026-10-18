using Avalonia.Media;
using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using SaleManeger.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class ClientHistorySaleRow
    {
        public string SaleName { get; set; }
        public DateTime? LastActivity { get; set; }
        public double OrderedKg { get; set; }
        public double OrderedValue { get; set; }
        public double Spent { get; set; }
        public bool HasOrder { get; set; }
        public bool HasPurchase { get; set; }
        public bool IsCurrentSale { get; set; }

        public string DateText => LastActivity?.ToString("dd.MM.yyyy") ?? "";
        public string OrderedText => HasOrder ? $"{ClientHistoryViewModel.Number(OrderedKg)} kg (ok. {ClientHistoryViewModel.Money(OrderedValue)})" : "-";
        public string BoughtText => HasPurchase ? ClientHistoryViewModel.Money(Spent) : "-";

        public string Status =>
            HasPurchase ? "Odebrano"
            : HasOrder && IsCurrentSale ? "Zamówienie czeka"
            : HasOrder ? "Nie odebrał"
            : "";

        public IBrush StatusBrush =>
            HasPurchase ? ThemeService.GoodBrush
            : HasOrder && IsCurrentSale ? ThemeService.WarnBrush
            : ThemeService.BadBrush;
    }

    public class ClientHistoryProductRow
    {
        public string Name { get; set; }
        public int Sales { get; set; }
        public double OrderedKg { get; set; }
        public double Spent { get; set; }

        public string SalesText => $"{Sales}x";
        public string OrderedText => OrderedKg > 0 ? $"{ClientHistoryViewModel.Number(OrderedKg)} kg" : "-";
        public string SpentText => Spent > 0 ? ClientHistoryViewModel.Money(Spent) : "-";
    }

    /// <summary>Everything the app knows about one client across all sales: visits, money, favourite products.</summary>
    public class ClientHistoryViewModel : ViewModelBase
    {
        private const int FavouriteCount = 8;

        public string ClientName { get; }
        public string PhoneNumber { get; }
        public string Tags { get; }
        public string Notes { get; }

        public List<ClientHistorySaleRow> Sales { get; }
        public List<ClientHistoryProductRow> FavouriteProducts { get; }

        public int SalesWithOrder { get; }
        public int SalesWithPurchase { get; }
        public int MissedSales { get; }
        public double TotalSpent { get; }
        public double AverageSpent => SalesWithPurchase == 0 ? 0 : TotalSpent / SalesWithPurchase;
        public DateTime? LastVisit { get; }

        public bool HasHistory => Sales.Count > 0;
        public bool HasTags => !string.IsNullOrWhiteSpace(Tags);
        public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

        public string TotalSpentText => Money(TotalSpent);
        public string AverageSpentText => SalesWithPurchase == 0 ? "-" : Money(AverageSpent);
        public string LastVisitText => LastVisit?.ToString("dd.MM.yyyy") ?? "-";
        public string VisitsText => $"{SalesWithPurchase} z {Sales.Count}";
        public string MissedText => MissedSales.ToString(CultureInfo.InvariantCulture);

        public ReactiveCommand<Unit, string> ReturnCommand { get; }

        public ClientHistoryViewModel(IProjectRepository dataBase, string clientID, string currentSaleID)
        {
            var client = dataBase.GetAll<Client>().FirstOrDefault(x => x.ID == clientID);
            ClientName = client?.Name ?? "";
            PhoneNumber = client?.PhoneNumber ?? "";
            Tags = client?.Tags ?? "";
            Notes = client?.Notes ?? "";

            var products = dataBase.GetAll<Product>().ToDictionary(x => x.ID);
            var sales = dataBase.GetAll<Sale>().ToList();
            var orders = dataBase.GetAll<ClientOrder>()
                .Where(x => x.ClientID == clientID)
                .ToList()
                .Where(x => !OrderValue.IsComment(x))
                .ToList();

            Sales = new List<ClientHistorySaleRow>();
            foreach (var group in orders.GroupBy(x => x.SaleID))
            {
                var sale = sales.FirstOrDefault(x => x.SaleID == group.Key);
                if (sale == null)
                    continue;

                var reserved = group.Where(x => x.IsReserved).ToList();
                var bought = group.Where(x => !x.IsReserved).ToList();
                Sales.Add(new ClientHistorySaleRow
                {
                    SaleName = sale.SaleName,
                    LastActivity = group.Max(x => x.Date),
                    HasOrder = reserved.Count > 0,
                    HasPurchase = bought.Count > 0,
                    OrderedKg = reserved.Sum(x => OrderValue.Parse(x.Value)),
                    OrderedValue = reserved.Sum(x => OrderValue.Parse(x.Value) * PriceOf(products, x.ProductID)),
                    Spent = bought.Sum(x => OrderValue.Parse(x.Value)),
                    IsCurrentSale = sale.SaleID == currentSaleID
                });
            }
            Sales = Sales.OrderByDescending(x => x.LastActivity).ToList();

            SalesWithOrder = Sales.Count(x => x.HasOrder);
            SalesWithPurchase = Sales.Count(x => x.HasPurchase);
            MissedSales = Sales.Count(x => x.HasOrder && !x.HasPurchase && !x.IsCurrentSale);
            TotalSpent = Sales.Sum(x => x.Spent);
            LastVisit = Sales.Where(x => x.HasPurchase).Select(x => x.LastActivity).DefaultIfEmpty().Max();

            FavouriteProducts = orders
                .Where(x => x.ProductID != null && products.ContainsKey(x.ProductID))
                .GroupBy(x => x.ProductID)
                .Select(g => new ClientHistoryProductRow
                {
                    Name = products[g.Key].Name,
                    Sales = g.Select(x => x.SaleID).Distinct().Count(),
                    OrderedKg = g.Where(x => x.IsReserved).Sum(x => OrderValue.Parse(x.Value)),
                    Spent = g.Where(x => !x.IsReserved).Sum(x => OrderValue.Parse(x.Value))
                })
                .OrderByDescending(x => x.Sales)
                .ThenByDescending(x => x.OrderedKg + x.Spent)
                .ThenBy(x => x.Name)
                .Take(FavouriteCount)
                .ToList();

            ReturnCommand = ReactiveCommand.Create(() => currentSaleID);
        }

        private static double PriceOf(Dictionary<string, Product> products, string productID) =>
            productID != null && products.TryGetValue(productID, out var product) ? product.PricePerKg : 0;

        internal static string Money(double value) => value.ToString("0.00", CultureInfo.InvariantCulture) + " zł";

        internal static string Number(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
