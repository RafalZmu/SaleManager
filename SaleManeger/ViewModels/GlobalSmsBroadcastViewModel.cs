using ReactiveUI;
using System.Reactive;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using SaleManeger.Repositories;
using SaleManeger.Models;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SaleManeger.ViewModels
{
    public class GlobalSmsBroadcastViewModel : ViewModelBase
    {
        private string _apiUrl = "https://api.sms-gate.app/3rdparty/v1/message";
        public string ApiUrl { get => _apiUrl; set => this.RaiseAndSetIfChanged(ref _apiUrl, value); }
        
        private string _login = "";
        public string Login { get => _login; set => this.RaiseAndSetIfChanged(ref _login, value); }
        
        private string _password = "";
        public string Password { get => _password; set => this.RaiseAndSetIfChanged(ref _password, value); }

        private string _messageTemplate = "Witaj {imie}! Pojawiła się nowa oferta. Odwiedź nas, by zobaczyć co nowego przygotowaliśmy!";
        public string MessageTemplate { get => _messageTemplate; set => this.RaiseAndSetIfChanged(ref _messageTemplate, value); }

        private string _statusText = "Gotowy do wysyłki.";
        public string StatusText { get => _statusText; set => this.RaiseAndSetIfChanged(ref _statusText, value); }

        private bool _showConfirmationDialog;
        public bool ShowConfirmationDialog { get => _showConfirmationDialog; set => this.RaiseAndSetIfChanged(ref _showConfirmationDialog, value); }

        private int _clientsToReceiveSmsCount;
        public int ClientsToReceiveSmsCount { get => _clientsToReceiveSmsCount; set => this.RaiseAndSetIfChanged(ref _clientsToReceiveSmsCount, value); }

        public ReactiveCommand<Unit, Unit> GoBackCommand { get; }
        public ReactiveCommand<Unit, Unit> PreviewBroadcastCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelBroadcastCommand { get; }
        public ReactiveCommand<Unit, Unit> SendBroadcastCommand { get; }

        private readonly IProjectRepository _dataBase;
        private List<Client> _validTargets = new();

        public GlobalSmsBroadcastViewModel(IProjectRepository dataBase)
        {
            _dataBase = dataBase;
            GoBackCommand = ReactiveCommand.Create(() => { });
            
            PreviewBroadcastCommand = ReactiveCommand.Create(() => 
            {
                if (string.IsNullOrWhiteSpace(ApiUrl) || string.IsNullOrWhiteSpace(MessageTemplate))
                {
                    StatusText = "Błąd: Brak adresu API lub pusty szablon wiadomości.";
                    return;
                }

                CalculateValidTargets();

                if (_validTargets.Count > 0)
                {
                    ClientsToReceiveSmsCount = _validTargets.Count;
                    ShowConfirmationDialog = true;
                }
                else
                {
                    StatusText = "Brak numerów w bazie, które spełniają kryteria (poprawny polski prefiks / format).";
                }
            });

            CancelBroadcastCommand = ReactiveCommand.Create(() => 
            {
                ShowConfirmationDialog = false;
            });

            SendBroadcastCommand = ReactiveCommand.CreateFromTask(ExecuteBroadcastAsync);
        }

        private string CleanPhoneNumber(string phone)
        {
             return phone?.Replace(" ", "").Replace("-", "") ?? "";
        }

        private bool IsValidPolishPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            string cleaned = CleanPhoneNumber(phone);
            return Regex.IsMatch(cleaned, @"^(?:\+?48|0048)?\d{9}$");
        }

        private void CalculateValidTargets()
        {
            _validTargets.Clear();
            var allClients = _dataBase.GetAll<Client>().ToList();

            // Słownik zabezpieczający przed wielokrotnym wysyłaniem SMS i pozwalający wyłuskać pierwsze spotkane imię dla duplikatów numeru
            var deduplicatedClients = new Dictionary<string, Client>();
            
            foreach(var client in allClients)
            {
                if (IsValidPolishPhoneNumber(client.PhoneNumber))
                {
                    // Clean numbers to pure numerical format to group duplicates
                    string cleaned = CleanPhoneNumber(client.PhoneNumber);
                    string standardized = cleaned;
                    
                    if (cleaned.Length == 9) standardized = "+48" + cleaned;
                    else if (cleaned.StartsWith("48") && cleaned.Length == 11) standardized = "+" + cleaned;
                    else if (cleaned.StartsWith("0048")) standardized = "+" + cleaned.Substring(2);

                    // Jeśli jeszcze nie dodaliśmy klienta z identycznym numerem fizycznie:
                    if (!deduplicatedClients.ContainsKey(standardized))
                    {
                        deduplicatedClients.Add(standardized, client);
                    }
                }
            }

            _validTargets = deduplicatedClients.Values.ToList();
        }

        private async Task ExecuteBroadcastAsync()
        {
            ShowConfirmationDialog = false;
            StatusText = $"Rozpoczynanie wysyłki globalnej do {_validTargets.Count} klientów...";

            int successCount = 0;
            int errorCount = 0;

            using var httpClient = new HttpClient();
            if (!string.IsNullOrWhiteSpace(Login) || !string.IsNullOrWhiteSpace(Password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{Login}:{Password}");
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            for(int i = 0; i < _validTargets.Count; i++)
            {
                var target = _validTargets[i];
                StatusText = $"Wysyłanie {i + 1} / {_validTargets.Count}... ({target.Name})";

                string formattedMessage = MessageTemplate.Replace("{imie}", target.Name);

                string safePhone = CleanPhoneNumber(target.PhoneNumber);
                if (safePhone.Length == 9) safePhone = "+48" + safePhone;
                else if (safePhone.StartsWith("48") && safePhone.Length == 11) safePhone = "+" + safePhone;
                else if (safePhone.StartsWith("0048")) safePhone = "+" + safePhone.Substring(2);

                var payload = new 
                {
                    phoneNumbers = new[] { safePhone },
                    message = formattedMessage
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    var response = await httpClient.PostAsync(ApiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        successCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }
                catch
                {
                    errorCount++;
                }

                await Task.Delay(200); 
            }

            StatusText = $"Zakończono wysyłkę Globalną! Wysłano: {successCount}, Błędy: {errorCount}.";
        }
    }
}
