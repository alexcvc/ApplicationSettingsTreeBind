using System;
using System.Collections.Generic;

namespace ApplicationSettingsTreeBind.Source
{
    /// <summary>
    /// Represents the application settings, including network, user interface, and logging configurations.
    /// </summary>
    public readonly struct DataModel
    {
        /// <summary>
        /// Gets or initializes the network settings for the application, 
        /// including configurations such as host, port, SSL usage, and connection limits.
        /// </summary>
        public NetworkSettings Network { get; init; }
        /// <summary>
        /// 
        /// </summary>
        public UiSettings Ui { get; init; }
        public LoggingSettings Logging { get; init; }
    }

    /// <summary>
    /// Represents the network settings for the application, including host and port configuration.
    /// </summary>
    public class NetworkSettings
    {
        private string RemoteIp = "127.0.0.1";
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 502;

        public bool UseSsl { get; set; } = false;
        public int Timeout { get; set; } = 1000;
        public int MaxConnections { get; set; } = 100;

        public void ShowMemory()
        {
            GC.Collect();
            long memory = GC.GetTotalMemory(true);
            Console.WriteLine($"Memory used: {memory} bytes");
        }
    }

    /// <summary>
    /// Represents the user interface settings for the application.
    /// </summary>
    public class UiSettings
    {
        public bool DarkMode { get; set; } = true;
        public double Scale { get; set; } = 1.0;
    }

    /// <summary>
    /// Represents the logging settings for the application, including log level, file path, 
    /// associated tags, and additional configuration options.
    /// </summary>
    public class LoggingSettings
    {
        public string Level { get; set; } = "Info";
        public string FilePath { get; set; } = "app.log";
        public List<string> Tags { get; set; } = new() { "core", "startup" };
        public Dictionary<string, string> Extras { get; set; } = new()
        {
            ["RetentionDays"] = "7",
            ["Rotate"] = "true"
        };
    }
}
