using UnityEngine;

public enum PsycheFunction
{
    Intuition,
    Sensation,
    Thinking,
    Feeling
}

public enum CardFamily
{
    Chrono, Foresight, Echo, Fortune, Phantasm,
    Force, Velocity, Ward, Venom, Hunt,
    Binding, Contract, Seal, Mirror, Mimic,
    Leech, Siphon, Bond, Mend, Dread
}

public enum EffectType
{
    Damage, Heal, Drain, Siphon, Freeze, Slow, Haste, Shield, Poison, Status,
    Reveal, Counter, Copy, Seal, Dispel, Illusion, Link, Rewind, Delay, Utility
}

[CreateAssetMenu(menuName = "Master of Order/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    public CardFamily family;
    public PsycheFunction function;
    [Range(0, 10)] public int cost;
    public EffectType effect;
    public float power;
    public float duration;
    [TextArea] public string description;
    public Texture2D art;
    public AudioClip voice;
}
