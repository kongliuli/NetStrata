namespace NetStrata.Core.Models;

public enum Layer
{
    Wifi,
    Lan,
    Broadband,
    OverseasDirect,
    Proxy,
    Ai
}

public enum LayerState
{
    Ok,
    Degraded,
    Fail,
    Skipped,
    Unknown
}

public enum OverallVerdict
{
    Healthy,
    DirectBlockedProxyOk,
    ProxyBad,
    BroadbandBad,
    WifiBad,
    LanBad,
    Degraded,
    Unknown
}

public enum AiState
{
    Ok,
    ProxyOnly,
    DirectOnly,
    Degraded,
    Fail,
    Skipped,
    Unknown
}
