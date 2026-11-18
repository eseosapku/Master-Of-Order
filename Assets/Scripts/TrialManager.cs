using System;
using System.Collections;
using TMPro;
using UnityEngine;

public enum TrialPhase { Idle, Prepare, Clash, Reckoning, Result }

public class TrialManager : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private Fighter[] allies;
    [SerializeField] private Fighter[] rivals;
    [SerializeField] private TMP_Text banner;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private AudioSource gong;
    [SerializeField] private int trialsToWin = 3;
    [SerializeField] private int libidoPerRound = 3;
    [SerializeField] private float prepareTime = 4f, clashTime = 20f, reckonTime = 3f, resultTime = 5f;

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
    private bool lastWin;

    private void OnEnable() => game.StateChanged += OnState;
    private void OnDisable() => game.StateChanged -= OnState;

    private void OnState(FloorState s)
    {
        if (s == FloorState.Hall && Phase == TrialPhase.Idle) running = StartCoroutine(Climb());
        if ((s != FloorState.Menu && s != FloorState.Archive) || running == null) return;
        StopCoroutine(running);
        running = null;
        SetPhase(TrialPhase.Idle, "", "");
    }

    private IEnumerator Climb()
    {
        Phase = TrialPhase.Prepare;
        int won = 0;
        for (TrialIndex = 1; won < trialsToWin; TrialIndex++)
        {
            yield return Bout(allies, rivals, "TRIAL " + Numeral(TrialIndex));
            if (!lastWin)
            {
                yield return Fail("The floor gives way. Draft again.");
                yield break;
            }
            won++;
            SetPhase(TrialPhase.Result, "TRIAL " + Numeral(TrialIndex) + " WON", won + " of " + trialsToWin + " trials");
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
        yield return Bout(new[] { allies[0] }, new[] { traitor }, "THE FINAL CUT");
        if (!lastWin)
        {
            yield return Fail("Juno ascends. You fall.");
            yield break;
        }
        SetPhase(TrialPhase.Result, "YOU ASCEND", "Juno falls into the fog.");
        yield return new WaitForSeconds(resultTime);
        running = null;
        game.SetState(FloorState.Reward);
    }

    private IEnumerator Bout(Fighter[] a, Fighter[] b, string title)
    {
        SideA = a; SideB = b;
        ResetAll();
        Round = 0;
        while (true)
        {
            Round++;
            foreach (Fighter f in a) if (!f.IsDown) f.GainLibido(libidoPerRound);
            foreach (Fighter f in b) if (!f.IsDown) f.GainLibido(libidoPerRound);
            RoundStarted?.Invoke(Round);
            SetPhase(TrialPhase.Prepare, title, "Round " + Round + "  ·  ready your Orbit");
            game.SetState(FloorState.Hall);
            yield return new WaitForSeconds(prepareTime);
            if (gong != null) gong.Play();
            SetPhase(TrialPhase.Clash, "CLASH", "");
            game.SetState(FloorState.Trial);
            for (float left = clashTime; left > 0f && !Down(a) && !Down(b); left -= Time.deltaTime)
            {
                subtitle.text = Mathf.CeilToInt(left).ToString();
                yield return null;
            }
            game.SetState(FloorState.Hall);
            SetPhase(TrialPhase.Reckoning, "RECKONING", "Statuses settle");
            yield return new WaitForSeconds(reckonTime);
            if (Down(a) || Down(b)) break;
        }
        lastWin = !Down(a);
        BoutEnded?.Invoke(TrialIndex, lastWin);
    }

    private IEnumerator Fail(string sub)
    {
        SetPhase(TrialPhase.Result, "THE CUT", sub);
        yield return new WaitForSeconds(resultTime);
        ResetAll();
        running = null;
        SetPhase(TrialPhase.Idle, "", "");
        game.SetState(FloorState.Archive);
    }

    private static bool Down(Fighter[] team) => Array.TrueForAll(team, f => f.IsDown);

    private void ResetAll()
    {
        foreach (Fighter f in SideA) Revive(f);
        foreach (Fighter f in SideB) Revive(f);
    }

    private static void Revive(Fighter f)
    {
        Animator an = f.GetComponent<Animator>();
        if (f.IsDown && an != null && an.runtimeAnimatorController != null) an.SetTrigger("Rise");
        f.Restore();
    }

    private void SetPhase(TrialPhase p, string title, string sub)
    {
        Phase = p;
        banner.text = title;
        subtitle.text = sub;
        PhaseChanged?.Invoke(p);
    }

    private static string Numeral(int n) => n >= 1 && n <= Numerals.Length ? Numerals[n - 1] : n.ToString();
}
