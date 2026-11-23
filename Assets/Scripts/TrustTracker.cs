using System;
using UnityEngine;

public class TrustTracker : MonoBehaviour
{
    [SerializeField] private TrialManager trials;
    [SerializeField] private GameManager game;
    [SerializeField] private CutFloor cut;
    [SerializeField] private Fighter juno;
    [SerializeField] private CardData gift;
    [SerializeField] private int trust = 40;
    [SerializeField] private int threshold = 50;
    [SerializeField] private int strongWin = 8, closeWin = 2, loss = 4;

    public event Action<int> TrustChanged;

    public int Trust => trust;
    public bool IsHigh => trust >= threshold;

    private void OnEnable()
    {
        trials.BoutEnded += OnBout;
        game.StateChanged += OnState;
    }

    private void OnDisable()
    {
        trials.BoutEnded -= OnBout;
        game.StateChanged -= OnState;
    }

    private void OnBout(int trial, bool won)
    {
        if (trial == 0 || juno.Team != 0) return;
        Shift(won ? (trials.Player.BodyRatio >= 0.5f ? strongWin : closeWin) : -loss);
    }

    private void OnState(FloorState s)
    {
        if (s == FloorState.Archive) juno.Team = 0;
        if (s != FloorState.Cut) return;
        if (IsHigh)
        {
            cut.Sacrifice(juno, gift);
            return;
        }
        juno.Team = 1;
        trials.StartDuel(juno);
    }

    private void Shift(int amount)
    {
        trust = Mathf.Clamp(trust + amount, 0, 100);
        TrustChanged?.Invoke(trust);
    }
}
