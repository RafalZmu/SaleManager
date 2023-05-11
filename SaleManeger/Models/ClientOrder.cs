using System;

namespace SaleManeger.Models
{
    public class ClientOrder
    {
        #region Public Properties

        public string ClientID { get; set; }
        public string ClientOrderID { get; set; }
        public DateTime Date { get; set; }
        public bool IsReserved { get; set; }
        public string? ProductID { get; set; }
        public string SaleID { get; set; }
        public string Value { get; set; }

        #endregion Public Properties
    }
}