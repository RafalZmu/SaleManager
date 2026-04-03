using Microsoft.EntityFrameworkCore;
using SaleManeger.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SaleManeger.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        #region Public Fields

        public SaleContext _context;

        #endregion Public Fields

        #region Public Constructors

        public ProjectRepository(SaleContext context)
        {
            context.Database.EnsureCreated();
            
            // Ensure the SalesProducts table exists for older databases that have already been EnsureCreated() previously
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""SalesProducts"" (
                    ""ID"" TEXT NOT NULL CONSTRAINT ""PK_SalesProducts"" PRIMARY KEY,
                    ""ProductID"" TEXT NULL,
                    ""ProductName"" TEXT NULL,
                    ""ProductCode"" TEXT NULL,
                    ""SaleID"" TEXT NULL,
                    ""Amount"" REAL NOT NULL,
                    ""PricePerKg"" REAL NOT NULL
                );
            ");

            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""ClientSaleInfos"" (
                    ""ID"" TEXT NOT NULL CONSTRAINT ""PK_ClientSaleInfos"" PRIMARY KEY,
                    ""ClientID"" TEXT NULL,
                    ""SaleID"" TEXT NULL,
                    ""FirstPurchaseTime"" TEXT NULL,
                    ""FirstOrderTime"" TEXT NULL,
                    ""ExpectedArrivalTime"" TEXT NULL
                );
            ");

            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""SaleDayPlans"" (
                    ""ID"" TEXT NOT NULL CONSTRAINT ""PK_SaleDayPlans"" PRIMARY KEY,
                    ""SaleID"" TEXT NULL,
                    ""StartTime"" TEXT NOT NULL,
                    ""EndTime"" TEXT NOT NULL
                );
            ");

            _context = context;
        }

        #endregion Public Constructors

        #region Public Methods

        public void Add<T>(T entity) where T : class
        {
            _context.Set<T>().Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            _context.Set<T>().Remove(entity);
        }

        public IQueryable<T> GetAll<T>() where T : class
        {
            return _context.Set<T>().AsQueryable();
        }

        public void Save()
        {
            _context.SaveChanges();
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Update<T>(T entity) where T : class
        {
            _context.Set<T>().Update(entity);
        }

        #endregion Public Methods
    }
}