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
    }
}
