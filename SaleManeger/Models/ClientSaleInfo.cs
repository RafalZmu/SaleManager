using System;

namespace SaleManeger.Models
{
    public class ClientSaleInfo
    {
        public string ID { get; set; }
        public string ClientID { get; set; }
        public string SaleID { get; set; }
        public DateTime? FirstPurchaseTime { get; set; }
        public DateTime? FirstOrderTime { get; set; }
        public DateTime? ExpectedArrivalTime { get; set; }

        // How long serving the client took on the sale day. Counted from the first bought item entered until the
        // client is closed; visits add up and long idle gaps are cut (see ServiceTimer). Null start = never timed.
        public DateTime? ServiceStartTime { get; set; }
        public DateTime? ServiceEndTime { get; set; }
        public double ServiceSeconds { get; set; }
    }
}
