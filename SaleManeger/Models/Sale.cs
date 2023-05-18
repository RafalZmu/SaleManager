namespace SaleManeger.Models
{
    public class Sale
    {
        #region Public Properties

        public string SaleName { get; set; }
        public string SaleID { get; set; }

        #endregion Public Properties

        #region Public Constructors

        public Sale()
        {
        }

        public Sale(string saleDate)
        {
            SaleName = saleDate;
        }

        #endregion Public Constructors
    }
}