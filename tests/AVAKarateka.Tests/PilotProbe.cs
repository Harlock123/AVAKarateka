using AVAKarateka.Engine;

namespace AVAKarateka.Tests;

/// <summary>How the computer hero gets on (set KARATEKA_PROBE to run it; KARATEKA_PROBE=lazy runs on and kicks).</summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_hero_does()
    {
        if (Environment.GetEnvironmentVariable("KARATEKA_PROBE") is not { } mode) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var w = new World();
        Sim.Start(w);
        var p = new Pilot();
        var falls = new List<string>();
        int t = 0;
        bool prev = false;
        for (; t < 60 * 60 * 6 && !w.Won && falls.Count < 10; t++)
        {
            Controls c;
            if (mode == "lazy")
            {
                bool want = w.Foe is { } f && f.X - w.Hero.X < 300;
                c = w.Hero.Fighting != want && !prev ? new Controls(Stance: true) : new Controls(1, 0, false, t % 30 == 0);
                prev = c.Stance;
            }
            else c = p.Decide(w);
            Sim.Step(w, c);
            if (!w.Hero.Alive)
            {
                falls.Add($"{w.Cause} at {w.Hero.X:0} [{p.Doing}]");
                if (w.Cause == "the princess") break;
                Sim.Respawn(w);
                p.Reset();
            }
        }
        output.WriteLine($"{(w.Won ? "RESCUED" : "not rescued")} after {t / 60}s; beaten {w.Foes.Count(f => !f.Alive)}/{w.Foes.Count}, eagles {w.Eagles.Count(e => e.Gone)}/{w.Eagles.Count}, score {w.Score}, {falls.Count} falls; {sw.ElapsedMilliseconds} ms");
        foreach (var f in falls) output.WriteLine("   " + f);
    }
}
