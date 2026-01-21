using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace Aurum;

/// <summary>
/// Copies Aurum-specific logs from Dalamud log to a dedicated file for easier debugging.
/// Runs periodically to extract [Aurum] entries.
/// </summary>
public class FileLogger : IDisposable
{
    private readonly string logFilePath;
    private readonly string errorLogPath;
    private readonly string dalamudLogPath;
    private readonly System.Threading.Timer? syncTimer;
    
    public FileLogger(IPluginLog dalamudLog, string pluginDirectory)
    {
        // Create log file path next to the DLL
        logFilePath = Path.Combine(pluginDirectory, "aurum.log");
        errorLogPath = Path.Combine(pluginDirectory, "aurum_errors.log");
        
        // Dalamud log path
        var dalamudDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");
        dalamudLogPath = Path.Combine(dalamudDir, "dalamud.log");
        
        try
        {
            // Create initial log file
            File.WriteAllText(logFilePath, $"========================================\n" +
                                           $"AURUM LOG - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                           $"========================================\n" +
                                           $"Monitoring Dalamud log at: {dalamudLogPath}\n" +
                                           $"Filtering for [Aurum] entries...\n\n");
            
            // Initialize error log
            if (!File.Exists(errorLogPath))
            {
                 File.WriteAllText(errorLogPath, $"========================================\n" +
                                           $"AURUM ERROR LOG - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                           $"========================================\n" +
                                           $"Contains only [Aurum] [Error] and [Fatal] entries\n\n");
            }
            else
            {
                 File.AppendAllText(errorLogPath, $"\n========================================\n" +
                                           $"SESSION START - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                           $"========================================\n");
            }

            dalamudLog.Information($"File logging enabled: {logFilePath}");
            dalamudLog.Information($"Error logging enabled: {errorLogPath}");
            
            // Start periodic sync timer (every 2 seconds)
            syncTimer = new System.Threading.Timer(SyncLogsCallback, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            dalamudLog.Error(ex, $"Failed to create log file at {logFilePath}");
        }
    }
    
    private long lastPosition = 0;
    
    private void SyncLogsCallback(object? state)
    {
        try
        {
            if (!File.Exists(dalamudLogPath))
                return;
                
            using var dalamudStream = new FileStream(dalamudLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            // Seek to last read position
            dalamudStream.Seek(lastPosition, SeekOrigin.Begin);
            
            using var reader = new StreamReader(dalamudStream);
            var newLines = new List<string>();
            var newErrorLines = new List<string>();
            
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Only capture lines with [Aurum] tag
                if (line.Contains("[Aurum]"))
                {
                    newLines.Add(line);

                    // Separate capture for errors
                    if (line.Contains("[Error]") || line.Contains("[Fatal]") || line.Contains("Exception"))
                    {
                        newErrorLines.Add(line);
                    }
                }
            }
            
            // Update last position
            lastPosition = dalamudStream.Position;
            
            // Append new lines to our log file
            if (newLines.Any())
            {
                File.AppendAllLines(logFilePath, newLines);
            }

            // Append new error lines to error log file
            if (newErrorLines.Any())
            {
                File.AppendAllLines(errorLogPath, newErrorLines);
            }
        }
        catch
        {
            // Silently fail - don't crash plugin over logging
        }
    }
    
    public string GetLogFilePath() => logFilePath;
    public string GetErrorLogFilePath() => errorLogPath;
    
    public void Dispose()
    {
        syncTimer?.Dispose();
        
        try
        {
            File.AppendAllText(logFilePath, $"\n========================================\n" +
                                            $"LOG ENDED - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                            $"========================================\n");
        }
        catch
        {
            // Silently fail
        }
    }
}
