using System.Collections.ObjectModel;

namespace SaleManeger.Models
{
    public class Client
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }

        public ObservableCollection<Product> Products { get; set; }
    }
}
