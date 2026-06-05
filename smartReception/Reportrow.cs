using Windows.UI;
using Windows.UI.Xaml.Media;

namespace smartReception
{
    public class ReportRow
    {
        public string LogId { get; set; }
        public string ClientName { get; set; }
        public string FloorTarget { get; set; }
        public string AccessDate { get; set; }
        public string TimeIn { get; set; }
        public string TimeOut { get; set; }
        public string Status { get; set; }
        public string RowType { get; set; } // "Client" or "Visitor"

        public SolidColorBrush StatusColor
        {
            get
            {
                switch (Status == null ? "" : Status.ToLower())
                {
                    case "signed in": return new SolidColorBrush(Color.FromArgb(255, 209, 250, 229));
                    case "signed out": return new SolidColorBrush(Color.FromArgb(255, 219, 234, 254));
                    case "active": return new SolidColorBrush(Color.FromArgb(255, 209, 250, 229));
                    case "completed": return new SolidColorBrush(Color.FromArgb(255, 219, 234, 254));
                    case "flagged": return new SolidColorBrush(Color.FromArgb(255, 254, 226, 226));
                    case "deleted": return new SolidColorBrush(Color.FromArgb(255, 254, 226, 226));
                    case "blocked": return new SolidColorBrush(Color.FromArgb(255, 254, 249, 195));
                    default: return new SolidColorBrush(Color.FromArgb(255, 241, 245, 249));
                }
            }
        }

        public SolidColorBrush StatusTextColor
        {
            get
            {
                switch (Status == null ? "" : Status.ToLower())
                {
                    case "signed in": return new SolidColorBrush(Color.FromArgb(255, 4, 120, 87));
                    case "signed out": return new SolidColorBrush(Color.FromArgb(255, 29, 78, 216));
                    case "active": return new SolidColorBrush(Color.FromArgb(255, 4, 120, 87));
                    case "completed": return new SolidColorBrush(Color.FromArgb(255, 29, 78, 216));
                    case "flagged": return new SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
                    case "deleted": return new SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
                    case "blocked": return new SolidColorBrush(Color.FromArgb(255, 133, 77, 14));
                    default: return new SolidColorBrush(Color.FromArgb(255, 71, 85, 105));
                }
            }
        }

        public SolidColorBrush RowTypeBadgeColor
        {
            get
            {
                return RowType == "Visitor"
                    ? new SolidColorBrush(Color.FromArgb(255, 239, 246, 255))
                    : new SolidColorBrush(Color.FromArgb(255, 236, 253, 245));
            }
        }

        public SolidColorBrush RowTypeBadgeTextColor
        {
            get
            {
                return RowType == "Visitor"
                    ? new SolidColorBrush(Color.FromArgb(255, 29, 78, 216))
                    : new SolidColorBrush(Color.FromArgb(255, 4, 120, 87));
            }
        }
    }
}