using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAKarateka.Engine;
using AVAKarateka.Views;
using Controls = AVAKarateka.Engine.Controls;

namespace AVAKarateka.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set KARATEKA_SHOTS to a folder to
/// save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open()
    {
        var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avakarateka-ui-" + Guid.NewGuid().ToString("N")))) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("KARATEKA_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAKarateka-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int maxSeconds = 200)
    {
        for (int t = 0; t < 60 * maxSeconds && g.State == GameState.Playing; t++)
        {
            if (done(g)) { c.Advance(0.016); return true; }
            g.Tick(g.Pilot.Decide(g));
            if (t % 3 == 0) c.Advance(1 / 20.0);
        }
        return false;
    }

    private static void Moment(string name, Func<GameEngine, bool> moment)
    {
        var (w, c, g) = Open();
        g.Go();
        g.SetLives(9);
        Assert.True(PlayUntil(g, c, moment), $"never saw the moment for {name}");
        Shot(w, c, name);
    }

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(2.1);
        Shot(w, c, "title");
        g.HighScores.Insert("AVA", 17700, 6);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        for (int i = 0; i < 60 * 120 && g.State != GameState.HighScores; i++) g.Idle(1);
        Assert.Equal(GameState.HighScores, g.State);
        Shot(w, c, "high-scores");
    }

    [AvaloniaFact]
    public void A_kick_landing() =>
        Moment("kick", g => g.World.Hero.Act == Act.Kick && g.World.Hero.T > 0.2 && g.World.Foe is { Awake: true } f && f.X - g.World.Hero.X < 130);

    [AvaloniaFact]
    public void The_eagle() => Moment("eagle", g => g.World.Bird is { } e && e.Y > Field.Floor - 220 && g.World.Hero.Fighting);

    [AvaloniaFact]
    public void The_warlord() => Moment("warlord", g => g.World.Foe is { Boss: true, Awake: true } f && f.X - g.World.Hero.X < 140 && f.Act is Act.Punch or Act.Kick && f.T > 0.15);

    [AvaloniaFact]
    public void The_princess() => Moment("princess", g => g.World.Won && g.PhaseTime > 0.5);

    [AvaloniaFact]
    public void Game_over()
    {
        var (w, c, g) = Open();
        g.Go();
        g.SetLives(1);
        g.World.Score = 2400;
        // Run at the first guard, not fighting: he cuts you down.
        for (int i = 0; i < 60 * 60 && g.State == GameState.Playing; i++) { g.Tick(new Controls(1)); if (i % 3 == 0) c.Advance(0.05); }
        for (int i = 0; i < 90; i++) { g.Tick(default); if (i % 3 == 0) c.Advance(0.05); }
        Assert.Equal(GameState.GameOver, g.State);
        Shot(w, c, "game-over");
    }
}
