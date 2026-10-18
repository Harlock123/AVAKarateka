using Avalonia.Input;
using AVAKarateka.Engine;

namespace AVAKarateka.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avakarateka-tests-" + Guid.NewGuid().ToString("N"));

    private static GameEngine Engine() => new(HighScoreTable.Load(TempFolder())) { Silent = true };

    private static World Fresh()
    {
        var w = new World();
        Sim.Start(w);
        return w;
    }

    private static void Run(World w, int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Sim.Step(w, c);
    }

    /// <summary>The hero in his fighting stance a set distance before the first guard, the guard awake and standing on guard.</summary>
    private static (World W, Fighter H, Fighter G) Facing(double d)
    {
        var w = Fresh();
        var g = w.Foes[0];
        var h = w.Hero;
        h.X = g.X - d;
        h.Fighting = true;
        g.Awake = true;
        g.Still = true; // the tests set his guard
        return (w, h, g);
    }

    // ───────────────────────────── the fight ─────────────────────────────

    [Fact]
    public void A_strike_at_the_height_he_isnt_guarding_lands_and_a_kick_hurts_twice_as_much()
    {
        var (w, h, g) = Facing(80);
        int hp = g.Hp;
        g.Block = 0;
        Sim.Step(w, new Controls(Dy: 1, Punch: true));
        g.Block = 0;
        Run(w, 30);
        Assert.Equal(hp - 1, g.Hp);
        Assert.Equal(Sim.HitPoints, w.Score);
        (w, h, g) = Facing(110);
        hp = g.Hp;
        g.Block = 2;
        Sim.Step(w, new Controls(Dy: -1, Kick: true));
        Run(w, 40);
        Assert.Equal(hp - 2, g.Hp);
    }

    [Fact]
    public void A_strike_at_the_height_he_is_guarding_is_blocked()
    {
        var (w, h, g) = Facing(80);
        int hp = g.Hp;
        g.Block = 0;
        Sim.Step(w, new Controls(Dy: -1, Punch: true));
        Run(w, 30);
        Assert.Equal(hp, g.Hp);
    }

    [Fact]
    public void A_punch_doesnt_reach_as_far_as_a_kick()
    {
        var (w, h, g) = Facing(Sim.PunchReach + 20);
        int hp = g.Hp;
        g.Block = 0;
        Sim.Step(w, new Controls(Punch: true));
        Run(w, 30);
        Assert.Equal(hp, g.Hp);
        Sim.Step(w, new Controls(Kick: true));
        Run(w, 40);
        Assert.True(g.Hp < hp);
    }

    [Fact]
    public void Guarding_the_height_of_his_strike_keeps_the_hero_whole()
    {
        foreach (int height in new[] { 0, 1, 2 })
        foreach (bool guarded in new[] { true, false })
        {
            var (w, h, g) = Facing(80);
            g.Height = height;
            g.Act = Act.Punch;
            g.T = 0;
            g.Dur = Sim.Duration(Act.Punch);
            g.Struck = false;
            int dy = guarded ? height - 1 : (height + 1) % 3 - 1;
            Run(w, 20, new Controls(Dy: dy));
            Assert.Equal(guarded ? Sim.Hp0 : Sim.Hp0 - 1, h.Hp);
        }
    }

    [Fact]
    public void Caught_running_one_blow_is_the_end()
    {
        var (w, h, g) = Facing(80);
        h.Fighting = false;
        g.Height = 1;
        g.Act = Act.Kick;
        g.T = 0;
        g.Dur = Sim.Duration(Act.Kick);
        Run(w, 30);
        Assert.False(h.Alive);
        Assert.Equal("a guard", w.Cause);
    }

    [Fact]
    public void A_guard_bows_when_the_hero_comes_near_then_comes_to_fight()
    {
        var w = Fresh();
        var g = w.Foes[0];
        w.Hero.X = g.X - Sim.Wake - 100;
        Run(w, 5);
        Assert.False(g.Awake);
        Run(w, 40, new Controls(1));
        Assert.True(g.Awake);
        Assert.Equal(Act.Bow, g.Act);
        double x = g.X;
        Sim.Step(w, new Controls(Stance: true));
        Run(w, 240);
        Assert.True(g.X < x, "the guard should step in");
    }

    [Fact]
    public void Beaten_a_guard_falls_and_scores_and_the_boss_scores_triple()
    {
        var (w, h, g) = Facing(80);
        g.Hp = 1;
        g.Block = 0;
        Sim.Step(w, new Controls(Dy: 1, Punch: true));
        Run(w, 30);
        Assert.False(g.Alive);
        Assert.Equal(Sim.HitPoints + Sim.GuardPoints, w.Score);
        var boss = w.Foes.Last();
        Assert.True(boss.Boss);
        foreach (var f in w.Foes) f.Act = Act.Down;
        boss.Act = Act.Idle;
        boss.Awake = true;
        boss.Still = true;
        boss.Hp = 1;
        boss.Block = 0;
        h.X = boss.X - 80;
        w.Score = 0;
        Sim.Step(w, new Controls(Dy: 1, Punch: true));
        Run(w, 30);
        Assert.Equal(Sim.HitPoints + 3 * Sim.GuardPoints, w.Score);
    }

    [Fact]
    public void Out_of_a_fight_strength_comes_back()
    {
        var w = Fresh();
        w.Hero.Hp = 3;
        Run(w, 60 * 5);
        Assert.True(w.Hero.Hp >= 5);
        Run(w, 60 * 20);
        Assert.Equal(Sim.Hp0, w.Hero.Hp);
    }

    // ───────────────────────────── the eagle ─────────────────────────────

    [Fact]
    public void The_eagle_wakes_swoops_and_pecks_unless_struck_down_high()
    {
        var w = Fresh();
        foreach (var f in w.Foes) f.Act = Act.Down;
        var h = w.Hero;
        h.X = Sim.EagleAt[0] - 250;
        h.Fighting = true;
        Run(w, 60 * 3);
        Assert.True(w.Eagles[0].Awake);
        Assert.Equal("the eagle", w.Cause);
        // Now strike high as it comes in, again and again.
        w = Fresh();
        foreach (var f in w.Foes) f.Act = Act.Down;
        w.Hero.X = Sim.EagleAt[0] - 250;
        w.Hero.Fighting = true;
        var p = new Pilot();
        for (int i = 0; i < 60 * 20 && !w.Eagles[0].Gone; i++) Sim.Step(w, p.Decide(w));
        Assert.True(w.Eagles[0].Gone);
        Assert.True(w.Score >= Sim.EaglePoints);
    }

    // ───────────────────────────── the princess ─────────────────────────────

    private static World AtThePrincess(bool fighting)
    {
        var w = Fresh();
        foreach (var f in w.Foes) f.Act = Act.Down;
        foreach (var e in w.Eagles) e.Gone = true;
        w.Hero.X = Sim.PrincessX - 300;
        w.Hero.Fighting = fighting;
        return w;
    }

    [Fact]
    public void Run_to_the_princess_and_she_is_free()
    {
        var w = AtThePrincess(false);
        Run(w, 120, new Controls(1));
        Assert.True(w.Won);
        Assert.True(w.Hero.Alive);
    }

    [Fact]
    public void Come_at_her_ready_to_fight_and_she_kicks_you_down()
    {
        var w = AtThePrincess(true);
        Run(w, 60 * 8, new Controls(1));
        Assert.False(w.Won);
        Assert.False(w.Hero.Alive);
        Assert.Equal("the princess", w.Cause);
    }

    [Fact]
    public void After_a_fall_the_hero_starts_again_before_the_guard_who_beat_him_both_at_full_strength()
    {
        var (w, h, g) = Facing(80);
        g.Hp = 1;
        h.Act = Act.Down;
        h.Hp = 0;
        Sim.Respawn(w);
        Assert.True(h.Alive);
        Assert.Equal(Sim.Hp0, h.Hp);
        Assert.False(h.Fighting);
        Assert.Equal(g.MaxHp, g.Hp);
        Assert.False(g.Awake);
        Assert.True(g.X - h.X > Sim.Wake);
    }

    // ───────────────────────────── the computer hero ─────────────────────────────

    [Fact]
    public void The_computer_hero_fights_through_the_fortress_and_rescues_the_princess()
    {
        var w = Fresh();
        var p = new Pilot();
        int falls = 0;
        for (int t = 0; t < 60 * 60 * 4 && !w.Won && falls < 3; t++)
        {
            Sim.Step(w, p.Decide(w));
            if (!w.Hero.Alive) { falls++; Sim.Respawn(w); p.Reset(); }
        }
        Assert.True(w.Won);
        Assert.Equal(0, falls);
        Assert.All(w.Foes, f => Assert.False(f.Alive));
    }

    [Fact]
    public void Kicking_away_without_guarding_doesnt_get_far()
    {
        var w = Fresh();
        int falls = 0;
        bool prev = false;
        for (int t = 0; t < 60 * 60 * 3 && !w.Won && falls < 10; t++)
        {
            bool want = w.Foe is { } f && f.X - w.Hero.X < 300;
            var c = w.Hero.Fighting != want && !prev ? new Controls(Stance: true) : new Controls(1, 0, false, t % 30 == 0);
            prev = c.Stance;
            Sim.Step(w, c);
            if (!w.Hero.Alive) { falls++; Sim.Respawn(w); }
        }
        Assert.False(w.Won);
        Assert.True(falls >= 3);
    }

    // ───────────────────────────── the game ─────────────────────────────

    [Fact]
    public void The_stance_key_changes_on_a_press_not_a_hold()
    {
        var g = Engine();
        g.Go();
        g.Run(30, new Controls(Stance: true));
        Assert.True(g.World.Hero.Fighting);
        g.Run(2);
        g.Run(30, new Controls(Stance: true));
        Assert.False(g.World.Hero.Fighting);
    }

    [Fact]
    public void Beating_a_guard_draws_a_kiai()
    {
        var g = Engine();
        g.Silent = false;
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Go();
        var foe = g.World.Foes[0];
        g.World.Hero.X = foe.X - 80;
        g.World.Hero.Fighting = true;
        foe.Awake = true;
        foe.Still = true;
        foe.Hp = 1;
        foe.Block = 0;
        g.Tick(new Controls(Dy: 1, Punch: true));
        g.Run(30);
        Assert.Contains(Sfx.SayHai, heard);
        Assert.Contains(Sfx.Fall, heard);
    }

    [Fact]
    public void Three_falls_and_the_game_is_over_then_a_high_score_is_entered()
    {
        var g = Engine();
        g.Go();
        for (int i = 0; i < 60 * 120 && g.State == GameState.Playing; i++) g.Tick(new Controls(1));
        Assert.Equal(GameState.GameOver, g.State);
        Assert.Equal(0, g.Lives);
        g.World.Score = 5000;
        g.Run((int)(60 * GameEngine.OverSeconds) + 5);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('K');
        g.Initials.Type('A');
        g.Initials.Type('R');
        g.Tick(default);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal("KAR", g.HighScores.Entries[g.NewEntryIndex].Initials);
    }

    [Fact]
    public void The_title_runs_a_demonstration_then_the_high_scores_then_the_title()
    {
        var g = Engine();
        g.Idle((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle((int)(60 * GameEngine.AttractSeconds) + 5);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle((int)(60 * GameEngine.HighScoreSeconds) + 5);
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Enter_starts_a_game_and_a_punch_on_the_title_does_too()
    {
        var g = Engine();
        g.Tick(default, start: true);
        Assert.Equal(GameState.Playing, g.State);
        g = Engine();
        g.Idle(40);
        g.Tick(new Controls(Punch: true));
        Assert.Equal(GameState.Playing, g.State);
    }

    [Fact]
    public void The_keys_up_is_a_high_strike_down_a_low_one()
    {
        using var input = new InputHandler();
        input.OnKeyDown(Key.Up);
        input.OnKeyDown(Key.Z);
        var c = input.Player;
        Assert.Equal(-1, c.Dy);
        Assert.True(c.Punch);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.Down);
        input.OnKeyDown(Key.X);
        input.OnKeyDown(Key.LeftShift);
        c = input.Player;
        Assert.Equal(1, c.Dy);
        Assert.True(c.Kick);
        Assert.True(c.Stance);
        // And Up means high in the fight too.
        var (w, h, g) = Facing(80);
        Sim.Step(w, new Controls(Dy: -1, Punch: true));
        Assert.Equal(0, h.Height);
    }
}
