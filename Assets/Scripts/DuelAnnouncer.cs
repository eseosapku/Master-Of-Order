using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DuelAnnouncer : MonoBehaviour
{
    [SerializeField] private TrialManager trials;
    [SerializeField] private Board board;
    [SerializeField] private FighterHud foeHud;
    [SerializeField] private TMP_Text banner;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private AudioSource voice;
    [SerializeField] private AudioSource gong;
    [SerializeField] private Fighter[] callers;
    [SerializeField] private AudioClip[] callouts;
    [SerializeField] private AudioClip yourTurn, frozenClip, missedClip, victoryClip, defeatClip, beginClip;

    private readonly List<string> notes = new List<string>();
    private Fighter[] fighters = new Fighter[0];

    private void Awake() => fighters = FindObjectsByType<Fighter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    private void OnEnable() => Listen(true);
    private void OnDisable() => Listen(false);
    private void OnNote(string s) => notes.Add(s.Replace("You's", "Your").Replace("You rewinds", "You rewind"));
    private void OnFighterNote(Fighter f, string s) => OnNote(s.Replace("You rises", "You rise"));
    private void OnBoutEnded(int index, bool won) => Say(won ? victoryClip : defeatClip);

    private void Listen(bool on)
    {
        trials.Message -= Show; trials.BoutStarted -= OnBoutStarted; trials.TurnStarted -= OnTurnStarted;
        trials.CardAnnounced -= OnCardAnnounced; trials.TurnResolved -= OnTurnResolved; trials.BoutEnded -= OnBoutEnded;
        board.Note -= OnNote; board.IntentRevealed -= OnIntent;
        foreach (Fighter f in fighters) f.Note -= OnFighterNote;
        if (!on) return;
        trials.Message += Show; trials.BoutStarted += OnBoutStarted; trials.TurnStarted += OnTurnStarted;
        trials.CardAnnounced += OnCardAnnounced; trials.TurnResolved += OnTurnResolved; trials.BoutEnded += OnBoutEnded;
        board.Note += OnNote; board.IntentRevealed += OnIntent;
        foreach (Fighter f in fighters) f.Note += OnFighterNote;
    }

    private void Show(string title, string sub)
    {
        banner.text = title;
        subtitle.text = sub;
    }

    private string TakeNotes()
    {
        string s = notes.Count > 0 ? "<color=#E8C86A>" + string.Join("  ·  ", notes) + "</color>" : "";
        notes.Clear();
        return s;
    }

    private void OnBoutStarted(Fighter foe, int index)
    {
        foeHud.Bind(foe);
        notes.Clear();
        Show(index > 0 ? "DUEL " + index + "  ·  " + foe.DisplayName.ToUpper() : "THE FINAL CUT  ·  " + foe.DisplayName.ToUpper(), "One card each turn  ·  you play first");
        Say(beginClip);
        if (gong != null) gong.Play();
    }

    private void OnTurnStarted(Fighter f, bool skipped)
    {
        bool me = f == trials.Player;
        string before = TakeNotes();
        string pre = before.Length > 0 ? before + "\n" : "";
        if (skipped) { Show((me ? "YOU LOSE" : f.DisplayName.ToUpper() + " LOSES") + " THE TURN", pre + "Frozen in place."); Say(frozenClip); }
        else if (me) { Show("YOUR TURN", pre + "Pick a card on your right-hand panel  ·  press PLAY"); Say(yourTurn); }
        else Show(f.DisplayName.ToUpper() + "'S TURN", pre + "Read the board  ·  what will they do?");
    }

    private void OnCardAnnounced(Fighter f, CardData card)
    {
        if (card.kind == CardKind.Omen)
        {
            Show(f.DisplayName.ToUpper() + " SETS A HIDDEN OMEN", "A face-down card  ·  it will trigger later. Guess what it is.");
            Say(Callout(f));
            return;
        }
        Show(f.DisplayName.ToUpper() + " PLAYS " + card.cardName.ToUpper(), "<size=80%>" + card.KindLabel + "</size>  ·  " + card.description);
        StartCoroutine(SayPair(Callout(f), card.voice));
    }

    private void OnIntent(Fighter f)
    {
        FighterAI ai = f.GetComponent<FighterAI>();
        CardData next = ai != null ? ai.Intent : null;
        notes.Add(next != null ? f.DisplayName + " plans to play " + next.cardName.ToUpper() : f.DisplayName + "'s plans are laid bare");
        if (next != null) StartCoroutine(SayPair(null, next.voice));
    }

    private void OnTurnResolved(Fighter f, CardData card, Fighter foe, float body, float ego, float foeBody, float foeEgo)
    {
        bool me = f == trials.Player;
        string title = card == null ? (me ? "YOU PASS" : f.DisplayName.ToUpper() + " PASSES") : (me ? "YOU PLAYED " : f.DisplayName.ToUpper() + " PLAYED ") + (card.kind == CardKind.Omen && !me ? "A HIDDEN OMEN" : card.cardName.ToUpper());
        string result = (Delta(foe, foeBody, foeEgo) + "   " + Delta(f, body, ego)).Trim();
        string extra = TakeNotes();
        bool missed = extra.Contains("miss") || extra.Contains("passes through") || extra.Contains("fizzles") || extra.Contains("cancelled");
        Show(title, (result.Length > 0 ? result : "No direct change") + (extra.Length > 0 ? "\n" + extra : ""));
        if (missed) Say(missedClip);
    }

    private string Delta(Fighter x, float body, float ego)
    {
        string r = "";
        if (body <= -0.5f) r += "<color=#FF6B6B>-" + Mathf.RoundToInt(-body) + " Body</color> ";
        if (body >= 0.5f) r += "<color=#7CFFB0>+" + Mathf.RoundToInt(body) + " Body</color> ";
        if (ego <= -0.5f) r += "<color=#7FA8FF>-" + Mathf.RoundToInt(-ego) + " Ego</color> ";
        foreach (KeyValuePair<StatusType, int> s in x.Statuses) r += "<color=#E8C86A>[" + s.Key + "]</color> ";
        return r.Length > 0 ? (x == trials.Player ? "You" : x.DisplayName) + ":  " + r : "";
    }

    private AudioClip Callout(Fighter f)
    {
        for (int i = 0; i < callers.Length && i < callouts.Length; i++) if (callers[i] == f) return callouts[i];
        return null;
    }

    private void Say(AudioClip c)
    {
        if (voice == null || c == null) return;
        voice.Stop();
        voice.PlayOneShot(c);
    }

    private IEnumerator SayPair(AudioClip a, AudioClip b)
    {
        Say(a);
        if (a != null) yield return new WaitForSeconds(a.length);
        Say(b);
    }
}
