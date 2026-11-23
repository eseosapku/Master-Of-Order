using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class CardHand : MonoBehaviour, ISatchel
{
    [SerializeField] private ThrowableCard cardPrefab;
    [SerializeField] private Material faceTemplate;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private Board board;
    [SerializeField] private Fighter owner;
    [SerializeField] private CardData[] starterDeck;
    [SerializeField] private Button[] tiles;
    [SerializeField] private RawImage detailArt;
    [SerializeField] private TMP_Text detailName, detailText, playLabel, status, timer;
    [SerializeField] private Button playButton;
    [SerializeField] private AudioSource tick;
    [SerializeField] private int restTurns = 2;
    [SerializeField] private float fieldScale = 3.2f, turnTime = 20f, quickTime = 8f;
    [SerializeField] private Transform head, leftPanel;
    [SerializeField] private float sideAngle = 55f, distance = 0.75f, drop = 0.35f, logAngle = 60f, logDistance = 1.6f, logDrop = 0.05f, recenterAngle = 100f;
    [SerializeField] private Color[] functionColors = { new Color(0.7f, 0.5f, 1f), new Color(1f, 0.55f, 0.3f), new Color(0.4f, 0.75f, 1f), new Color(0.4f, 1f, 0.65f) };

    public event Action<CardData> CardResolved;

    public Fighter Owner => owner;
    public float Clock => myTurn ? Mathf.Max(0f, clock) : -1f;
    private readonly List<CardData> deck = new List<CardData>(), satchel = new List<CardData>();
    private readonly List<int> rest = new List<int>();
    private int selected = -1, lastTick;
    private bool myTurn;
    private Fighter foe;
    private float yaw = float.NaN, goal = float.NaN, clock;

    private void Awake()
    {
        for (int i = 0; i < tiles.Length; i++) { int slot = i; tiles[i].onClick.AddListener(() => Select(slot)); }
        playButton.onClick.AddListener(Play);
        if (satchel.Count == 0 && starterDeck != null) Fill(new List<CardData>(starterDeck));
    }

    private void LateUpdate()
    {
        if (head == null) return;
        float look = head.eulerAngles.y;
        if (float.IsNaN(goal) || Mathf.Abs(Mathf.DeltaAngle(look, goal)) > recenterAngle) goal = look;
        yaw = float.IsNaN(yaw) ? goal : Mathf.LerpAngle(yaw, goal, Time.deltaTime * 3f);
        Place(transform, yaw + sideAngle, distance, drop);
        if (leftPanel != null) Place(leftPanel, yaw - logAngle, logDistance, logDrop);
        if (timer != null) { timer.text = myTurn ? Mathf.CeilToInt(Mathf.Max(0f, clock)).ToString() : ""; timer.color = clock <= 5f ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.86f, 0.55f); }
        if (!myTurn) return;
        clock -= Time.deltaTime;
        if (clock <= 5f && Mathf.CeilToInt(clock) != lastTick && tick != null) { lastTick = Mathf.CeilToInt(clock); tick.Play(); }
        status.text = "Your turn  ·  Libido " + owner.Libido + (owner.Has(StatusType.Slowed) ? "  ·  SLOWED +2" : "");
        if (clock <= 0f) Pass();
    }

    private void Place(Transform t, float angle, float dist, float down)
    {
        Vector3 at = head.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist + Vector3.down * down;
        t.SetPositionAndRotation(at, Quaternion.LookRotation(at - head.position));
    }

    public void Fill(List<CardData> cards) { satchel.Clear(); satchel.AddRange(cards); Reset(); }
    public void Add(CardData c) { satchel.Add(c); Give(c); }
    public void Reset() { goal = float.NaN; deck.Clear(); rest.Clear(); foreach (CardData c in satchel) Give(c); board.Register(owner, this); }
    public void Give(CardData c) { if (deck.Count >= tiles.Length) return; deck.Add(c); rest.Add(0); Refresh(); }
    public void Seal(CardData c, int turns) { for (int i = 0; i < deck.Count; i++) if (deck[i] == c) rest[i] += turns; Refresh(); }
    public void SealRandom() { if (deck.Count > 0) rest[Random.Range(0, deck.Count)] = 99; Refresh(); }
    public void Unseal() { for (int i = 0; i < rest.Count; i++) rest[i] = 0; Refresh(); }

    public CardData Take()
    {
        int i = deck.Count > 0 ? Random.Range(0, deck.Count) : -1;
        CardData c = i >= 0 ? deck[i] : null;
        if (i >= 0) { deck.RemoveAt(i); rest.RemoveAt(i); selected = -1; Refresh(); }
        return c;
    }

    public void BeginTurn(Fighter against)
    {
        foe = against; myTurn = true; clock = turnTime;
        for (int i = 0; i < rest.Count; i++) rest[i] = Mathf.Max(0, rest[i] - 1);
        Refresh();
    }

    private bool Ready(int i) => myTurn && i >= 0 && i < deck.Count && rest[i] == 0 && owner.Libido >= board.Cost(deck[i], owner);
    private bool Stuck => myTurn && !deck.Exists(c => Ready(deck.IndexOf(c)));

    private void Select(int i)
    {
        if (i >= deck.Count) return; selected = i;
        CardData c = deck[i];
        detailArt.texture = c.art;
        detailName.text = c.cardName + "   <size=65%>" + c.Group + "  ·  " + c.KindLabel + "  ·  COST " + board.Cost(c, owner) + "</size>";
        detailText.text = c.description + (rest[i] >= 50 ? "\n<color=#FF8080>Sealed for the rest of the duel.</color>" : rest[i] > 0 ? "\n<color=#E8C86A>Resting " + rest[i] + " more turn(s).</color>" : "");
        Refresh();
    }

    private void Pass() { myTurn = false; Refresh(); CardResolved?.Invoke(null); }

    private void Play()
    {
        if (Stuck) { Pass(); return; }
        if (!Ready(selected)) return;
        CardData c = deck[selected];
        rest[selected] = restTurns + 1;
        myTurn = false;
        if (turnTime - clock <= quickTime) owner.GainLibido(1);
        Refresh();
        StartCoroutine(Launch(c, turnTime - clock <= quickTime));
    }

    private IEnumerator Launch(CardData c, bool quick)
    {
        status.text = c.cardName + " is on its way..." + (quick ? "  ·  QUICK PLAY +1 Libido" : "");
        ThrowableCard card = Instantiate(cardPrefab, throwPoint.position, throwPoint.rotation);
        card.Setup(c, faceTemplate, c.art);
        yield return card.Fly(board.Aim(c, owner, foe), c.kind == CardKind.Instant ? 1f : fieldScale, head);
        Destroy(card.gameObject);
        yield return board.Play(c, owner, foe);
        Refresh();
        CardResolved?.Invoke(c);
    }

    private void Refresh()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            bool has = i < deck.Count;
            tiles[i].gameObject.SetActive(has);
            if (!has) continue;
            CardData c = deck[i];
            int cost = board.Cost(c, owner);
            tiles[i].transform.Find("Frame").GetComponent<Image>().color = i == selected ? Color.white : functionColors[(int)c.function];
            tiles[i].transform.Find("Art").GetComponent<RawImage>().texture = c.art;
            tiles[i].transform.Find("Name").GetComponent<TMP_Text>().text = c.cardName;
            tiles[i].transform.Find("Cost").GetComponent<TMP_Text>().text = cost.ToString();
            tiles[i].transform.Find("Rest").GetComponent<TMP_Text>().text = rest[i] >= 50 ? "SEALED" : rest[i] > 0 ? "REST " + rest[i] : c.kind == CardKind.Instant ? "" : "<size=60%>" + c.KindLabel + "</size>";
            tiles[i].GetComponent<CanvasGroup>().alpha = rest[i] > 0 || owner.Libido < cost ? 0.4f : 1f;
        }
        playButton.interactable = Ready(selected) || Stuck;
        playLabel.text = !myTurn ? "WAIT FOR YOUR TURN" : Stuck ? "PASS TURN" : selected < 0 ? "PICK A CARD" : Ready(selected) ? "PLAY CARD" : "CAN'T PLAY";
        if (!myTurn) status.text = "Opponent's turn";
    }
}
