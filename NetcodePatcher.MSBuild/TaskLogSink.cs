using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace NetcodePatcher.MSBuild;

public class TaskLogSink : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider = null;
    private readonly TaskLoggingHelper _taskLoggingHelper;

    public TaskLogSink(TaskLoggingHelper taskLoggingHelper, IFormatProvider? formatProvider)
    {
        _taskLoggingHelper = taskLoggingHelper;
        _formatProvider = formatProvider;
    }
    
    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage(_formatProvider);
            
        switch (logEvent.Level)
        {
            case LogEventLevel.Debug:
                _taskLoggingHelper.LogMessage(MessageImportance.Low, message);
                break;
            case LogEventLevel.Verbose:
                _taskLoggingHelper.LogMessage(MessageImportance.Normal, message);
                break;
            case LogEventLevel.Information:
                _taskLoggingHelper.LogMessage(MessageImportance.High, message);
                break;
            case LogEventLevel.Warning:
                _taskLoggingHelper.LogWarning(message);
                if (logEvent.Exception is not null)
                    _taskLoggingHelper.LogWarningFromException(logEvent.Exception);
                break;
            case LogEventLevel.Error:
            case LogEventLevel.Fatal:
                _taskLoggingHelper.LogError(message);
                if (logEvent.Exception is not null)
                    _taskLoggingHelper.LogErrorFromException(logEvent.Exception);
                break;
        }
    }
}

public static class TaskLogSinkExtensions
{
    public static LoggerConfiguration TaskLoggingHelper(
        this LoggerSinkConfiguration loggerConfiguration,
        TaskLoggingHelper taskLoggingHelper,
        IFormatProvider? formatProvider = null
    ) {
        return loggerConfiguration.Sink(new TaskLogSink(taskLoggingHelper, formatProvider));
    }
}