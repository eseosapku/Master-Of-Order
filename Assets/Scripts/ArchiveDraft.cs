using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArchiveDraft : MonoBehaviour
{
    [SerializeField] private CardData[] pool;
    [SerializeField] private Button tileTemplate;
    [SerializeField] private Transform content;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button[] filters;
    [SerializeField] private Button[] slots;
    [SerializeField] private RectTransform detail;
    [SerializeField] private RawImage detailArt;
    [SerializeField] private TMP_Text detailName, detailMeta, detailText, detailAction, counter, hint;
    [SerializeField] private Button detailButton, scrollUp, scrollDown, quickFill, sealButton;
    [SerializeField] private int maxPicks = 12, perGroup = 3;

    public event Action<List<CardData>> DraftDone;
    public event Action<CardData, bool> CardToggled;

    private readonly List<CardData> picked = new List<CardData>();
    private readonly List<Button> tiles = new List<Button>();
    private CardData current;

    private void Awake()
    {
        foreach (CardData c in pool)
        {
            Button t = Instantiate(tileTemplate, content);
            t.gameObject.SetActive(true);
            t.transform.Find("Art").GetComponent<RawImage>().texture = c.art;
            t.transform.Find("Cost").GetComponent<TMP_Text>().text = c.cost.ToString();
            CardData card = c;
            t.onClick.AddListener(() => Select(card));
            tiles.Add(t);
        }
        for (int i = 0; i < filters.Length; i++) { int f = i - 1; filters[i].onClick.AddListener(() => Filter(f)); }
        for (int i = 0; i < slots.Length; i++) { int s = i; slots[i].onClick.AddListener(() => RemoveSlot(s)); }
        detailButton.onClick.AddListener(Add);
        scrollUp.onClick.AddListener(() => scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + 0.2f));
        scrollDown.onClick.AddListener(() => scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - 0.2f));
        quickFill.onClick.AddListener(QuickFill);
        sealButton.onClick.AddListener(Seal);
        detail.gameObject.SetActive(false);
        hint.text = "Pick 3 cards from each group. Copies are allowed: three Strikes is fine.";
        Refresh();
    }

    private void Update()
    {
        if (detail.gameObject.activeSelf) detail.localScale = Vector3.Lerp(detail.localScale, Vector3.one, Time.deltaTime * 14f);
    }

    private int Count(Predicate<CardData> match) => picked.FindAll(match).Count;
    private int InGroup(PsycheFunction f) => Count(c => c.function == f);

    private void Filter(int function)
    {
        for (int i = 0; i < pool.Length; i++) tiles[i].gameObject.SetActive(function < 0 || (int)pool[i].function == function);
        scroll.verticalNormalizedPosition = 1f;
    }

    public void Select(CardData card)
    {
        current = card;
        detail.gameObject.SetActive(true);
        detail.localScale = Vector3.one * 0.85f;
        detailArt.texture = card.art;
        detailName.text = card.cardName;
        detailMeta.text = card.Group + "  ·  " + card.KindLabel + "  ·  Cost " + card.cost + " Libido";
        detailText.text = card.description + "\n\n<size=80%><color=#C9B98F>" + (card.kind == CardKind.Field ? "Field: stays on your side of the board" : card.kind == CardKind.Hidden ? "Hidden: set face-down, springs when the rival acts" : "Instant: happens at once") + "</color></size>";
        Refresh();
    }

    private void Add()
    {
        if (current == null) return;
        if (picked.Count >= maxPicks) hint.text = "Your satchel is full. Tap a slot below to take a card out.";
        else if (InGroup(current.function) >= perGroup) hint.text = "You already carry " + perGroup + " " + current.Group + " cards. Take one out first.";
        else
        {
            picked.Add(current);
            picked.Sort((a, b) => a.function != b.function ? a.function.CompareTo(b.function) : string.CompareOrdinal(a.cardName, b.cardName));
            CardToggled?.Invoke(current, true);
            hint.text = current.cardName + " added  ·  " + Groups();
        }
        Refresh();
    }

    private void RemoveSlot(int slot)
    {
        if (slot >= picked.Count) return;
        CardData card = picked[slot];
        picked.RemoveAt(slot);
        CardToggled?.Invoke(card, false);
        hint.text = card.cardName + " taken out  ·  " + Groups();
        Refresh();
    }

    private string Groups() => "Destruction " + InGroup(PsycheFunction.Sensation) + "/" + perGroup + "   Defense " + InGroup(PsycheFunction.Feeling) + "/" + perGroup + "   Control " + InGroup(PsycheFunction.Thinking) + "/" + perGroup + "   Fate " + InGroup(PsycheFunction.Intuition) + "/" + perGroup;

    private void QuickFill()
    {
        foreach (PsycheFunction f in Enum.GetValues(typeof(PsycheFunction)))
        {
            List<CardData> options = new List<CardData>(Array.FindAll(pool, c => c.function == f));
            while (InGroup(f) < perGroup && options.Count > 0)
            {
                CardData c = options[UnityEngine.Random.Range(0, options.Count)];
                options.Remove(c);
                picked.Add(c);
            }
        }
        picked.Sort((a, b) => a.function != b.function ? a.function.CompareTo(b.function) : string.CompareOrdinal(a.cardName, b.cardName));
        hint.text = "Satchel filled  ·  tap any slot to swap a card out.";
        Refresh();
    }

    private void Refresh()
    {
        counter.text = "SATCHEL  " + picked.Count + " / " + maxPicks;
        for (int i = 0; i < tiles.Count; i++)
        {
            int n = Count(c => c == pool[i]);
            tiles[i].transform.Find("Check").gameObject.SetActive(n > 0);
            tiles[i].transform.Find("Name").GetComponent<TMP_Text>().text = pool[i].cardName + (n > 0 ? "  ×" + n : "");
        }
        for (int i = 0; i < slots.Length; i++)
        {
            bool has = i < picked.Count;
            slots[i].GetComponentInChildren<TMP_Text>().text = has ? picked[i].cardName + "  ×" : "·";
            slots[i].interactable = has;
        }
        if (current != null) detailAction.text = "ADD TO SATCHEL" + (Count(c => c == current) > 0 ? "  (+1 copy)" : "");
        sealButton.interactable = picked.Count == maxPicks;
    }

    private void Seal()
    {
        if (picked.Count == maxPicks) DraftDone?.Invoke(new List<CardData>(picked));
    }
}
