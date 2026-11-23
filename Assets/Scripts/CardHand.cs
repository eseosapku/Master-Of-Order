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
    [SerializeField] private GameManager game;
    [SerializeField] private Fighter owner;
    [SerializeField] private CardData[] starterDeck;
    [SerializeField] private Button[] tiles;
    [SerializeField] private RawImage detailArt;
    [SerializeField] private TMP_Text detailName, detailText, playLabel, status;
    [SerializeField] private Button playButton;
    [SerializeField] private AudioSource voice;
    [SerializeField] private int restTurns = 2;
    [SerializeField] private float throwWindow = 20f;
    [SerializeField] private Transform head;
    [SerializeField] private float sideAngle = 48f, distance = 0.62f, drop = 0.12f, turnLimit = 70f;
    [SerializeField] private Color[] functionColors = { new Color(0.7f, 0.5f, 1f), new Color(1f, 0.55f, 0.3f), new Color(0.4f, 0.75f, 1f), new Color(0.4f, 1f, 0.65f) };

    public event Action<ThrowableCard, Collider, float> CardLanded;
    public event Action<ThrowableCard> CardThrown;
    public event Action<CardData, bool> CardResolved;

    public Fighter Owner => owner;
    public CardData LastCard { get; private set; }

    private readonly List<CardData> deck = new List<CardData>();
    private readonly List<int> rest = new List<int>();
    private int selected = -1;
    private bool myTurn, hit;
    private float yaw = float.NaN, goal = float.NaN;

    private void Awake()
    {
        for (int i = 0; i < tiles.Length; i++) { int slot = i; tiles[i].onClick.AddListener(() => Select(slot)); }
        playButton.onClick.AddListener(Play);
        if (deck.Count == 0 && starterDeck != null) Fill(new List<CardData>(starterDeck));
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

    public void Add(CardData c)
    {
        deck.Add(c);
        rest.Add(0);
        Refresh();
    }

    public void BeginTurn()
    {
        myTurn = true;
        for (int i = 0; i < rest.Count; i++) rest[i] = Mathf.Max(0, rest[i] - 1);
        Refresh();
    }

    private bool Ready(int i) => myTurn && rest[i] == 0 && owner.Libido >= deck[i].cost;

    private void Select(int i)
    {
        if (i >= deck.Count) return;
        selected = i;
        CardData c = deck[i];
        detailArt.texture = c.art;
        detailName.text = c.cardName + "   <size=70%>COST " + c.cost + "</size>";
        detailText.text = c.description + (c.effect == EffectType.Freeze ? " In a duel, they lose their next turn." : "");
        if (voice != null && c.voice != null) { voice.Stop(); voice.PlayOneShot(c.voice); }
        Refresh();
    }

    private void Play()
    {
        if (selected < 0 || selected >= deck.Count || !Ready(selected)) return;
        CardData c = deck[selected];
        owner.Spend(c.cost);
        rest[selected] = restTurns + 1;
        myTurn = false;
        LastCard = c;
        if (c.effect == EffectType.Heal) owner.Heal(c.power);
        if (c.effect == EffectType.Shield) owner.AddStatus(StatusType.Shielded, 0f, 999f);
        Refresh();
        if (c.effect == EffectType.Heal || c.effect == EffectType.Shield) { CardResolved?.Invoke(c, true); return; }
        ThrowableCard card = Instantiate(cardPrefab, throwPoint.position, throwPoint.rotation);
        card.Setup(c, faceTemplate, game != null ? game.AimAssist : 1f, 999f);
        card.Thrown += t => CardThrown?.Invoke(t);
        card.Landed += (k, target, speed) => { hit = target.GetComponentInParent<Fighter>() != null; CardLanded?.Invoke(k, target, speed); };
        hit = false;
        StartCoroutine(Watch(card));
    }

    private IEnumerator Watch(ThrowableCard card)
    {
        status.text = "Your card is floating by this panel  ·  grab it and throw!";
        for (float t = 0f; card.Floating && t < throwWindow; t += Time.deltaTime) yield return null;
        for (float w = 0f; w < 3.5f && !card.Floating && !hit; w += Time.deltaTime) yield return null;
        yield return new WaitForSeconds(0.3f);
        Destroy(card.gameObject);
        CardResolved?.Invoke(LastCard, hit);
    }

    private void Refresh()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            bool has = i < deck.Count;
            tiles[i].gameObject.SetActive(has);
            if (!has) continue;
            CardData c = deck[i];
            Color col = functionColors[(int)c.function];
            tiles[i].transform.Find("Frame").GetComponent<Image>().color = i == selected ? Color.white : col;
            tiles[i].transform.Find("Art").GetComponent<RawImage>().texture = c.art;
            tiles[i].transform.Find("Name").GetComponent<TMP_Text>().text = c.cardName;
            tiles[i].transform.Find("Cost").GetComponent<TMP_Text>().text = c.cost.ToString();
            tiles[i].transform.Find("Rest").GetComponent<TMP_Text>().text = rest[i] > 0 ? "REST " + rest[i] : "";
            tiles[i].GetComponent<CanvasGroup>().alpha = rest[i] > 0 || owner.Libido < c.cost ? 0.4f : 1f;
        }
        bool can = selected >= 0 && selected < deck.Count && Ready(selected);
        playButton.interactable = can;
        playLabel.text = !myTurn ? "WAIT FOR YOUR TURN" : (selected < 0 ? "PICK A CARD" : (can ? "PLAY CARD" : "CAN'T PLAY"));
        status.text = myTurn ? "Your turn  ·  Libido " + owner.Libido : "Opponent's turn";
    }

    public static Collider FindTarget(Vector3 from, Vector3 v, float cone)
    {
        Collider best = null;
        foreach (Collider c in Physics.OverlapSphere(from, 25f))
        {
            float a = Vector3.Angle(v, c.bounds.center - from);
            if (c.CompareTag("Target") && a < cone) { cone = a; best = c; }
        }
        return best;
    }
}
