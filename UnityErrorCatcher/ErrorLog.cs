using UnityEngine;

namespace Xname.UnityErrorCatcher;

internal readonly struct ErrorLog
{
    public ErrorLog(string condition, string stackTrace, LogType type)
    {
        Condition = condition;
        StackTrace = stackTrace;
        Type = type;
    }

    public string Condition { get; }

    public string StackTrace { get; }

    public LogType Type { get; }
}
