using Microsoft.AspNetCore.SignalR;

namespace DACS_TimeManagement.Hubs
{
    public class NotificationHub : Hub
    {
        // Hub cho phép giao tiếp realtime giữa server và client
        // ASP.NET Core sẽ tự map UserId thông qua Identity NameIdentifier
    }
}
