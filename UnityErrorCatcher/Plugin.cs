using PluginAPI.Core;
using PluginAPI.Core.Attributes;
using PluginAPI.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using LogType = UnityEngine.LogType;

namespace Xname.UnityErrorCatcher;

internal sealed class Plugin
{
    [PluginConfig]
    public static Config Config;

    [PluginPriority(0x0b)]
    [PluginEntryPoint("Unity Error Catcher", "1.0.0", "Plugin for catching Unity's exceptions and printing them to Server's console", "Xname")]
    public void Load()
    {
        _serverErrorLogsDirectoryPath = Path.Combine(PluginHandler.Get(this).PluginDirectoryPath, Server.Port.ToString());

        if (!Directory.Exists(_serverErrorLogsDirectoryPath) && Config.LoggingToFileEnabled)
            Directory.CreateDirectory(_serverErrorLogsDirectoryPath);

        EventManager.RegisterEvents(this);
    }

    [PluginUnload]
    public void Unload()
    {
        EventManager.UnregisterEvents(this);
        Application.logMessageReceived -= Application_LogMessageReceived;
        _initialized = false;

        if (Config.LoggingToFileEnabled)
            _errorLoggingThread.Abort();
    }

    private static StreamWriter NewLog => new(Path.Combine(_serverErrorLogsDirectoryPath, $"Error Log {(DateTime.Now - Round.Duration):yyyy-MM-dd HH.mm.ss}.txt"), true);

    private static readonly List<ErrorLog> _logs = new();
    private static readonly object _lock = new();
    private static string _serverErrorLogsDirectoryPath;
    private static Thread _errorLoggingThread;
    private static bool _initialized = false;
    private static bool _newRound = false;

    [PluginEvent]
    private void OnMapGenerated(MapGeneratedEvent ev)
    {
        if (_initialized)
        {
            _newRound = true;
            return;
        }

        _initialized = true;

        if (Config.LoggingToFileEnabled)
        {
            _errorLoggingThread = new Thread(new ThreadStart(WriteLogs))
            {
                Name = "Error Logging Thread",
                IsBackground = true,
            };

            _errorLoggingThread.Start();
        }
        
        Application.logMessageReceived += Application_LogMessageReceived;
    }

    private void Application_LogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type is not LogType.Error and not LogType.Exception)
            return;

        if (stackTrace.Contains("Clutter"))
            return;

        if (condition.Contains("A scripted object"))
            return;

        if (Config.LoggingToConsoleEnabled)
        {
            Log.Error($"Catched Unity Message of type {type}:");
            Log.Error(condition + "\n" + stackTrace);
        }

        if (Config.LoggingToFileEnabled)
        {
            lock (_lock)
                _logs.Add(new(condition, stackTrace, type));
        }
    }

    private void WriteLogs()
    {
        StreamWriter file = null;

        while (_initialized)
        {
            if (_newRound)
            {
                file?.Dispose();
                file = null;
            }

            lock (_lock)
            {
                if (_logs.Count > 0 && file is null)
                    file = NewLog;

                foreach (ErrorLog log in _logs)
                {
                    string toWrite = $"[{DateTime.Now:yyyy-MM-dd HH.mm.ss.fff zzz}] Catched Unity Message of type {log.Type}:\n{log.Condition}\n{log.StackTrace}";
                    file.WriteLine(toWrite);
                }

                file?.Flush();
                _logs.Clear();
            }

            Task.Delay(1000).Wait();
        }

        file?.Dispose();
    }
}