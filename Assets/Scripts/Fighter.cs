using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusType { Poison, Marked, Rot, Shackled, Skip, Panic, Phoenix }

public class Fighter : MonoBehaviour
{
    [SerializeField] private string displayName = "Climber";
    [SerializeField] private int team;
    [SerializeField] private float maxBody = 100f;
    [SerializeField] private float maxEgo = 100f;
    [SerializeField] private int maxLibido = 10;
    [SerializeField] private int startLibido = 3;
    [SerializeField] private float brokenEgo = 40f, phoenixBody = 30f, poisonDamage = 4f;

    public event Action<Fighter> Changed;
    public event Action<Fighter, float> Damaged;
    public event Action<Fighter> Defeated;
    public event Action<Fighter> EgoBroken;
    public event Action<Fighter, string> Note;

    public string DisplayName => displayName;
    public int Team { get => team; set => team = value; }
    public float Body { get; private set; }
    public float Ego { get; private set; }
    public int Libido { get; private set; }
    public float BodyRatio => Body / maxBody;
    public float EgoRatio => Ego / maxEgo;
    public float ShadowRatio => 0f;
    public bool IsDown => Body <= 0f;
    public Dictionary<StatusType, int> Statuses { get; } = new Dictionary<StatusType, int>();

    private float savedBody, savedEgo;

    private void Awake() => Restore();

    public void Restore()
    {
        Body = maxBody;
        Ego = maxEgo;
        Libido = startLibido;
        Statuses.Clear();
        Remember();
        Changed?.Invoke(this);
    }

    public bool Has(StatusType t) => Statuses.ContainsKey(t);
    public void Remember() { savedBody = Body; savedEgo = Ego; }

    public void Rewind()
    {
        Body = Mathf.Max(Body, savedBody);
        Ego = Mathf.Max(Ego, savedEgo);
        Changed?.Invoke(this);
    }

    public bool Consume(StatusType t)
    {
        if (!Statuses.Remove(t)) return false;
        Changed?.Invoke(this);
        return true;
    }

    public void AddStatus(StatusType t, int turns)
    {
        Statuses[t] = Mathf.Max(turns, Statuses.TryGetValue(t, out int left) ? left : 0);
        Changed?.Invoke(this);
    }

    public void ClearStatuses()
    {
        Statuses.Clear();
        Changed?.Invoke(this);
    }

    public void Spend(int cost)
    {
        Libido = Mathf.Max(0, Libido - cost);
        Changed?.Invoke(this);
    }

    public void GainLibido(int amount)
    {
        Libido = Mathf.Clamp(Libido + amount, 0, maxLibido);
        Changed?.Invoke(this);
    }

    public void Damage(float amount)
    {
        if (IsDown || amount <= 0f) return;
        if (Consume(StatusType.Marked)) amount *= 1.5f;
        Body = Mathf.Max(0f, Body - amount);
        if (Body <= 0f && Consume(StatusType.Phoenix))
        {
            Body = phoenixBody;
            Note?.Invoke(this, "Phoenix Seed! " + displayName + " rises again");
        }
        Damaged?.Invoke(this, amount);
        Changed?.Invoke(this);
        if (Body <= 0f) Defeated?.Invoke(this);
    }

    public void Heal(float amount)
    {
        if (IsDown || amount <= 0f) return;
        if (Has(StatusType.Rot)) { Note?.Invoke(this, "Rot blocks the healing"); return; }
        Body = Mathf.Min(maxBody, Body + amount);
        Changed?.Invoke(this);
    }

    public void Mind(float loss)
    {
        if (IsDown || loss <= 0f) return;
        Ego = Mathf.Max(0f, Ego - loss);
        if (Ego <= 0f)
        {
            Ego = brokenEgo;
            AddStatus(StatusType.Skip, 1);
            Note?.Invoke(this, displayName + "'s Ego breaks  ·  they lose a turn");
            EgoBroken?.Invoke(this);
        }
        Changed?.Invoke(this);
    }

    public void StartTurn(int libidoGain)
    {
        GainLibido(libidoGain);
        if (Statuses.ContainsKey(StatusType.Poison)) Damage(Has(StatusType.Rot) ? poisonDamage * 2f : poisonDamage);
        foreach (StatusType t in new[] { StatusType.Poison, StatusType.Rot, StatusType.Phoenix })
        {
            if (!Statuses.TryGetValue(t, out int left)) continue;
            if (left <= 1) Statuses.Remove(t);
            else Statuses[t] = left - 1;
        }
        Changed?.Invoke(this);
    }
}
