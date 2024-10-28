using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleManeger.Models
{
    public class SaleProduct
    {
        public string ID { get; set; }
        public string ProductID { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public string SaleID { get; set; }
        public double Amount { get; set; }
        public double PricePerKg { get; set; }

    }
}
