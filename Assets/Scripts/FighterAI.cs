using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FighterAI : MonoBehaviour
{
    [SerializeField] private Fighter fighter;
    [SerializeField] private Animator animator;
    [SerializeField] private Board board;
    [SerializeField] private ThrowableCard cardPrefab;
    [SerializeField] private Material faceTemplate;
    [SerializeField] private Texture cardBack;
    [SerializeField] private CardData[] deck;
    [SerializeField] private int restTurns = 2;
    [SerializeField] private float windup = 0.42f, turnSpeed = 6f, fieldScale = 3.2f, noise = 5f;

    public CardData Intent { get; private set; }

    private readonly Dictionary<CardData, int> rest = new Dictionary<CardData, int>();
    private Transform hand;
    private Fighter facing;

    private void Start()
    {
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        animator.SetBool("Showcase", false);
        animator.SetInteger("Mood", 3);
    }

    private void OnEnable() => board.RestAdded += OnRest;
    private void OnDisable() => board.RestAdded -= OnRest;
    private void OnRest(Fighter f, CardData c, int turns) { if (f == fighter) rest[c] = Rest(c) + turns; }
    private int Rest(CardData c) => rest.TryGetValue(c, out int r) ? r : 0;

    public void Face(Fighter target) => facing = target;
    public bool Ready(CardData c) => c != null && Rest(c) == 0 && board.Cost(c, fighter) <= fighter.Libido && !board.Foreseen(fighter, c);
    public void Prepare(Fighter foe) { Face(foe); rest.Clear(); Plan(foe); }
    public void Plan(Fighter foe) => Intent = Choose(foe);

    public CardData Pick(Fighter foe)
    {
        foreach (CardData c in new List<CardData>(rest.Keys)) rest[c] = Mathf.Max(0, rest[c] - 1);
        return Ready(Intent) ? Intent : Choose(foe);
    }

    private void Update()
    {
        if (facing == null || fighter.IsDown) return;
        Vector3 away = transform.position - facing.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), Time.deltaTime * turnSpeed);
    }

    public CardData Choose(Fighter foe)
    {
        CardData best = null;
        float top = float.MinValue;
        foreach (CardData c in deck)
        {
            if (!Ready(c)) continue;
            float s = Value(c, foe) - c.cost * 1.5f + Random.Range(0f, noise);
            if (s > top) { top = s; best = c; }
        }
        return best;
    }

    private float Value(CardData c, Fighter foe)
    {
        float v = c.damage + c.ego * 0.6f + c.sap * 3f + (c.status == StatusType.Skip ? 14f : 6f) * Mathf.Min(c.statusTurns, 3) + c.libido * 3f + c.heal * (1f - fighter.BodyRatio) * 2.2f;
        bool foeGuarded = board.Guard(foe) != null;
        if (c.kind == CardKind.Field) v = v * 3f + c.resolve * 0.4f + (c.guard ? (board.Guard(fighter) == null ? 14f : -12f) : 4f);
        if (c.kind == CardKind.Omen) v = c.trigger == OmenTrigger.Countdown ? v * 0.9f : 10f;
        if (c.statusTurns > 0 && foe.Has(c.status)) v -= 20f;
        switch (c.special)
        {
            case Special.Pierce: case Special.Shockwave: if (foeGuarded) v += 12f; break;
            case Special.Charged: if (fighter.Libido - c.cost >= 5) v += 10f; break;
            case Special.Copy: v += 7f; break;
            case Special.Rewind: v += fighter.BodyRatio < 0.5f ? 15f : -10f; break;
            case Special.CoinToss: v += 6f; break;
            case Special.Dispel: v += board.Units.Exists(u => u.owner == foe && u.card.kind == CardKind.Field && u.resolve <= 15) ? 15f : -20f; break;
            case Special.Glimpse: case Special.ReadHand: v += board.Units.Exists(u => u.owner == foe && !u.revealed) ? 12f : -10f; break;
            case Special.Phoenix: v += fighter.BodyRatio < 0.4f ? 20f : -5f; break;
        }
        return v;
    }

    public IEnumerator Play(CardData c, Fighter foe)
    {
        rest[c] = restTurns + 1;
        Face(foe);
        animator.SetTrigger(c.kind == CardKind.Instant && c.Offensive ? "Throw" : "Cast");
        yield return new WaitForSeconds(windup);
        Vector3 from = hand != null ? hand.position : transform.position + Vector3.up * 1.4f;
        ThrowableCard card = Instantiate(cardPrefab, from, transform.rotation);
        card.Setup(c, faceTemplate, c.kind == CardKind.Omen ? cardBack : c.art);
        if (c.kind == CardKind.Omen) card.Show(cardBack, "HIDDEN OMEN");
        yield return card.Fly(board.Aim(c, fighter, foe), c.kind == CardKind.Instant ? 1f : fieldScale, foe.transform);
        Destroy(card.gameObject);
        board.Play(c, fighter, foe);
    }
}
