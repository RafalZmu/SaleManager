using System;
using System.Collections.Generic;
using System.Linq;

namespace SaleManeger.Models
{
    /// <summary>Tags are kept in one text column as "a, b, c"; this reads and writes that text.</summary>
    public static class ClientTags
    {
        // Offered in the client screen and the filter even before any client has it.
        public const string AfterDeadline = "po terminie";

        public static readonly IReadOnlyList<string> Suggested = new[] { AfterDeadline };

        public static List<string> Parse(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return result;
            foreach (var part in text.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var tag = part.Trim();
                if (tag.Length > 0 && !result.Contains(tag, StringComparer.CurrentCultureIgnoreCase))
                    result.Add(tag);
            }
            return result;
        }

        public static string Join(IEnumerable<string> tags) =>
            string.Join(", ", Parse(string.Join(",", tags ?? Enumerable.Empty<string>())));

        public static bool Has(Client client, string tag) =>
            Parse(client?.Tags).Contains(tag, StringComparer.CurrentCultureIgnoreCase);

        /// <summary>Every tag in use (case-insensitive, first spelling wins) plus the suggested ones, sorted.</summary>
        public static List<string> AllKnown(IEnumerable<Client> clients)
        {
            var known = new List<string>(Suggested);
            foreach (var tag in clients.SelectMany(c => Parse(c.Tags)))
            {
                if (!known.Contains(tag, StringComparer.CurrentCultureIgnoreCase))
                    known.Add(tag);
            }
            return known.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
