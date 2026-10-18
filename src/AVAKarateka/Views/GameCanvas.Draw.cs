using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAKarateka.Engine;

namespace AVAKarateka.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAKarateka/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round);
        return p;
    }

    private const uint White = 0xFFF2F2FF, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Dim = 0xFF8A9AC8, Cyan = 0xFF6AE8FF, Orange = 0xFFFF9A30;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xC0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, g);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);


    private bool InGame => Engine.State is GameState.Attract or GameState.Playing or GameState.Paused;

    private const double Floor = Field.Floor;

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawScene(dc, 1500 + _clock * 40, null); DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawScene(dc, 3000, null); DrawInitials(dc); break;
            case GameState.GameOver: DrawGameOver(dc); break;
            default:
                var w = g.World;
                var foe = w.Foe;
                double cam = foe != null && foe.X - w.Hero.X < Sim.Wake ? (w.Hero.X + foe.X) / 2 - 512 : w.Hero.X - 380;
                DrawScene(dc, cam, w);
                DrawBars(dc, w);
                if (g.Message is { } msg && g.Phase is Phase.Start or Phase.Won)
                {
                    dc.FillRectangle(Brush(0xB0000000), new Rect(0, 250, Field.Width, 70));
                    Text(dc, msg, new Point(512, 285), 50, 0xFFF0E0C0, center: true);
                }
                break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0xD0000000), new Rect(0, 0, Field.Width, Field.Height));
            Text(dc, "PAUSED", new Point(512, 300), 80, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(512, 360), 26, White, center: true);
        }
        if (g.State == GameState.Attract)
            Text(dc, "DEMONSTRATION - PRESS ENTER TO PLAY", new Point(512, 22), 22, Yellow, center: true);
    }

    private static uint Blend(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        uint Ch(int shift) => (uint)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t);
        return 0xFF000000 | Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    /// <summary>The sky and the mountain, the cliff path then the fortress's halls, scrolled to a point; and the fighters.</summary>
    private void DrawScene(DrawingContext dc, double cam, World? w)
    {
        for (int i = 0; i < 20; i++)
            dc.FillRectangle(Brush(Blend(0xFF2A1A4A, 0xFFF09060, i / 19.0)), new Rect(0, i * 32, Field.Width, 33));
        // The mountain, far off: barely moving.
        double mx = 700 - cam * 0.05 % 1600;
        Poly(dc, 0xFF4A3A6A, null, 0, new Point(mx - 420, Floor), new Point(mx - 60, 230), new Point(mx + 60, 230), new Point(mx + 420, Floor));
        Poly(dc, 0xFFF0F0FF, null, 0, new Point(mx - 60, 230), new Point(mx + 60, 230), new Point(mx + 100, 290), new Point(mx + 40, 270), new Point(mx, 300), new Point(mx - 40, 270), new Point(mx - 100, 290));
        dc.FillEllipse(820, 150, 40, 0xFFFFD0A0);
        // Inside the fortress from 3000 on: wooden halls with paper screens; before that, the cliff path.
        for (int k = (int)Math.Floor(cam / 200) - 1; k <= (int)Math.Floor((cam + Field.Width) / 200) + 1; k++)
        {
            double x = k * 200 - cam;
            if (k * 200 >= 3000)
            {
                dc.FillRectangle(Brush(0xFFE8D8B0), new Rect(x, 180, 200, Floor - 180));
                for (int j = 0; j < 4; j++) dc.DrawLine(Pen(0xFF8A6A40, 3), new Point(x, 180 + j * 105), new Point(x + 200, 180 + j * 105));
                dc.DrawLine(Pen(0xFF8A6A40, 3), new Point(x + 100, 180), new Point(x + 100, Floor));
                dc.FillRectangle(Brush(0xFF5A3A1A), new Rect(x - 10, 150, 20, Floor - 150));
                dc.FillRectangle(Brush(0xFF3A2410), new Rect(x - 30, 150, 260, 30));
            }
            else if (Hash(k, 1, 5) % 3 == 0)
            {
                // A pine on the cliff.
                double tx = x + 60 + Hash(k, 2, 5) % 80;
                dc.FillRectangle(Brush(0xFF3A2A1A), new Rect(tx - 5, Floor - 120, 10, 120));
                for (int j = 0; j < 3; j++) Poly(dc, 0xFF1A4A2A, null, 0, new Point(tx - 50 + j * 10, Floor - 70 - j * 30), new Point(tx, Floor - 130 - j * 30), new Point(tx + 50 - j * 10, Floor - 70 - j * 30));
            }
        }
        dc.FillRectangle(Brush(0xFF6A4A2A), new Rect(0, Floor, Field.Width, 20));
        for (double x = -(cam % 80); x < Field.Width; x += 80) dc.DrawLine(Pen(0xFF4A3018, 2), new Point(x, Floor), new Point(x, Floor + 20));
        dc.FillRectangle(Brush(0xFF2A1A10), new Rect(0, Floor + 20, Field.Width, Field.Height - Floor - 20));
        // The princess at the end, by her door.
        double px = Sim.PrincessX - cam;
        if (px > -100 && px < Field.Width + 100)
        {
            dc.FillRectangle(Brush(0xFF3A2410), new Rect(px + 30, Floor - 200, 120, 200));
            dc.FillRectangle(Brush(0xFF100804), new Rect(px + 45, Floor - 185, 90, 185));
            DrawPrincess(dc, px, w is { Won: true });
        }
        if (w == null) return;
        foreach (var f in w.Foes)
            if (f.X - cam > -100 && f.X - cam < Field.Width + 100) DrawFighter(dc, f, f.X - cam, guard: true);
        DrawFighter(dc, w.Hero, w.Hero.X - cam, guard: false);
        foreach (var e in w.Eagles)
            if (e.Awake && !e.Gone) DrawEagle(dc, e.X - cam, e.Y);
    }

    private void DrawPrincess(DrawingContext dc, double x, bool happy)
    {
        double y = Floor;
        Poly(dc, 0xFFE070A0, 0xFF802050, 2, new Point(x - 22, y), new Point(x - 12, y - 70), new Point(x + 12, y - 70), new Point(x + 22, y));
        dc.FillRectangle(Brush(0xFFE8C040), new Rect(x - 13, y - 52, 26, 6));
        dc.FillEllipse(x, y - 82, 11, 0xFFF0D0B0);
        dc.DrawEllipse(Brush(0xFF101010), null, new Point(x, y - 90), 13, 9);
        dc.FillEllipse(x, y - 100, 6, 0xFF101010);
        if (happy) dc.DrawLine(Pen(0xFFE070A0, 4), new Point(x - 10, y - 60), new Point(x - 30, y - 80));
    }

    /// <summary>A fighter in his pose: the hero in white, the guards in red, the warlord in black.</summary>
    private void DrawFighter(DrawingContext dc, Fighter f, double x, bool guard)
    {
        double y = Floor;
        int d = f.Facing;
        uint gi = guard ? (f.Boss ? 0xFF202028 : 0xFFC02828) : 0xFFF0F0F0, belt = guard ? 0xFF101010 : 0xFF101010, skin = 0xFFE8B888;
        var limb = Pen(gi, 10);
        var arm = Pen(gi, 8);
        double k = f.Dur > 0 ? Math.Clamp(f.T / f.Dur, 0, 1) : 0;
        if (f.Act == Act.Down)
        {
            dc.FillRectangle(Brush(gi), new Rect(x - 50, y - 18, 90, 18), 6);
            dc.FillEllipse(x + 50 * -d, y - 10, 10, skin);
            return;
        }
        double hip = y - 70, shoulder = hip - 52, head = shoulder - 18;
        bool run = !f.Fighting && f.Act is Act.Run or Act.Idle && !guard;
        double lean = f.Act == Act.Hurt ? -12 * d : f.Act == Act.Bow ? 20 * d * Math.Sin(Math.PI * k) : 0;
        // Legs.
        if (f.Act == Act.Kick)
        {
            double ext = Math.Sin(Math.PI * Math.Min(1, k * 1.3));
            double ky = f.Height switch { 0 => shoulder - 4, 1 => hip - 14, _ => y - 22 };
            dc.DrawLine(limb, new Point(x, hip), new Point(x - 16 * d, y));
            dc.DrawLine(limb, new Point(x, hip), new Point(x + (20 + 80 * ext) * d, hip + (ky - hip) * ext));
        }
        else if (run)
        {
            double s = Math.Sin(f.T * 12) * 26;
            dc.DrawLine(limb, new Point(x, hip), new Point(x + s * d, y));
            dc.DrawLine(limb, new Point(x, hip), new Point(x - s * d, y));
        }
        else
        {
            double s = f.Act is Act.Step or Act.Back ? Math.Sin(Math.PI * k) * 14 : 0;
            dc.DrawLine(limb, new Point(x, hip), new Point(x + (26 + s) * d, y));
            dc.DrawLine(limb, new Point(x, hip), new Point(x - (22 - s) * d, y));
        }
        // Body and belt.
        dc.DrawLine(Pen(gi, 22), new Point(x, hip), new Point(x + lean, shoulder));
        dc.DrawLine(Pen(belt, 6), new Point(x - 11, hip - 4), new Point(x + 11, hip - 4));
        double hx = x + lean + 3 * d;
        dc.FillEllipse(hx, head, 12, skin);
        if (guard && f.Boss)
        {
            Poly(dc, 0xFF101010, null, 0, new Point(hx - 14, head - 8), new Point(hx, head - 44), new Point(hx + 14, head - 8));
            dc.FillEllipse(hx, head - 20, 4, 0xFFE0C040);
        }
        else if (guard) dc.DrawLine(Pen(0xFFF0F0F0, 4), new Point(hx - 12, head - 6), new Point(hx + 12, head - 6));
        else dc.DrawEllipse(Brush(0xFF101010), null, new Point(hx - 2 * d, head - 7), 12, 7);
        dc.FillEllipse(hx + 6 * d, head - 1, 1.8, 0xFF000000);
        // Arms.
        var sh = new Point(x + lean + 4 * d, shoulder + 6);
        if (f.Act == Act.Punch)
        {
            double ext = Math.Sin(Math.PI * Math.Min(1, k * 1.4));
            double py = f.Height switch { 0 => head + 2, 1 => shoulder + 14, _ => hip + 4 };
            dc.DrawLine(arm, sh, new Point(sh.X + (16 + 52 * ext) * d, sh.Y + (py - sh.Y) * ext));
            dc.FillEllipse(sh.X + (16 + 52 * ext) * d, sh.Y + (py - sh.Y) * ext, 6, skin);
            dc.DrawLine(arm, sh, new Point(sh.X - 6 * d, sh.Y + 22));
        }
        else if (run)
        {
            double s = Math.Sin(f.T * 12) * 20;
            dc.DrawLine(arm, sh, new Point(sh.X + s * d, sh.Y + 34));
        }
        else if (f.Fighting || guard)
        {
            // On guard: the fists held at the height guarded.
            double gy = f.Block switch { 0 => head + 4, 1 => shoulder + 16, _ => hip - 6 };
            dc.DrawLine(arm, sh, new Point(sh.X + 26 * d, gy));
            dc.FillEllipse(sh.X + 26 * d, gy, 6, skin);
            dc.DrawLine(arm, sh, new Point(sh.X + 12 * d, gy + 14));
        }
        else dc.DrawLine(arm, sh, new Point(sh.X, sh.Y + 40));
        if (f.Act == Act.Hurt) dc.FillEllipse(x, shoulder, 34, 0x40FF2020);
    }

    private void DrawEagle(DrawingContext dc, double x, double y)
    {
        double flap = Math.Sin(_clock * 14) * 22;
        Poly(dc, 0xFF6A4020, 0xFF3A2010, 2, new Point(x, y), new Point(x + 50, y - 10 - flap), new Point(x + 30, y + 6));
        Poly(dc, 0xFF6A4020, 0xFF3A2010, 2, new Point(x, y), new Point(x - 50, y - 10 - flap), new Point(x - 30, y + 6));
        dc.DrawEllipse(Brush(0xFF5A3418), null, new Point(x, y + 4), 20, 10);
        dc.FillEllipse(x - 18, y - 2, 8, 0xFFF0F0F0);
        Poly(dc, 0xFFE0C040, null, 0, new Point(x - 24, y - 2), new Point(x - 34, y + 2), new Point(x - 24, y + 4));
        dc.FillEllipse(x - 20, y - 4, 1.6, 0xFF000000);
    }

    private void DrawBars(DrawingContext dc, World w)
    {
        double top = 670;
        dc.FillRectangle(Brush(0xFF000000), new Rect(0, top, Field.Width, Field.Height - top));
        var h = w.Hero;
        for (int i = 0; i < h.MaxHp; i++)
        {
            double x = 24 + i * 26;
            Poly(dc, i < h.Hp ? 0xFFF0F0F0 : 0, 0xFFF0F0F0, 2, new Point(x - 9, top + 34), new Point(x + 9, top + 34), new Point(x, top + 16));
        }
        var foe = w.Foe;
        if (foe != null && foe.Awake)
            for (int i = 0; i < foe.MaxHp; i++)
            {
                double x = Field.Width - 24 - i * 26;
                Poly(dc, i < foe.Hp ? 0xFFE03030 : 0, 0xFFE03030, 2, new Point(x - 9, top + 34), new Point(x + 9, top + 34), new Point(x, top + 16));
            }
        var g = Engine;
        Text(dc, $"SCORE {g.Score}", new Point(512, top + 26), 28, 0xFFF0E0C0, center: true);
        Text(dc, w.Hero.Fighting ? "FIGHTING" : "RUNNING", new Point(512, top + 66), 24, w.Hero.Fighting ? Red : Cyan, center: true);
        for (int i = 0; i < Math.Min(g.Lives - 1, 5); i++) dc.FillEllipse(30 + i * 24, top + 70, 8, 0xFFF0F0F0);
    }

    // ───────────────────────────── title, game over and scores ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        DrawScene(dc, 120, null);
        var hero = new Fighter { Fighting = true, Act = Act.Kick, T = (_clock % 1.2) / 2, Dur = 0.58, Height = 1, Facing = 1 };
        DrawFighter(dc, hero, 380, guard: false);
        DrawFighter(dc, new Fighter { Fighting = true, Facing = -1, Block = (int)(_clock) % 3 }, 520, guard: true);
        dc.FillRectangle(Brush(0xC0000000), new Rect(cx - 410, 60, 820, 290), 12);
        string title = "AVA KARATEKA";
        for (int i = 0; i < title.Length; i++)
        {
            double tx = cx - title.Length * 30 + 30 + i * 60, ty = 116 + Math.Sin(_clock * 2.4 + i * 0.45) * 5;
            Text(dc, title[i].ToString(), new Point(tx, ty), 108, i < 3 ? 0xFFF0E0C0 : 0xFFE03030, center: true);
        }
        Text(dc, "AFTER JORDAN MECHNER'S 1984 GAME", new Point(cx, 180), 26, White, center: true);
        Text(dc, "THE WARLORD HOLDS THE PRINCESS IN HIS FORTRESS. FIGHT YOUR WAY IN:", new Point(cx, 222), 23, White, center: true);
        Text(dc, "PUNCH AND KICK HIGH, MIDDLE OR LOW WHERE THE GUARD ISN'T GUARDING, AND", new Point(cx, 250), 23, White, center: true);
        Text(dc, "GUARD THE HEIGHT OF HIS STRIKES. KNOCK DOWN THE EAGLE. AND WHEN YOU REACH", new Point(cx, 278), 23, White, center: true);
        Text(dc, "HER, DON'T COME AT HER READY TO FIGHT.", new Point(cx, 306), 23, White, center: true);
        if ((int)(_clock * 2) % 2 == 0) Text(dc, "PRESS ENTER TO START", new Point(cx, 400), 44, Yellow, center: true);
        dc.FillRectangle(Brush(0xD0000000), new Rect(0, 700, Field.Width, 68));
        Text(dc, "ARROWS: STEP/RUN   UP/DOWN: HIGH/LOW   Z: PUNCH   X: KICK   SHIFT: FIGHT OR RUN   P: PAUSE", new Point(cx, 718), 19, 0xFFE0D0B0, center: true);
        Text(dc, "MUSIC: SAKURA SAKURA (TRADITIONAL) AND A TUNE OF ITS OWN", new Point(cx, 746), 18, Dim, center: true);
    }

    private void DrawGameOver(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        DrawScene(dc, g.World.Won ? Sim.PrincessX - 600 : g.World.Hero.X - 380, null);
        dc.FillRectangle(Brush(0xC0000000), new Rect(cx - 380, 120, 760, 250), 12);
        bool won = g.World.Won;
        Text(dc, won ? "THE PRINCESS IS FREE" : "THE END", new Point(cx, 190), 74, (int)(_clock * 4) % 2 == 0 ? Yellow : White, center: true);
        Text(dc, $"SCORE {g.Score}", new Point(cx, 270), 44, White, center: true);
        Text(dc, won ? "EVERY GUARD, BOTH EAGLES, AND THE WARLORD" : g.World.Cause == "the princess" ? "SHE TOOK YOU FOR AN ENEMY" : $"{g.Beaten} OF {g.World.Foes.Count} FOES DOWN", new Point(cx, 322), 28, Cyan, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xD0000000), new Rect(cx - 330, 70, 660, 640), 12);
        Text(dc, "THE GREATEST KARATEKA", new Point(cx, 120), 54, Yellow, center: true);
        Text(dc, "RANK NAME    SCORE   FOES", new Point(cx, 190), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,8}   {e.Stage,4}", new Point(cx, 236 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 680), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xD0000000), new Rect(cx - 330, 140, 660, 460), 12);
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 64, Yellow, center: true);
        Text(dc, g.Score.ToString(), new Point(cx, 270), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}

internal static class DrawingExtensions
{
    public static void FillEllipse(this DrawingContext dc, double x, double y, double r, uint colour) =>
        dc.DrawEllipse(GameCanvas.Brush(colour), null, new Point(x, y), r, r);
    public static void FillRectangle(this DrawingContext dc, IBrush brush, Rect rect, double radius) => dc.FillRectangle(brush, rect, (float)radius);
}
