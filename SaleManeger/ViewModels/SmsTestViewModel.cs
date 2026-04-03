using ReactiveUI;
using System.Reactive;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace SaleManeger.ViewModels
{
    public class SmsTestViewModel : ViewModelBase
    {
        private string _apiUrl = "https://api.sms-gate.app/3rdparty/v1/message";
        public string ApiUrl { get => _apiUrl; set => this.RaiseAndSetIfChanged(ref _apiUrl, value); }
        
        private string _login = "";
        public string Login { get => _login; set => this.RaiseAndSetIfChanged(ref _login, value); }
        
        private string _password = "";
        public string Password { get => _password; set => this.RaiseAndSetIfChanged(ref _password, value); }

        private string _phoneNumber = "";
        public string PhoneNumber { get => _phoneNumber; set => this.RaiseAndSetIfChanged(ref _phoneNumber, value); }

        private string _messageText = "";
        public string MessageText { get => _messageText; set => this.RaiseAndSetIfChanged(ref _messageText, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => this.RaiseAndSetIfChanged(ref _statusText, value); }

        public ReactiveCommand<Unit, string> GoBackCommand { get; }
        public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }

        private readonly string _saleID;

        public SmsTestViewModel(string saleID)
        {
            _saleID = saleID;
            GoBackCommand = ReactiveCommand.Create(() => _saleID);
            
            SendMessageCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                StatusText = "Wysyłanie...";
                try
                {
                    using var client = new HttpClient();
                    if (!string.IsNullOrWhiteSpace(Login) || !string.IsNullOrWhiteSpace(Password))
                    {
                        var byteArray = Encoding.ASCII.GetBytes($"{Login}:{Password}");
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                    }

                    // Capcom6 SMS Gateway Payload structure
                    var payload = new 
                    {
                        phoneNumbers = new[] { PhoneNumber },
                        message = MessageText
                    };

                    string json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(ApiUrl, content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        StatusText = "Sukces! Wiadomość SMS została pomyślnie wysłana.";
                    }
                    else
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        StatusText = $"Błąd HTTP {(int)response.StatusCode}: {errorBody}";
                    }
                }
                catch (Exception ex)
                {
                    StatusText = $"Wyjątek: {ex.Message}";
                }
            });
        }
    }
}
