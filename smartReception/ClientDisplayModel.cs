using System;
using System.Linq;

namespace smartReception
{
    public class ClientDisplayModel
    {
        public int clientID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string NIN { get; set; }
        public string FloorID { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }

        public string Initials => string.IsNullOrWhiteSpace(FullName) ? "??" :
            string.Join("", FullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s[0])).ToUpper();

        public string DateRange => $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
    }
}