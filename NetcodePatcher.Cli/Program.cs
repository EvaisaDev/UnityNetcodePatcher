﻿using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using NetcodePatcher.Cli;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Fatal()
    .WriteTo.Console()
    .CreateLogger();

try
{
    return await new CommandLineBuilder(new NetcodePatchCommand())
        .UseDefaults()
        .UseExceptionHandler((ex, ctx) => {
            Log.Fatal(ex, "Exception, cannot continue!");
            ctx.ExitCode = -1;
        })
        .Build()
        .InvokeAsync(args);
}
finally
{
    Log.CloseAndFlush();
}
