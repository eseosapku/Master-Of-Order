using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class FieldUnit { public CardData card; public Fighter owner; public int resolve, slot; public bool revealed; public Vector3 at; }

public interface ISatchel { void Seal(CardData c, int turns); void SealRandom(); void Unseal(); CardData Take(); void Give(CardData c); }

public class Board : MonoBehaviour
{
    [SerializeField] private CoinFlip coin;
    [SerializeField] private int slots = 3, sealTurns = 3;
    [SerializeField] private float slotForward = 1.7f, slotSpacing = 1.15f;

    public event Action<FieldUnit> UnitPlaced, UnitChanged, UnitRemoved;
    public event Action<Fighter> IntentRevealed;
    public event Action<string, string> Note;
    public event Action<CardData> Landed;

    public List<FieldUnit> Units { get; } = new List<FieldUnit>();
    private readonly Dictionary<Fighter, ISatchel> satchels = new Dictionary<Fighter, ISatchel>();
    private readonly Dictionary<Fighter, CardData> last = new Dictionary<Fighter, CardData>();
    private bool call, bounced;

    public void Register(Fighter f, ISatchel s) => satchels[f] = s;
    public int Cost(CardData c, Fighter f) => c.cost + (f.Has(StatusType.Slowed) ? 2 : 0);
    public FieldUnit Guard(Fighter f) => Units.Find(u => u.owner == f && u.card.guard);
    public bool HasHidden(Fighter f) => Units.Exists(u => u.owner == f && !u.revealed);
    private void Say(string s, string voice = null) => Note?.Invoke(s, voice);
    private static string Does(Fighter f, string verb) => f.DisplayName + " " + verb + (f.DisplayName == "You" ? "" : "s");
    private void Remove(FieldUnit u) { if (Units.Remove(u)) UnitRemoved?.Invoke(u); }
    public void Clear() { foreach (FieldUnit u in Units.ToArray()) Remove(u); last.Clear(); }

    public Vector3 SlotPosition(Fighter owner, Fighter foe, int i)
    {
        Vector3 to = Vector3.ProjectOnPlane(foe.transform.position - owner.transform.position, Vector3.up).normalized;
        return owner.transform.position + to * slotForward + Vector3.Cross(Vector3.up, to) * (i - 1) * slotSpacing;
    }

    public int NextSlot(Fighter owner)
    {
        foreach (int i in new[] { 1, 0, 2 }) if (!Units.Exists(u => u.owner == owner && u.slot == i)) return i;
        return Units.Find(u => u.owner == owner).slot;
    }

    public Vector3 Aim(CardData c, Fighter caster, Fighter foe)
    {
        if (c.kind != CardKind.Instant) return SlotPosition(caster, foe, NextSlot(caster)) + Vector3.up * 0.7f;
        if (!c.Offensive) return caster.transform.position + Vector3.up * 1.2f;
        FieldUnit g = c.special == Special.Shatter ? Units.FindLast(u => u.owner == foe) : c.special == Special.Pierce ? null : Guard(foe);
        return g != null ? g.at + Vector3.up * 0.7f : foe.transform.position + Vector3.up * 1.2f;
    }

    public IEnumerator Play(CardData c, Fighter caster, Fighter foe)
    {
        caster.Spend(Cost(c, caster));
        CardData real = c;
        if (c.special == Special.Echo && !last.TryGetValue(caster, out real)) { Say("Echo finds nothing to repeat", "Fizzles"); yield break; }
        if (c.special == Special.Echo) Say("Echo replays " + real.cardName);
        last[caster] = real;
        if (Intercept(real, caster, foe)) yield break;
        call = true; bounced = false;
        if (real.Coin && coin != null) yield return coin.Flip(caster, caster.GetComponent<FighterAI>() == null, r => call = r);
        Resolve(real, caster, foe);
    }

    private bool Intercept(CardData c, Fighter caster, Fighter foe)
    {
        foreach (FieldUnit u in Units.ToArray())
        {
            if (u.owner != foe || u.card.kind != CardKind.Hidden) continue;
            if (u.card.special == Special.Negate) { Remove(u); Say(foe.Possessive + " Negate cancels " + c.cardName, "Negated"); return true; }
            if (u.card.special == Special.Mirror && c.kind == CardKind.Instant && c.Offensive) { Remove(u); Say(foe.Possessive + " Mirror bounces " + c.cardName + " back", "Reflected"); call = true; bounced = true; Resolve(c, foe, caster); return true; }
        }
        return false;
    }

    private void Resolve(CardData c, Fighter caster, Fighter foe)
    {
        if (c.kind == CardKind.Instant) { Apply(c, caster, foe); return; }
        if (Units.FindAll(u => u.owner == caster).Count >= slots) Remove(Units.Find(u => u.owner == caster));
        FieldUnit n = new FieldUnit { card = c, owner = caster, resolve = c.resolve, slot = NextSlot(caster), revealed = c.kind == CardKind.Field, at = SlotPosition(caster, foe, NextSlot(caster)) };
        Units.Add(n); UnitPlaced?.Invoke(n);
        if (c.kind == CardKind.Field) Landed?.Invoke(c); else Say(Does(caster, "set") + " a hidden card");
    }

    private void Apply(CardData c, Fighter caster, Fighter foe)
    {
        string right = call ? "Right call" : "Wrong call", voice = call ? "Right" : "Wrong";
        switch (c.special)
        {
            case Special.Gamble: if (call) { Say(right, voice); break; } Say(right + "  ·  Gamble Blow hits " + caster.DisplayName + " for " + c.damage, voice); caster.Damage(c.damage); return;
            case Special.Stillframe: caster.AddStatus(call ? StatusType.Extra : StatusType.Skip, call ? 2 : 1); Say(right + "  ·  " + (call ? Does(caster, "get") + " two moves in a row" : Does(caster, "lose") + " the next turn"), voice); return;
            case Special.Double: caster.AddStatus(call ? StatusType.Doubled : StatusType.Fumble, 99); Say(right + "  ·  " + caster.Possessive + " next attack " + (call ? "deals double" : "will fizzle"), voice); return;
            case Special.Thief:
                Fighter from = call ? foe : caster, to = call ? caster : foe;
                CardData stolen = satchels.ContainsKey(from) ? satchels[from].Take() : null;
                if (stolen != null && satchels.ContainsKey(to)) satchels[to].Give(stolen);
                Say(right + "  ·  " + Does(to, "steal") + " " + (stolen != null ? stolen.cardName : "nothing") + " from " + from.DisplayName, voice); return;
            case Special.Shatter:
                FieldUnit t = Units.FindLast(u => u.owner == foe);
                if (t == null) { Say("Shatter finds nothing on the field", "Fizzles"); return; }
                Landed?.Invoke(c);
                if (t.card.kind == CardKind.Hidden) { Remove(t); Say("Shatter destroys a hidden " + t.card.cardName, "Broken"); } else Strike(t, c.damage); return;
            case Special.Glimpse: foreach (FieldUnit u in Units) if (u.owner == foe && !u.revealed) { u.revealed = true; UnitChanged?.Invoke(u); }
                IntentRevealed?.Invoke(foe);
                break;
            case Special.Cleanse: caster.ClearStatuses(); if (satchels.ContainsKey(caster)) satchels[caster].Unseal(); break;
            case Special.Seal: if (last.TryGetValue(foe, out CardData x) && satchels.ContainsKey(foe)) { satchels[foe].Seal(x, sealTurns); Say(foe.Possessive + " " + x.cardName + " is sealed for " + sealTurns + " turns"); } break;
        }
        int dmg = c.damage;
        if (dmg > 0 && !bounced && caster.Consume(StatusType.Fumble)) { Say(c.cardName + " fizzles", "Fizzles"); return; }
        if (dmg > 0 && !bounced && caster.Consume(StatusType.Doubled)) { dmg *= 2; Say("Double damage!"); }
        Landed?.Invoke(c);
        if (dmg > 0) Hit(foe, dmg, c.special == Special.Pierce);
        caster.Heal(c.heal);
        if (c.statusTurns > 0) foe.AddStatus(c.status, c.statusTurns);
    }

    private void Hit(Fighter target, int dmg, bool pierce)
    {
        FieldUnit g = pierce ? null : Guard(target);
        if (g != null) Strike(g, dmg); else { target.Damage(dmg); Say(dmg + " damage to " + target.DisplayName); }
    }

    private void Strike(FieldUnit u, int dmg)
    {
        u.resolve -= dmg;
        if (u.resolve <= 0) { Say(u.owner.Possessive + " " + u.card.cardName + " breaks", "Broken"); Remove(u); } else { Say(u.owner.Possessive + " " + u.card.cardName + " absorbs " + dmg, "Blocked"); UnitChanged?.Invoke(u); }
    }

    public void StartTurn(Fighter owner, Fighter foe)
    {
        foreach (FieldUnit u in Units.ToArray())
        {
            if (u.owner != owner || u.card.kind != CardKind.Field || !Units.Contains(u)) continue;
            if (u.card.heal > 0) { owner.Heal(u.card.heal); Say(u.card.cardName + " heals " + owner.DisplayName + " " + u.card.heal, "Healed"); }
            if (u.card.special == Special.Erosion && satchels.ContainsKey(foe)) { satchels[foe].SealRandom(); Say("Erosion seals one of " + foe.Possessive + " cards for good", "Eroded"); }
            if (u.card.special != Special.LoadedDie) continue;
            int roll = Random.Range(1, 7), dmg = roll <= 2 ? 0 : roll <= 5 ? 10 : 20;
            Say("Loaded Die rolls " + roll + (dmg > 0 ? "" : "  ·  nothing"));
            if (dmg > 0) Hit(foe, dmg, false);
        }
    }
}
