using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SaleManeger.Services
{
    public class SaleTrendRow
    {
        public string SaleID { get; set; }
        public string SaleName { get; set; }
        public DateTime FirstActivity { get; set; }
        public int ClientsOrdered { get; set; }
        public int ClientsBought { get; set; }
        public int ClientsMissed { get; set; }
        public double Revenue { get; set; }
        public double SoldKg { get; set; }

        // Revenue per client who bought; 0 when nobody bought.
        public double AverageBasket => ClientsBought == 0 ? 0 : Revenue / ClientsBought;

        // Revenue against the sale before it (null for the first sale or when that one earned nothing).
        public double? ChangePercent { get; set; }

        public string DateText => FirstActivity.ToString("dd.MM.yyyy");
        public string RevenueText => Money(Revenue);
        public string BasketText => ClientsBought == 0 ? "-" : Money(AverageBasket);
        public string SoldKgText => Kg(SoldKg);
        public string ChangeText => ChangePercent == null ? "-" : $"{(ChangePercent >= 0 ? "+" : "")}{ChangePercent.Value.ToString("0", CultureInfo.InvariantCulture)}%";

        internal static string Money(double value) => value.ToString("0.00", CultureInfo.InvariantCulture) + " zł";
        internal static string Kg(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture) + " kg";
    }

    public class ProductPerformanceRow
    {
        public string ProductID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }

        // Sales in the chosen range in which the product was ordered or sold.
        public int ActiveSales { get; set; }
        public double OrderedKg { get; set; }
        public double SoldKg { get; set; }
        public double LastSoldKg { get; set; }

        // Typical need per sale: for every sale the larger of ordered and sold kg, averaged. Null without data.
        public double? SuggestedKg { get; set; }

        // Sold against ordered in percent (null when nothing was ordered).
        public double? AccuracyPercent => OrderedKg > 0 ? (SoldKg - OrderedKg) / OrderedKg * 100.0 : null;

        // "rośnie" / "maleje" / "stabilnie" comparing the last sale with the earlier ones; empty with fewer than two sales.
        public string Trend { get; set; } = "";

        public double AverageSoldKg => ActiveSales == 0 ? 0 : SoldKg / ActiveSales;

        public string OrderedText => SaleTrendRow.Kg(OrderedKg);
        public string SoldText => SaleTrendRow.Kg(SoldKg);
        public string AverageSoldText => SaleTrendRow.Kg(AverageSoldKg);
        public string SuggestedText => SuggestedKg == null ? "-" : SaleTrendRow.Kg(SuggestedKg.Value);
        public string AccuracyText => AccuracyPercent == null ? "-" : $"{(AccuracyPercent >= 0 ? "+" : "")}{AccuracyPercent.Value.ToString("0.0", CultureInfo.InvariantCulture)}%";
    }

    /// <summary>
    /// Numbers across sales. Bought lines hold the price paid ("Kupione" values are money), so kilograms are
    /// worked out as price / price per kg, the same way the order accuracy tab does it.
    /// </summary>
    public static class SalesAnalytics
    {
        public static List<SaleTrendRow> BuildSaleTrends(IProjectRepository dataBase)
        {
            var products = dataBase.GetAll<Product>().ToDictionary(x => x.ID);
            var orders = dataBase.GetAll<ClientOrder>().ToList().Where(x => !OrderValue.IsComment(x)).ToList();
            var rows = new List<SaleTrendRow>();

            foreach (var sale in dataBase.GetAll<Sale>().ToList())
            {
                var saleOrders = orders.Where(x => x.SaleID == sale.SaleID).ToList();
                if (saleOrders.Count == 0)
                    continue;

                var bought = saleOrders.Where(x => !x.IsReserved).ToList();
                var buyers = bought.Select(x => x.ClientID).ToHashSet();
                var orderers = saleOrders.Where(x => x.IsReserved).Select(x => x.ClientID).ToHashSet();

                rows.Add(new SaleTrendRow
                {
                    SaleID = sale.SaleID,
                    SaleName = sale.SaleName,
                    FirstActivity = saleOrders.Min(x => x.Date),
                    ClientsOrdered = orderers.Count,
                    ClientsBought = buyers.Count,
                    ClientsMissed = orderers.Count(x => !buyers.Contains(x)),
                    Revenue = bought.Sum(x => OrderValue.Parse(x.Value)),
                    SoldKg = bought.Sum(x => KgFromPrice(products, x))
                });
            }

            rows = rows.OrderBy(x => x.FirstActivity).ToList();
            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i - 1].Revenue > 0)
                    rows[i].ChangePercent = (rows[i].Revenue - rows[i - 1].Revenue) / rows[i - 1].Revenue * 100.0;
            }
            return rows;
        }

        /// <param name="lastSales">How many of the newest sales to look at; 0 means all of them.</param>
        public static List<ProductPerformanceRow> BuildProductPerformance(IProjectRepository dataBase, int lastSales)
        {
            var trends = BuildSaleTrends(dataBase);
            var inScope = (lastSales > 0 ? trends.Skip(Math.Max(0, trends.Count - lastSales)) : trends).Select(x => x.SaleID).ToList();
            var products = dataBase.GetAll<Product>().ToList();
            var productsById = products.ToDictionary(x => x.ID);
            var orders = dataBase.GetAll<ClientOrder>().ToList()
                .Where(x => !OrderValue.IsComment(x) && x.ProductID != null && inScope.Contains(x.SaleID))
                .ToList();

            var rows = new List<ProductPerformanceRow>();
            foreach (var product in products.OrderBy(x => x.Code))
            {
                var perSale = new List<(double ordered, double sold)>();
                foreach (var saleId in inScope)
                {
                    var lines = orders.Where(x => x.SaleID == saleId && x.ProductID == product.ID).ToList();
                    if (lines.Count == 0)
                        continue;
                    perSale.Add((
                        lines.Where(x => x.IsReserved).Sum(x => OrderValue.Parse(x.Value)),
                        lines.Where(x => !x.IsReserved).Sum(x => KgFromPrice(productsById, x))));
                }
                if (perSale.Count == 0)
                    continue;

                var row = new ProductPerformanceRow
                {
                    ProductID = product.ID,
                    Code = product.Code,
                    Name = product.Name,
                    ActiveSales = perSale.Count,
                    OrderedKg = perSale.Sum(x => x.ordered),
                    SoldKg = perSale.Sum(x => x.sold),
                    LastSoldKg = perSale[^1].sold,
                    SuggestedKg = Math.Ceiling(perSale.Average(x => Math.Max(x.ordered, x.sold)) * 2) / 2
                };

                if (perSale.Count >= 2)
                {
                    var earlier = perSale.Take(perSale.Count - 1).Average(x => x.sold);
                    var last = perSale[^1].sold;
                    row.Trend = earlier <= 0 ? (last > 0 ? "rośnie" : "stabilnie")
                        : last > earlier * 1.2 ? "rośnie"
                        : last < earlier * 0.8 ? "maleje"
                        : "stabilnie";
                }
                rows.Add(row);
            }
            return rows.OrderByDescending(x => x.SoldKg).ThenBy(x => x.Name).ToList();
        }

        private static double KgFromPrice(Dictionary<string, Product> products, ClientOrder bought)
        {
            if (bought.ProductID == null || !products.TryGetValue(bought.ProductID, out var product) || product.PricePerKg <= 0)
                return 0;
            return OrderValue.Parse(bought.Value) / product.PricePerKg;
        }
    }
}
