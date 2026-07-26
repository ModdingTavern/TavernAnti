using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MelonLoader.Logging;

namespace TavernAnti;

internal static class TavernAntiLogger
{
    private static void Log(ColorARGB color, string callerName, object msg, string prefix = "")
    {
        var stackTrace = new StackTrace();
        var callerType = stackTrace.GetFrame(2)?.GetMethod()?.DeclaringType;

        if (IsCompilerGeneratedAsyncStateMachine(callerType)) callerType = callerType?.DeclaringType;

        var typeString = "UNKNOWN";
        if (callerType != null) typeString = $"{callerType}.{callerName}";

        TavernAntiPlugin.Logger.Msg(color, $"{prefix} [{typeString}]: {msg}");
    }

    private static bool IsCompilerGeneratedAsyncStateMachine(Type type)
    {
        return typeof(IAsyncStateMachine).IsAssignableFrom(type) && type.IsDefined(typeof(CompilerGeneratedAttribute), false);
    }

    public static void Msg(object msg, [CallerMemberName] string callerName = "")
    {
        Log(ColorARGB.MediumSpringGreen, callerName, msg);
    }

    public static void Warn(object msg, [CallerMemberName] string callerName = "")
    {
        Log(ColorARGB.SandyBrown, callerName, msg, "WARNING");
    }

    public static void Error(object msg, [CallerMemberName] string callerName = "")
    {
        Log(ColorARGB.Tomato, callerName, msg, "ERROR");
    }
}
