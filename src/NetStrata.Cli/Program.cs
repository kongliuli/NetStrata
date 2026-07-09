// NetStrata CLI — Phase 1: --once outputs JSON (stub until SampleCollector lands)
if (args.Contains("--once"))
{
    Console.Error.WriteLine("netstrata: --once not implemented yet (Phase 1 in progress)");
    return 1;
}

Console.WriteLine("NetStrata — use --once for single probe");
return 0;
