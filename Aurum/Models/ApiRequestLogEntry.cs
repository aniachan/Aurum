using System;

namespace Aurum.Models;

public class ApiRequestLogEntry
{
    public long Id { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public long ResponseTimeMs { get; set; }
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public long PayloadSize { get; set; }
}
