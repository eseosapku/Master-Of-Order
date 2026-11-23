using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardHand : MonoBehaviour
{
    [SerializeField] private ThrowableCard cardPrefab;
    [SerializeField] private Material faceTemplate;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private Board board;
    [SerializeField] private Fighter owner;
    [SerializeField] private CardData[] starterDeck;
    [SerializeField] private Button[] tiles;
    [SerializeField] private RawImage detailArt;
    [SerializeField] private TMP_Text detailName, detailText, playLabel, status;
    [SerializeField] private Button playButton;
    [SerializeField] private AudioSource voice;
    [SerializeField] private int restTurns = 2;
    [SerializeField] private float fieldScale = 3.2f;
    [SerializeField] private Transform head;
    [SerializeField] private float sideAngle = 48f, distance = 0.62f, drop = 0.12f, turnLimit = 70f;
    [SerializeField] private Color[] functionColors = { new Color(0.7f, 0.5f, 1f), new Color(1f, 0.55f, 0.3f), new Color(0.4f, 0.75f, 1f), new Color(0.4f, 1f, 0.65f) };

    public event Action<CardData> CardResolved;

    public Fighter Owner => owner;
    public CardData LastCard { get; private set; }

    private readonly List<CardData> deck = new List<CardData>();
    private readonly List<int> rest = new List<int>();
    private int selected = -1;
    private bool myTurn;
    private Fighter foe;
    private float yaw = float.NaN, goal = float.NaN;

    private void Awake()
    {
        for (int i = 0; i < tiles.Length; i++) { int slot = i; tiles[i].onClick.AddListener(() => Select(slot)); }
        playButton.onClick.AddListener(Play);
        if (deck.Count == 0 && starterDeck != null) Fill(new List<CardData>(starterDeck));
    }

    private void OnEnable() => board.RestAdded += OnRest;
    private void OnDisable() => board.RestAdded -= OnRest;

    private void OnRest(Fighter f, CardData c, int turns)
    {
        int i = deck.IndexOf(c);
        if (f != owner || i < 0) return;
        rest[i] += turns;
        Refresh();
    }

    private void LateUpdate()
    {
        if (head == null) return;
        float look = head.eulerAngles.y;
        if (float.IsNaN(goal) || Mathf.Abs(Mathf.DeltaAngle(look, goal)) > sideAngle + turnLimit) goal = look + sideAngle;
        yaw = float.IsNaN(yaw) ? goal : Mathf.LerpAngle(yaw, goal, Time.deltaTime * 4f);
        Vector3 at = head.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance + Vector3.down * drop;
        transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - head.position));
    }

    public void Fill(List<CardData> cards) { deck.Clear(); rest.Clear(); foreach (CardData c in cards) Add(c); }
    public void ResetRest() { for (int i = 0; i < rest.Count; i++) rest[i] = 0; Refresh(); }

    public void Add(CardData c)
    {
        deck.Add(c);
        rest.Add(0);
        Refresh();
    }

    public void BeginTurn(Fighter against)
    {
        foe = against;
        myTurn = true;
        for (int i = 0; i < rest.Count; i++) rest[i] = Mathf.Max(0, rest[i] - 1);
        Refresh();
    }

    private bool Ready(int i) => myTurn && rest[i] == 0 && owner.Libido >= board.Cost(deck[i], owner);
    private bool Stuck => myTurn && !deck.Exists(c => Ready(deck.IndexOf(c)));

    private void Select(int i)
    {
        if (i >= deck.Count) return;
        selected = i;
        CardData c = deck[i];
        detailArt.texture = c.art;
        detailName.text = c.cardName + "   <size=65%>" + c.KindLabel + "  ·  COST " + board.Cost(c, owner) + "</size>";
        string kind = c.kind == CardKind.Field ? "Stays on your field" + (c.resolve > 0 ? " with " + c.resolve + " Resolve" : "") + (c.guard ? "  ·  GUARD" : "") : c.kind == CardKind.Omen ? "Set face-down  ·  hidden until it triggers" : "Happens at once";
        detailText.text = "<color=#E8C86A><size=85%>" + kind + "</size></color>\n" + c.description;
        if (voice != null && c.voice != null) { voice.Stop(); voice.PlayOneShot(c.voice); }
        Refresh();
    }

    private void Play()
    {
        if (Stuck) { myTurn = false; LastCard = null; Refresh(); CardResolved?.Invoke(null); return; }
        if (selected < 0 || selected >= deck.Count || !Ready(selected)) return;
        CardData c = deck[selected];
        rest[selected] = restTurns + 1;
        myTurn = false;
        LastCard = c;
        Refresh();
        StartCoroutine(Launch(c));
    }

    private IEnumerator Launch(CardData c)
    {
        status.text = c.cardName + " is on its way...";
        ThrowableCard card = Instantiate(cardPrefab, throwPoint.position, throwPoint.rotation);
        card.Setup(c, faceTemplate, c.art);
        yield return card.Fly(board.Aim(c, owner, foe), c.kind == CardKind.Instant ? 1f : fieldScale, head);
        Destroy(card.gameObject);
        board.Play(c, owner, foe);
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
            tiles[i].transform.Find("Rest").GetComponent<TMP_Text>().text = rest[i] > 0 ? "REST " + rest[i] : c.kind == CardKind.Instant ? "" : "<size=60%>" + c.KindLabel + "</size>";
            tiles[i].GetComponent<CanvasGroup>().alpha = rest[i] > 0 || owner.Libido < cost ? 0.4f : 1f;
        }
        bool can = selected >= 0 && selected < deck.Count && Ready(selected);
        playButton.interactable = can || Stuck;
        playLabel.text = !myTurn ? "WAIT FOR YOUR TURN" : Stuck ? "PASS TURN" : (selected < 0 ? "PICK A CARD" : (can ? "PLAY CARD" : "CAN'T PLAY"));
        status.text = myTurn ? "Your turn  ·  Libido " + owner.Libido + (owner.Has(StatusType.Shackled) ? "  ·  SHACKLED +3" : "") : "Opponent's turn";
    }
}
