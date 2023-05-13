using System.Linq;
using System.Threading.Tasks;

namespace SaleManeger.Repositories
{
    public interface IProjectRepository
    {
        #region Public Methods

        void Add<T>(T entity) where T : class;

        void Delete<T>(T entity) where T : class;

        IQueryable<T> GetAll<T>() where T : class;

        void Save();

        Task SaveAsync();

        void Update<T>(T entity) where T : class;

        #endregion Public Methods
    }
}