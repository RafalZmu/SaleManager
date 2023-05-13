using SaleManeger.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SaleManeger.Repositories
{
    internal class ProjectRepository : IProjectRepository
    {
        #region Public Fields

        public SaleContext _context;

        #endregion Public Fields

        #region Public Constructors

        public ProjectRepository(SaleContext context)
        {
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