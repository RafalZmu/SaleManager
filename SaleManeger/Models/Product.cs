namespace SaleManeger.Models
{
    public class Product
    {
        #region Public Properties

        public string Code { get; set; }
        public string ID { get; set; }
        public bool IsReserved { get; set; }
        public string Name { get; set; }
        public double PricePerKg { get; set; }
        public string Value { get; set; }

        #endregion Public Properties

        #region Public Constructors

        public Product()
        { }

        public Product(string productName, double productPricePerKg, string productCode)
        {
            Name = productName;
            PricePerKg = productPricePerKg;
            Code = productCode;
        }

        #endregion Public Constructors
    }
}