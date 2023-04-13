namespace SaleManeger.Models
{
    public class Product
    {
        public string Name { get; set; }
        public double PricePerKg { get; set; }
        public string Code { get; set; }
        public bool IsReserved { get; set; }
        public string Value { get; set; }

        public Product()
        {

        }
        public Product(string productName, double productPricePerKg, string productCode)
        {
            Name = productName;
            PricePerKg = productPricePerKg;
            Code = productCode;
        }
    }
}
