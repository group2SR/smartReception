using System;

namespace smartReception
{
    // ── Client display model ───────────────────────────────────────────────────
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
                if (string.IsNullOrEmpty(FullName)) return "?";
                int sp = FullName.IndexOf(' ');
                return sp >= 0
                    ? $"{FullName[0]}{FullName[sp + 1]}".ToUpper()
                    : $"{FullName[0]}".ToUpper();
            }
        }

        public string DateRange =>
            $"{StartDate:dd MMM yyyy} \u2013 {EndDate:dd MMM yyyy}";
    }

    // ── Visitor display model ──────────────────────────────────────────────────
    public class VisitorDisplayModel
    {
        public int VisitorID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string NIN { get; set; }
        public string FloorName { get; set; }
        public int? FloorID { get; set; }
        public string VisitDate { get; set; }
        public string TimeIn { get; set; }
        public string TimeOut { get; set; }

        public string Initials
        {
            get
            {
                if (string.IsNullOrEmpty(FullName)) return "?";
                int sp = FullName.IndexOf(' ');
                return sp >= 0
                    ? $"{FullName[0]}{FullName[sp + 1]}".ToUpper()
                    : $"{FullName[0]}".ToUpper();
            }
        }

        public string TimeRange
        {
            get
            {
                string range = string.IsNullOrEmpty(TimeIn) ? "\u2014" : TimeIn;
                if (!string.IsNullOrEmpty(TimeOut)) range += $" \u2192 {TimeOut}";
                return range;
            }
        }
    }
}