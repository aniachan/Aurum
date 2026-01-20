using System;
using System.IO;
using Dalamud.Plugin.Services;

namespace Aurum;

/// <summary>
/// Writes logs to a file in the plugin bin directory for easier debugging.
/// Also mirrors all logs to Dalamud's logging system.
/// </summary>
public class FileLogger : IDisposable
{
    private readonly IPluginLog dalamudLog;
    private readonly StreamWriter? fileWriter;
    private readonly string logFilePath;
    private readonly object lockObj = new();
    
    public FileLogger(IPluginLog dalamudLog, string pluginDirectory)
    {
        this.dalamudLog = dalamudLog;
        
        // Create log file path next to the DLL
        logFilePath = Path.Combine(pluginDirectory, "aurum.log");
        
        try
        {
            // Create or overwrite the log file
            fileWriter = new StreamWriter(logFilePath, false)
            {
                AutoFlush = true
            };
            
            // Write header
            fileWriter.WriteLine("========================================");
            fileWriter.WriteLine($"AURUM LOG - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            fileWriter.WriteLine("========================================");
            fileWriter.WriteLine();
            
            dalamudLog.Information($"File logging enabled: {logFilePath}");
        }
        catch (Exception ex)
        {
            dalamudLog.Error(ex, $"Failed to create log file at {logFilePath}");
            fileWriter = null;
        }
    }
    
    public void Information(string message)
    {
        dalamudLog.Information(message);
        WriteToFile($"[INFO] {message}");
    }
    
    public void Info(string message) => Information(message);
    
    public void Warning(string message)
    {
        dalamudLog.Warning(message);
        WriteToFile($"[WARN] {message}");
    }
    
    public void Warn(string message) => Warning(message);
    
    public void Error(string message)
    {
        dalamudLog.Error(message);
        WriteToFile($"[ERROR] {message}");
    }
    
    public void Error(Exception ex, string message)
    {
        dalamudLog.Error(ex, message);
        WriteToFile($"[ERROR] {message}");
        WriteToFile($"[ERROR] Exception: {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace != null)
        {
            WriteToFile($"[ERROR] Stack trace: {ex.StackTrace}");
        }
    }
    
    public void Fatal(string message)
    {
        dalamudLog.Fatal(message);
        WriteToFile($"[FATAL] {message}");
    }
    
    public void Debug(string message)
    {
        dalamudLog.Debug(message);
        WriteToFile($"[DEBUG] {message}");
    }
    
    public void Verbose(string message)
    {
        dalamudLog.Verbose(message);
        WriteToFile($"[VERBOSE] {message}");
    }
    
    private void WriteToFile(string message)
    {
        if (fileWriter == null)
            return;
            
        lock (lockObj)
        {
            try
            {
                fileWriter.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            }
            catch
            {
                // Silently fail if we can't write to file
            }
        }
    }
    
    public void Dispose()
    {
        if (fileWriter != null)
        {
            lock (lockObj)
            {
                try
                {
                    fileWriter.WriteLine();
                    fileWriter.WriteLine("========================================");
                    fileWriter.WriteLine($"LOG ENDED - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    fileWriter.WriteLine("========================================");
                    fileWriter.Flush();
                    fileWriter.Dispose();
                }
                catch
                {
                    // Silently fail during disposal
                }
            }
        }
    }
    
    public string GetLogFilePath() => logFilePath;
}
