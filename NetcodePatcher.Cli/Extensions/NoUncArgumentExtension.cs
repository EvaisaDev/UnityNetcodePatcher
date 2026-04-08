using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;

namespace NetcodePatcher.Cli.Extensions;

public static class NoUncArgumentExtension
{
    public static Argument<FileSystemInfo> NoUnc(this Argument<FileSystemInfo> argument)
    {
        argument.AddValidator(IsNotUnc);
        return argument;
    }

    public static Argument<T> NoUnc<T>(this Argument<T> argument) where T : IEnumerable<FileSystemInfo>
    {
        argument.AddValidator(IsNotUnc);
        return argument;
    }

    public static Option<FileSystemInfo> NoUnc(this Option<FileSystemInfo> option)
    {
        option.AddValidator(IsNotUnc);
        return option;
    }

    public static Option<T> NoUnc<T>(this Option<T> option) where T : IEnumerable<FileSystemInfo>
    {
        option.AddValidator(IsNotUnc);
        return option;
    }

    private static void IsNotUnc(SymbolResult result)
    {
        foreach (var token in result.Tokens)
        {
            if (!token.Value.StartsWith(@"\\")) continue;
            result.ErrorMessage = "Argument cannot accept universal name specifiers - must provide local file/directory path.";
            return;
        }
    }
}
