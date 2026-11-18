using System;
using System.Collections.Generic;
using UnityEngine;

public class CardOrbit : MonoBehaviour
{
    [SerializeField] private ThrowableCard cardPrefab;
    [SerializeField] private Material faceTemplate;
    [SerializeField] private Transform head;
    [SerializeField] private GameManager game;
    [SerializeField] private Fighter owner;
    [SerializeField] private CardData[] starterDeck;
    [SerializeField] private float radius = 0.48f;
    [SerializeField] private float drop = 0.42f;
    [SerializeField] private float arc = 200f;
    [SerializeField] private float drift = 4f;
    [SerializeField] private float follow = 6f;
    [SerializeField] private float rechargeTime = 5f;

    public event Action<ThrowableCard, Collider, float> CardLanded;
    public event Action<ThrowableCard> CardThrown;

    public IReadOnlyList<ThrowableCard> Cards => cards;
    public Fighter Owner => owner;

    private readonly List<ThrowableCard> cards = new List<ThrowableCard>();
    private float yaw;

    private void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (cards.Count == 0 && starterDeck != null && starterDeck.Length > 0) Fill(new List<CardData>(starterDeck));
    }

    public void Fill(List<CardData> deck)
    {
        Clear();
        foreach (CardData d in deck) Add(d);
        Arrange(true);
    }

    public void Add(CardData d)
    {
        ThrowableCard c = Instantiate(cardPrefab, transform);
        c.name = "Orbit" + d.name;
        c.Setup(d, faceTemplate, game != null ? game.AimAssist : 1f, rechargeTime);
        c.Landed += OnLanded;
        c.Thrown += OnThrown;
        if (owner != null) foreach (Collider oc in owner.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(c.Hitbox, oc);
        foreach (ThrowableCard other in cards) Physics.IgnoreCollision(c.Hitbox, other.Hitbox);
        cards.Add(c);
    }

    public void Clear()
    {
        foreach (ThrowableCard c in cards)
        {
            if (c == null) continue;
            c.Landed -= OnLanded;
            c.Thrown -= OnThrown;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
        cards.Clear();
    }

    private void Update()
    {
        Arrange(false);
    }

    public void Arrange(bool snap)
    {
        if (head == null || cards.Count == 0) return;
        Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (flat.sqrMagnitude > 0.01f)
        {
            float target = Quaternion.LookRotation(flat).eulerAngles.y;
            yaw = snap ? target : Mathf.LerpAngle(yaw, target, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
        }
        Vector3 center = head.position + Vector3.down * drop;
        float step = cards.Count > 1 ? arc / (cards.Count - 1) : 0f;
        float t = Application.isPlaying ? Time.time : 0f;
        for (int i = 0; i < cards.Count; i++)
        {
            float a = yaw - arc * 0.5f + step * i + Mathf.Sin(t * 0.5f + i) * drift;
            Quaternion around = Quaternion.Euler(0f, a, 0f);
            Vector3 pos = center + around * Vector3.forward * radius + Vector3.up * Mathf.Sin(t * 1.3f + i * 0.7f) * 0.015f;
            Quaternion rot = Quaternion.LookRotation(head.position - pos);
            if (owner != null && Application.isPlaying) cards[i].SetReady(!owner.IsDown && owner.Libido >= cards[i].Data.cost);
            if (snap) cards[i].transform.SetPositionAndRotation(pos, rot);
            else cards[i].Hold(pos, rot, follow);
        }
    }

    private void OnThrown(ThrowableCard card)
    {
        if (owner != null) owner.Spend(card.Data.cost);
        CardThrown?.Invoke(card);
    }

    private void OnLanded(ThrowableCard card, Collider hit, float speed)
    {
        CardLanded?.Invoke(card, hit, speed);
    }

    public static Collider FindTarget(Vector3 from, Vector3 v, float cone)
    {
        Collider best = null;
        foreach (Collider c in Physics.OverlapSphere(from, 25f))
        {
            if (!c.CompareTag("Target")) continue;
            float a = Vector3.Angle(v, c.bounds.center - from);
            if (a < cone)
            {
                cone = a;
                best = c;
            }
        }
        return best;
    }
}
