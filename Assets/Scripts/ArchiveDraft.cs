using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArchiveDraft : MonoBehaviour
{
    [SerializeField] private CardData[] pool;
    [SerializeField] private Button[] familyTabs;
    [SerializeField] private Image[] tabFrames;
    [SerializeField] private Button[] variantButtons;
    [SerializeField] private RawImage[] variantArts;
    [SerializeField] private TMP_Text[] variantNames;
    [SerializeField] private TMP_Text[] variantCosts;
    [SerializeField] private TMP_Text[] variantTexts;
    [SerializeField] private GameObject[] variantChecks;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private TMP_Text familyTitle;
    [SerializeField] private TMP_Text counter;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private Button sealButton;
    [SerializeField] private int maxPicks = 10;
    [SerializeField] private int maxPerFamily = 2;
    [SerializeField] private Color tabIdle = new Color(0.75f, 0.72f, 0.8f);
    [SerializeField] private Color tabActive = new Color(1f, 0.85f, 0.5f);

    public event Action<List<CardData>> DraftDone;
    public event Action<CardData, bool> CardToggled;

    private readonly List<CardData> picked = new List<CardData>();
    private readonly List<CardData> shown = new List<CardData>();
    private CardFamily current;

    private void Awake()
    {
        for (int i = 0; i < familyTabs.Length; i++)
        {
            CardFamily f = (CardFamily)i;
            familyTabs[i].onClick.AddListener(() => ShowFamily(f));
        }
        for (int i = 0; i < variantButtons.Length; i++)
        {
            int slot = i;
            variantButtons[i].onClick.AddListener(() => Toggle(slot));
        }
        sealButton.onClick.AddListener(Seal);
    }

    private void OnEnable()
    {
        ShowFamily(current);
    }

    public void ShowFamily(CardFamily family)
    {
        current = family;
        shown.Clear();
        foreach (CardData c in pool)
        {
            if (c.family == family) shown.Add(c);
        }
        familyTitle.text = family.ToString().ToUpper() + "  ·  " + (shown.Count > 0 ? shown[0].function.ToString().ToUpper() : "");
        for (int i = 0; i < variantButtons.Length; i++)
        {
            bool has = i < shown.Count;
            variantButtons[i].gameObject.SetActive(has);
            if (!has) continue;
            variantArts[i].texture = shown[i].art;
            variantNames[i].text = shown[i].cardName;
            variantCosts[i].text = shown[i].cost.ToString();
            variantTexts[i].text = shown[i].description;
        }
        Refresh();
    }

    private void Toggle(int slot)
    {
        CardData card = shown[slot];
        if (picked.Contains(card))
        {
            picked.Remove(card);
            CardToggled?.Invoke(card, false);
        }
        else if (picked.Count >= maxPicks)
        {
            hint.text = "Your Orbit is full. Remove a card first.";
            return;
        }
        else if (CountFamily(card.family) >= maxPerFamily)
        {
            hint.text = "Only " + maxPerFamily + " cards per family.";
            return;
        }
        else
        {
            picked.Add(card);
            CardToggled?.Invoke(card, true);
        }
        hint.text = picked.Count == maxPicks ? "The Orbit is complete. Seal it." : "Choose " + (maxPicks - picked.Count) + " more.";
        Refresh();
    }

    private int CountFamily(CardFamily family)
    {
        int n = 0;
        foreach (CardData c in picked)
        {
            if (c.family == family) n++;
        }
        return n;
    }

    private void Refresh()
    {
        counter.text = "ORBIT  " + picked.Count + " / " + maxPicks;
        for (int i = 0; i < shown.Count && i < variantChecks.Length; i++) variantChecks[i].SetActive(picked.Contains(shown[i]));
        for (int i = 0; i < slotLabels.Length; i++) slotLabels[i].text = i < picked.Count ? picked[i].cardName : "·";
        for (int i = 0; i < tabFrames.Length; i++)
        {
            CardFamily f = (CardFamily)i;
            tabFrames[i].color = f == current ? tabActive : (CountFamily(f) > 0 ? Color.white : tabIdle);
        }
        sealButton.interactable = picked.Count == maxPicks;
    }

    private void Seal()
    {
        if (picked.Count != maxPicks) return;
        DraftDone?.Invoke(new List<CardData>(picked));
    }
}
