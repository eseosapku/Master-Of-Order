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
    [SerializeField] private int maxPicks = 10;
    [SerializeField] private int maxPerFamily = 2;

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
            t.transform.Find("Name").GetComponent<TMP_Text>().text = c.cardName;
            t.transform.Find("Cost").GetComponent<TMP_Text>().text = c.cost.ToString();
            CardData card = c;
            t.onClick.AddListener(() => Select(card));
            tiles.Add(t);
        }
        for (int i = 0; i < filters.Length; i++)
        {
            int f = i - 1;
            filters[i].onClick.AddListener(() => Filter(f));
        }
        for (int i = 0; i < slots.Length; i++)
        {
            int s = i;
            slots[i].onClick.AddListener(() => RemoveSlot(s));
        }
        detailButton.onClick.AddListener(() => Toggle(current));
        scrollUp.onClick.AddListener(() => scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + 0.15f));
        scrollDown.onClick.AddListener(() => scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - 0.15f));
        quickFill.onClick.AddListener(QuickFill);
        sealButton.onClick.AddListener(Seal);
        detail.gameObject.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (detail.gameObject.activeSelf) detail.localScale = Vector3.Lerp(detail.localScale, Vector3.one, Time.deltaTime * 14f);
    }

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
        detailMeta.text = card.family + "  ·  " + card.function + "  ·  Cost " + card.cost + " Libido";
        detailText.text = card.description + "\n\n<size=80%><color=#C9B98F>Effect: " + card.effect + "</color></size>";
        Refresh();
    }

    private void Toggle(CardData card)
    {
        if (card == null) return;
        if (picked.Contains(card))
        {
            picked.Remove(card);
            CardToggled?.Invoke(card, false);
            hint.text = card.cardName + " removed from your Orbit.";
        }
        else if (picked.Count >= maxPicks) hint.text = "Your Orbit is full. Click a slot below to remove a card.";
        else if (CountFamily(card.family) >= maxPerFamily) hint.text = "Only " + maxPerFamily + " " + card.family + " cards allowed.";
        else
        {
            picked.Add(card);
            CardToggled?.Invoke(card, true);
            hint.text = card.cardName + " added. " + (maxPicks - picked.Count) + " to go.";
        }
        Refresh();
    }

    private void RemoveSlot(int slot)
    {
        if (slot >= picked.Count) return;
        CardData card = picked[slot];
        Select(card);
        Toggle(card);
    }

    private void QuickFill()
    {
        List<CardData> options = new List<CardData>(pool);
        while (picked.Count < maxPicks && options.Count > 0)
        {
            CardData c = options[UnityEngine.Random.Range(0, options.Count)];
            options.Remove(c);
            if (!picked.Contains(c) && CountFamily(c.family) < maxPerFamily) picked.Add(c);
        }
        hint.text = "Orbit filled. Click any slot to swap a card out.";
        Refresh();
    }

    private int CountFamily(CardFamily family)
    {
        int n = 0;
        foreach (CardData c in picked) if (c.family == family) n++;
        return n;
    }

    private void Refresh()
    {
        counter.text = "ORBIT  " + picked.Count + " / " + maxPicks;
        for (int i = 0; i < tiles.Count; i++) tiles[i].transform.Find("Check").gameObject.SetActive(picked.Contains(pool[i]));
        for (int i = 0; i < slots.Length; i++)
        {
            bool has = i < picked.Count;
            slots[i].GetComponentInChildren<TMP_Text>().text = has ? picked[i].cardName + "  ×" : "·";
            slots[i].interactable = has;
        }
        if (current != null) detailAction.text = picked.Contains(current) ? "REMOVE FROM ORBIT" : "ADD TO ORBIT";
        sealButton.interactable = picked.Count == maxPicks;
    }

    private void Seal()
    {
        if (picked.Count == maxPicks) DraftDone?.Invoke(new List<CardData>(picked));
    }
}
