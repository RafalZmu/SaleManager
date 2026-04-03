using System;

namespace SaleManeger.Models
{
    public class SaleDayPlan
    {
        public string ID { get; set; }
        public string SaleID { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
