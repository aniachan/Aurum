using System;

namespace Aurum.Utils;

public static class ErrorMessageUtils
{
    public static string GetUserFriendlyMessage(Exception ex)
    {
        var message = ex.Message;
        
        // Network errors
        if (message.Contains("No such host is known") || message.Contains("Resolution failed"))
            return "Unable to connect to the internet. Please check your connection.";
            
        if (message.Contains("429"))
            return "Universalis is busy right now. Please try again in a few minutes.";
            
        if (message.Contains("500") || message.Contains("502") || message.Contains("503") || message.Contains("504"))
            return "Universalis servers are having trouble. Please try again later.";
            
        if (message.Contains("timeout") || message.Contains("timed out"))
            return "The request took too long. Universalis might be slow right now.";

        // Database errors
        if (message.Contains("SQLite Error") || message.Contains("database is locked"))
            return "Local database is busy. Please wait a moment and try again.";
            
        // Generic fallback
        return $"An unexpected error occurred: {message}";
    }
    
    public static string GetSuggestion(Exception ex)
    {
        var message = ex.Message;
        
        if (message.Contains("No such host is known"))
            return "Check your internet connection and try again.";
            
        if (message.Contains("429"))
            return "We're sending too many requests. The plugin will automatically slow down.";
            
        if (message.Contains("timeout"))
            return "Try refreshing fewer items or wait a bit.";
            
        if (message.Contains("500") || message.Contains("502") || message.Contains("503") || message.Contains("504"))
            return "Server maintenance or temporary issue. Try again in 10-15 minutes.";

        if (message.Contains("SQLite Error") || message.Contains("database is locked"))
            return "Restarting the game might fix this if it keeps happening.";

        return "If this persists, please report it on GitHub.";
    }
}
