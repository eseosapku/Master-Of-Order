using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CardEffects : MonoBehaviour
{
    [SerializeField] private CardOrbit orbit;
    [SerializeField] private ParticleSystem burstPrefab;
    [SerializeField] private TextMeshPro popupPrefab;
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private AudioSource throwSound;
    [SerializeField] private int poolSize = 8;
    [SerializeField] private float knockbackSpeed = 7f;
    [SerializeField] private float reviveDelay = 6f;
    [SerializeField] private Color[] functionColors = { new Color(0.75f, 0.55f, 1f), new Color(1f, 0.5f, 0.3f), new Color(0.45f, 0.8f, 1f), new Color(0.45f, 1f, 0.7f) };

    private readonly List<ParticleSystem> bursts = new List<ParticleSystem>();
    private readonly List<TextMeshPro> popups = new List<TextMeshPro>();
    private readonly List<float> popupTimes = new List<float>();
    private Fighter[] fighters = new Fighter[0];
    private int nextBurst;
    private int nextPopup;

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            bursts.Add(Instantiate(burstPrefab, transform));
            TextMeshPro p = Instantiate(popupPrefab, transform);
            p.gameObject.SetActive(false);
            popups.Add(p);
            popupTimes.Add(0f);
        }
        fighters = FindObjectsByType<Fighter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void OnEnable()
    {
        foreach (Fighter f in fighters)
        {
            f.Damaged += OnDamaged;
            f.Defeated += OnDefeated;
        }
        if (orbit == null) return;
        orbit.CardLanded += OnLanded;
        orbit.CardThrown += OnThrown;
    }

    private void OnDisable()
    {
        foreach (Fighter f in fighters)
        {
            f.Damaged -= OnDamaged;
            f.Defeated -= OnDefeated;
        }
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
        Burst(hit.ClosestPoint(card.transform.position), functionColors[(int)card.Data.function], speed);
        Fighter f = hit.GetComponentInParent<Fighter>();
        if (f != null && f != orbit.Owner && !f.IsDown) f.ReceiveCard(card.Data, orbit.Owner, speed);
        Animator target = hit.GetComponentInParent<Animator>();
        if (target != null && target.runtimeAnimatorController != null && (f == null || !f.IsDown)) React(card.Data, target, speed);
    }

    private void React(CardData data, Animator target, float speed)
    {
        if (data.effect == EffectType.Freeze) StartCoroutine(TimeScale(target, 0f, data.duration));
        else if (data.effect == EffectType.Slow) StartCoroutine(TimeScale(target, 0.45f, data.duration + 2f));
        if (data.effect == EffectType.Shield) target.SetTrigger("Block");
        else if (data.effect == EffectType.Illusion || data.effect == EffectType.Reveal || data.effect == EffectType.Counter) target.SetTrigger("Refuse");
        else if (data.effect == EffectType.Damage) target.SetTrigger(speed > knockbackSpeed || data.family == CardFamily.Force ? "Knockback" : "Hit");
        else if (data.effect != EffectType.Freeze) target.SetTrigger("Hit");
    }

    private IEnumerator TimeScale(Animator target, float scale, float seconds)
    {
        target.speed = scale;
        yield return new WaitForSeconds(Mathf.Max(seconds, 1f));
        if (target != null) target.speed = 1f;
    }

    private void OnDamaged(Fighter f, float amount)
    {
        if (amount < 1f) return;
        TextMeshPro p = popups[nextPopup];
        popupTimes[nextPopup] = Time.time;
        nextPopup = (nextPopup + 1) % popups.Count;
        p.text = "-" + Mathf.RoundToInt(amount);
        p.color = f == orbit.Owner ? new Color(1f, 0.35f, 0.35f) : new Color(1f, 0.85f, 0.45f);
        p.transform.position = f.transform.position + Vector3.up * 2.1f + Random.insideUnitSphere * 0.15f;
        p.gameObject.SetActive(true);
    }

    private void OnDefeated(Fighter f)
    {
        Animator an = f.GetComponent<Animator>();
        if (an != null && an.runtimeAnimatorController != null) an.SetTrigger("Die");
        StartCoroutine(Revive(f, an));
    }

    private IEnumerator Revive(Fighter f, Animator an)
    {
        yield return new WaitForSeconds(reviveDelay);
        if (an != null && an.runtimeAnimatorController != null) an.SetTrigger("Rise");
        f.Restore();
    }

    private void Update()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        for (int i = 0; i < popups.Count; i++)
        {
            if (!popups[i].gameObject.activeSelf) continue;
            float age = Time.time - popupTimes[i];
            popups[i].transform.position += Vector3.up * 0.6f * Time.deltaTime;
            if (cam != null) popups[i].transform.rotation = Quaternion.LookRotation(popups[i].transform.position - cam.position);
            popups[i].alpha = 1f - age;
            if (age > 1f) popups[i].gameObject.SetActive(false);
        }
    }

    private void Burst(Vector3 at, Color color, float speed)
    {
        ParticleSystem p = bursts[nextBurst];
        nextBurst = (nextBurst + 1) % bursts.Count;
        p.transform.position = at;
        ParticleSystem.MainModule main = p.main;
        main.startColor = color;
        main.startSpeed = Mathf.Clamp(speed * 0.5f, 1.5f, 6f);
        p.Clear();
        p.Play();
        if (hitSound == null) return;
        hitSound.transform.position = at;
        hitSound.pitch = Random.Range(0.9f, 1.15f);
        hitSound.Play();
    }
}
