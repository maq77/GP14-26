namespace SSSP.Api.Realtime.Contracts.System;

public static class SystemTopics
{
    public const string Topic = "system";
    public const string Notification = "notification";
    public const string Updated = "updated";
}

public sealed record SystemNotificationPayload(
    string Level, // info/warn/error
    string Message
);
