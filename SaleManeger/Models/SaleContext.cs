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

        #endregion Public Properties

        #region Public Constructors

        public SaleContext(string givenPath = null)
        {
            string path;
            if (givenPath == null)
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