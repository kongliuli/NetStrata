using System.Text.RegularExpressions;

namespace NetStrata.Core.Ui;

public static class LangResolver
{
    /// <summary>Default Chinese. "auto" follows UI culture; unknown → zh.</summary>
    public static string Resolve(string? lang)
    {
        // ponytail: product default Chinese; "auto" also zh for now (no locale sniffing)
        if (string.IsNullOrWhiteSpace(lang) || lang.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return "zh";

        return lang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
    }
}

public static class UiStrings
{
    public static string T(string lang, string zh, string en) =>
        LangResolver.Resolve(lang) == "en" ? en : zh;

    public static string LayerName(string lang, string layer) => layer switch
    {
        "wifi" => T(lang, "Wi‑Fi", "Wi‑Fi"),
        "lan" => T(lang, "局域网", "LAN"),
        "broadband" => T(lang, "国内宽带", "Broadband"),
        "overseas_direct" => T(lang, "国际直连", "Overseas"),
        "proxy" => T(lang, "代理", "Proxy"),
        "ai" => T(lang, "AI 汇总", "AI summary"),
        _ => layer
    };

    public static string StateName(string lang, string state) => state switch
    {
        "ok" => T(lang, "正常", "OK"),
        "fail" => T(lang, "失败", "Fail"),
        "degraded" => T(lang, "降级", "Degraded"),
        "skipped" => T(lang, "跳过", "Skipped"),
        "unknown" => T(lang, "未知", "Unknown"),
        "proxy_only" => T(lang, "仅代理", "Proxy only"),
        "direct_only" => T(lang, "仅直连", "Direct only"),
        _ => state
    };

    public static string OverallName(string lang, string overall) => overall switch
    {
        "healthy" => T(lang, "健康", "healthy"),
        "degraded" => T(lang, "降级", "degraded"),
        "wifi_bad" => T(lang, "Wi‑Fi 异常", "wifi_bad"),
        "lan_bad" => T(lang, "局域网异常", "lan_bad"),
        "broadband_bad" => T(lang, "宽带异常", "broadband_bad"),
        "proxy_bad" => T(lang, "代理异常", "proxy_bad"),
        "direct_blocked_proxy_ok" => T(lang, "直连被拦·代理可用", "direct_blocked_proxy_ok"),
        _ => overall
    };

    /// <summary>Localize a verdict phrase (headline / reason / insight). Engine keeps English keys.</summary>
    public static string Phrase(string lang, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? "";
        if (LangResolver.Resolve(lang) == "en")
            return text;

        // Headlines may join several reasons with "; "
        if (text.Contains("; ", StringComparison.Ordinal))
            return string.Join("；", text.Split("; ").Select(p => PhraseOne(p.Trim())));

        return PhraseOne(text);
    }

    public static IReadOnlyList<string> Phrases(string lang, IEnumerable<string> items) =>
        items.Select(i => Phrase(lang, i)).ToList();

    private static string PhraseOne(string text)
    {
        // Exact / prefix table first (stable machine strings from VerdictEngine)
        if (Exact.TryGetValue(text, out var exact))
            return exact;

        foreach (var (re, fmt) in Patterns)
        {
            var m = re.Match(text);
            if (m.Success)
                return fmt(m);
        }

        return text;
    }

    private static readonly Dictionary<string, string> Exact = new(StringComparer.Ordinal)
    {
        ["all green"] = "一切正常",
        ["some checks slow"] = "部分检测偏慢",
        ["direct overseas blocked, proxy works"] = "国际直连被屏蔽，代理可用",
        ["primary link is not Wi-Fi"] = "主链路不是 Wi‑Fi",
        ["no Wi-Fi interface"] = "未检测到 Wi‑Fi 网卡",
        ["no gateway"] = "无网关",
        ["no gateway ping result"] = "无网关 Ping 结果",
        ["LAN fail → cannot judge"] = "局域网失败 → 无法继续判断",
        ["ping 223.5.5.5 fail (likely firewall), https ok"] = "Ping 223.5.5.5 失败（多半是防火墙），HTTPS 正常",
        ["ping 223.5.5.5 fail"] = "Ping 223.5.5.5 失败",
        ["dns_udp_blocked: dig 223.5.5.5 fail but ping/https ok"] =
            "DNS UDP 被拦：dig 223.5.5.5 失败，但 Ping/HTTPS 正常",
        ["dig baidu via 223.5.5.5 fail"] = "经 223.5.5.5 解析 baidu.com 失败",
        ["broadband fail → cannot judge"] = "宽带失败 → 无法继续判断",
        ["all direct overseas fail (blocked)"] = "国际直连全部失败（被屏蔽）",
        ["no proxy configured"] = "未配置代理",
        ["proxy not listening"] = "代理未在监听",
        ["no proxy HTTPS probes"] = "无代理 HTTPS 探测结果",
        ["all proxy HTTPS fail"] = "代理 HTTPS 全部失败",
        ["dns_path_blocked: public DNS UDP 53 likely filtered; HTTP/proxy may still work"] =
            "DNS 路径受阻：公网 DNS UDP/53 可能被过滤；HTTP/代理仍可能可用",
    };

    private static readonly (Regex Re, Func<Match, string> Fmt)[] Patterns =
    [
        (new Regex(@"^primary link is (\w+) \((.+)\)$", RegexOptions.Compiled),
            m => $"主链路为 {LinkTypeZh(m.Groups[1].Value)}（{m.Groups[2].Value}）"),
        (new Regex(@"^primary link is (\w+)$", RegexOptions.Compiled),
            m => $"主链路为 {LinkTypeZh(m.Groups[1].Value)}"),
        (new Regex(@"^status=(.+)$", RegexOptions.Compiled),
            m => $"状态={m.Groups[1].Value}"),
        (new Regex(@"^weak signal (.+)$", RegexOptions.Compiled),
            m => $"信号弱 {m.Groups[1].Value}"),
        (new Regex(@"^marginal signal (.+)$", RegexOptions.Compiled),
            m => $"信号一般 {m.Groups[1].Value}"),
        (new Regex(@"^low tx rate (.+)$", RegexOptions.Compiled),
            m => $"发送速率偏低 {m.Groups[1].Value}"),
        (new Regex(@"^gw ping loss=(.+)$", RegexOptions.Compiled),
            m => $"网关 Ping 丢包={m.Groups[1].Value}"),
        (new Regex(@"^gw rtt (.+)$", RegexOptions.Compiled),
            m => $"网关往返时延 {m.Groups[1].Value}"),
        (new Regex(@"^baidu https fail: (.*)$", RegexOptions.Compiled),
            m => $"百度 HTTPS 失败：{m.Groups[1].Value}"),
        (new Regex(@"^baidu slow (.+)$", RegexOptions.Compiled),
            m => $"百度访问偏慢 {m.Groups[1].Value}"),
        (new Regex(@"^(\d+)/(\d+) direct overseas ok$", RegexOptions.Compiled),
            m => $"国际直连 {m.Groups[1].Value}/{m.Groups[2].Value} 可达"),
        (new Regex(@"^proxy port (.+) not listening$", RegexOptions.Compiled),
            m => $"代理端口 {m.Groups[1].Value} 未在监听"),
        (new Regex(@"^(\d+)/(\d+) proxy targets ok$", RegexOptions.Compiled),
            m => $"代理目标 {m.Groups[1].Value}/{m.Groups[2].Value} 可达"),
        (new Regex(@"^egress fetch failed: (.*)$", RegexOptions.Compiled),
            m => $"出口 IP 获取失败：{m.Groups[1].Value}"),
        (new Regex(@"^system HTTP proxy port (.+) ≠ active (.+)$", RegexOptions.Compiled),
            m => $"系统 HTTP 代理端口 {m.Groups[1].Value} ≠ 当前代理 {m.Groups[2].Value}"),
        (new Regex(@"^(.+) 直连: (.*)$", RegexOptions.Compiled),
            m => $"{m.Groups[1].Value} 直连：{m.Groups[2].Value}"),
        (new Regex(@"^(.+) 代理: (.*)$", RegexOptions.Compiled),
            m => $"{m.Groups[1].Value} 代理：{m.Groups[2].Value}"),
    ];

    private static string LinkTypeZh(string t) => t switch
    {
        "ethernet" => "有线",
        "wifi" => "Wi‑Fi",
        "other" => "其他",
        _ => t
    };

    public static string WaitingHeadline(string lang) =>
        T(lang, "等待 Daemon 写入探测结果…", "Waiting for first sample…");

    public static string NoState(string lang) =>
        T(lang, "尚无 state.json", "No state.json yet");

    public static string CycleMeta(string lang, int cycle, string time, string ms) =>
        T(lang, $"周期 #{cycle} · {time} · {ms}ms", $"cycle #{cycle} · {time} · {ms}ms");

    public static string SectionLayers(string lang) => T(lang, "网络分层", "Network layers");
    public static string SectionAi(string lang) => T(lang, "AI / 开发者 API", "AI / Dev APIs");
    public static string SectionPing(string lang) => T(lang, "自定义目标", "Custom targets");
    public static string SectionLocal(string lang) => T(lang, "本机网络", "Local network");
    public static string OpenSiteHint(string lang) =>
        T(lang, "单击或右键 → 打开官网（探测走 API 端点，浏览器打开产品站）",
            "Click / right-click → open official site (probe uses API host; browser opens product site)");
    public static string Refresh(string lang) => T(lang, "刷新", "Refresh");
    public static string SettingsTitle(string lang) => T(lang, "设置", "Settings");
    public static string Language(string lang) => T(lang, "界面语言", "Language");
    public static string Theme(string lang) => T(lang, "外观主题", "Theme");
    public static string ThemeSystem(string lang) => T(lang, "跟随系统", "System");
    public static string ThemeLight(string lang) => T(lang, "浅色", "Light");
    public static string ThemeDark(string lang) => T(lang, "深色", "Dark");
    public static string LangZh(string lang) => T(lang, "中文", "Chinese");
    public static string LangEn(string lang) => T(lang, "English", "English");
    public static string LangAuto(string lang) => T(lang, "自动（默认中文）", "Auto (default Chinese)");
    public static string Direct(string lang) => T(lang, "直连", "Direct");
    public static string ViaProxy(string lang) => T(lang, "代理", "Proxy");
    public static string Ms(string lang, double? ms) =>
        ms is null ? "—" : $"{ms:F0} ms";

    public static string MetricKey(string lang, string key) => key switch
    {
        "rssi" => T(lang, "信号", "rssi"),
        "noise" => T(lang, "噪声", "noise"),
        "channel" => T(lang, "信道", "channel"),
        "txRate" => T(lang, "发送速率", "txRate"),
        "ssid" => T(lang, "SSID", "ssid"),
        "linkType" => T(lang, "链路", "linkType"),
        "gateway" => T(lang, "网关", "gateway"),
        "avgMs" => T(lang, "平均时延", "avgMs"),
        "loss" => T(lang, "丢包", "loss"),
        "configured" => T(lang, "已配置", "configured"),
        "proxyUrl" => T(lang, "代理地址", "proxyUrl"),
        "listening" => T(lang, "监听中", "listening"),
        "egressIp" => T(lang, "出口 IP", "egressIp"),
        "listenerProcess" => T(lang, "监听进程", "listenerProcess"),
        "baiduMs" => T(lang, "百度耗时", "baiduMs"),
        _ when key.EndsWith("DirectOk", StringComparison.Ordinal) =>
            T(lang, key[..^8] + "直连", key),
        _ when key.EndsWith("ProxyOk", StringComparison.Ordinal) =>
            T(lang, key[..^7] + "代理", key),
        _ => key
    };
}
