using UnityEngine;

public enum PsycheFunction { Intuition, Sensation, Thinking, Feeling }

public enum CardFamily
{
    Chrono, Foresight, Echo, Fortune, Phantasm,
    Force, Velocity, Ward, Venom, Hunt,
    Binding, Contract, Seal, Mirror, Mimic,
    Leech, Siphon, Bond, Mend, Dread
}

public enum CardKind { Instant, Field, Hidden }

public enum Special { None, Pierce, Shatter, Gamble, Mirror, Cleanse, Negate, Seal, Erosion, Stillframe, Glimpse, Double, Thief, Echo, LoadedDie }

[CreateAssetMenu(menuName = "Master of Order/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    public CardFamily family;
    public PsycheFunction function;
    public CardKind kind;
    [Range(0, 10)] public int cost;
    public int damage, heal, resolve, statusTurns;
    public bool guard;
    public StatusType status;
    public Special special;
    [TextArea] public string description;
    public Texture2D art;
    public AudioClip voice;

    public bool Coin => special == Special.Gamble || special == Special.Stillframe || special == Special.Double || special == Special.Thief;
    public bool Offensive => damage > 0 || statusTurns > 0 || special == Special.Seal || special == Special.Stillframe || special == Special.Thief || special == Special.Shatter;
    public string KindLabel => kind == CardKind.Instant ? "INSTANT" : kind == CardKind.Field ? "FIELD" : "HIDDEN";
    public string Group => function == PsycheFunction.Sensation ? "DESTRUCTION" : function == PsycheFunction.Feeling ? "DEFENSE" : function == PsycheFunction.Thinking ? "CONTROL" : "FATE";
}
