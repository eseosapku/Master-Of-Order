using System;
using System.Collections;
using TMPro;
using UnityEngine;

public enum TrialPhase { Idle, Prepare, Clash, Reckoning, Result }

public class TrialManager : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private Fighter player;
    [SerializeField] private Fighter[] rivals;
    [SerializeField] private AudioClip[] rivalCallouts;
    [SerializeField] private AudioClip traitorCallout;
    [SerializeField] private CardHand hand;
    [SerializeField] private FighterHud foeHud;
    [SerializeField] private Transform rivalSpot;
    [SerializeField] private TMP_Text banner, subtitle;
    [SerializeField] private AudioSource gong, voice;
    [SerializeField] private AudioClip yourTurn, frozenClip, missedClip, victoryClip, defeatClip, beginClip;
    [SerializeField] private int libidoPerTurn = 3;
    [SerializeField] private float resolveTime = 2.5f, readTime = 2.8f, resultTime = 5f;

    public event Action<TrialPhase> PhaseChanged;
    public event Action<int> RoundStarted;
    public event Action<int, bool> BoutEnded;

    public TrialPhase Phase { get; private set; }
    public int TrialIndex { get; private set; }
    public int Round { get; private set; }
    public Fighter[] SideA { get; private set; } = new Fighter[0];
    public Fighter[] SideB { get; private set; } = new Fighter[0];

    private static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };
    private Coroutine running;
    private bool lastWin, resolved, lastHit;

    private void OnEnable() { game.StateChanged += OnState; hand.CardResolved += OnResolved; }
    private void OnDisable() { game.StateChanged -= OnState; hand.CardResolved -= OnResolved; }
    private void OnResolved(CardData c, bool hit) { resolved = true; lastHit = hit; }

    private void OnState(FloorState s)
    {
        if (s == FloorState.Hall && Phase == TrialPhase.Idle) running = StartCoroutine(Climb());
        if ((s != FloorState.Menu && s != FloorState.Archive) || running == null) return;
        StopCoroutine(running);
        running = null;
        Fighter.Paused = false;
        SetPhase(TrialPhase.Idle, "", "");
    }

    private IEnumerator Climb()
    {
        Phase = TrialPhase.Prepare;
        for (TrialIndex = 1; TrialIndex <= rivals.Length; TrialIndex++)
        {
            Fighter foe = rivals[TrialIndex - 1];
            yield return Bout(foe, "DUEL " + Numerals[TrialIndex - 1] + "  ·  " + foe.DisplayName.ToUpper(), rivalCallouts[TrialIndex - 1]);
            if (!lastWin) { yield return Fail("The floor gives way. Draft again."); yield break; }
            SetPhase(TrialPhase.Result, "DUEL " + Numerals[TrialIndex - 1] + " WON", TrialIndex + " of " + rivals.Length + " opponents defeated");
            yield return new WaitForSeconds(resultTime);
        }
        SetPhase(TrialPhase.Result, "THE FINAL CUT", "One face per pair ascends.");
        running = null;
        game.SetState(FloorState.Cut);
    }

    public void StartDuel(Fighter traitor) => running = StartCoroutine(Duel(traitor));

    private IEnumerator Duel(Fighter traitor)
    {
        SetPhase(TrialPhase.Result, "JUNO PLAYS STEAL", "She was never climbing with you.");
        yield return new WaitForSeconds(resultTime);
        yield return Bout(traitor, "THE FINAL CUT  ·  JUNO", traitorCallout);
        if (!lastWin) { yield return Fail("Juno ascends. You fall."); yield break; }
        SetPhase(TrialPhase.Result, "YOU ASCEND", "Juno falls into the fog.");
        yield return new WaitForSeconds(resultTime);
        running = null;
        game.SetState(FloorState.Reward);
    }

    private IEnumerator Bout(Fighter foe, string title, AudioClip callout)
    {
        foreach (Fighter r in rivals) r.gameObject.SetActive(r == foe);
        foe.gameObject.SetActive(true);
        foe.transform.position = rivalSpot.position;
        foe.GetComponent<FighterAI>().Face(player);
        foeHud.Bind(foe);
        SideA = new[] { player };
        SideB = new[] { foe };
        Revive(player);
        Revive(foe);
        Fighter.Paused = true;
        Round = 0;
        SetPhase(TrialPhase.Prepare, title, "Turn-based duel  ·  you play first");
        game.SetState(FloorState.Hall);
        Say(beginClip);
        if (gong != null) gong.Play();
        yield return new WaitForSeconds(3.5f);
        while (true)
        {
            Round++;
            RoundStarted?.Invoke(Round);
            yield return Turn(player, foe, null);
            if (player.IsDown || foe.IsDown) break;
            yield return Turn(foe, player, callout);
            if (player.IsDown || foe.IsDown) break;
        }
        Fighter.Paused = false;
        lastWin = !player.IsDown;
        Say(lastWin ? victoryClip : defeatClip);
        BoutEnded?.Invoke(TrialIndex, lastWin);
    }

    private IEnumerator Turn(Fighter f, Fighter foe, AudioClip callout)
    {
        f.GainLibido(libidoPerTurn);
        string who = f == player ? "YOU" : f.DisplayName.ToUpper();
        if (f.Has(StatusType.Frozen))
        {
            f.Statuses.RemoveAll(s => s.type == StatusType.Frozen);
            SetPhase(TrialPhase.Reckoning, who + (f == player ? " ARE" : " IS") + " FROZEN", "The turn is lost.");
            Say(frozenClip);
            yield return new WaitForSeconds(readTime);
            yield break;
        }
        float body = foe.Body, ego = foe.Ego;
        CardData card;
        if (f == player)
        {
            SetPhase(TrialPhase.Clash, "YOUR TURN", "Turn to your hand on the right  ·  pick a card  ·  PLAY  ·  throw it");
            Say(yourTurn);
            game.SetState(FloorState.Trial);
            resolved = false;
            hand.BeginTurn();
            while (!resolved) yield return null;
            card = hand.LastCard;
        }
        else
        {
            FighterAI ai = f.GetComponent<FighterAI>();
            card = ai.Choose(foe);
            if (card == null) { SetPhase(TrialPhase.Reckoning, who + " PASSES", "Not enough Libido."); yield return new WaitForSeconds(readTime); yield break; }
            SetPhase(TrialPhase.Clash, who + " PLAYS " + card.cardName.ToUpper(), card.description);
            StartCoroutine(SayPair(callout, card.voice));
            yield return new WaitForSeconds(readTime + (callout != null ? callout.length : 0f));
            yield return ai.Play(card, foe);
            lastHit = ai.LastHit;
        }
        Fighter.Paused = false;
        yield return new WaitForSeconds(resolveTime);
        Fighter.Paused = true;
        SetPhase(TrialPhase.Reckoning, who + (f == player ? " PLAYED " : " PLAYED ") + card.cardName.ToUpper(), Outcome(card, foe, body, ego, f));
        if (!lastHit) Say(missedClip);
        yield return new WaitForSeconds(readTime);
    }

    private string Outcome(CardData card, Fighter foe, float body, float ego, Fighter f)
    {
        if (card.effect == EffectType.Heal) return f.DisplayName + " heals " + Mathf.RoundToInt(card.power) + " Body";
        if (card.effect == EffectType.Shield) return "A shield rises. The next hit is blocked.";
        if (!lastHit) return "It missed!";
        string target = foe == player ? "You" : foe.DisplayName;
        string r = target + ": ";
        if (body - foe.Body >= 0.5f) r += "-" + Mathf.RoundToInt(body - foe.Body) + " Body  ";
        if (ego - foe.Ego >= 0.5f) r += "-" + Mathf.RoundToInt(ego - foe.Ego) + " Ego  ";
        foreach (StatusEffect s in foe.Statuses) r += "[" + s.type + "] ";
        return r == target + ": " ? target + " shrugs it off" : r;
    }

    private IEnumerator Fail(string sub)
    {
        SetPhase(TrialPhase.Result, "THE CUT", sub);
        yield return new WaitForSeconds(resultTime);
        Revive(player);
        running = null;
        SetPhase(TrialPhase.Idle, "", "");
        game.SetState(FloorState.Archive);
    }

    private static void Revive(Fighter f)
    {
        Animator an = f.GetComponent<Animator>();
        if (f.IsDown && an != null && an.runtimeAnimatorController != null) an.SetTrigger("Rise");
        f.Restore();
    }

    private void Say(AudioClip c) { if (voice != null && c != null) { voice.Stop(); voice.PlayOneShot(c); } }

    private IEnumerator SayPair(AudioClip a, AudioClip b)
    {
        Say(a);
        if (a != null) yield return new WaitForSeconds(a.length);
        Say(b);
    }

    private void SetPhase(TrialPhase p, string title, string sub)
    {
        Phase = p;
        banner.text = title;
        subtitle.text = sub;
        PhaseChanged?.Invoke(p);
    }
}
