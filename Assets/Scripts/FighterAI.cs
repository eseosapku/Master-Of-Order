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
    [SerializeField] private float windup = 0.42f;
    [SerializeField] private float flightSpeed = 8f;
    [SerializeField] private float inaccuracy = 0.2f;
    [SerializeField] private float hitRadius = 0.32f;
    [SerializeField] private float turnSpeed = 6f;

    public event Action<CardData, Fighter, Collider, Vector3, float> CardHit;

    public bool LastHit { get; private set; }

    private MaterialPropertyBlock block;
    private Transform hand;
    private Fighter facing;

    private void Start()
    {
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        block = new MaterialPropertyBlock();
        cardVisual.gameObject.SetActive(false);
        animator.SetBool("Showcase", false);
        animator.SetInteger("Mood", 3);
    }

    public void Face(Fighter target) => facing = target;

    private void Update()
    {
        if (facing == null || fighter.IsDown) return;
        Vector3 away = transform.position - facing.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), Time.deltaTime * turnSpeed);
    }

    public CardData Choose(Fighter foe)
    {
        CardData best = null;
        float bestScore = float.MinValue;
        foreach (CardData c in deck)
        {
            if (c.cost > fighter.Libido) continue;
            float s = c.power + c.cost + UnityEngine.Random.Range(0f, 6f);
            if ((c.effect == EffectType.Heal || c.effect == EffectType.Drain) && fighter.BodyRatio < 0.45f) s += 25f;
            if (c.effect == EffectType.Freeze && !foe.Has(StatusType.Frozen)) s += 8f;
            if (c.family == CardFamily.Hunt && foe.Has(StatusType.Marked)) s -= 20f;
            if (c.effect == EffectType.Damage && foe.Has(StatusType.Marked)) s += 10f;
            if (c.effect == EffectType.Damage && foe.Has(StatusType.Shielded)) s -= 12f;
            if (c.effect == EffectType.Shield && fighter.Has(StatusType.Shielded)) s -= 30f;
            if (s > bestScore)
            {
                bestScore = s;
                best = c;
            }
        }
        return best;
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
        Vector3 axis = Vector3.down * 1.2f;
        float t = Mathf.Clamp01(Vector3.Dot(at - top, axis) / axis.sqrMagnitude);
        return Vector3.Distance(at, top + axis * t) - hitRadius;
    }

    public IEnumerator Play(CardData card, Fighter victim)
    {
        LastHit = false;
        fighter.Spend(card.cost);
        Face(victim);
        animator.SetTrigger(card.effect == EffectType.Heal || card.effect == EffectType.Shield ? "Cast" : "Throw");
        yield return new WaitForSeconds(windup);
        if (card.effect == EffectType.Heal || card.effect == EffectType.Shield)
        {
            if (card.effect == EffectType.Heal) fighter.Heal(card.power);
            else fighter.AddStatus(StatusType.Shielded, 0f, 999f);
            LastHit = true;
            yield break;
        }
        Transform v = cardVisual.transform;
        v.gameObject.SetActive(true);
        v.position = hand != null ? hand.position : transform.position + Vector3.up * 1.4f;
        block.SetTexture("_BaseMap", card.art);
        cardVisual.SetPropertyBlock(block, 0);
        Vector3 aim = AimPoint(victim) + UnityEngine.Random.insideUnitSphere * inaccuracy;
        Vector3 start = v.position;
        float total = Vector3.Distance(start, aim);
        for (float travelled = 0f; travelled < total + 2f && !fighter.IsDown; travelled += flightSpeed * Time.deltaTime)
        {
            Vector3 dir = (aim - start).normalized;
            v.position = start + dir * travelled + Vector3.up * Mathf.Sin(Mathf.Clamp01(travelled / total) * Mathf.PI) * 0.25f;
            v.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, Time.time * 900f, 0f);
            if (!victim.IsDown && Miss(victim, v.position) < 0.12f)
            {
                CardHit?.Invoke(card, fighter, victim.GetComponentInChildren<Collider>(), v.position, flightSpeed);
                LastHit = true;
                break;
            }
            yield return null;
        }
        v.gameObject.SetActive(false);
    }
}
