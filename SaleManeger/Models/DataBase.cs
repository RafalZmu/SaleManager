using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace SaleManeger.Models
{
    public class DataBase
    {
        private string connectionString = "Data Source=SaleManeger.sqlite;Version=3";

        public DataBase()
        {
            if (!File.Exists("SaleManeger.sqlite"))
            {
                SQLiteConnection.CreateFile("SaleManeger.sqlite");
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    var createClientsTableCommand = new SQLiteCommand("CREATE TABLE Clients (ClientID TEXT PRIMARY KEY, Name TEXT, Number TEXT)", connection);
                    createClientsTableCommand.ExecuteNonQuery();

                    var createProductsTableCommand = new SQLiteCommand(
                        "CREATE TABLE Products (Code TEXT PRIMARY KEY, Name TEXT, PricePerKg REAL)", connection);
                    createProductsTableCommand.ExecuteNonQuery();

                    var createSalesTableCommand = new SQLiteCommand("CREATE TABLE Sales (SaleName TEXT PRIMARY KEY)", connection);
                    createSalesTableCommand.ExecuteNonQuery();

                    var createClientOrderTableCommand = new SQLiteCommand(
                        "CREATE TABLE ClientOrder (ProductID TEXT, ClientID TEXT NOT NULL, SaleID TEXT NOT NULL, IsReserved INTEGER, Value TEXT)", connection);
                    createClientOrderTableCommand.ExecuteNonQuery();
                }
            }
        }

        public ObservableCollection<Sale> GetSalesList()
        {
            var sales = new ObservableCollection<Sale>();

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                using var command = new SQLiteCommand("SELECT * FROM Sales", connection);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var sale = new Sale(reader.GetString(0));
                    sales.Add(sale);
                }
            }

            return sales;
        }
        public List<Product> GetProducts()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                //Get products from data base 
                connection.Open();
                using (var getProductsCommand = new SQLiteCommand("SELECT Name, PricePerKg, Code FROM Products", connection))
                {
                    using (var productsReader = getProductsCommand.ExecuteReader())
                    {
                        var products = new List<Product>();
                        while (productsReader.Read())
                        {
                            var product = new Product(productsReader.GetString(0), productsReader.GetDouble(1),
                                                  productsReader.GetString(2));
                            products.Add(product);
                        }
                        return products;
                    }
                }
            }
        }

        public ObservableCollection<Client> GetClientsFromSale(string saleName)
        {
            List<Product> products = GetProducts();
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                //Get clients from data base
                var clients = new ObservableCollection<Client>();
                using (var getClientsCommand = new SQLiteCommand("SELECT ClientID, Name, Number FROM Clients", connection))
                {

                    using (var clientsReader = getClientsCommand.ExecuteReader())
                    {
                        while (clientsReader.Read())
                        {
                            var client = new Client
                            {
                                ID = clientsReader.GetString(0),
                                Name = clientsReader.GetString(1),
                                PhoneNumber = clientsReader.GetString(2),
                                Products = new ObservableCollection<Product>()
                            };
                            clients.Add(client);
                        }
                        clientsReader.Close();
                    }

                    //Assign products to clients
                    using (var command = new SQLiteCommand("SELECT ClientID, SaleID, ProductID, IsReserved, Value FROM ClientOrder WHERE SaleID = @saleName", connection))
                    {
                        command.Parameters.AddWithValue("saleName", saleName);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Add the products to the orde
                                var productID = reader.GetString(2);
                                if (productID == "")
                                {
                                    Product comment = new()
                                    {
                                        Name = "",
                                        Code = "",
                                        Value = reader.GetString(4),
                                        IsReserved = reader.GetBoolean(3)
                                    };
                                    clients.FirstOrDefault(c => c.ID == reader.GetString(0))?.Products.Add(comment);
                                    continue;

                                }
                                else if(!products.Any(x => x.Code == productID))
                                {
                                    Product notFound = new()
                                    {
                                        Name = "Brak produktu w bazie",
                                        Code = productID,
                                        Value = reader.GetString(4),
                                        IsReserved = reader.GetBoolean(3)
                                    };
                                    continue;
                                }
                                Product product = new()
                                {
                                    Name = products.FirstOrDefault(p => p.Code == productID).Name,
                                    Code = productID,
                                    PricePerKg = products.FirstOrDefault(p => p.Code == productID).PricePerKg,
                                    Value = reader.GetString(4),
                                    IsReserved = reader.GetBoolean(3)
                                };
                                clients.FirstOrDefault(c => c.ID == reader.GetString(0))?.Products.Add(product);
                            }
                            reader.Close();
                            return clients;
                        }
                    }
                }

            }
        }

        public void AddToTable(string tableName, params (string column, object value)[] columnsAndValues)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                var columnNames = string.Join(",", columnsAndValues.Select(x => x.column));
                var values = string.Join(",", columnsAndValues.Select(x => $"@{x.column}"));
                var insertCommand = $"INSERT INTO {tableName} ({columnNames}) VALUES ({values})";

                using (var command = new SQLiteCommand(insertCommand, connection))
                {
                    foreach (var pair in columnsAndValues)
                    {
                        command.Parameters.AddWithValue($"@{pair.column}", pair.value);
                    }

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteAllFromTable(string tableName)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                SQLiteCommand command = new SQLiteCommand($"DELETE FROM {tableName}", connection);
                command.ExecuteNonQuery();
            }
        }

        public void AddProductsToProductsTable(ObservableCollection<Product> products)
        {
            DeleteAllFromTable("Products");
            foreach (var product in products.OrderBy(x => x.Code))
            {
                if (string.IsNullOrWhiteSpace(product.Name) || string.IsNullOrWhiteSpace(product.Code))
                {
                    continue;
                }
                AddToTable("Products", ("Name", product.Name), ("Code", product.Code), ("PricePerKg", product.PricePerKg));
            }
        }
        public void UpdateOrCreateClient(Client client, string saleName)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand("SELECT ClientID FROM Clients WHERE ClientID == @ID", connection))
                {
                    command.Parameters.AddWithValue("@ID", client.ID);
                    using (var reader = command.ExecuteReader())
                    {

                        if (reader.Read())
                        {
                            //Update client
                            using (SQLiteCommand updateClientCommand = new SQLiteCommand("UPDATE Clients SET Name = @name, Number = @number WHERE ClientID = @id", connection))
                            {
                                updateClientCommand.Parameters.AddWithValue("@name", client.Name);
                                updateClientCommand.Parameters.AddWithValue("@number", client.PhoneNumber);
                                updateClientCommand.Parameters.AddWithValue("@id", client.ID);
                                updateClientCommand.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            //Create client
                            AddToTable("Clients", ("ClientID", client.ID), ("Name", client.Name), ("Number", client.PhoneNumber));
                        }

                        //Update clients products
                        using (SQLiteCommand updateClientOrderCommand = new SQLiteCommand("DELETE FROM ClientOrder WHERE ClientID = @ClientID AND SaleID = @SaleID", connection))
                        {
                            updateClientOrderCommand.Parameters.AddWithValue("@ClientID", client.ID);
                            updateClientOrderCommand.Parameters.AddWithValue("@SaleID", saleName);
                            updateClientOrderCommand.ExecuteNonQuery();
                        }

                        if (client.Products == null)
                        {
                            client.Products = new ObservableCollection<Product>();
                        }
                        foreach (var item in client.Products)
                        {

                            using (SQLiteCommand addProduct = new SQLiteCommand("INSERT INTO ClientOrder (ProductID, ClientID, SaleID, IsReserved, Value) VALUES (@ProductID, @ClientID, @SaleID, @IsReserved, @Value)", connection))
                            {
                                addProduct.Parameters.AddWithValue("@ProductID", item.Code);
                                addProduct.Parameters.AddWithValue("@ClientID", client.ID);
                                addProduct.Parameters.AddWithValue("@SaleID", saleName);
                                addProduct.Parameters.AddWithValue("@IsReserved", item.IsReserved);
                                addProduct.Parameters.AddWithValue("@Value", item.Value);
                                addProduct.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
        }
        public double GetSumOfProduct(string saleName, string code, bool isReserved)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand GetProductSum = new("SELECT Value FROM ClientOrder WHERE SaleID = @saleID AND IsReserved ==@isReserved AND ProductID ==@product", connection))
                {
                    GetProductSum.Parameters.AddWithValue("@saleID", saleName);
                    GetProductSum.Parameters.AddWithValue("@isReserved", isReserved);
                    GetProductSum.Parameters.AddWithValue("@product", code);
                    using (var reader = GetProductSum.ExecuteReader())
                    {
                        double sum = 0;
                        while (reader.Read())
                        {
                            string valueString = reader.GetString(0);
                            string[] valueParts = valueString.Split(' ');
                            if (double.TryParse(valueParts[0], out double value))
                            {
                                sum += value;
                            }
                        }
                        return sum;
                    }
                }
            }
        }
        public double GetLeftProduct(string saleName, string code, bool isReserved)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand GetProductSum = new("SELECT Value FROM ClientOrder WHERE IsReserved = 1 AND ProductID == @product AND SaleID == @saleID AND ClientID NOT IN(SELECT DISTINCT ClientID FROM ClientOrder WHERE SaleID = @saleID AND IsReserved = 0)", connection))
                {
                    GetProductSum.Parameters.AddWithValue("@saleID", saleName);
                    GetProductSum.Parameters.AddWithValue("@product", code);
                    using (var reader = GetProductSum.ExecuteReader())
                    {
                        double sum = 0;
                        while (reader.Read())
                        {
                            string valueString = reader.GetString(0);
                            string[] valueParts = valueString.Split(' ');
                            if (double.TryParse(valueParts[0], out double value))
                            {
                                sum += value;
                            }
                        }
                        return sum;
                    }
                }
            }
        }
        public void DeleteClient(string id, string saleName)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand("DELETE FROM Clients WHERE ClientID == @ID", connection))
                {
                    command.Parameters.AddWithValue("@ID", id);
                    command.ExecuteNonQuery();
                }
                using (SQLiteCommand DeleteClientOrder = new("DELETE FROM ClientOrder WHERE ClientID == @ID AND SaleID = @SaleID", connection))
                {
                    DeleteClientOrder.Parameters.AddWithValue("@ID", id);
                    DeleteClientOrder.Parameters.AddWithValue("@SaleID", saleName);
                    DeleteClientOrder.ExecuteNonQuery();
                }
            }

        }

        public void DeleteSale(string saleName)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand DeleteSale = new("DELETE FROM Sales WHERE SaleName == @SaleName", connection))
                {
                    DeleteSale.Parameters.AddWithValue("@SaleName", saleName);
                    DeleteSale.ExecuteNonQuery();
                }
                using (SQLiteCommand DeleteClientOrder = new("DELETE FROM ClientOrder WHERE SaleID == @SaleName", connection))
                {
                    DeleteClientOrder.Parameters.AddWithValue("@SaleName", saleName);
                    DeleteClientOrder.ExecuteNonQuery();
                }
            }
        }
    }
}
