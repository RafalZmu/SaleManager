using Dropbox.Api;
using System.Text;

namespace SQLiteCloudService.Helpers
{
    public class SQLiteCloudeHelpers
    {

        public static async Task UploadFile(string uri)
        {

        }

        public static async Task DownloadFile(string uri)
        {
            var databasePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\sale.db";
        }
    }
}
