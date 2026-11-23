using System.Collections;
using TMPro;
using UnityEngine;

public class DuelAnnouncer : MonoBehaviour
{
    [SerializeField] private TrialManager trials;
    [SerializeField] private FighterHud foeHud;
    [SerializeField] private TMP_Text banner;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private AudioSource voice;
    [SerializeField] private AudioSource gong;
    [SerializeField] private Fighter[] callers;
    [SerializeField] private AudioClip[] callouts;
    [SerializeField] private AudioClip yourTurn, frozenClip, missedClip, victoryClip, defeatClip, beginClip;

    private void OnEnable()
    {
        trials.Message += Show;
        trials.BoutStarted += OnBoutStarted;
        trials.TurnStarted += OnTurnStarted;
        trials.CardAnnounced += OnCardAnnounced;
        trials.TurnResolved += OnTurnResolved;
        trials.BoutEnded += OnBoutEnded;
    }

    private void OnDisable()
    {
        trials.Message -= Show;
        trials.BoutStarted -= OnBoutStarted;
        trials.TurnStarted -= OnTurnStarted;
        trials.CardAnnounced -= OnCardAnnounced;
        trials.TurnResolved -= OnTurnResolved;
        trials.BoutEnded -= OnBoutEnded;
    }

    private void Show(string title, string sub)
    {
        banner.text = title;
        subtitle.text = sub;
    }

    private void OnBoutStarted(Fighter foe, int index)
    {
        foeHud.Bind(foe);
        Show(index > 0 ? "DUEL " + index + "  ·  " + foe.DisplayName.ToUpper() : "THE FINAL CUT  ·  " + foe.DisplayName.ToUpper(), "Turn-based duel  ·  you play first");
        Say(beginClip);
        if (gong != null) gong.Play();
    }

    private void OnTurnStarted(Fighter f, bool frozen)
    {
        bool me = f == trials.Player;
        if (frozen)
        {
            Show((me ? "YOU ARE" : f.DisplayName.ToUpper() + " IS") + " FROZEN", "The turn is lost.");
            Say(frozenClip);
        }
        else if (me)
        {
            Show("YOUR TURN", "Turn to your hand on the right  ·  pick a card  ·  PLAY  ·  throw it");
            Say(yourTurn);
        }
        else Show(f.DisplayName.ToUpper() + "'S TURN", "Watch what they play...");
    }

    private void OnCardAnnounced(Fighter f, CardData card)
    {
        Show(f.DisplayName.ToUpper() + " PLAYS " + card.cardName.ToUpper(), card.description);
        StartCoroutine(SayPair(Callout(f), card.voice));
    }

    private void OnTurnResolved(Fighter f, CardData card, Fighter foe, bool hit, float bodyLoss, float egoLoss)
    {
        string who = f == trials.Player ? "YOU PLAYED " : f.DisplayName.ToUpper() + " PLAYED ";
        Show(who + card.cardName.ToUpper(), Outcome(card, f, foe, hit, bodyLoss, egoLoss));
        if (!hit && card.effect != EffectType.Heal && card.effect != EffectType.Shield) Say(missedClip);
    }

    private void OnBoutEnded(int index, bool won) => Say(won ? victoryClip : defeatClip);

    private string Outcome(CardData card, Fighter f, Fighter foe, bool hit, float bodyLoss, float egoLoss)
    {
        if (card.effect == EffectType.Heal) return f.DisplayName + " heals " + Mathf.RoundToInt(card.power) + " Body";
        if (card.effect == EffectType.Shield) return "A shield rises  ·  the next hit is blocked";
        if (!hit) return "It missed!";
        string target = foe == trials.Player ? "You" : foe.DisplayName;
        string r = "";
        if (bodyLoss >= 0.5f) r += "<color=#FF6B6B>-" + Mathf.RoundToInt(bodyLoss) + " Body</color>   ";
        if (egoLoss >= 0.5f) r += "<color=#7FA8FF>-" + Mathf.RoundToInt(egoLoss) + " Ego</color>   ";
        foreach (StatusEffect s in foe.Statuses) r += "<color=#E8C86A>[" + s.type + "]</color> ";
        return target + ":  " + (r.Length > 0 ? r : "no effect");
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
