using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class FieldUnit { public CardData card; public Fighter owner; public int resolve, countdown, slot; public bool revealed; public Vector3 at; }

public class Board : MonoBehaviour
{
    [SerializeField] private int slots = 3, snareDamage = 15, thornDamage = 6, clauseDamage = 4, coinBackfire = 8, dispelLimit = 15;
    [SerializeField] private float slotForward = 1.7f, slotSpacing = 1.15f, decoyMiss = 0.5f, mirageMiss = 0.3f, panicFizzle = 0.3f;

    public event Action<FieldUnit> UnitPlaced, UnitChanged, UnitRemoved;
    public event Action<Fighter, CardData, int> RestAdded;
    public event Action<Fighter> IntentRevealed;
    public event Action<string> Note;

    public List<FieldUnit> Units { get; } = new List<FieldUnit>();
    private readonly Dictionary<Fighter, CardData> last = new Dictionary<Fighter, CardData>(), lastInstant = new Dictionary<Fighter, CardData>(), foreseen = new Dictionary<Fighter, CardData>();

    public bool Foreseen(Fighter f, CardData c) => c != null && foreseen.TryGetValue(f, out CardData x) && x == c;
    public int Cost(CardData c, Fighter f) => c.cost + (f.Has(StatusType.Shackled) ? 3 : 0);
    public FieldUnit Guard(Fighter f) => Units.Find(u => u.owner == f && u.card.kind == CardKind.Field && u.card.guard);
    private bool Has(Fighter f, Special s) => Units.Exists(u => u.owner == f && u.card.special == s);
    private bool Nulled => Units.Exists(u => u.card.special == Special.NullZone);
    private void Say(string s) => Note?.Invoke(s);
    private void Remove(FieldUnit u) { if (Units.Remove(u)) UnitRemoved?.Invoke(u); }
    public void Clear() { foreach (FieldUnit u in Units.ToArray()) Remove(u); last.Clear(); lastInstant.Clear(); foreseen.Clear(); }

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
        FieldUnit g = c.special == Special.Pierce || c.special == Special.Shockwave ? null : Guard(foe);
        return g != null ? SlotPosition(foe, caster, g.slot) + Vector3.up * 0.7f : foe.transform.position + Vector3.up * 1.2f;
    }

    public void Play(CardData c, Fighter caster, Fighter foe)
    {
        caster.Spend(Cost(c, caster));
        caster.Consume(StatusType.Shackled);
        if (Foreseen(caster, c)) { foreseen.Remove(caster); Say(c.cardName + " was foreseen  ·  it fizzles"); return; }
        foreseen.Remove(caster);
        if (caster.Consume(StatusType.Panic) && Random.value < panicFizzle) { Say(caster.DisplayName + " panics  ·  " + c.cardName + " fizzles"); return; }
        foreach (FieldUnit u in Units.ToArray()) if (u.owner == foe && u.card.special == Special.Clause) { caster.Damage(clauseDamage); Say("Clause  ·  " + caster.DisplayName + " pays " + clauseDamage + " Body"); }
        last[caster] = c;
        if (!Intercept(c, caster, foe)) Resolve(c, caster, foe);
    }

    private bool Intercept(CardData c, Fighter caster, Fighter foe)
    {
        if (Nulled) return false;
        foreach (FieldUnit u in Units.ToArray())
        {
            if (u.owner != foe || u.card.kind != CardKind.Omen || u.card.trigger == OmenTrigger.Countdown || c.kind == CardKind.Omen) continue;
            if ((u.card.trigger == OmenTrigger.FoeField) != (c.kind == CardKind.Field) || (c.kind == CardKind.Instant && !c.Offensive)) continue;
            Remove(u);
            Say(foe.DisplayName + "'s Omen  ·  " + u.card.cardName + "!");
            if (u.card.special == Special.Reflect) { Say(c.cardName + " bounces back"); Resolve(c, foe, caster); return true; }
            if (u.card.special == Special.Cancel) { Say(c.cardName + " is cancelled"); return true; }
            if (u.card.special == Special.Bounce) { Say(c.cardName + " returns to the hand"); RestAdded?.Invoke(caster, c, 2); return true; }
            Resolve(c, caster, foe);
            if (Units.FindLast(x => x.owner == caster) is FieldUnit landed) Strike(landed, snareDamage, null);
            return true;
        }
        return false;
    }

    private void Resolve(CardData c, Fighter caster, Fighter foe)
    {
        if (c.kind == CardKind.Instant) { if (c.special != Special.Echo) lastInstant[caster] = c; Apply(c, caster, foe); return; }
        if (Units.FindAll(u => u.owner == caster).Count >= slots) Remove(Units.Find(u => u.owner == caster));
        FieldUnit n = new FieldUnit { card = c, owner = caster, resolve = c.resolve, countdown = c.turns, slot = NextSlot(caster), revealed = c.kind == CardKind.Field, at = SlotPosition(caster, foe, NextSlot(caster)) };
        Units.Add(n);
        UnitPlaced?.Invoke(n);
        if (c.special == Special.Phoenix) caster.AddStatus(StatusType.Phoenix, c.turns);
    }

    private void Apply(CardData c, Fighter caster, Fighter foe)
    {
        CardData x;
        switch (c.special)
        {
            case Special.Copy: if (last.TryGetValue(foe, out x) && x.special != Special.Copy) { Say("Copy becomes " + x.cardName); Resolve(x, caster, foe); } else Say("Nothing to copy"); return;
            case Special.Rewind: caster.Rewind(); Say(caster.DisplayName + " rewinds their last wounds"); return;
            case Special.Echo: if (lastInstant.TryGetValue(caster, out x)) { Say("Long Echo repeats " + x.cardName); Apply(x, caster, foe); } return;
            case Special.CoinToss: if (Random.value < 0.5f) { Say("Coin Toss  ·  heads!"); break; } Say("Coin Toss  ·  tails, it backfires"); caster.Damage(coinBackfire); return;
            case Special.LoadedDie: int roll = Random.Range(1, 7); Say("Loaded Die rolls " + roll); Hit(foe, caster, roll <= 3 ? 4 : roll <= 5 ? 8 : 16, false); return;
            case Special.Dispel: FieldUnit w = Units.Find(u => u.owner == foe && u.card.kind == CardKind.Field && u.resolve <= dispelLimit);
                if (w != null) { Say("Dispel shatters " + w.card.cardName); Remove(w); } else { foe.ClearStatuses(); Say("Dispel wipes " + foe.DisplayName + "'s effects"); } return;
            case Special.CardLock: if (last.TryGetValue(foe, out x)) { RestAdded?.Invoke(foe, x, 2); Say(x.cardName + " is locked for 2 more turns"); } break;
            case Special.Glimpse: case Special.ReadHand:
                foreach (FieldUnit u in Units) if (u.owner == foe && !u.revealed) { u.revealed = true; UnitChanged?.Invoke(u); }
                if (c.special == Special.ReadHand && foe.TryGetComponent(out FighterAI ai) && ai.Intent != null) foreseen[foe] = ai.Intent;
                IntentRevealed?.Invoke(foe);
                break;
            case Special.Shockwave: foreach (FieldUnit u in Units.ToArray()) if (u.owner == foe && u.card.kind == CardKind.Field) Strike(u, c.damage, caster); break;
        }
        int dmg = c.damage + (c.special == Special.Charged && caster.Libido >= 5 ? 10 : 0);
        if (dmg > 0) Hit(foe, caster, dmg, c.special == Special.Pierce || c.special == Special.Shockwave);
        foe.Mind(c.ego);
        caster.Heal(c.heal);
        if (c.libido != 0) caster.GainLibido(c.libido);
        if (c.sap > 0) foe.GainLibido(-c.sap);
        if (c.statusTurns > 0) foe.AddStatus(c.status, c.statusTurns);
    }

    private void Hit(Fighter target, Fighter attacker, int dmg, bool pierce)
    {
        FieldUnit g = pierce ? null : Guard(target);
        if (!pierce && Has(target, Special.Mirage) && Random.value < mirageMiss) { Say("Mirage  ·  the attack misses"); return; }
        if (g != null && g.card.special == Special.Decoy && Random.value < decoyMiss) { Say("Decoy  ·  the attack passes through"); return; }
        if (g != null) Strike(g, dmg, attacker); else target.Damage(dmg);
    }

    private void Strike(FieldUnit u, int dmg, Fighter attacker)
    {
        if (u.card.special == Special.Thorns && attacker != null) { attacker.Damage(thornDamage); Say("Thorn Ward cuts back " + thornDamage); }
        u.resolve -= dmg;
        if (u.resolve <= 0) { Say(u.card.cardName + " is destroyed"); Remove(u); } else { Say(u.card.cardName + " absorbs " + dmg); UnitChanged?.Invoke(u); }
    }

    public void StartTurn(Fighter owner, Fighter foe)
    {
        foreach (FieldUnit u in Units.ToArray())
        {
            if (u.owner != owner || !Units.Contains(u)) continue;
            CardData c = u.card;
            if (c.kind == CardKind.Field) { if (c.damage + c.heal + c.ego + c.libido > 0 || c.special == Special.LoadedDie) Apply(c, owner, foe); continue; }
            if (c.trigger != OmenTrigger.Countdown || Nulled) continue;
            if (--u.countdown > 0) { UnitChanged?.Invoke(u); continue; }
            Remove(u);
            if (c.special == Special.Phoenix) continue;
            Say(owner.DisplayName + "'s Omen  ·  " + c.cardName + "!");
            Apply(c, owner, foe);
        }
    }
}
