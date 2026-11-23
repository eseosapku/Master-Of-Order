using UnityEngine;

public enum PsycheFunction { Intuition, Sensation, Thinking, Feeling }

public enum CardFamily
{
    Chrono, Foresight, Echo, Fortune, Phantasm,
    Force, Velocity, Ward, Venom, Hunt,
    Binding, Contract, Seal, Mirror, Mimic,
    Leech, Siphon, Bond, Mend, Dread
}

public enum CardKind { Instant, Field, Omen }

public enum OmenTrigger { Countdown, FoeInstant, FoeField, AimedAtMe }

public enum Special
{
    None, Pierce, Shockwave, Charged, Rot, Mark, Snare, Thorns, CardLock, Shackle, Dispel, Copy,
    Reflect, Bounce, Clause, NullZone, Glimpse, ReadHand, Rewind, CoinToss, Cancel, Echo,
    Decoy, Mirage, LoadedDie, Panic, Phoenix
}

[CreateAssetMenu(menuName = "Master of Order/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    public CardFamily family;
    public PsycheFunction function;
    public CardKind kind;
    [Range(0, 10)] public int cost;
    public int damage, ego, heal, libido, sap, resolve, turns, statusTurns;
    public StatusType status;
    public bool guard;
    public OmenTrigger trigger;
    public Special special;
    [TextArea] public string description;
    public Texture2D art;
    public AudioClip voice;

    public bool Offensive => damage > 0 || ego > 0 || sap > 0 || statusTurns > 0 || special == Special.CardLock || special == Special.Dispel || special == Special.CoinToss;

    public string KindLabel => kind == CardKind.Instant ? "INSTANT" : kind == CardKind.Field ? "FIELD" : "OMEN";
}
