using Microsoft.EntityFrameworkCore;
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

    /// <summary>One order line cut down to what the statistics need, with the amount already parsed.</summary>
    public sealed class OrderLine
    {
        public string SaleID { get; init; }
        public string ClientID { get; init; }
        public string ProductID { get; init; }
        public bool IsReserved { get; init; }

        // Parsed once when loading: kg for ordered lines, money for bought lines.
        public double Amount { get; init; }
    }

    /// <summary>
    /// Everything the Statystyki tab reads, loaded from the database in one pass and without change tracking,
    /// so switching sale or range afterwards costs no database access.
    /// </summary>
    public sealed class SalesSnapshot
    {
        public List<Sale> Sales { get; init; }
        public List<Product> Products { get; init; }
        public Dictionary<string, Product> ProductsById { get; init; }
        public List<OrderLine> Lines { get; init; }

        // Earliest order date per sale, read separately because parsing a date for every line is slow on big databases.
        public Dictionary<string, DateTime> FirstActivityBySale { get; init; }
        public Dictionary<string, List<OrderLine>> LinesBySale { get; init; }

        public static SalesSnapshot Load(IProjectRepository dataBase)
        {
            var products = dataBase.GetAll<Product>().AsNoTracking().ToList();
            var sales = dataBase.GetAll<Sale>().AsNoTracking().ToList();

            // Projecting to plain values skips entity materialization and change tracking, the slow part on big databases.
            var lines = dataBase.GetAll<ClientOrder>()
                .Where(x => x.ProductID == null || x.ProductID != OrderValue.CommentProductId)
                .Select(x => new { x.SaleID, x.ClientID, x.ProductID, x.IsReserved, x.Value })
                .AsEnumerable()
                .Select(x => new OrderLine
                {
                    SaleID = x.SaleID,
                    ClientID = x.ClientID,
                    ProductID = x.ProductID,
                    IsReserved = x.IsReserved,
                    Amount = OrderValue.Parse(x.Value)
                })
                .ToList();

            var firstActivity = dataBase.GetAll<ClientOrder>()
                .Where(x => x.ProductID == null || x.ProductID != OrderValue.CommentProductId)
                .GroupBy(x => x.SaleID)
                .Select(g => new { SaleID = g.Key, First = g.Min(x => x.Date) })
                .ToDictionary(x => x.SaleID, x => x.First);

            return new SalesSnapshot
            {
                FirstActivityBySale = firstActivity,
                Sales = sales,
                Products = products,
                ProductsById = products.ToDictionary(x => x.ID),
                Lines = lines,
                LinesBySale = lines.GroupBy(x => x.SaleID).ToDictionary(x => x.Key, x => x.ToList())
            };
        }

        /// <summary>Kilograms behind a bought line: money paid divided by the product's price per kg.</summary>
        public double KgFromPrice(OrderLine bought)
        {
            if (bought.ProductID == null || !ProductsById.TryGetValue(bought.ProductID, out var product) || product.PricePerKg <= 0)
                return 0;
            return bought.Amount / product.PricePerKg;
        }
    }

    /// <summary>
    /// Numbers across sales. Bought lines hold the price paid ("Kupione" values are money), so kilograms are
    /// worked out as price / price per kg, the same way the order accuracy tab does it.
    /// </summary>
    public static class SalesAnalytics
    {
        public static List<SaleTrendRow> BuildSaleTrends(IProjectRepository dataBase) =>
            BuildSaleTrends(SalesSnapshot.Load(dataBase));

        public static List<SaleTrendRow> BuildSaleTrends(SalesSnapshot data)
        {
            var rows = new List<SaleTrendRow>();

            foreach (var sale in data.Sales)
            {
                if (!data.LinesBySale.TryGetValue(sale.SaleID, out var saleLines) || saleLines.Count == 0)
                    continue;

                var buyers = new HashSet<string>();
                var orderers = new HashSet<string>();
                double revenue = 0, soldKg = 0;
                foreach (var line in saleLines)
                {
                    if (line.IsReserved)
                    {
                        orderers.Add(line.ClientID);
                    }
                    else
                    {
                        buyers.Add(line.ClientID);
                        revenue += line.Amount;
                        soldKg += data.KgFromPrice(line);
                    }
                }

                rows.Add(new SaleTrendRow
                {
                    SaleID = sale.SaleID,
                    SaleName = sale.SaleName,
                    FirstActivity = data.FirstActivityBySale.GetValueOrDefault(sale.SaleID),
                    ClientsOrdered = orderers.Count,
                    ClientsBought = buyers.Count,
                    ClientsMissed = orderers.Count(x => !buyers.Contains(x)),
                    Revenue = revenue,
                    SoldKg = soldKg
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
            var data = SalesSnapshot.Load(dataBase);
            return BuildProductPerformance(data, BuildSaleTrends(data), lastSales);
        }

        /// <param name="trends">The sales in time order, from <see cref="BuildSaleTrends(SalesSnapshot)"/>.</param>
        public static List<ProductPerformanceRow> BuildProductPerformance(SalesSnapshot data, IReadOnlyList<SaleTrendRow> trends, int lastSales)
        {
            var inScope = (lastSales > 0 ? trends.Skip(Math.Max(0, trends.Count - lastSales)) : trends).Select(x => x.SaleID).ToList();

            // One pass over the lines of the sales in range: ordered kg, sold kg and line count per (sale, product).
            var perSaleAndProduct = new Dictionary<(string sale, string product), (double ordered, double sold, int lines)>();
            foreach (var saleId in inScope)
            {
                if (!data.LinesBySale.TryGetValue(saleId, out var saleLines))
                    continue;
                foreach (var line in saleLines)
                {
                    if (line.ProductID == null)
                        continue;
                    var key = (saleId, line.ProductID);
                    perSaleAndProduct.TryGetValue(key, out var sums);
                    perSaleAndProduct[key] = line.IsReserved
                        ? (sums.ordered + line.Amount, sums.sold, sums.lines + 1)
                        : (sums.ordered, sums.sold + data.KgFromPrice(line), sums.lines + 1);
                }
            }

            var rows = new List<ProductPerformanceRow>();
            foreach (var product in data.Products.OrderBy(x => x.Code))
            {
                var perSale = new List<(double ordered, double sold)>();
                foreach (var saleId in inScope)
                {
                    if (perSaleAndProduct.TryGetValue((saleId, product.ID), out var sums))
                        perSale.Add((sums.ordered, sums.sold));
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
    }
}
