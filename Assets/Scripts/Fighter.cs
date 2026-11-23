using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusType { Poison, Slow, Frozen, Marked, Shielded, Rot }

[Serializable]
public class StatusEffect
{
    public StatusType type;
    public float power;
    public float timeLeft;
}

public class Fighter : MonoBehaviour
{
    [SerializeField] private string displayName = "Climber";
    [SerializeField] private int team;
    [SerializeField] private float maxBody = 100f;
    [SerializeField] private float maxEgo = 100f;
    [SerializeField] private int maxLibido = 10;
    [SerializeField] private float startLibido = 10f;
    [SerializeField] private float libidoPerSecond = 0.5f;

    public event Action<Fighter> Changed;
    public event Action<Fighter, float> Damaged;
    public event Action<Fighter> Defeated;
    public event Action<Fighter> EgoBroken;

    public string DisplayName => displayName;
    public int Team { get => team; set => team = value; }
    public float Body { get; private set; }
    public float Ego { get; private set; }
    public float Shadow { get; private set; }
    public int Libido => Mathf.FloorToInt(libido);
    public float BodyRatio => Body / maxBody;
    public float EgoRatio => Ego / maxEgo;
    public float ShadowRatio => Shadow / 100f;
    public bool IsDown => Body <= 0f;
    public List<StatusEffect> Statuses { get; } = new List<StatusEffect>();

    private float libido;
    public static bool Paused;
    private void Awake() => Restore();

    public void Restore()
    {
        Body = maxBody;
        Ego = maxEgo;
        Shadow = 0f;
        libido = startLibido;
        Statuses.Clear();
        Changed?.Invoke(this);
    }

    public bool Has(StatusType t) => Statuses.Exists(s => s.type == t);

    public bool Spend(int cost)
    {
        if (Libido < cost) return false;
        libido -= cost;
        Changed?.Invoke(this);
        return true;
    }

    public void Damage(float amount)
    {
        if (IsDown || amount <= 0f) return;
        if (Has(StatusType.Shielded))
        {
            Statuses.RemoveAll(s => s.type == StatusType.Shielded);
            Changed?.Invoke(this);
            return;
        }
        if (Has(StatusType.Marked)) amount *= 1.2f;
        Body = Mathf.Max(0f, Body - amount);
        Damaged?.Invoke(this, amount);
        Changed?.Invoke(this);
        if (Body <= 0f) Defeated?.Invoke(this);
    }

    public void Heal(float amount)
    {
        if (IsDown) return;
        if (Has(StatusType.Rot)) { Damage(amount); return; }
        Body = Mathf.Min(maxBody, Body + amount);
        Changed?.Invoke(this);
    }

    public void Mind(float egoLoss, float shadowGain)
    {
        bool wasUp = Ego > 0f;
        Ego = Mathf.Clamp(Ego - egoLoss, 0f, maxEgo);
        Shadow = Mathf.Clamp(Shadow + shadowGain, 0f, 100f);
        Changed?.Invoke(this);
        if (wasUp && Ego <= 0f) EgoBroken?.Invoke(this);
    }

    public void GainLibido(float amount)
    {
        libido = Mathf.Clamp(libido + amount, 0f, maxLibido);
        Changed?.Invoke(this);
    }

    public void AddStatus(StatusType t, float power, float seconds)
    {
        Statuses.RemoveAll(s => s.type == t);
        Statuses.Add(new StatusEffect { type = t, power = power, timeLeft = seconds });
        Changed?.Invoke(this);
    }

    public void ReceiveCard(CardData d, Fighter source, float speed)
    {
        float p = d.power;
        switch (d.effect)
        {
            case EffectType.Damage: Damage(p * (speed > 7f ? 1.25f : 1f)); break;
            case EffectType.Poison: AddStatus(StatusType.Poison, p, 6f); break;
            case EffectType.Drain: Damage(p); if (source != null) source.Heal(p * 0.5f); break;
            case EffectType.Siphon: GainLibido(-p); if (source != null) source.GainLibido(p); break;
            case EffectType.Freeze: AddStatus(StatusType.Frozen, 0f, d.duration); Damage(4f); break;
            case EffectType.Slow: AddStatus(StatusType.Slow, 0.5f, d.duration + 2f); Damage(4f); break;
            case EffectType.Heal: Heal(p); break;
            case EffectType.Shield: if (source != null) source.AddStatus(StatusType.Shielded, 0f, 8f); break;
            case EffectType.Dispel: Statuses.Clear(); Mind(4f, 0f); break;
            case EffectType.Status:
                if (d.family == CardFamily.Hunt) AddStatus(StatusType.Marked, 0f, 8f);
                else if (d.family == CardFamily.Venom) AddStatus(StatusType.Rot, 0f, 8f);
                else Mind(p * (d.family == CardFamily.Dread ? 3f : 2f), p * 2f);
                break;
            default: Mind(6f, 3f); break;
        }
    }

    private void Update()
    {
        if (IsDown || Paused) return;
        int before = Libido;
        if (!Has(StatusType.Frozen)) libido = Mathf.Min(maxLibido, libido + libidoPerSecond * Time.deltaTime);
        int count = Statuses.Count;
        for (int i = Statuses.Count - 1; i >= 0; i--)
        {
            StatusEffect s = Statuses[i];
            s.timeLeft -= Time.deltaTime;
            if (s.type == StatusType.Poison) Body = Mathf.Max(0.01f, Body - s.power * Time.deltaTime);
            if (s.timeLeft <= 0f) Statuses.RemoveAt(i);
        }
        if (Libido != before || Statuses.Count != count || Has(StatusType.Poison)) Changed?.Invoke(this);
    }
}
