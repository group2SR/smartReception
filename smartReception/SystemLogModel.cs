using System;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace smartReception
{
    public class SystemLogModel
    {
        public int SystemLogId { get; set; }
        public int UserId { get; set; }
        public string Actor { get; set; }
        public string Role { get; set; }
        public string Activity { get; set; }
        public string Description { get; set; }
        public DateTimeOffset LogDate { get; set; }

        public string FormattedDate =>
            LogDate.LocalDateTime.ToString("dd MMM yyyy  HH:mm:ss");
        public string FormattedTime =>
            LogDate.LocalDateTime.ToString("HH:mm:ss");
        public string FormattedDay =>
            LogDate.LocalDateTime.ToString("dd MMM yyyy");

        public string ActivityBadgeColor
        {
            get
            {
                switch (Activity)
                {
                    case "Login": return "#10B981";
                    case "Logout": return "#EF4444";
                    case "Access Log": return "#3B82F6";
                    case "Generate Report": return "#8B5CF6";
                    case "Backup Database": return "#F59E0B";
                    default: return "#64748B";
                }
            }
        }

        public string ActivityIcon
        {
            get
            {
                switch (Activity)
                {
                    case "Login": return "🟢";
                    case "Logout": return "🔴";
                    case "Access Log": return "📋";
                    case "Generate Report": return "📈";
                    case "Backup Database": return "💾";
                    default: return "📌";
                }
            }
        }

        public string RoleBadgeColor =>
            Role == "Administrator" ? "#DC2626" : "#0369A1";

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Actor)) return "?";
                var parts = Actor.Trim().Split(' ');
                return parts.Length >= 2
                    ? parts[0][0].ToString().ToUpper() + parts[1][0].ToString().ToUpper()
                    : parts[0][0].ToString().ToUpper();
            }
        }
    }

    public class ReceptionistSession
    {
        public string ReceptionistName { get; set; }
        public string Role { get; set; }
        public string Date { get; set; }
        public string TimeIn { get; set; }
        public string TimeOut { get; set; }
        public string Duration { get; set; }
        public string Status { get; set; }  // "Completed" | "Active"

        public string StatusColor =>
            Status == "Active" ? "#10B981" : "#3B82F6";
        public string TimeOutDisplay =>
            string.IsNullOrEmpty(TimeOut) ? "Still Active" : TimeOut;

        public string RoleBadgeColor =>
            Role == "Administrator" ? "#DC2626" : "#0369A1";

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ReceptionistName)) return "?";
                var parts = ReceptionistName.Trim().Split(' ');
                return parts.Length >= 2
                    ? parts[0][0].ToString().ToUpper() + parts[1][0].ToString().ToUpper()
                    : parts[0][0].ToString().ToUpper();
            }
        }
    }
}