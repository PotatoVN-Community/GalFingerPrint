using Microsoft.AspNetCore.Mvc;

namespace GalFingerPrint.Server.Helper;

public static class ControllerBaseExtension
{
    public static string GetClientIp(this ControllerBase controller)
    {
        var ip = controller.HttpContext.Connection.RemoteIpAddress;
        if (ip?.IsIPv4MappedToIPv6 is true) 
            ip = ip.MapToIPv4();
        var ipStr = ip?.ToString();
        return string.IsNullOrWhiteSpace(ipStr) ? "127.0.0.1" : ipStr;
    }
}