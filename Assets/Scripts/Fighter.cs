using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusType { Poison, Slowed, Skip, Doubled, Fumble, Extra }

public class Fighter : MonoBehaviour
{
    [SerializeField] private string displayName = "Climber";
    [SerializeField] private int team;
    [SerializeField] private float maxBody = 100f;
    [SerializeField] private int maxLibido = 10;
    [SerializeField] private int startLibido = 3;
    [SerializeField] private float poisonDamage = 10f;

    public event Action<Fighter> Changed;
    public event Action<Fighter, float> Damaged;
    public event Action<Fighter> Defeated;

    public string DisplayName => displayName;
    public string Possessive => displayName == "You" ? "Your" : displayName + "'s";
    public int Team { get => team; set => team = value; }
    public float Body { get; private set; }
    public int Libido { get; private set; }
    public float BodyRatio => Body / maxBody;
    public bool IsDown => Body <= 0f;
    public Dictionary<StatusType, int> Statuses { get; } = new Dictionary<StatusType, int>();

    private void Awake() => Restore();

    public void Restore()
    {
        Body = maxBody;
        Libido = startLibido;
        Statuses.Clear();
        Changed?.Invoke(this);
    }

    public bool Has(StatusType t) => Statuses.ContainsKey(t);

    public bool Consume(StatusType t)
    {
        if (!Statuses.Remove(t)) return false;
        Changed?.Invoke(this);
        return true;
    }

    public bool TakeExtra()
    {
        if (!Statuses.TryGetValue(StatusType.Extra, out int left)) return false;
        if (left <= 1) Statuses.Remove(StatusType.Extra); else Statuses[StatusType.Extra] = left - 1;
        return true;
    }

    public void AddStatus(StatusType t, int turns)
    {
        Statuses[t] = Mathf.Max(turns, Statuses.TryGetValue(t, out int left) ? left : 0);
        Changed?.Invoke(this);
    }

    public void ClearStatuses()
    {
        Statuses.Remove(StatusType.Poison);
        Statuses.Remove(StatusType.Slowed);
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
        Body = Mathf.Max(0f, Body - amount);
        Damaged?.Invoke(this, amount);
        Changed?.Invoke(this);
        if (Body <= 0f) Defeated?.Invoke(this);
    }

    public void Heal(float amount)
    {
        if (IsDown || amount <= 0f) return;
        Body = Mathf.Min(maxBody, Body + amount);
        Changed?.Invoke(this);
    }

    public void StartTurn(int libidoGain)
    {
        GainLibido(libidoGain);
        if (Consume(StatusType.Poison)) Damage(poisonDamage);
        if (Statuses.TryGetValue(StatusType.Slowed, out int left))
        {
            if (left <= 1) Statuses.Remove(StatusType.Slowed);
            else Statuses[StatusType.Slowed] = left - 1;
        }
        Changed?.Invoke(this);
    }
}
