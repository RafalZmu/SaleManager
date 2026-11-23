using SaleManeger.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SaleManeger.Services
{
    /// <summary>One timed client of one sale: how long serving took and what was handed out.</summary>
    public sealed class ServiceRecord
    {
        public string SaleID { get; init; }
        public string SaleName { get; init; }
        public string ClientID { get; init; }
        public string ClientName { get; init; }
        public DateTime Start { get; init; }
        public double Seconds { get; init; }

        // What the client bought: kilograms (price / price per kg), money paid and the number of bought lines.
        public double Kg { get; init; }
        public double Value { get; init; }
        public int Items { get; init; }
        public Dictionary<string, (int Lines, double Kg)> ByProduct { get; init; } = new();
    }

    /// <summary>The raw timing row read from ClientSaleInfos.</summary>
    public sealed class ServiceInfoRow
    {
        public string SaleID { get; init; }
        public string ClientID { get; init; }
        public DateTime Start { get; init; }
        public double Seconds { get; init; }
    }

    public class ServiceSummary
    {
        public int Count { get; init; }
        public double AverageSeconds { get; init; }
        public double MedianSeconds { get; init; }
        public double FastestSeconds { get; init; }
        public double SlowestSeconds { get; init; }
        public double TotalSeconds { get; init; }

        // Typical "slow" client: 90% were served faster than this.
        public double Percentile90Seconds { get; init; }

        // Time needed per kilogram / per bought line / per 100 zł, over all clients together.
        public double? SecondsPerKg { get; init; }
        public double? SecondsPerItem { get; init; }
        public double? SecondsPerHundredZl { get; init; }

        // Clients served per hour of pure serving time.
        public double? ClientsPerHour { get; init; }
    }

    public class ServiceBucket
    {
        public string Label { get; init; }
        public int Count { get; init; }
    }

    public class ServiceSaleRow
    {
        public string SaleName { get; init; }
        public DateTime Date { get; init; }
        public int Clients { get; init; }
        public double AverageSeconds { get; init; }
        public double TotalSeconds { get; init; }
        public double AverageKg { get; init; }
        public string DateText => Date.ToString("dd.MM.yyyy");
        public string AverageText => ServiceAnalytics.Duration(AverageSeconds);
        public string TotalText => ServiceAnalytics.Duration(TotalSeconds);
        public string AverageKgText => ServiceAnalytics.Num(AverageKg) + " kg";
    }

    public class ServiceProductRow
    {
        public string Name { get; init; }
        public int Clients { get; init; }
        public double AverageClientSeconds { get; init; }

        // Clients with this product against the average of all timed clients (positive = they take longer).
        public double DifferenceSeconds { get; init; }

        // The client's time shared out over the lines they bought, averaged over this product's lines.
        public double SecondsPerLine { get; init; }
        public double Kg { get; init; }

        public string AverageText => ServiceAnalytics.Duration(AverageClientSeconds);
        public string DifferenceText => (DifferenceSeconds >= 0 ? "+" : "-") + ServiceAnalytics.Duration(Math.Abs(DifferenceSeconds));
        public string PerLineText => ServiceAnalytics.Duration(SecondsPerLine);
        public string KgText => ServiceAnalytics.Num(Kg) + " kg";
    }

    public class ServiceHourRow
    {
        public int Hour { get; init; }
        public int Clients { get; init; }
        public double AverageSeconds { get; init; }
        public string HourText => $"{Hour:00}:00";
    }

    public class ServiceClientRow
    {
        public string Name { get; init; }
        public int Visits { get; init; }
        public double AverageSeconds { get; init; }
        public double AverageValue { get; init; }
        public double? SecondsPerKg { get; init; }
        public string AverageText => ServiceAnalytics.Duration(AverageSeconds);
        public string ValueText => ServiceAnalytics.Num(AverageValue) + " zł";
        public string PerKgText => SecondsPerKg == null ? "-" : ServiceAnalytics.Duration(SecondsPerKg.Value);
    }

    public enum ServiceMetric
    {
        Kg,
        Items,
        Value
    }

    public class ServiceCorrelation
    {
        public ServiceMetric Metric { get; init; }
        public int Count { get; init; }

        // Pearson coefficient between the metric and the serving time; null with fewer than 3 clients or no variation.
        public double? R { get; init; }

        // Straight line through the points: seconds = Intercept + Slope * metric.
        public double Slope { get; init; }
        public double Intercept { get; init; }

        public string Description
        {
            get
            {
                if (R == null)
                    return "Za mało danych na korelację (potrzeba co najmniej 3 obsłużonych klientów o różnych wartościach).";
                var strength = Math.Abs(R.Value) switch
                {
                    >= 0.7 => "silna",
                    >= 0.4 => "umiarkowana",
                    >= 0.2 => "słaba",
                    _ => "praktycznie brak"
                };
                var direction = R.Value >= 0 ? "dodatnia" : "ujemna";
                var unit = Metric switch { ServiceMetric.Kg => "każdy dodatkowy kg", ServiceMetric.Items => "każda dodatkowa pozycja", _ => "każde dodatkowe 100 zł" };
                var perUnit = Metric == ServiceMetric.Value ? Slope * 100 : Slope;
                var text = Math.Abs(R.Value) < 0.2
                    ? $"Korelacja: {strength} (r = {R.Value.ToString("0.00", CultureInfo.InvariantCulture)}). Czas obsługi prawie nie zależy od tej wielkości."
                    : $"Korelacja {strength}, {direction} (r = {R.Value.ToString("0.00", CultureInfo.InvariantCulture)}): {unit} to średnio {(perUnit >= 0 ? "+" : "-")}{ServiceAnalytics.Duration(Math.Abs(perUnit))} obsługi.";
                return text;
            }
        }
    }

    /// <summary>Numbers about how long serving clients takes. Works on <see cref="ServiceRecord"/>s built from the snapshot.</summary>
    public static class ServiceAnalytics
    {
        public static List<ServiceRecord> BuildRecords(SalesSnapshot data)
        {
            var saleNames = data.Sales.ToDictionary(x => x.SaleID, x => x.SaleName);

            // What each client bought in each sale, in one pass over the lines.
            var bought = new Dictionary<(string sale, string client), (double kg, double value, int items, Dictionary<string, (int Lines, double Kg)> products)>();
            foreach (var line in data.Lines)
            {
                if (line.IsReserved || line.ProductID == null || line.ClientID == null || line.SaleID == null)
                    continue;
                var key = (line.SaleID, line.ClientID);
                if (!bought.TryGetValue(key, out var sums))
                    sums = (0, 0, 0, new Dictionary<string, (int Lines, double Kg)>());
                var lineKg = data.KgFromPrice(line);
                var before = sums.products.GetValueOrDefault(line.ProductID);
                sums.products[line.ProductID] = (before.Lines + 1, before.Kg + lineKg);
                bought[key] = (sums.kg + lineKg, sums.value + line.Amount, sums.items + 1, sums.products);
            }

            var records = new List<ServiceRecord>();
            foreach (var info in data.ServiceInfos)
            {
                if (info.Seconds <= 0 || info.SaleID == null || info.ClientID == null)
                    continue;
                bought.TryGetValue((info.SaleID, info.ClientID), out var sums);
                if (sums.products == null)
                    continue; // timed, but the purchases are gone
                records.Add(new ServiceRecord
                {
                    SaleID = info.SaleID,
                    SaleName = saleNames.GetValueOrDefault(info.SaleID) ?? "?",
                    ClientID = info.ClientID,
                    ClientName = data.ClientNames.GetValueOrDefault(info.ClientID) ?? "(usunięty klient)",
                    Start = info.Start,
                    Seconds = info.Seconds,
                    Kg = sums.kg,
                    Value = sums.value,
                    Items = sums.items,
                    ByProduct = sums.products
                });
            }
            return records;
        }

        public static ServiceSummary Summarize(IReadOnlyList<ServiceRecord> records)
        {
            if (records.Count == 0)
                return new ServiceSummary();

            var sorted = records.Select(x => x.Seconds).OrderBy(x => x).ToList();
            var total = sorted.Sum();
            var kg = records.Sum(x => x.Kg);
            var items = records.Sum(x => x.Items);
            var value = records.Sum(x => x.Value);

            return new ServiceSummary
            {
                Count = records.Count,
                AverageSeconds = total / records.Count,
                MedianSeconds = Percentile(sorted, 0.5),
                FastestSeconds = sorted[0],
                SlowestSeconds = sorted[^1],
                Percentile90Seconds = Percentile(sorted, 0.9),
                TotalSeconds = total,
                SecondsPerKg = kg > 0 ? total / kg : null,
                SecondsPerItem = items > 0 ? total / items : null,
                SecondsPerHundredZl = value > 0 ? total / value * 100 : null,
                ClientsPerHour = total > 0 ? records.Count / (total / 3600.0) : null
            };
        }

        /// <summary>Counts of clients per serving-time bucket; the buckets are as wide as needed for about 8-12 bars.</summary>
        public static List<ServiceBucket> Distribution(IReadOnlyList<ServiceRecord> records)
        {
            if (records.Count == 0)
                return new List<ServiceBucket>();

            var seconds = records.Select(x => x.Seconds).OrderBy(x => x).ToList();
            // Ignore the extreme tail when sizing, the slowest clients all go in the last bar.
            var upper = Math.Max(Percentile(seconds, 0.95), 1);
            var widths = new[] { 30, 60, 120, 180, 300, 600, 900, 1800 };
            var width = widths.FirstOrDefault(w => upper / w <= 10);
            if (width == 0)
                width = widths[^1];
            var bars = Math.Max(1, (int)Math.Ceiling(upper / width));

            var counts = new int[bars];
            foreach (var s in seconds)
                counts[Math.Min(bars - 1, (int)(s / width))]++;

            return Enumerable.Range(0, bars).Select(i => new ServiceBucket
            {
                Label = i == bars - 1 && seconds[^1] > bars * width
                    ? $"{Duration(i * width)}+"
                    : $"{Duration(i * width)}–{Duration((i + 1) * width)}",
                Count = counts[i]
            }).ToList();
        }

        public static List<ServiceSaleRow> BySale(IReadOnlyList<ServiceRecord> records) =>
            records.GroupBy(x => x.SaleID).Select(g => new ServiceSaleRow
            {
                SaleName = g.First().SaleName,
                Date = g.Min(x => x.Start),
                Clients = g.Count(),
                AverageSeconds = g.Average(x => x.Seconds),
                TotalSeconds = g.Sum(x => x.Seconds),
                AverageKg = g.Average(x => x.Kg)
            }).OrderBy(x => x.Date).ToList();

        public static List<ServiceProductRow> ByProduct(IReadOnlyList<ServiceRecord> records, IReadOnlyDictionary<string, Product> products)
        {
            if (records.Count == 0)
                return new List<ServiceProductRow>();

            var overall = records.Average(x => x.Seconds);
            var rows = new List<ServiceProductRow>();
            foreach (var group in records.SelectMany(r => r.ByProduct.Keys.Select(p => (product: p, record: r))).GroupBy(x => x.product))
            {
                if (!products.TryGetValue(group.Key, out var product))
                    continue;
                var clients = group.Select(x => x.record).ToList();

                // The seconds of a client are shared equally over the bought lines: a rough, but fair guess.
                double allocated = 0;
                int lines = 0;
                foreach (var r in clients)
                {
                    var n = r.ByProduct[group.Key].Lines;
                    allocated += r.Seconds * n / r.Items;
                    lines += n;
                }

                rows.Add(new ServiceProductRow
                {
                    Name = product.Name,
                    Clients = clients.Count,
                    AverageClientSeconds = clients.Average(x => x.Seconds),
                    DifferenceSeconds = clients.Average(x => x.Seconds) - overall,
                    SecondsPerLine = allocated / lines,
                    Kg = clients.Sum(r => r.ByProduct[group.Key].Kg)
                });
            }
            return rows.OrderByDescending(x => x.SecondsPerLine).ThenBy(x => x.Name).ToList();
        }

        public static List<ServiceHourRow> ByHour(IReadOnlyList<ServiceRecord> records) =>
            records.GroupBy(x => x.Start.Hour).Select(g => new ServiceHourRow
            {
                Hour = g.Key,
                Clients = g.Count(),
                AverageSeconds = g.Average(x => x.Seconds)
            }).OrderBy(x => x.Hour).ToList();

        public static List<ServiceClientRow> ByClient(IReadOnlyList<ServiceRecord> records) =>
            records.GroupBy(x => x.ClientID).Select(g =>
            {
                var kg = g.Sum(x => x.Kg);
                return new ServiceClientRow
                {
                    Name = g.First().ClientName,
                    Visits = g.Count(),
                    AverageSeconds = g.Average(x => x.Seconds),
                    AverageValue = g.Average(x => x.Value),
                    SecondsPerKg = kg > 0 ? g.Sum(x => x.Seconds) / kg : null
                };
            }).OrderByDescending(x => x.AverageSeconds).ToList();

        public static double ValueOf(ServiceRecord record, ServiceMetric metric) => metric switch
        {
            ServiceMetric.Kg => record.Kg,
            ServiceMetric.Items => record.Items,
            _ => record.Value
        };

        public static ServiceCorrelation Correlate(IReadOnlyList<ServiceRecord> records, ServiceMetric metric)
        {
            var xs = records.Select(x => ValueOf(x, metric)).ToList();
            var ys = records.Select(x => x.Seconds).ToList();
            int n = xs.Count;
            if (n < 3)
                return new ServiceCorrelation { Metric = metric, Count = n };

            double mx = xs.Average(), my = ys.Average();
            double sxx = xs.Sum(x => (x - mx) * (x - mx));
            double syy = ys.Sum(y => (y - my) * (y - my));
            double sxy = xs.Zip(ys, (x, y) => (x - mx) * (y - my)).Sum();
            if (sxx <= 0 || syy <= 0)
                return new ServiceCorrelation { Metric = metric, Count = n };

            var slope = sxy / sxx;
            return new ServiceCorrelation
            {
                Metric = metric,
                Count = n,
                R = sxy / Math.Sqrt(sxx * syy),
                Slope = slope,
                Intercept = my - slope * mx
            };
        }

        /// <summary>"45 s", "5 min 12 s", "1 h 05 min".</summary>
        public static string Duration(double seconds)
        {
            var total = (int)Math.Round(seconds);
            if (total < 60)
                return $"{total} s";
            if (total < 3600)
                return $"{total / 60} min {total % 60:00} s";
            return $"{total / 3600} h {total % 3600 / 60:00} min";
        }

        public static string Num(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

        private static double Percentile(IReadOnlyList<double> sortedAscending, double p)
        {
            if (sortedAscending.Count == 1)
                return sortedAscending[0];
            var position = (sortedAscending.Count - 1) * p;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            return sortedAscending[lower] + (sortedAscending[upper] - sortedAscending[lower]) * (position - lower);
        }
    }
}
