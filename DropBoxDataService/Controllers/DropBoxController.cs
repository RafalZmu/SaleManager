using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.IO;


namespace SQLiteCloudService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DropBoxController : ControllerBase
    {

        private readonly ILogger<DropBoxController> _logger;
        private readonly string _apiKey = Environment.GetEnvironmentVariable("SQLITE_CLOUD");
        private readonly string projectId = Environment.GetEnvironmentVariable("PROJECT_ID");
        public readonly HttpClient client;

        public DropBoxController(ILogger<DropBoxController> logger)
        {
            _logger = logger;
            client = new HttpClient();
        }

        [HttpGet("upldoaFile", Name = "UploadFiles")]
        public async Task<bool> UploadDb()
        {
            var databasePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\sale.db";
            var uri = $"https://{projectId}.sqlite.cloud:8090/v2/weblite/sale.db";
            var request = new HttpRequestMessage(HttpMethod.Patch, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"sqlitecloud://{projectId}.sqlite.cloud:8860?apikey={_apiKey}");
            request.Content = new StreamContent(System.IO.File.OpenRead(databasePath));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Database uploaded successfully.");
                return true;
            }
            else
            {
                Console.WriteLine($"Error uploading database: {response.StatusCode}");
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return false;
            }
        }


        //[HttpGet("downloadFile", Name = "DownloadFiles")]
        //public async Task<string> DownloadDb()
        //{
        //}
    }
}
