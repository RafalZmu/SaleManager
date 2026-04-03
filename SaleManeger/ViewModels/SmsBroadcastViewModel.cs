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
    public class SmsBroadcastViewModel : ViewModelBase
    {
        private string _apiUrl = "https://api.sms-gate.app/3rdparty/v1/message";
        public string ApiUrl { get => _apiUrl; set => this.RaiseAndSetIfChanged(ref _apiUrl, value); }
        
        private string _login = "";
        public string Login { get => _login; set => this.RaiseAndSetIfChanged(ref _login, value); }
        
        private string _password = "";
        public string Password { get => _password; set => this.RaiseAndSetIfChanged(ref _password, value); }

        private string _messageTemplate = "Witaj {imie}! Zapraszamy po odbiór zamówienia na godzinę {godzina_odbioru} dnia {data_odbioru}.";
        public string MessageTemplate { get => _messageTemplate; set => this.RaiseAndSetIfChanged(ref _messageTemplate, value); }

        private string _statusText = "Gotowy do wysyłki.";
        public string StatusText { get => _statusText; set => this.RaiseAndSetIfChanged(ref _statusText, value); }

        private bool _showConfirmationDialog;
        public bool ShowConfirmationDialog { get => _showConfirmationDialog; set => this.RaiseAndSetIfChanged(ref _showConfirmationDialog, value); }

        private int _clientsToReceiveSmsCount;
        public int ClientsToReceiveSmsCount { get => _clientsToReceiveSmsCount; set => this.RaiseAndSetIfChanged(ref _clientsToReceiveSmsCount, value); }

        public ReactiveCommand<Unit, string> GoBackCommand { get; }
        public ReactiveCommand<Unit, Unit> PreviewBroadcastCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelBroadcastCommand { get; }
        public ReactiveCommand<Unit, Unit> SendBroadcastCommand { get; }

        private readonly string _saleID;
        private readonly IProjectRepository _dataBase;
        private List<(Client client, DateTime expectedArrival)> _validTargets = new();

        public SmsBroadcastViewModel(string saleID, IProjectRepository dataBase)
        {
            _saleID = saleID;
            _dataBase = dataBase;
            GoBackCommand = ReactiveCommand.Create(() => _saleID);
            
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
                    StatusText = "Brak klientów spełniających warunki: muszą posiadać zamówienie (brak odbioru), zaplanowaną godzinę i poprawny numer telefonu.";
                }
            });

            CancelBroadcastCommand = ReactiveCommand.Create(() => 
            {
                ShowConfirmationDialog = false;
            });

            SendBroadcastCommand = ReactiveCommand.CreateFromTask(ExecuteBroadcastAsync);
        }

        private bool IsValidPolishPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            // Usunięcie spacji i myślników
            string cleaned = phone.Replace(" ", "").Replace("-", "");
            // Dopuszczamy 9 cyfr, opcjonalnie poprzedzonych +48, 0048, lub 48
            return Regex.IsMatch(cleaned, @"^(?:\+?48|0048)?\d{9}$");
        }

        private void CalculateValidTargets()
        {
            _validTargets.Clear();
            var saleInfos = _dataBase.GetAll<ClientSaleInfo>()
                .Where(x => x.SaleID == _saleID && x.ExpectedArrivalTime != null)
                .ToList();
                
            var allClients = _dataBase.GetAll<Client>().ToList();
            var allClientOrders = _dataBase.GetAll<ClientOrder>().Where(x => x.SaleID == _saleID).ToList();

            foreach(var info in saleInfos)
            {
                var client = allClients.FirstOrDefault(x => x.ID == info.ClientID);
                if(client != null && IsValidPolishPhoneNumber(client.PhoneNumber))
                {
                    var clientOrders = allClientOrders.Where(p => p.ClientID == client.ID).ToList();
                    
                    // Upewniamy się, że klient ma zamówienie i NIE odebrał jeszcze zakupów (IsReserved == false oznacza pickup)
                    bool hasOrder = clientOrders.Any(p => p.IsReserved == true);
                    bool hasPurchase = clientOrders.Any(p => p.IsReserved == false);

                    if (hasOrder && !hasPurchase)
                    {
                        _validTargets.Add((client, info.ExpectedArrivalTime.Value));
                    }
                }
            }
        }

        private async Task ExecuteBroadcastAsync()
        {
            ShowConfirmationDialog = false;
            StatusText = $"Rozpoczynanie wysyłki do {_validTargets.Count} klientów. Proszę czekać...";

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
                StatusText = $"Wysyłanie {i + 1} / {_validTargets.Count}... ({target.client.Name})";

                string formattedMessage = MessageTemplate
                    .Replace("{imie}", target.client.Name)
                    .Replace("{godzina_odbioru}", target.expectedArrival.ToString("HH:mm"))
                    .Replace("{data_odbioru}", target.expectedArrival.ToString("dd.MM.yyyy"));

                // Wymuszamy +48 jeśli brakuje ze względu na chmurowy standard bramki (optymalizacja)
                string safePhone = target.client.PhoneNumber.Replace(" ", "").Replace("-", "");
                if (safePhone.Length == 9)
                {
                    safePhone = "+48" + safePhone;
                }
                else if (safePhone.StartsWith("48") && safePhone.Length == 11)
                {
                    safePhone = "+" + safePhone;
                }
                else if (safePhone.StartsWith("0048"))
                {
                    safePhone = "+" + safePhone.Substring(2);
                }

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

                // Dodajmy minimalne odczekanie 200ms na wypadek blokad rate-limit po stronie CapCom6 
                await Task.Delay(200); 
            }

            StatusText = $"Zakończono! Wysłano pomyślnie: {successCount}, Błędy: {errorCount}.";
        }
    }
}
