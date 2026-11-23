using Avalonia.Controls.Shapes;
using Microsoft.EntityFrameworkCore;
using System;

namespace SaleManeger.Models
{
    public class SaleContext : DbContext
    {
        #region Public Properties

        public DbSet<Client> Clients { get; set; }
        public DbSet<ClientOrder> ClientsOrders { get; set; }
        public string DbPath { get; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleProduct> SalesProducts { get; set; }
        public DbSet<ClientSaleInfo> ClientSaleInfos { get; set; }
        public DbSet<SaleDayPlan> SaleDayPlans { get; set; }

        #endregion Public Properties

        #region Public Constructors

        public SaleContext(string givenPath = null)
        {
            string path;
            // SALEMANAGER_DATA_DIR lets the program run against another folder (for example a copy of the database).
            givenPath ??= Environment.GetEnvironmentVariable("SALEMANAGER_DATA_DIR");
            if (string.IsNullOrEmpty(givenPath))
            {
                var folder = Environment.SpecialFolder.LocalApplicationData;
                path = Environment.GetFolderPath(folder);
            }
            else
            {
                path = givenPath;
            }
            DbPath = System.IO.Path.Join(path, "sale.db");
        }

        #endregion Public Constructors

        #region Protected Methods

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }

        #endregion Protected Methods
    }
}