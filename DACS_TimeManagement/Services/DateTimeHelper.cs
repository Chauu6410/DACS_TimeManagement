using System.Globalization;

namespace DACS_TimeManagement.Services
{
    public static class DateTimeHelper
    {
        /**
         * Định dạng thời gian dựa trên ngôn ngữ và múi giờ tương ứng.
         * Logic: Luôn coi đầu vào là giờ UTC (vì đã cấu hình DB lưu UTC).
         * 'vi' -> Chuyển sang GMT+7, định dạng 24h
         * 'en' -> Giữ nguyên UTC, định dạng 12h AM/PM
         */
        public static string FormatByLanguage(DateTime dateTime, string language)
        {
            try 
            {
                // Đảm bảo dateTime là UTC
                DateTime utcTime = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();

                if (language == "vi")
                {
                    // Múi giờ Việt Nam (GMT+7)
                    TimeZoneInfo vnZone;
                    try {
                        vnZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    } catch {
                        // Cho Linux/Docker
                        vnZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                    }
                    
                    DateTime vnTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, vnZone);
                    return vnTime.ToString("dd/MM/yyyy HH:mm:ss");
                }
                else
                {
                    // Giờ UTC, định dạng 12h (AM/PM) chuẩn quốc tế
                    return utcTime.ToString("MM/dd/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return dateTime.ToString("g");
            }
        }
    }
}
