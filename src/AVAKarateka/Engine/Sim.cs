namespace AVAKarateka.Engine;

/// <summary>The rules, one 60th of a second at a time.</summary>
public static class Sim
{
    public const double RunSpeed = 250, StepLength = 46, PunchReach = 92, KickReach = 128, Wake = 520;
    public const double PrincessX = 9300;
    public const int Hp0 = 10, HitPoints = 100, GuardPoints = 1000, EaglePoints = 500, RescuePoints = 5000;
    /// <summary>Where along the fortress each guard waits, his strength, and how good he is.</summary>
    public static readonly (double X, int Hp, double Skill, bool Boss)[] Guards =
        [(1300, 3, 0.15, false), (2500, 4, 0.25, false), (4500, 5, 0.35, false), (5600, 6, 0.45, false), (7400, 7, 0.55, false), (8400, 10, 0.65, true)];
    public static readonly double[] EagleAt = [3500, 6500];

    public static double Duration(Act a) => a switch
    {
        Act.Step or Act.Back => 0.3, Act.Punch => 0.42, Act.Kick => 0.58, Act.Hurt => 0.38, Act.Switch => 0.28, Act.Bow => 1.2, _ => 0,
    };

    /// <summary>When a strike lands, how far into it: the hero is a shade quicker than the guards.</summary>
    public static double HitAt(Act a, bool hero = false) => a == Act.Punch ? (hero ? 0.16 : 0.22) : (hero ? 0.24 : 0.34);

    public static void Start(World w)
    {
        w.Hero = new Fighter { X = 200, Hp = Hp0, MaxHp = Hp0, Act = Act.Idle };
        w.Foes = Guards.Select(g => new Fighter { X = g.X, Facing = -1, Fighting = true, Hp = g.Hp, MaxHp = g.Hp, Skill = g.Skill, Boss = g.Boss }).ToList();
        w.Eagles = EagleAt.Select(x => new Eagle { StartX = x }).ToList();
        w.Won = false;
        w.Score = 0;
        w.Clock = 0;
        w.Dice = new Dice(1984);
    }

    private static void Begin(Fighter f, Act a, bool hero = false)
    {
        f.Act = a;
        f.T = 0;
        // The hero recovers from a strike quicker than the guards do.
        f.Dur = Duration(a) * (hero && a is Act.Punch or Act.Kick ? 0.75 : 1);
        f.Struck = false;
    }

    public static void Step(World w, Controls c)
    {
        double dt = Field.Step;
        w.Clock += dt;
        var h = w.Hero;
        if (!h.Alive || w.Won) return;
        h.T += dt;
        foreach (var f in w.Foes) f.T += dt;
        // Moving acts carry the fighter as they go.
        Carry(h);
        foreach (var f in w.Foes) Carry(f);
        if (h.Ready) Hero(w, h, c);
        var foe = w.Foe;
        foreach (var f in w.Foes)
            if (f.Alive && f.Ready) Guard(w, f);
        Blows(w);
        Eagles(w);
        // Out of a fight, strength comes back.
        if (foe == null || foe.X - h.X > Wake)
        {
            w.Rest += dt;
            if (w.Rest > 1.5 && h.Hp < h.MaxHp) { h.Hp++; w.Rest = 0; }
        }
        else w.Rest = 0;
        // The princess.
        if (h.X > PrincessX - 70 && w.Foes.All(f => !f.Alive))
        {
            if (h.Fighting)
            {
                // Come at her ready to fight, and she kicks you down.
                h.Act = Act.Down;
                h.Hp = 0;
                w.Cause = "the princess";
                w.Heard.Add(Sfx.Kick);
                w.Heard.Add(Sfx.Fall);
            }
            else
            {
                w.Won = true;
                w.Score += RescuePoints;
                w.Heard.Add(Sfx.Rescue);
            }
        }
    }

    private static void Carry(Fighter f)
    {
        if (f.Act is not (Act.Step or Act.Back or Act.Hurt) || f.T > f.Dur) return;
        double v = f.Act switch { Act.Step => StepLength, Act.Back => -StepLength, _ => -22 } / f.Dur * Field.Step;
        f.X += f.Facing * v;
    }

    /// <summary>The hero's next act, from the controls.</summary>
    private static void Hero(World w, Fighter h, Controls c)
    {
        if (h.Act != Act.Run || c.Dx == 0 || !h.Fighting && c.Stance) { h.Act = Act.Idle; h.T = h.Dur = 0; }
        h.Block = c.Dy + 1;
        if (c.Stance)
        {
            h.Fighting = !h.Fighting;
            Begin(h, Act.Switch);
            w.Heard.Add(Sfx.Stance);
            return;
        }
        var foe = w.Foe;
        if (!h.Fighting)
        {
            // Running: right on, or back.
            if (c.Dx != 0)
            {
                h.Facing = c.Dx;
                double x = h.X + c.Dx * RunSpeed * Field.Step;
                if (foe != null && foe.X - x < 50) x = foe.X - 50;
                h.X = Math.Max(60, x);
                h.Act = Act.Run;
                h.T += Field.Step * 3;
            }
            return;
        }
        h.Facing = 1;
        if (c.Punch || c.Kick)
        {
            h.Height = c.Dy + 1;
            Begin(h, c.Kick ? Act.Kick : Act.Punch, hero: true);
            w.Heard.Add(Sfx.Swing);
            return;
        }
        if (c.Dx > 0 && (foe == null || foe.X - h.X > 70)) Begin(h, Act.Step);
        else if (c.Dx < 0) Begin(h, Act.Back);
    }

    /// <summary>A guard's mind: come on to fighting distance, strike at a height, or guard the height of the hero's strike.</summary>
    private static void Guard(World w, Fighter g)
    {
        var h = w.Hero;
        double d = g.X - h.X;
        if (g.Still) { g.Act = Act.Idle; return; }
        g.Act = Act.Idle;
        g.T = 0;
        g.Dur = 0.25;
        if (!g.Awake) { if (d < Wake) { g.Awake = true; w.Heard.Add(Sfx.Bow); Begin(g, Act.Bow); } return; }
        if (!h.Alive) return;
        // A strike coming: guard against it, if quick enough.
        if (h.Act is Act.Punch or Act.Kick && h.T < HitAt(h.Act, true) && w.Dice.Next() < g.Skill + 0.25) { g.Block = h.Height; g.Dur = HitAt(h.Act, true) - h.T + 0.05; return; }
        if (d > KickReach - 6 && d > 70) { if (w.Dice.Next() < 0.7) Begin(g, Act.Step); return; }
        double roll = w.Dice.Next();
        if (roll < 0.14 + g.Skill * 0.22)
        {
            g.Height = w.Dice.Next(3);
            Begin(g, d > PunchReach - 4 ? Act.Kick : w.Dice.Next(2) == 0 ? Act.Kick : Act.Punch);
            w.Heard.Add(Sfx.Swing);
            return;
        }
        if (roll < 0.35 && d < 80) { Begin(g, Act.Back); return; }
        // On guard: mostly at the height the hero last struck at, the way a fighter learns.
        g.Block = w.Dice.Next() < 0.5 + g.Skill / 2 ? h.Height : w.Dice.Next(3);
    }

    /// <summary>Strikes landing: a hit unless the other is guarding that height (or out of reach).</summary>
    private static void Blows(World w)
    {
        var h = w.Hero;
        foreach (var g in w.Foes)
        {
            if (!g.Alive) continue;
            double d = g.X - h.X;
            if (h.Act is Act.Punch or Act.Kick && !h.Struck && h.T >= HitAt(h.Act, true))
            {
                double reach = h.Act == Act.Kick ? KickReach : PunchReach;
                if (d > 0 && d <= reach)
                {
                    h.Struck = true;
                    if (g.Act == Act.Idle && g.Block == h.Height) w.Heard.Add(Sfx.Block);
                    else
                    {
                        int dmg = h.Act == Act.Kick ? 2 : 1;
                        g.Hp -= dmg;
                        w.Score += HitPoints * dmg;
                        w.Heard.Add(Sfx.Hit);
                        if (g.Hp <= 0) { g.Act = Act.Down; w.Score += GuardPoints * (g.Boss ? 3 : 1); w.Heard.Add(Sfx.Fall); }
                        else Begin(g, Act.Hurt);
                    }
                }
            }
            if (g.Act is Act.Punch or Act.Kick && !g.Struck && g.T >= HitAt(g.Act))
            {
                g.Struck = true;
                double reach = g.Act == Act.Kick ? KickReach : PunchReach;
                if (d > 0 && d <= reach && h.Alive)
                {
                    if (h.Fighting && h.Act is Act.Idle or Act.Step or Act.Back && h.Block == g.Height) w.Heard.Add(Sfx.Block);
                    else
                    {
                        // Caught running, it's the end.
                        int dmg = !h.Fighting ? h.Hp : g.Act == Act.Kick ? 2 : 1;
                        h.Hp -= dmg;
                        w.Heard.Add(Sfx.Hurt);
                        w.Cause = g.Boss ? "the warlord" : "a guard";
                        if (h.Hp <= 0) { h.Hp = 0; h.Act = Act.Down; w.Heard.Add(Sfx.Fall); }
                        else Begin(h, Act.Hurt);
                    }
                }
            }
        }
    }

    /// <summary>The eagle: it swoops down at the hero's head, and back up, until a high strike catches it.</summary>
    private static void Eagles(World w)
    {
        var h = w.Hero;
        foreach (var e in w.Eagles)
        {
            if (e.Gone) continue;
            if (!e.Awake)
            {
                if (h.X > e.StartX - 300) { e.Awake = true; e.T = 0; w.Heard.Add(Sfx.Screech); }
                continue;
            }
            e.T += Field.Step;
            double cycle = 2.4, k = e.T % cycle / cycle;
            // In from high on the right, down to head height in front of the hero, and away up again.
            double near = h.X + 70;
            e.X = near + (1 - Math.Sin(Math.PI * k)) * 260;
            e.Y = Field.Floor - 120 - (1 - Math.Sin(Math.PI * k)) * 260;
            if (k < 0.45) e.Struck = false;
            bool low = Math.Abs(k - 0.5) < 0.06;
            if (low && h.Act is Act.Punch or Act.Kick && h.Height == 0 && h.T >= HitAt(h.Act, true) - 0.08 && h.T <= h.Dur && !e.Struck)
            {
                e.Struck = true;
                e.Hp--;
                w.Heard.Add(Sfx.Hit);
                if (e.Hp <= 0) { e.Gone = true; w.Score += EaglePoints; w.Heard.Add(Sfx.Screech); }
                continue;
            }
            if (Math.Abs(k - 0.56) < 0.01 && !e.Struck && h.Alive)
            {
                e.Struck = true;
                h.Hp -= 1;
                w.Cause = "the eagle";
                w.Heard.Add(Sfx.Hurt);
                if (h.Hp <= 0) { h.Act = Act.Down; w.Heard.Add(Sfx.Fall); }
            }
        }
    }

    /// <summary>After a fall: the hero back on his feet a little before where he fell, strength restored; the guard there too.</summary>
    public static void Respawn(World w)
    {
        var h = w.Hero;
        var foe = w.Foes.Where(f => f.Alive).OrderBy(f => f.X).FirstOrDefault();
        h.X = Math.Max(200, (foe?.X ?? h.X) - 700);
        h.Hp = h.MaxHp;
        h.Act = Act.Idle;
        h.T = h.Dur = 0;
        h.Fighting = false;
        if (foe != null) { foe.Hp = foe.MaxHp; foe.Awake = false; foe.Act = Act.Idle; }
        foreach (var e in w.Eagles) if (!e.Gone && e.StartX > h.X) e.Awake = false;
    }
}
