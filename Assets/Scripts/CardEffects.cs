using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class CardEffects : MonoBehaviour
{
    [SerializeField] private CardOrbit orbit;
    [SerializeField] private ParticleSystem burstPrefab;
    [SerializeField] private TextMeshPro popupPrefab;
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private AudioSource throwSound;
    [SerializeField] private int poolSize = 8;
    [SerializeField] private float knockbackSpeed = 7f, reviveDelay = 6f;
    [SerializeField] private Color[] functionColors = { new Color(0.75f, 0.55f, 1f), new Color(1f, 0.5f, 0.3f), new Color(0.45f, 0.8f, 1f), new Color(0.45f, 1f, 0.7f) };

    private readonly List<ParticleSystem> bursts = new List<ParticleSystem>();
    private readonly List<TextMeshPro> popups = new List<TextMeshPro>();
    private readonly List<float> popupTimes = new List<float>();
    private readonly List<InputDevice> pads = new List<InputDevice>();
    private Fighter[] fighters = new Fighter[0];
    private FighterAI[] brains = new FighterAI[0];
    private int nextBurst, nextPopup;

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            bursts.Add(Instantiate(burstPrefab, transform));
            popups.Add(Instantiate(popupPrefab, transform));
            popups[i].gameObject.SetActive(false);
            popupTimes.Add(0f);
        }
        fighters = FindObjectsByType<Fighter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        brains = FindObjectsByType<FighterAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void OnEnable() => Listen(true);
    private void OnDisable() => Listen(false);

    private void Listen(bool on)
    {
        foreach (Fighter f in fighters)
        {
            f.Damaged -= OnDamaged;
            f.Defeated -= OnDefeated;
            if (on) { f.Damaged += OnDamaged; f.Defeated += OnDefeated; }
        }
        foreach (FighterAI ai in brains)
        {
            ai.CardHit -= Strike;
            if (on) ai.CardHit += Strike;
        }
        if (orbit == null) return;
        orbit.CardLanded -= OnLanded;
        orbit.CardThrown -= OnThrown;
        if (on) { orbit.CardLanded += OnLanded; orbit.CardThrown += OnThrown; }
    }

    private void OnThrown(ThrowableCard card) => Play(throwSound, card.transform.position);
    private void OnLanded(ThrowableCard card, Collider hit, float speed) => Strike(card.Data, orbit.Owner, hit, hit.ClosestPoint(card.transform.position), speed);

    public void Strike(CardData data, Fighter source, Collider hit, Vector3 at, float speed)
    {
        Burst(at, functionColors[(int)data.function], speed);
        Fighter f = hit != null ? hit.GetComponentInParent<Fighter>() : null;
        if (f != null && f != source && !f.IsDown) f.ReceiveCard(data, source, speed);
        if (f != null && f == orbit.Owner) InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, pads);
        if (f != null && f == orbit.Owner) foreach (InputDevice d in pads) d.SendHapticImpulse(0u, 0.8f, 0.25f);
        Animator a = hit != null ? hit.GetComponentInParent<Animator>() : null;
        if (a == null || a.runtimeAnimatorController == null || (f != null && f.IsDown)) return;
        if (data.effect == EffectType.Freeze) StartCoroutine(TimeScale(a, 0f, data.duration));
        else if (data.effect == EffectType.Slow) StartCoroutine(TimeScale(a, 0.45f, data.duration + 2f));
        if (data.effect == EffectType.Shield) a.SetTrigger("Block");
        else if (data.effect == EffectType.Illusion || data.effect == EffectType.Reveal || data.effect == EffectType.Counter) a.SetTrigger("Refuse");
        else if (data.effect == EffectType.Damage) a.SetTrigger(speed > knockbackSpeed || data.family == CardFamily.Force ? "Knockback" : "Hit");
        else if (data.effect != EffectType.Freeze) a.SetTrigger("Hit");
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
        bool me = f == orbit.Owner && Camera.main != null;
        p.text = "-" + Mathf.RoundToInt(amount);
        p.color = me ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.85f, 0.45f);
        p.transform.position = me ? Camera.main.transform.position + Camera.main.transform.forward * 0.9f + Vector3.down * 0.15f : f.transform.position + Vector3.up * 2.1f + Random.insideUnitSphere * 0.15f;
        p.transform.localScale = Vector3.one * (me ? 0.35f : 1f);
        p.gameObject.SetActive(true);
    }

    private void OnDefeated(Fighter f)
    {
        Animator an = f.GetComponent<Animator>();
        if (an != null && an.runtimeAnimatorController != null) an.SetTrigger("Die");
        if (f.Team == 2) StartCoroutine(Revive(f, an));
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
            popups[i].transform.position += Vector3.up * 0.5f * Time.deltaTime;
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
        Play(hitSound, at);
    }

    private void Play(AudioSource s, Vector3 at)
    {
        if (s == null) return;
        s.transform.position = at;
        s.pitch = Random.Range(0.9f, 1.15f);
        s.Play();
    }
}
