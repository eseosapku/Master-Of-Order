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
    [SerializeField] private float prepareTime = 4f;
    [SerializeField] private float clashTime = 20f;
    [SerializeField] private float reckonTime = 3f;
    [SerializeField] private float resultTime = 5f;

    public event Action<TrialPhase> PhaseChanged;
    public event Action<int> RoundStarted;
    public event Action<int, bool> BoutEnded;

    public TrialPhase Phase { get; private set; }
    public int TrialIndex { get; private set; }
    public int Round { get; private set; }
    public float TimeLeft { get; private set; }

    private static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };
    private Coroutine running;

    private void OnEnable() => game.StateChanged += OnState;
    private void OnDisable() => game.StateChanged -= OnState;

    private void OnState(FloorState s)
    {
        if (s == FloorState.Hall && Phase == TrialPhase.Idle) running = StartCoroutine(Climb());
        if ((s == FloorState.Menu || s == FloorState.Archive) && running != null)
        {
            StopCoroutine(running);
            running = null;
            SetPhase(TrialPhase.Idle, "", "");
        }
    }

    private IEnumerator Climb()
    {
        int won = 0;
        Phase = TrialPhase.Prepare;
        for (TrialIndex = 1; won < trialsToWin; TrialIndex++)
        {
            ResetAll();
            Round = 0;
            bool? result = null;
            while (result == null)
            {
                Round++;
                foreach (Fighter f in All()) if (!f.IsDown) f.GainLibido(libidoPerRound);
                RoundStarted?.Invoke(Round);
                SetPhase(TrialPhase.Prepare, "TRIAL " + Numeral(TrialIndex), "Round " + Round + "  ·  ready your Orbit");
                game.SetState(FloorState.Hall);
                yield return new WaitForSeconds(prepareTime);
                if (gong != null) gong.Play();
                SetPhase(TrialPhase.Clash, "CLASH", "");
                game.SetState(FloorState.Trial);
                TimeLeft = clashTime;
                while (TimeLeft > 0f && !Down(allies) && !Down(rivals))
                {
                    TimeLeft -= Time.deltaTime;
                    subtitle.text = Mathf.CeilToInt(TimeLeft).ToString();
                    yield return null;
                }
                game.SetState(FloorState.Hall);
                SetPhase(TrialPhase.Reckoning, "RECKONING", "Statuses settle");
                yield return new WaitForSeconds(reckonTime);
                if (Down(allies)) result = false;
                else if (Down(rivals)) result = true;
            }
            BoutEnded?.Invoke(TrialIndex, result.Value);
            if (!result.Value)
            {
                SetPhase(TrialPhase.Result, "THE CUT", "The floor gives way. Draft again.");
                yield return new WaitForSeconds(resultTime);
                ResetAll();
                running = null;
                SetPhase(TrialPhase.Idle, "", "");
                game.SetState(FloorState.Archive);
                yield break;
            }
            won++;
            SetPhase(TrialPhase.Result, "TRIAL " + Numeral(TrialIndex) + " WON", won + " of " + trialsToWin + " trials");
            yield return new WaitForSeconds(resultTime);
        }
        SetPhase(TrialPhase.Result, "THE FINAL CUT", "One face per pair ascends.");
        game.SetState(FloorState.Cut);
        running = null;
    }

    private Fighter[] All()
    {
        Fighter[] all = new Fighter[allies.Length + rivals.Length];
        allies.CopyTo(all, 0);
        rivals.CopyTo(all, allies.Length);
        return all;
    }

    private static bool Down(Fighter[] team)
    {
        foreach (Fighter f in team) if (!f.IsDown) return false;
        return true;
    }

    private void ResetAll()
    {
        foreach (Fighter f in All())
        {
            Animator an = f.GetComponent<Animator>();
            if (f.IsDown && an != null && an.runtimeAnimatorController != null) an.SetTrigger("Rise");
            f.Restore();
        }
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
