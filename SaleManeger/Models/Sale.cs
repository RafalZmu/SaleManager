namespace SaleManeger.Models
{
    public class Sale
    {
        #region Public Properties

        public string SaleDate { get; set; }
        public string SaleID { get; set; }

        #endregion Public Properties

        #region Public Constructors

        public Sale()
        {
        }

        public Sale(string saleDate)
        {
            SaleDate = saleDate;
        }

        #endregion Public Constructors
    }
}