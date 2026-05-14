using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace smartReception
{
    public class RegisterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private string firstName;
        public string FirstName
        {
            get => firstName;
            set
            {
                firstName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullName));
                OnPropertyChanged(nameof(Initials));
            }
        }
        private string lastName;
        public string LastName
        {
            get => lastName;
            set
            {
                lastName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullName));
                OnPropertyChanged(nameof(Initials));
            }
        }
        private string email;
        public string Email
        {
            get => email;
            set
            {
                email = value;
                OnPropertyChanged();
            }
        }
        private string phone;
        public string PhoneNumber
        {
            get => phone;
            set
            {
                phone = value;
                OnPropertyChanged();
            }
        }
        private string nin;
        public string NIN
        {
            get => nin;
            set
            {
                nin = value;
                OnPropertyChanged();
            }
        }
        private int? floorid;
        public int? FloorID
        {
            get => floorid;
            set
            {
                floorid = value;
                OnPropertyChanged();
            }
        }
        private DateTimeOffset? startDate;
        public DateTimeOffset? StartDate
        {
            get => startDate;
            set
            {
                startDate = value;
                OnPropertyChanged();
            }
        }
        private DateTimeOffset? endDate;
        public DateTimeOffset? EndDate
        {
            get => endDate;
            set
            {
                endDate = value;
                OnPropertyChanged();
            }
        }
        public string FullName => $"{FirstName} {LastName}";
        public string Initials => $"{(string.IsNullOrEmpty(FirstName) ? "": FirstName[0].ToString())}" + $"{(string.IsNullOrEmpty(LastName) ? " " : LastName[0].ToString())}";

    }
}
