using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardEffects : MonoBehaviour
{
    [SerializeField] private CardOrbit orbit;
    [SerializeField] private ParticleSystem burstPrefab;
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private AudioSource throwSound;
    [SerializeField] private int poolSize = 8;
    [SerializeField] private float knockbackSpeed = 7f;
    [SerializeField] private Color[] functionColors =
    {
        new Color(0.75f, 0.55f, 1f), new Color(1f, 0.5f, 0.3f), new Color(0.45f, 0.8f, 1f), new Color(0.45f, 1f, 0.7f)
    };

    private readonly List<ParticleSystem> pool = new List<ParticleSystem>();
    private int next;

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            ParticleSystem p = Instantiate(burstPrefab, transform);
            p.gameObject.SetActive(true);
            pool.Add(p);
        }
    }

    private void OnEnable()
    {
        if (orbit == null) return;
        orbit.CardLanded += OnLanded;
        orbit.CardThrown += OnThrown;
    }

    private void OnDisable()
    {
        if (orbit == null) return;
        orbit.CardLanded -= OnLanded;
        orbit.CardThrown -= OnThrown;
    }

    private void OnThrown(ThrowableCard card)
    {
        if (throwSound == null) return;
        throwSound.transform.position = card.transform.position;
        throwSound.Play();
    }

    private void OnLanded(ThrowableCard card, Collider hit, float speed)
    {
        Color c = functionColors[(int)card.Data.function];
        Burst(hit.ClosestPoint(card.transform.position), c, speed);
        Animator target = hit.GetComponentInParent<Animator>();
        if (target != null && target.runtimeAnimatorController != null) Resolve(card.Data, target, speed);
    }

    public void Resolve(CardData data, Animator target, float speed)
    {
        switch (data.effect)
        {
            case EffectType.Freeze:
                StartCoroutine(TimeScale(target, 0f, data.duration));
                break;
            case EffectType.Slow:
                target.SetTrigger("Hit");
                StartCoroutine(TimeScale(target, 0.45f, data.duration + 2f));
                break;
            case EffectType.Shield:
                target.SetTrigger("Block");
                break;
            case EffectType.Illusion:
            case EffectType.Reveal:
            case EffectType.Counter:
                target.SetTrigger("Refuse");
                break;
            case EffectType.Damage:
                target.SetTrigger(speed > knockbackSpeed || data.family == CardFamily.Force ? "Knockback" : "Hit");
                break;
            default:
                target.SetTrigger("Hit");
                break;
        }
    }

    private IEnumerator TimeScale(Animator target, float scale, float seconds)
    {
        target.speed = scale;
        yield return new WaitForSeconds(Mathf.Max(seconds, 1f));
        if (target != null) target.speed = 1f;
    }

    private void Burst(Vector3 at, Color color, float speed)
    {
        if (pool.Count == 0) return;
        ParticleSystem p = pool[next];
        next = (next + 1) % pool.Count;
        p.transform.position = at;
        ParticleSystem.MainModule main = p.main;
        main.startColor = color;
        main.startSpeed = Mathf.Clamp(speed * 0.5f, 1.5f, 6f);
        p.Clear();
        p.Play();
        if (hitSound != null)
        {
            hitSound.transform.position = at;
            hitSound.pitch = Random.Range(0.9f, 1.15f);
            hitSound.Play();
        }
    }
}
