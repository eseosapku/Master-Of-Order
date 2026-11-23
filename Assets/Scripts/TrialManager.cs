using System;
using System.Collections;
using UnityEngine;

public enum TrialPhase { Idle, Prepare, Clash, Reckoning, Result }

public class TrialManager : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private Fighter player;
    [SerializeField] private Fighter[] rivals;
    [SerializeField] private CardHand hand;
    [SerializeField] private Transform rivalSpot;
    [SerializeField] private int libidoPerTurn = 3;
    [SerializeField] private float resolveTime = 2.5f, readTime = 3.2f, resultTime = 5f;

    public event Action<string, string> Message;
    public event Action<Fighter, int> BoutStarted;
    public event Action<Fighter, bool> TurnStarted;
    public event Action<Fighter, CardData> CardAnnounced;
    public event Action<Fighter, CardData, Fighter, bool, float, float> TurnResolved;
    public event Action<int> RoundStarted;
    public event Action<int, bool> BoutEnded;

    public TrialPhase Phase { get; private set; }
    public int TrialIndex { get; private set; }
    public int Round { get; private set; }
    public Fighter Player => player;
    public Fighter[] SideA { get; private set; } = new Fighter[0];
    public Fighter[] SideB { get; private set; } = new Fighter[0];

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
        Halt();
    }

    private void Halt() { running = null; Fighter.Paused = false; Phase = TrialPhase.Idle; Message?.Invoke("", ""); }
    private void Finish(FloorState s) { running = null; game.SetState(s); }
    private IEnumerator Show(string title, string sub, float wait) { Message?.Invoke(title, sub); yield return new WaitForSeconds(wait); }

    private IEnumerator Climb()
    {
        Phase = TrialPhase.Prepare;
        for (TrialIndex = 1; TrialIndex <= rivals.Length; TrialIndex++)
        {
            yield return Bout(rivals[TrialIndex - 1]);
            if (!lastWin) { yield return Fail("The floor gives way. Draft again."); yield break; }
            yield return Show("DUEL WON", TrialIndex + " of " + rivals.Length + " opponents defeated", resultTime);
        }
        Message?.Invoke("THE FINAL CUT", "One face per pair ascends.");
        Finish(FloorState.Cut);
    }

    public void StartDuel(Fighter traitor) => running = StartCoroutine(Duel(traitor));

    private IEnumerator Duel(Fighter traitor)
    {
        yield return Show("JUNO PLAYS STEAL", "She was never climbing with you.", resultTime);
        TrialIndex = 0;
        yield return Bout(traitor);
        if (!lastWin) { yield return Fail("Juno ascends. You fall."); yield break; }
        yield return Show("YOU ASCEND", "Juno falls into the fog.", resultTime);
        Finish(FloorState.Reward);
    }

    private IEnumerator Bout(Fighter foe)
    {
        foreach (Fighter r in rivals) r.gameObject.SetActive(r == foe);
        foe.gameObject.SetActive(true);
        foe.transform.position = rivalSpot.position;
        foe.GetComponent<FighterAI>().Face(player);
        SideA = new[] { player }; SideB = new[] { foe };
        foreach (Fighter f in new[] { player, foe }) Revive(f);
        Fighter.Paused = true;
        Round = 0;
        game.SetState(FloorState.Hall);
        BoutStarted?.Invoke(foe, TrialIndex);
        yield return new WaitForSeconds(3.5f);
        while (!player.IsDown && !foe.IsDown)
        {
            Round++;
            RoundStarted?.Invoke(Round);
            yield return Turn(player, foe);
            if (!player.IsDown && !foe.IsDown) yield return Turn(foe, player);
        }
        Fighter.Paused = false;
        lastWin = !player.IsDown;
        BoutEnded?.Invoke(TrialIndex, lastWin);
    }

    private IEnumerator Turn(Fighter f, Fighter foe)
    {
        f.GainLibido(libidoPerTurn);
        bool frozen = f.Has(StatusType.Frozen);
        Phase = TrialPhase.Clash;
        TurnStarted?.Invoke(f, frozen);
        if (frozen) { f.Statuses.RemoveAll(s => s.type == StatusType.Frozen); yield return new WaitForSeconds(readTime); yield break; }
        float body = foe.Body, ego = foe.Ego;
        CardData card;
        if (f == player)
        {
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
            if (card == null) { yield return Show(f.DisplayName.ToUpper() + " PASSES", "Not enough Libido.", readTime); yield break; }
            CardAnnounced?.Invoke(f, card);
            yield return new WaitForSeconds(readTime + 1f);
            yield return ai.Play(card, foe);
            lastHit = ai.LastHit;
        }
        Fighter.Paused = false;
        yield return new WaitForSeconds(resolveTime);
        Fighter.Paused = true;
        TurnResolved?.Invoke(f, card, foe, lastHit, body - foe.Body, ego - foe.Ego);
        yield return new WaitForSeconds(readTime);
    }

    private IEnumerator Fail(string sub)
    {
        yield return Show("THE CUT", sub, resultTime);
        Revive(player);
        Halt();
        game.SetState(FloorState.Archive);
    }

    private static void Revive(Fighter f)
    {
        Animator an = f.GetComponent<Animator>();
        if (f.IsDown && an != null && an.runtimeAnimatorController != null) an.SetTrigger("Rise");
        f.Restore();
    }
}
