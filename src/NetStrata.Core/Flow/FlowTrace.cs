namespace NetStrata.Core.Flow;

public enum FlowTraceMode
{
    /// <summary>Shared trunk + fan-out to many monitored targets.</summary>
    MultiTarget,
    /// <summary>Single linear probe path (legacy / drill-down).</summary>
    Probe,
    Layers,
    Routes,
    Tls
}

public enum FlowNodeState
{
    Pending,
    Active,
    Passed,
    Degraded,
    Failed,
    Skipped,
    Unknown
}

public sealed record FlowNode(
    string Id,
    string Label,
    FlowNodeState State,
    double? DurationMs,
    string Detail,
    int Stage,
    string? Lane = null);

public sealed record FlowEdge(string From, string To, string? Lane = null);

public sealed record FlowTrace(
    FlowTraceMode Mode,
    string Language,
    string Title,
    string Disclosure,
    string CapturedAt,
    IReadOnlyList<FlowNode> Nodes,
    IReadOnlyList<FlowEdge> Edges,
    bool HasData,
    /// <summary>"direct" or "proxy" when the graph has a single active traffic path; null for TLS.</summary>
    string? ActiveLane = null);
