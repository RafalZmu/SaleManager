using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SaleManeger.Models
{
    /// <summary>
    /// The one place that reads the free-text amount of an order line ("2,5", "2.5 kg", "3 szt, bez skórki").
    /// Only the first word counts, both decimal separators are accepted and anything unreadable is 0.
    /// </summary>
    public static class OrderValue
    {
        public static bool TryParse(string text, out double amount)
        {
            amount = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var first = text.Trim().Split(' ')[0].Replace(',', '.');
            return double.TryParse(first, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        }

        public static double Parse(string text) => TryParse(text, out var amount) ? amount : 0;

        public static double Sum(IEnumerable<string> values) => values.Sum(Parse);

        // Orders keep a comment as a line with the ProductID "Comment" and not a real product.
        public const string CommentProductId = "Comment";

        public static bool IsComment(ClientOrder order) => order.ProductID == CommentProductId;
    }
}
