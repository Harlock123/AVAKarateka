namespace AVAKarateka.Engine;

/// <summary>The screen: the fortress scrolling past, the floor the fighters stand on, and the strength bars below.</summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
    public const double Floor = 600;
}

/// <summary>A small random number generator whose state can be copied.</summary>
public struct Dice
{
    private ulong _s;
    public Dice(int seed) => _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public double Next()
    {
        _s ^= _s << 13;
        _s ^= _s >> 7;
        _s ^= _s << 17;
        return (_s >> 11) * (1.0 / (1UL << 53));
    }

    public int Next(int n) => (int)(Next() * n);
}

/// <summary>
/// The controls: left or right; up or down for the height of a strike or a guard (high, middle, low); punch; kick;
/// and the stance (fighting or running).
/// </summary>
public readonly record struct Controls(int Dx = 0, int Dy = 0, bool Punch = false, bool Kick = false, bool Stance = false);

public enum Act { Idle, Step, Back, Run, Punch, Kick, Hurt, Switch, Down, Bow }

/// <summary>A fighter: the hero, a guard, or the warlord himself.</summary>
public sealed class Fighter
{
    public double X;
    public int Facing = 1;
    public bool Fighting;
    public Act Act = Act.Idle;
    public double T, Dur;
    /// <summary>The height of the strike under way, or of the guard held (0 high, 1 middle, 2 low).</summary>
    public int Height = 1, Block = 1;
    public int Hp, MaxHp;
    public double Skill;
    public bool Struck, Boss, Awake;
    /// <summary>For the tests: a guard who stands on guard where he's put, and does nothing of his own.</summary>
    public bool Still;
    public Fighter Copy() => (Fighter)MemberwiseClone();
    public bool Alive => Act != Act.Down;
    public bool Ready => Act is Act.Idle or Act.Run || T >= Dur;
}

/// <summary>The eagle: swooping down at the hero's head, again and again, until struck down.</summary>
public sealed class Eagle
{
    public double X, Y, T, StartX;
    public int Hp = 2;
    public bool Awake, Gone, Struck;
    public Eagle Copy() => (Eagle)MemberwiseClone();
}

/// <summary>
/// Everything in the fortress that moves or changes, kept together so it can be copied whole: the hero, the guards
/// and the warlord, the eagles, the princess, the score.
/// </summary>
public sealed class World
{
    public Fighter Hero = new();
    public List<Fighter> Foes = [];
    public List<Eagle> Eagles = [];
    public bool Won;
    public int Score;
    public double Clock, Rest;
    public Dice Dice = new(1);
    public string Cause = "";
    public List<Sfx> Heard = [];

    public World Copy() => new()
    {
        Hero = Hero.Copy(), Foes = Foes.Select(f => f.Copy()).ToList(), Eagles = Eagles.Select(e => e.Copy()).ToList(), Won = Won, Score = Score,
        Clock = Clock, Rest = Rest, Dice = Dice, Cause = Cause,
    };

    /// <summary>The foe the hero is facing up to: the nearest one ahead, awake and standing.</summary>
    public Fighter? Foe => Foes.Where(f => f.Alive && f.X > Hero.X - 40).OrderBy(f => f.X).FirstOrDefault();
    public Eagle? Bird => Eagles.FirstOrDefault(e => e.Awake && !e.Gone);
}
