using System;

namespace smartReception
{
    public class ClientDisplayModel
    {
        public int clientID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string NIN { get; set; }
        // Changed to string to resolve CS0029 at line 115 in entry.xaml.cs
        public string FloorID { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }

        public string Initials => $"{(string.IsNullOrEmpty(FullName) ? "" : FullName.Split(' ')[0][0].ToString())}" +
            $"{(FullName.Contains(" ") ? FullName.Split(' ')[1][0].ToString() : "")}";

        public string DateRange => $"{startDate:dd MMM} - {endDate:dd MMM}";
    }
}