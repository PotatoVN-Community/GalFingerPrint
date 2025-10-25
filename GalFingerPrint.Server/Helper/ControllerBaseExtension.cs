using Microsoft.AspNetCore.Mvc;

namespace GalFingerPrint.Server.Helper;

public static class ControllerBaseExtension
{
    public static string GetClientIp(this ControllerBase controller)
    {
        var ip = controller.HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip;
    }
}