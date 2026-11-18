using System;
using UnityEngine;

public class TrustTracker : MonoBehaviour
{
    [SerializeField] private CardOrbit orbit;
    [SerializeField] private TrialManager trials;
    [SerializeField] private GameManager game;
    [SerializeField] private CutFloor cut;
    [SerializeField] private Fighter juno;
    [SerializeField] private CardData gift;
    [SerializeField] private int trust = 40;
    [SerializeField] private int threshold = 50;
    [SerializeField] private int supportGain = 12;
    [SerializeField] private int betrayalLoss = 10;

    public event Action<int> TrustChanged;

    public int Trust => trust;
    public bool IsHigh => trust >= threshold;

    private void OnEnable()
    {
        orbit.CardLanded += OnLanded;
        trials.BoutEnded += OnBout;
        game.StateChanged += OnState;
    }

    private void OnDisable()
    {
        orbit.CardLanded -= OnLanded;
        trials.BoutEnded -= OnBout;
        game.StateChanged -= OnState;
    }

    private void OnLanded(ThrowableCard card, Collider hit, float speed)
    {
        Fighter f = hit.GetComponentInParent<Fighter>();
        if (f == null) return;
        if (f == juno)
        {
            EffectType e = card.Data.effect;
            bool support = e == EffectType.Heal || e == EffectType.Shield || e == EffectType.Link || e == EffectType.Dispel;
            Shift(support ? supportGain : -betrayalLoss);
        }
        else if (f.Team == 1) Shift(1);
    }

    private void OnBout(int trial, bool won)
    {
        if (trial == 0 || juno.Team != 0) return;
        Shift(won ? (juno.IsDown ? 2 : 6) : -4);
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
