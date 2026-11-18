using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FighterAI : MonoBehaviour
{
    [SerializeField] private Fighter fighter;
    [SerializeField] private Animator animator;
    [SerializeField] private GameManager game;
    [SerializeField] private Renderer cardVisual;
    [SerializeField] private CardData[] deck;
    [SerializeField] private float thinkMin = 2.5f;
    [SerializeField] private float thinkMax = 4.5f;
    [SerializeField] private float windup = 0.42f;
    [SerializeField] private float flightSpeed = 9f;
    [SerializeField] private float inaccuracy = 0.3f;
    [SerializeField] private float hitRadius = 0.32f;
    [SerializeField] private float turnSpeed = 6f;

    public event Action<CardData, Fighter, Collider, Vector3, float> CardHit;

    private readonly List<Fighter> foes = new List<Fighter>();
    private MaterialPropertyBlock block;
    private Transform hand;
    private Fighter target;
    private float nextThink;
    private bool throwing;

    private void Start()
    {
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        block = new MaterialPropertyBlock();
        cardVisual.gameObject.SetActive(false);
        nextThink = Time.time + UnityEngine.Random.Range(1.5f, 3.5f);
        foreach (Fighter f in FindObjectsByType<Fighter>(FindObjectsSortMode.None))
        {
            if (f != fighter) foes.Add(f);
        }
    }

    private bool Engaged => game == null || game.State == FloorState.Trial;

    private void Update()
    {
        animator.SetBool("Showcase", !Engaged);
        if (!Engaged || fighter.IsDown) return;
        if (target != null)
        {
            Vector3 away = transform.position - target.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), Time.deltaTime * turnSpeed);
        }
        if (throwing || Time.time < nextThink) return;
        nextThink = Time.time + UnityEngine.Random.Range(thinkMin, thinkMax);
        target = PickTarget();
        CardData card = PickCard();
        if (target == null || card == null || !fighter.Spend(card.cost)) return;
        StartCoroutine(Throw(card, target));
    }

    private Fighter PickTarget()
    {
        Fighter best = null;
        float bestDist = float.MaxValue;
        foreach (Fighter f in foes)
        {
            float d = Vector3.Distance(f.transform.position, transform.position);
            if (!f.IsDown && f.isActiveAndEnabled && f.Team != fighter.Team && f.Team != 2 && d < bestDist)
            {
                bestDist = d;
                best = f;
            }
        }
        return best;
    }

    private CardData PickCard()
    {
        List<CardData> options = new List<CardData>();
        foreach (CardData c in deck)
        {
            if (c.cost <= fighter.Libido) options.Add(c);
        }
        return options.Count > 0 ? options[UnityEngine.Random.Range(0, options.Count)] : null;
    }

    private Vector3 AimPoint(Fighter f)
    {
        Camera cam = f.GetComponentInChildren<Camera>();
        if (cam != null) return cam.transform.position + Vector3.down * 0.3f;
        Collider c = f.GetComponent<Collider>();
        return c != null ? c.bounds.center + Vector3.up * 0.25f : f.transform.position + Vector3.up * 1.3f;
    }

    private float Miss(Fighter f, Vector3 at)
    {
        Camera cam = f.GetComponentInChildren<Camera>();
        if (cam == null) return Vector3.Distance(f.GetComponent<Collider>().ClosestPoint(at), at);
        Vector3 top = cam.transform.position;
        Vector3 bottom = top + Vector3.down * 1.2f;
        Vector3 axis = bottom - top;
        float t = Mathf.Clamp01(Vector3.Dot(at - top, axis) / axis.sqrMagnitude);
        return Vector3.Distance(at, top + axis * t) - (cam != null ? hitRadius : 0f);
    }

    private IEnumerator Throw(CardData card, Fighter victim)
    {
        throwing = true;
        animator.SetTrigger("Throw");
        yield return new WaitForSeconds(windup);
        Transform v = cardVisual.transform;
        v.gameObject.SetActive(true);
        v.position = hand != null ? hand.position : transform.position + Vector3.up * 1.4f;
        block.SetTexture("_BaseMap", card.art);
        cardVisual.SetPropertyBlock(block, 0);
        Vector3 aim = AimPoint(victim) + UnityEngine.Random.insideUnitSphere * inaccuracy;
        Vector3 start = v.position;
        float total = Vector3.Distance(start, aim);
        float travelled = 0f;
        while (travelled < total + 2f && !fighter.IsDown)
        {
            travelled += flightSpeed * Time.deltaTime;
            Vector3 dir = (aim - start).normalized;
            v.position = start + dir * travelled + Vector3.up * Mathf.Sin(Mathf.Clamp01(travelled / total) * Mathf.PI) * 0.25f;
            v.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, Time.time * 900f, 0f);
            if (!victim.IsDown && Miss(victim, v.position) < 0.12f)
            {
                Collider hit = victim.GetComponentInChildren<Collider>();
                CardHit?.Invoke(card, fighter, hit, v.position, flightSpeed);
                break;
            }
            yield return null;
        }
        v.gameObject.SetActive(false);
        throwing = false;
    }
}
