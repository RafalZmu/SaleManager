using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Globalization;
using System.Linq;

namespace SaleManeger.Services
{
    /// <summary>The client that was served and closed most recently: shown so the seller can still read out the total.</summary>
    public class LastClosedClient
    {
        public string ClientID { get; init; }
        public string Name { get; init; }
        public string Number { get; init; }

        // Money paid for everything bought.
        public double BoughtValue { get; init; }
        public DateTime ClosedAt { get; init; }

        public string ValueText => BoughtValue.ToString("0.00", CultureInfo.InvariantCulture) + " zł";

        public string Text
        {
            get
            {
                var who = string.Join(" · ", new[] { Name, Number }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (who.Length == 0)
                    who = "(bez nazwy)";
                return $"Ostatnio zamknięty: {who} · kupione za {ValueText}";
            }
        }

        public static double BoughtValueOf(string saleText) =>
            string.IsNullOrWhiteSpace(saleText)
                ? 0
                : saleText.Split('\n').Where(x => x.Contains(':')).Sum(x => OrderValue.Parse(x.Split(':')[1]));

        /// <summary>Used after a restart, when nothing is remembered: the client closed last in this sale, if any was timed.</summary>
        public static LastClosedClient FromDatabase(IProjectRepository db, string saleId)
        {
            var info = db.GetAll<ClientSaleInfo>()
                .Where(x => x.SaleID == saleId && x.ServiceEndTime != null)
                .ToList()
                .OrderByDescending(x => x.ServiceEndTime)
                .FirstOrDefault();
            if (info == null)
                return null;

            var client = db.GetAll<Client>().ToList().FirstOrDefault(x => x.ID == info.ClientID);
            if (client == null)
                return null;

            var value = db.GetAll<ClientOrder>()
                .Where(x => x.ClientID == client.ID && x.SaleID == saleId && !x.IsReserved && x.ProductID != OrderValue.CommentProductId)
                .Select(x => x.Value)
                .ToList()
                .Sum(OrderValue.Parse);

            return new LastClosedClient
            {
                ClientID = client.ID,
                Name = client.Name,
                Number = client.PhoneNumber,
                BoughtValue = value,
                ClosedAt = info.ServiceEndTime.Value
            };
        }
    }
}
