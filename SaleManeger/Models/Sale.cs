namespace SaleManeger.Models
{
    public class Sale
    {
        public string SaleDate { get; set; }

        public Sale(string saleDate)
        {
            SaleDate = saleDate;
        }
    }
}
