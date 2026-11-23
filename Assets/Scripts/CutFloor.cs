using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CutFloor : MonoBehaviour
{
    [SerializeField] private TrialManager trials;
    [SerializeField] private GameManager game;
    [SerializeField] private CardHand orbit;
    [SerializeField] private Rigidbody tilePrefab;
    [SerializeField] private TMP_Text banner;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private AudioSource crack;
    [SerializeField] private float tileSize = 0.66f;
    [SerializeField] private float shakeTime = 0.6f;
    [SerializeField] private float fallDepth = 9f;
    [SerializeField] private float fallTime = 1.6f;

    public event Action<Fighter> Fell;

    private readonly List<Rigidbody> tiles = new List<Rigidbody>();
    private readonly Dictionary<Fighter, Vector3> homes = new Dictionary<Fighter, Vector3>();

    private void OnEnable()
    {
        trials.BoutEnded += OnBout;
        trials.RoundStarted += OnRound;
        game.StateChanged += OnState;
    }

    private void OnDisable()
    {
        trials.BoutEnded -= OnBout;
        trials.RoundStarted -= OnRound;
        game.StateChanged -= OnState;
    }

    private void OnBout(int trial, bool won)
    {
        foreach (Fighter f in won ? trials.SideB : trials.SideA) Collapse(f);
        foreach (Fighter f in won ? trials.SideA : trials.SideB) { Animator an = f.GetComponent<Animator>(); if (an != null && an.runtimeAnimatorController != null) an.SetTrigger("Cheer"); }
    }

    private void OnRound(int round)
    {
        if (round != 1) return;
        foreach (KeyValuePair<Fighter, Vector3> h in homes)
        {
            if (System.Array.IndexOf(trials.SideA, h.Key) < 0 && System.Array.IndexOf(trials.SideB, h.Key) < 0) continue;
            h.Key.transform.position = h.Value;
            h.Key.gameObject.SetActive(true);
        }
        homes.Clear();
        foreach (Rigidbody t in tiles) if (t != null) Destroy(t.gameObject);
        tiles.Clear();
    }

    private void OnState(FloorState s)
    {
        if (s != FloorState.Reward) return;
        banner.text = "FLOOR I CLEARED";
        subtitle.text = "Orbit slot 11 unlocked  ·  your first Complex stirs";
    }

    public void Collapse(Fighter f)
    {
        StartCoroutine(Fall(f));
    }

    private IEnumerator Fall(Fighter f)
    {
        Vector3 at = f.transform.position;
        if (!homes.ContainsKey(f)) homes[f] = at;
        if (crack != null)
        {
            crack.transform.position = at;
            crack.Play();
        }
        List<Rigidbody> mine = new List<Rigidbody>();
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Rigidbody t = Instantiate(tilePrefab, new Vector3(at.x + x * tileSize, 0.07f, at.z + z * tileSize), Quaternion.Euler(0f, UnityEngine.Random.Range(-6f, 6f), 0f));
                t.isKinematic = true;
                mine.Add(t);
                tiles.Add(t);
            }
        }
        for (float s = 0f; s < shakeTime; s += Time.deltaTime)
        {
            foreach (Rigidbody t in mine) t.transform.position += UnityEngine.Random.insideUnitSphere * 0.004f;
            yield return null;
        }
        foreach (Rigidbody t in mine)
        {
            t.isKinematic = false;
            t.AddTorque(UnityEngine.Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
        }
        Fell?.Invoke(f);
        if (f.GetComponentInChildren<Camera>() != null) yield break;
        Animator an = f.GetComponent<Animator>();
        if (an != null && an.runtimeAnimatorController != null && !f.IsDown) an.SetTrigger("Knockback");
        for (float s = 0f; s < fallTime; s += Time.deltaTime)
        {
            f.transform.position = at + Vector3.down * fallDepth * (s / fallTime) * (s / fallTime);
            yield return null;
        }
        f.gameObject.SetActive(false);
    }

    public void Sacrifice(Fighter juno, CardData gift)
    {
        StartCoroutine(Gift(juno, gift));
    }

    private IEnumerator Gift(Fighter juno, CardData gift)
    {
        banner.text = "THE FINAL CUT";
        subtitle.text = "Juno throws you her card: Understudy.";
        Animator an = juno.GetComponent<Animator>();
        if (an != null) an.SetTrigger("Throw");
        yield return new WaitForSeconds(1.6f);
        orbit.Add(gift);
        subtitle.text = "\"Take the next hit meant for you. Forever.\"";
        yield return new WaitForSeconds(2.5f);
        subtitle.text = "Juno steps off the edge.";
        Collapse(juno);
        yield return new WaitForSeconds(4f);
        game.SetState(FloorState.Reward);
    }
}
