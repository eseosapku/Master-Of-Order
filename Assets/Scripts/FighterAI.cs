using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FighterAI : MonoBehaviour, ISatchel
{
    [SerializeField] private Fighter fighter;
    [SerializeField] private Animator animator;
    [SerializeField] private Board board;
    [SerializeField] private ThrowableCard cardPrefab;
    [SerializeField] private Material faceTemplate;
    [SerializeField] private Texture cardBack;
    [SerializeField] private CardData[] deck;
    [SerializeField] private int restTurns = 2;
    [SerializeField] private float release = 0.38f, turnSpeed = 6f, fieldScale = 3.2f, noise = 5f;

    public CardData Intent { get; private set; }

    private readonly List<CardData> hand = new List<CardData>();
    private readonly List<int> rest = new List<int>();
    private Transform bone;
    private Fighter facing;

    private void Start()
    {
        bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        animator.SetBool("Showcase", false);
        animator.SetInteger("Mood", 3);
    }

    public void Face(Fighter target) => facing = target;
    public void Plan(Fighter foe) => Intent = Choose(foe);
    private bool Ready(int i) => i >= 0 && rest[i] == 0 && board.Cost(hand[i], fighter) <= fighter.Libido;

    public void Prepare(Fighter foe)
    {
        Face(foe);
        hand.Clear();
        rest.Clear();
        foreach (CardData c in deck) Give(c);
        board.Register(fighter, this);
        Plan(foe);
    }

    public void Seal(CardData c, int turns) { for (int i = 0; i < hand.Count; i++) if (hand[i] == c) rest[i] += turns; }
    public void SealRandom() { if (hand.Count > 0) rest[Random.Range(0, hand.Count)] = 99; }
    public void Unseal() { for (int i = 0; i < rest.Count; i++) rest[i] = 0; }
    public void Give(CardData c) { hand.Add(c); rest.Add(0); }

    public CardData Take()
    {
        if (hand.Count == 0) return null;
        int i = Random.Range(0, hand.Count);
        CardData c = hand[i];
        hand.RemoveAt(i);
        rest.RemoveAt(i);
        return c;
    }

    public CardData Pick(Fighter foe)
    {
        for (int i = 0; i < rest.Count; i++) rest[i] = Mathf.Max(0, rest[i] - 1);
        return Ready(hand.IndexOf(Intent)) ? Intent : Choose(foe);
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
        for (int i = 0; i < hand.Count; i++)
        {
            if (!Ready(i)) continue;
            float s = Value(hand[i], foe) - hand[i].cost * 1.5f + Random.Range(0f, noise);
            if (s > top) { top = s; best = hand[i]; }
        }
        return best;
    }

    private float Value(CardData c, Fighter foe)
    {
        bool guarded = board.Guard(foe) != null, mine = board.Units.Exists(u => u.owner == fighter && u.card == c);
        float v = c.damage * (guarded && c.special != Special.Pierce ? 0.5f : 1f) + c.heal * (1f - fighter.BodyRatio) * 2.5f;
        switch (c.special)
        {
            case Special.None: if (c.guard) v += board.Guard(fighter) == null ? 18f : -12f; if (c.statusTurns > 0) v += foe.Has(c.status) ? -15f : 8f; break;
            case Special.Pierce: if (guarded) v += 10f; break;
            case Special.Shatter: v = board.Units.Exists(u => u.owner == foe) ? 24f : -20f; break;
            case Special.Gamble: v = 8f; break;
            case Special.Mirror: case Special.Negate: v = mine ? -10f : 11f; break;
            case Special.Cleanse: v = fighter.Statuses.Count > 0 || rest.Exists(r => r > 2) ? 12f : -15f; break;
            case Special.Erosion: case Special.LoadedDie: v = mine ? -5f : 15f; break;
            case Special.Stillframe: v = 14f; break;
            case Special.Glimpse: v = board.HasHidden(foe) ? 12f : 1f; break;
            case Special.Double: v = fighter.Has(StatusType.Doubled) ? -10f : 7f; break;
            case Special.Thief: v = 6f; break;
            case Special.Seal: case Special.Echo: v = 7f; break;
        }
        if (c.special == Special.None && c.kind == CardKind.Field && c.heal > 0) v = mine ? -5f : 10f + (1f - fighter.BodyRatio) * 10f;
        return v;
    }

    public IEnumerator Play(CardData c, Fighter foe)
    {
        int i = hand.IndexOf(c);
        if (i >= 0) rest[i] = restTurns + 1;
        Face(foe);
        animator.SetTrigger("Throw");
        ThrowableCard card = Instantiate(cardPrefab, bone != null ? bone.position : transform.position + Vector3.up * 1.4f, transform.rotation);
        bool hidden = c.kind == CardKind.Hidden;
        card.Setup(c, faceTemplate, hidden ? cardBack : c.art);
        if (hidden) card.Show(cardBack, "HIDDEN CARD");
        for (float t = 0f; t < release; t += Time.deltaTime)
        {
            if (bone != null) card.transform.SetPositionAndRotation(bone.position, bone.rotation);
            yield return null;
        }
        yield return card.Fly(board.Aim(c, fighter, foe), c.kind == CardKind.Instant ? 1f : fieldScale, foe.transform, false);
        Destroy(card.gameObject);
        yield return board.Play(c, fighter, foe);
        Plan(foe);
    }
}
