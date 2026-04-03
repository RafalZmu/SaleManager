using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class SalePlanningViewModel : ViewModelBase
    {
        private readonly IProjectRepository _dataBase;
        private readonly string _saleID;

        public ReactiveCommand<Unit, string> GoBackCommand { get; }
        public ReactiveCommand<Unit, Unit> AutoScheduleCommand { get; }

        public ObservableCollection<SaleDayPlanWrapper> DayPlans { get; }

        private string _warningMessage;
        public string WarningMessage
        {
            get => _warningMessage;
            set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
        }

        private double _minutesPerProduct = 1.0;
        public double MinutesPerProduct
        {
            get => _minutesPerProduct;
            set => this.RaiseAndSetIfChanged(ref _minutesPerProduct, value);
        }

        private double _kgPerMinute = 5.0;
        public double KgPerMinute
        {
            get => _kgPerMinute;
            set => this.RaiseAndSetIfChanged(ref _kgPerMinute, value);
        }

        public ReactiveCommand<Unit, Unit> AddDayPlanCommand { get; }
        public ReactiveCommand<SaleDayPlanWrapper, Unit> RemoveDayPlanCommand { get; }

        public SalePlanningViewModel(IProjectRepository database, string saleID)
        {
            _dataBase = database;
            _saleID = saleID;

            GoBackCommand = ReactiveCommand.Create(() => _saleID);

            var dbPlans = _dataBase.GetAll<SaleDayPlan>().Where(x => x.SaleID == _saleID).OrderBy(x => x.StartTime).ToList();
            DayPlans = new ObservableCollection<SaleDayPlanWrapper>(dbPlans.Select(x => new SaleDayPlanWrapper(x)));

            AddDayPlanCommand = ReactiveCommand.Create(() =>
            {
                var newPlan = new SaleDayPlan 
                { 
                    ID = Guid.NewGuid().ToString(), 
                    SaleID = _saleID, 
                    StartTime = DateTime.Today.AddDays(1).AddHours(8), 
                    EndTime = DateTime.Today.AddDays(1).AddHours(16) 
                };
                var wrapper = new SaleDayPlanWrapper(newPlan);
                DayPlans.Add(wrapper);
                _dataBase.Add(newPlan);
                _dataBase.Save();
            });

            RemoveDayPlanCommand = ReactiveCommand.Create<SaleDayPlanWrapper>(wrapper =>
            {
                DayPlans.Remove(wrapper);
                _dataBase.Delete(wrapper.Model);
                _dataBase.Save();
            });

            AutoScheduleCommand = ReactiveCommand.Create(GenerateArrivalDates);
        }

        private void GenerateArrivalDates()
        {
            if (!DayPlans.Any()) return;

            // Commit wrapper modifications to Db
            foreach(var w in DayPlans) { _dataBase.Update(w.Model); }
            _dataBase.Save();

            var clientOrders = _dataBase.GetAll<ClientOrder>().Where(x => x.SaleID == _saleID).ToList();
            if (!clientOrders.Any()) return;
            
            var clientsWhoPurchased = clientOrders.Where(x => !x.IsReserved).Select(x => x.ClientID).ToHashSet();
            var clientIDs = clientOrders.Select(x => x.ClientID).Distinct().Where(x => !clientsWhoPurchased.Contains(x)).ToList();
            var allClientsSaleInfo = _dataBase.GetAll<ClientSaleInfo>().Where(x => x.SaleID == _saleID).ToList();
            
            var clientScores = new List<ClientScore>();
            foreach (var cid in clientIDs)
            {
                var orders = clientOrders.Where(x => x.ClientID == cid && x.IsReserved).ToList();
                if (!orders.Any()) continue;
                
                double totalTimeForValue = 0;
                int count = 0;
                foreach (var o in orders)
                {
                    if (o.ProductID == "Comment") continue;
                    
                    if (double.TryParse(o.Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        if (KgPerMinute > 0)
                        {
                            totalTimeForValue += (val / KgPerMinute) * MinutesPerProduct;
                        }
                        count++;
                    }
                }
                
                var info = allClientsSaleInfo.FirstOrDefault(x => x.ClientID == cid);
                DateTime firstOrderTime = info?.FirstOrderTime ?? DateTime.MaxValue;

                clientScores.Add(new ClientScore { ClientID = cid, Count = count, Value = totalTimeForValue, FirstOrderTime = firstOrderTime });
            }

            if (!clientScores.Any()) return;

            foreach (var c in clientScores)
            {
                c.FinalScore = Math.Ceiling(c.Value);
                if (c.FinalScore < 1) c.FinalScore = 1; // absolute minimum 1 min per client
            }

            var sortedClients = clientScores
                .OrderBy(x => x.FirstOrderTime)
                .ToList();

            var orderedPlans = DayPlans.OrderBy(x => x.Model.StartTime).ToList();
            if (orderedPlans.Count == 0) return;
            
            DateTime currentTime = orderedPlans.First().Model.StartTime;
            int currentDayIndex = 0;

            WarningMessage = string.Empty;

            foreach (var client in sortedClients)
            {
                if (currentTime >= orderedPlans[currentDayIndex].Model.EndTime)
                {
                    currentDayIndex++;
                    if (currentDayIndex >= orderedPlans.Count)
                    {
                        WarningMessage = "Zbyt mało czasu! Niektórzy klienci nie zostali zaplanowani.";
                        break;
                    }
                    currentTime = orderedPlans[currentDayIndex].Model.StartTime;
                }

                var info = allClientsSaleInfo.FirstOrDefault(x => x.ClientID == client.ClientID);
                bool isNew = false;
                if (info == null)
                {
                    info = new ClientSaleInfo { ID = Guid.NewGuid().ToString(), ClientID = client.ClientID, SaleID = _saleID };
                    isNew = true;
                }

                info.ExpectedArrivalTime = currentTime;
                if (isNew) _dataBase.Add(info); else _dataBase.Update(info);

                currentTime = currentTime.AddMinutes(client.FinalScore);
                
                int excessMinutes = currentTime.Minute % 5;
                if (excessMinutes != 0)
                {
                    currentTime = currentTime.AddMinutes(5 - excessMinutes);
                }
                currentTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, currentTime.Minute, 0);

                if (currentTime >= orderedPlans[currentDayIndex].Model.EndTime)
                {
                    currentTime = orderedPlans[currentDayIndex].Model.EndTime;
                }
            }

            _dataBase.Save();
        }

        private class ClientScore
        {
            public string ClientID { get; set; }
            public int Count { get; set; }
            public double Value { get; set; }
            public DateTime FirstOrderTime { get; set; }
            public double FinalScore { get; set; }
        }
    }

    public class SaleDayPlanWrapper : ReactiveObject
    {
        public SaleDayPlan Model { get; }

        public SaleDayPlanWrapper(SaleDayPlan model) { Model = model; }

        public DateTimeOffset? SelectedDate
        {
            get => Model.StartTime;
            set {
                if (value.HasValue) {
                    Model.StartTime = value.Value.Date + Model.StartTime.TimeOfDay;
                    Model.EndTime = value.Value.Date + Model.EndTime.TimeOfDay;
                    this.RaisePropertyChanged(nameof(SelectedDate));
                }
            }
        }

        public TimeSpan? StartTimeTimeSpan
        {
            get => Model.StartTime.TimeOfDay;
            set {
                if (value.HasValue) {
                    Model.StartTime = Model.StartTime.Date + value.Value;
                    this.RaisePropertyChanged(nameof(StartTimeTimeSpan));
                }
            }
        }

        public TimeSpan? EndTimeTimeSpan
        {
            get => Model.EndTime.TimeOfDay;
            set {
                if (value.HasValue) {
                    Model.EndTime = Model.EndTime.Date + value.Value;
                    this.RaisePropertyChanged(nameof(EndTimeTimeSpan));
                }
            }
        }
    }
}
