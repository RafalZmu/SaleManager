using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Globalization;
using System.Linq;

namespace SaleManeger.ViewModels
{
    // Which order lines of a client a rule looks at.
    public enum FilterStatus { Ordered, Bought, Any }

    // What is measured: the ordered weight or its price (weight * price per kg).
    public enum FilterMetric { Amount, Cost }

    // Whether the lines are summed up first or each line is checked on its own.
    public enum FilterAggregation { Total, SingleOrder }

    public enum FilterComparison { Above, Below }

    /// <summary>
    /// One advanced client filter, e.g. "ordered apples, total weight above 5".
    /// A client matches only if it has at least one order line in the rule's scope
    /// and the measured value passes the threshold. Rules without a valid threshold are ignored.
    /// </summary>
    public class ClientFilterRule : ReactiveObject
    {
        private Product _selectedProduct;
        private FilterStatus _status = FilterStatus.Ordered;
        private FilterMetric _metric = FilterMetric.Amount;
        private FilterAggregation _aggregation = FilterAggregation.Total;
        private FilterComparison _comparison = FilterComparison.Above;
        private string _thresholdText = "";

        // Null (or a product without an ID) means all products.
        public Product SelectedProduct
        {
            get => _selectedProduct;
            set => this.RaiseAndSetIfChanged(ref _selectedProduct, value);
        }

        public FilterStatus Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public FilterMetric Metric
        {
            get => _metric;
            set => this.RaiseAndSetIfChanged(ref _metric, value);
        }

        public FilterAggregation Aggregation
        {
            get => _aggregation;
            set => this.RaiseAndSetIfChanged(ref _aggregation, value);
        }

        public FilterComparison Comparison
        {
            get => _comparison;
            set => this.RaiseAndSetIfChanged(ref _comparison, value);
        }

        public string ThresholdText
        {
            get => _thresholdText;
            set => this.RaiseAndSetIfChanged(ref _thresholdText, value);
        }

        // Index wrappers so the view can bind ComboBoxes without converters.
        public int StatusIndex { get => (int)Status; set => Status = (FilterStatus)value; }
        public int MetricIndex { get => (int)Metric; set => Metric = (FilterMetric)value; }
        public int AggregationIndex { get => (int)Aggregation; set => Aggregation = (FilterAggregation)value; }
        public int ComparisonIndex { get => (int)Comparison; set => Comparison = (FilterComparison)value; }

        public ClientFilterRule Clone() => new()
        {
            SelectedProduct = SelectedProduct,
            Status = Status,
            Metric = Metric,
            Aggregation = Aggregation,
            Comparison = Comparison,
            ThresholdText = ThresholdText
        };

        public bool TryGetThreshold(out double threshold)
        {
            threshold = 0;
            return !string.IsNullOrWhiteSpace(ThresholdText)
                && TryParseNumber(ThresholdText, out threshold);
        }

        public bool IsActive => TryGetThreshold(out _);

        public bool Matches(Client client)
        {
            if (!TryGetThreshold(out var threshold))
                return true;

            var productId = SelectedProduct?.ID;
            var values = (client.Products ?? Enumerable.Empty<Product>())
                .Where(p => p.ID != "Comment")
                .Where(p => productId == null || p.ID == productId)
                .Where(p => Status == FilterStatus.Any || p.IsReserved == (Status == FilterStatus.Ordered))
                .Select(p =>
                {
                    TryParseNumber(p.Value, out var amount);
                    return Metric == FilterMetric.Amount ? amount : amount * p.PricePerKg;
                })
                .ToList();

            if (values.Count == 0)
                return false;

            bool Passes(double v) => Comparison == FilterComparison.Above ? v > threshold : v < threshold;

            return Aggregation == FilterAggregation.Total ? Passes(values.Sum()) : values.Any(Passes);
        }

        // Order values look like "2,5" or "2.5 kg"; both separators are accepted.
        internal static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var first = text.Trim().Split(' ')[0].Replace(',', '.');
            return double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
