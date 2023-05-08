using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaleManeger.Models
{
    public class Client
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Color { get; set; }

        [NotMapped]
        public ObservableCollection<Product> Products { get; set; }
    }
}