namespace AVAKarateka.Engine;

/// <summary>
/// The computer hero, for the demonstration and the tests. It runs on through the fortress, drops into its fighting
/// stance when a guard (or the eagle) is near. Against a guard it fights as a karateka is taught: facing a strike, it
/// guards that height; out of reach, it steps in; in reach, it strikes at a height he isn't guarding. Against the eagle
/// it tries every act it has on a copy of the moment and takes whichever strikes the bird without being pecked. And it
/// meets the princess as a friend, not a fighter.
/// </summary>
public sealed class Pilot
{
    public const int Ahead = 40;
    private static readonly Controls[] Acts =
    [
        new(Dx: 1), new(Dx: -1), new(), new(Dy: -1), new(Dy: 1),
        new(Dy: -1, Punch: true), new(Punch: true), new(Dy: 1, Punch: true),
        new(Dy: -1, Kick: true), new(Kick: true), new(Dy: 1, Kick: true),
    ];
    private bool _stancePrev;

    public string Doing { get; private set; } = "";

    public void Reset()
    {
        _stancePrev = false;
        Doing = "";
    }

    public Controls Decide(GameEngine g) => g.Phase != Phase.Play ? default : Decide(g.World);

    public Controls Decide(World w)
    {
        var h = w.Hero;
        if (!h.Alive || w.Won) return default;
        var foe = w.Foe;
        var bird = w.Bird;
        bool trouble = foe != null && foe.X - h.X < Sim.Wake - 60 || bird != null;
        // The stance: fighting near trouble, running otherwise (and always to the princess).
        if (h.Fighting != trouble)
        {
            if (!h.Ready) return default;
            bool press = !_stancePrev;
            _stancePrev = press;
            Doing = trouble ? "ready to fight" : "running on";
            return new Controls(Stance: press);
        }
        _stancePrev = false;
        if (!trouble) { Doing = foe == null ? "to the princess" : "running on"; return new Controls(1); }
        if (!h.Ready) return new Controls(Dy: h.Block - 1);
        if (bird != null)
        {
            // The eagle: try each act on a copy, and take whichever strikes it without being pecked.
            double best = double.NegativeInfinity;
            var pick = Acts[2];
            foreach (var a in Acts)
            {
                double v = Value(w, a);
                if (v > best) { best = v; pick = a; }
            }
            Doing = "fighting the eagle";
            return pick;
        }
        Doing = foe!.Boss ? "fighting the warlord" : "fighting a guard";
        double d = foe.X - h.X;
        // A strike coming: guard that height.
        if (foe.Act is Act.Punch or Act.Kick && foe.T < Sim.HitAt(foe.Act) && d <= (foe.Act == Act.Kick ? Sim.KickReach : Sim.PunchReach) + 10)
            return new Controls(Dy: foe.Height - 1);
        // Too far for a kick: closer.
        if (d > Sim.KickReach - 8) return new Controls(1);
        // Strike where he isn't guarding: a punch if near enough (it's quicker), else a kick.
        int height = (foe.Block + 1 + w.Dice.Next(2)) % 3;
        return d <= Sim.PunchReach - 4 ? new Controls(Dy: height - 1, Punch: true) : new Controls(Dy: height - 1, Kick: true);
    }

    private static double Value(World w, Controls a)
    {
        var c = w.Copy();
        var foe = c.Foe;
        int hp = c.Hero.Hp, foeHp = foe?.Hp ?? 0, eagleHp = c.Bird?.Hp ?? 0;
        Sim.Step(c, a);
        for (int i = 1; i < Ahead; i++)
        {
            // Then on guard against whatever's coming.
            var f = c.Foe;
            int block = f != null && f.Act is Act.Punch or Act.Kick ? f.Height : c.Hero.Block;
            Sim.Step(c, new Controls(Dy: block - 1));
            if (!c.Hero.Alive) break;
        }
        double v = (hp - c.Hero.Hp) * -16;
        if (foe != null) v += (foeHp - Math.Max(0, foe.Hp)) * 10;
        if (c.Bird is { } e) v += (eagleHp - e.Hp) * 12;
        else if (eagleHp > 0) v += eagleHp * 12;
        if (!c.Hero.Alive) v -= 1000;
        // Keep at fighting distance: inside a kick, not so close a punch is all there is; backing off costs.
        if (c.Foe is { } g) v -= Math.Abs(g.X - c.Hero.X - 105) * 0.04;
        if (a.Dx < 0) v -= 3;
        return v;
    }
}
