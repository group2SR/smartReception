using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Postgrest.Attributes;
using Postgrest.Models;
using System.Linq;

namespace smartReception
{
    [Table("clients")]
    public class Client : BaseModel
    {
        [PrimaryKey("id", false)]
        public int id { get; set; }

        [Column("first_name")]
        public string first_name { get; set; }

        [Column("last_name")]
        public string last_name { get; set; }

        [Column("email")]
        public string email { get; set; }

        [Column("phone_number")]
        public string phone_number { get; set; }

        [Column("nin")]
        public string nin { get; set; }

        [Column("floor_id")]
        public int floor_id { get; set; }

        [Column("start_date")]
        public DateTime start_date { get; set; }

        [Column("end_date")]
        public DateTime end_date { get; set; }
    }

    public sealed partial class entry : Page
    {
        public RegisterViewModel vm { get; set; } = new RegisterViewModel();
        public ObservableCollection<ClientDisplayModel> Clients { get; set; } = new ObservableCollection<ClientDisplayModel>();

        public entry()
        {
            this.InitializeComponent();
            this.DataContext = vm;

            // Ensure ViewModel is initialized to avoid null reference errors
            vm.StartDate = DateTimeOffset.Now;
            vm.EndDate = DateTimeOffset.Now;

            _ = LoadClients();
        }

        private async void btnSave(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(vm.FirstName) || string.IsNullOrWhiteSpace(vm.LastName) || cmbfloor.SelectedIndex == -1)
            {
                await showmessage("Please fill in the Required Fields.");
                return;
            }

            var newClient = new Client
            {
                first_name = vm.FirstName.Trim(),
                last_name = vm.LastName.Trim(),
                email = vm.Email?.Trim() ?? "",
                phone_number = vm.PhoneNumber?.Trim() ?? "",
                nin = vm.NIN?.Trim() ?? "",
                floor_id = cmbfloor.SelectedIndex + 1,
                // Using the PascalCase properties from RegisterViewModel
                start_date = vm.StartDate?.DateTime ?? DateTime.Now,
                end_date = vm.EndDate?.DateTime ?? DateTime.Now
            };

            try
            {
                await App.SupabaseClient.From<Client>().Insert(newClient);
                await showmessage("Client saved successfully.");
                ClearFields();
                await LoadClients();
            }
            catch (Exception ex)
            {
                await showmessage("Error: " + ex.Message);
            }
        }

        public async Task LoadClients()
        {
            try
            {
                var result = await App.SupabaseClient.From<Client>().Get();
                Clients.Clear();

                foreach (var item in result.Models)
                {
                    // Fixed: FloorID is assigned a string to match the updated ClientDisplayModel
                    var displayItem = new ClientDisplayModel
                    {
                        clientID = item.id,
                        FullName = $"{item.first_name} {item.last_name}",
                        Email = item.email,
                        PhoneNumber = item.phone_number,
                        NIN = item.nin,
                        FloorID = $"Floor {item.floor_id}",
                        startDate = item.start_date,
                        endDate = item.end_date
                    };

                    Clients.Add(displayItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Load Error: " + ex.Message);
            }
        }

        private void ClearFields()
        {
            vm.FirstName = "";
            vm.LastName = "";
            vm.Email = "";
            vm.PhoneNumber = "";
            vm.NIN = "";
            cmbfloor.SelectedIndex = -1;
            vm.StartDate = DateTimeOffset.Now;
            vm.EndDate = DateTimeOffset.Now;
        }

        private async Task showmessage(string v)
        {
            MessageDialog dialog = new MessageDialog(v);
            await dialog.ShowAsync();
        }
    }
}