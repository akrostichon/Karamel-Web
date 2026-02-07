using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Karamel.Backend.Services;

/// <summary>
/// Telemetry initializer that masks the last octet of IP addresses for privacy protection.
/// Implements Privacy-by-Design principle: IP addresses are always anonymized regardless of user consent.
/// Example: 192.168.1.142 → 192.168.1.0
/// </summary>
public class IpMaskingTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is RequestTelemetry requestTelemetry)
        {
            requestTelemetry.Context.Location.Ip = MaskIpAddress(requestTelemetry.Context.Location.Ip);
        }
        else if (telemetry is TraceTelemetry traceTelemetry)
        {
            traceTelemetry.Context.Location.Ip = MaskIpAddress(traceTelemetry.Context.Location.Ip);
        }
        else if (telemetry is ExceptionTelemetry exceptionTelemetry)
        {
            exceptionTelemetry.Context.Location.Ip = MaskIpAddress(exceptionTelemetry.Context.Location.Ip);
        }
        else if (telemetry is DependencyTelemetry dependencyTelemetry)
        {
            dependencyTelemetry.Context.Location.Ip = MaskIpAddress(dependencyTelemetry.Context.Location.Ip);
        }
        else if (telemetry is EventTelemetry eventTelemetry)
        {
            eventTelemetry.Context.Location.Ip = MaskIpAddress(eventTelemetry.Context.Location.Ip);
        }
        else if (telemetry is MetricTelemetry metricTelemetry)
        {
            metricTelemetry.Context.Location.Ip = MaskIpAddress(metricTelemetry.Context.Location.Ip);
        }
        else if (telemetry is PageViewTelemetry pageViewTelemetry)
        {
            pageViewTelemetry.Context.Location.Ip = MaskIpAddress(pageViewTelemetry.Context.Location.Ip);
        }
    }

    private static string? MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return ipAddress;
        }

        // Handle IPv4 addresses
        if (ipAddress.Contains('.'))
        {
            var parts = ipAddress.Split('.');
            if (parts.Length == 4)
            {
                // Mask last octet: 192.168.1.142 → 192.168.1.0
                return $"{parts[0]}.{parts[1]}.{parts[2]}.0";
            }
        }

        // Handle IPv6 addresses
        if (ipAddress.Contains(':'))
        {
            var parts = ipAddress.Split(':');
            if (parts.Length >= 4)
            {
                // Mask last 64 bits (last 4 groups): 2001:0db8:85a3:0000:0000:8a2e:0370:7334 → 2001:0db8:85a3:0000::
                return string.Join(":", parts.Take(4)) + "::";
            }
        }

        // If format is unknown, return null for maximum privacy
        return null;
    }
}
