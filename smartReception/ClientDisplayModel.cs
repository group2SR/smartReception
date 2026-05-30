using System;

namespace smartReception
{
    public class ClientDisplayModel
    {
        public int ClientID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string NIN { get; set; }
        public string FloorName { get; set; }
        public int? FloorID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string Initials
        {
            get
            {
                if (string.IsNullOrEmpty(FullName)) return "";
                string[] parts = FullName.Split(' ');
                string initials = parts[0].Length > 0 ? parts[0][0].ToString() : "";
                if (parts.Length > 1 && parts[1].Length > 0)
                    initials += parts[1][0].ToString();
                return initials.ToUpper();
            }
        }

        public string DateRange =>
            StartDate.ToString("dd/MM/yyyy") + " → " + EndDate.ToString("dd/MM/yyyy");
    }
}