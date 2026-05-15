using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Postgrest.Attributes;
using Postgrest.Models;
using System.Linq;

namespace smartReception
{
    [Table("clients")]
    public class Client : BaseModel
    {
        [PrimaryKey("id", false)] public int id { get; set; }
        [Column("first_name")] public string first_name { get; set; }
        [Column("last_name")] public string last_name { get; set; }
        [Column("email")] public string email { get; set; }
        [Column("phone_number")] public string phone_number { get; set; }
        [Column("nin")] public string nin { get; set; }
        [Column("floor_id")] public int? floor_id { get; set; }
        [Column("start_date")] public DateTime start_date { get; set; }
        [Column("end_date")] public DateTime? end_date { get; set; }
    }

    public sealed partial class entry : Page
    {
        public RegisterViewModel vm { get; set; } = new RegisterViewModel();
        public ObservableCollection<ClientDisplayModel> Clients { get; set; } = new ObservableCollection<ClientDisplayModel>();

        private int? editingClientId = null;

        public entry()
        {
            this.InitializeComponent();
            this.DataContext = vm;

            // Set default dates to today
            vm.StartDate = DateTimeOffset.Now;
            vm.EndDate = DateTimeOffset.Now.AddDays(1);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadClients();
        }

        public async Task LoadClients()
        {
            try
            {
                var result = await App.SupabaseClient.From<Client>().Get();

                // Ensure UI updates happen on the UI thread
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    Clients.Clear();
                    foreach (var item in result.Models)
                    {
                        Clients.Add(new ClientDisplayModel
                        {
                            clientID = item.id,
                            FullName = $"{item.first_name} {item.last_name}".Trim(),
                            Email = item.email,
                            NIN = item.nin,
                            FloorID = item.floor_id.HasValue ? $"Floor {item.floor_id}" : "General",
                            startDate = item.start_date,
                            endDate = item.end_date ?? DateTime.Now
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Database Load Error: " + ex.Message);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                var clientToEdit = Clients.FirstOrDefault(c => c.clientID == id);
                if (clientToEdit != null)
                {
                    // Logic to split names correctly
                    string[] nameParts = clientToEdit.FullName.Split(new[] { ' ' }, 2);
                    vm.FirstName = nameParts[0];
                    vm.LastName = nameParts.Length > 1 ? nameParts[1] : "";
                    vm.Email = clientToEdit.Email;
                    vm.NIN = clientToEdit.NIN;

                    vm.StartDate = new DateTimeOffset(clientToEdit.startDate);
                    vm.EndDate = new DateTimeOffset(clientToEdit.endDate);

                    // Floor selection logic
                    if (clientToEdit.FloorID.Contains("Floor"))
                    {
                        if (int.TryParse(clientToEdit.FloorID.Replace("Floor ", ""), out int fId))
                        {
                            cmbfloor.SelectedIndex = fId - 1;
                        }
                    }

                    editingClientId = id;

                    // Switch to Registration tab and scroll to top
                    rootPivot.SelectedIndex = 0;
                }
            }
        }

        private async void btnSave(object sender, RoutedEventArgs e)
        {
            // Validation with high visibility alerts
            if (string.IsNullOrWhiteSpace(vm.FirstName) || string.IsNullOrWhiteSpace(vm.Email))
            {
                await showmessage("Attention: First Name and Email are required to sync.");
                return;
            }

            var clientData = new Client
            {
                first_name = vm.FirstName,
                last_name = vm.LastName ?? "",
                email = vm.Email,
                nin = vm.NIN ?? "",
                floor_id = cmbfloor.SelectedIndex != -1 ? cmbfloor.SelectedIndex + 1 : (int?)null,
                start_date = vm.StartDate?.DateTime ?? DateTime.Now,
                end_date = vm.EndDate?.DateTime
            };

            try
            {
                if (editingClientId.HasValue)
                {
                    // Update existing record
                    await App.SupabaseClient.From<Client>()
                        .Where(x => x.id == editingClientId.Value)
                        .Update(clientData);
                    await showmessage("Success: Visitor record updated.");
                }
                else
                {
                    // Insert new record
                    await App.SupabaseClient.From<Client>().Insert(clientData);
                    await showmessage("Success: New visitor synced to cloud.");
                }

                ClearForm();
                await LoadClients();

                // Switch to database view to see the new entry
                rootPivot.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                await showmessage("Connection Error: " + ex.Message);
            }
        }

        private void ClearForm()
        {
            vm.FirstName = "";
            vm.LastName = "";
            vm.Email = "";
            vm.NIN = "";
            cmbfloor.SelectedIndex = -1;
            vm.StartDate = DateTimeOffset.Now;
            vm.EndDate = DateTimeOffset.Now.AddDays(1);
            editingClientId = null;
        }

        private async void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                try
                {
                    await App.SupabaseClient.From<Client>().Where(x => x.id == id).Delete();
                    await LoadClients();
                }
                catch (Exception ex)
                {
                    await showmessage("Delete failed: Check your internet connection.");
                }
            }
        }

        private async Task showmessage(string v)
        {
            await new MessageDialog(v, "Smart Reception System").ShowAsync();
        }
    }
}