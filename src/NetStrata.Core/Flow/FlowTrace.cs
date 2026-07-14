namespace NetStrata.Core.Flow;

public enum FlowTraceMode
{
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
    bool HasData);
