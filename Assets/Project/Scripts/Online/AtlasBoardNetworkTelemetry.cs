using System;
using UnityEngine;

public readonly struct AtlasBoardNetworkTelemetrySample
{
    public AtlasBoardNetworkTelemetrySample(
        string functionName,
        bool success,
        float roundTripMs,
        bool requestWasSent,
        double realtimeAt)
    {
        FunctionName =
            functionName ?? string.Empty;

        Success =
            success;

        RoundTripMs =
            Mathf.Max(
                0f,
                roundTripMs);

        RequestWasSent =
            requestWasSent;

        RealtimeAt =
            realtimeAt;
    }

    public string FunctionName
    {
        get;
    }

    public bool Success
    {
        get;
    }

    public float RoundTripMs
    {
        get;
    }

    public bool RequestWasSent
    {
        get;
    }

    public double RealtimeAt
    {
        get;
    }
}

public static class AtlasBoardNetworkTelemetry
{
    public static event Action<
        AtlasBoardNetworkTelemetrySample>
        SampleReported;

    public static void Report(
        string functionName,
        bool success,
        float roundTripMs)
    {
        SampleReported?.Invoke(
            new AtlasBoardNetworkTelemetrySample(
                functionName,
                success,
                roundTripMs,
                true,
                Time.realtimeSinceStartupAsDouble));
    }

    public static void ReportUnavailable(
        string functionName)
    {
        SampleReported?.Invoke(
            new AtlasBoardNetworkTelemetrySample(
                functionName,
                false,
                0f,
                false,
                Time.realtimeSinceStartupAsDouble));
    }
}
