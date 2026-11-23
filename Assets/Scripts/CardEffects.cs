using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class CardEffects : MonoBehaviour
{
    [SerializeField] private Board board;
    [SerializeField] private Fighter player;
    [SerializeField] private ThrowableCard cardPrefab;
    [SerializeField] private Material faceTemplate;
    [SerializeField] private Texture cardBack;
    [SerializeField] private ParticleSystem burstPrefab;
    [SerializeField] private TextMeshPro popupPrefab;
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private AudioSource throwSound;
    [SerializeField] private int poolSize = 8;
    [SerializeField] private float fieldScale = 3.2f, standHeight = 0.7f;
    [SerializeField] private Color[] functionColors = { new Color(0.75f, 0.55f, 1f), new Color(1f, 0.5f, 0.3f), new Color(0.45f, 0.8f, 1f), new Color(0.45f, 1f, 0.7f) };

    private readonly List<ParticleSystem> bursts = new List<ParticleSystem>();
    private readonly List<TextMeshPro> popups = new List<TextMeshPro>();
    private readonly List<float> popupTimes = new List<float>();
    private readonly List<InputDevice> pads = new List<InputDevice>();
    private readonly Dictionary<FieldUnit, ThrowableCard> shown = new Dictionary<FieldUnit, ThrowableCard>();
    private Fighter[] fighters = new Fighter[0];
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
    }

    private void OnEnable() => Listen(true);
    private void OnDisable() => Listen(false);

    private void Listen(bool on)
    {
        foreach (Fighter f in fighters) { f.Damaged -= OnDamaged; f.Defeated -= OnDefeated; f.Changed -= OnFighter; if (on) { f.Damaged += OnDamaged; f.Defeated += OnDefeated; f.Changed += OnFighter; } }
        board.UnitPlaced -= OnPlaced; board.UnitChanged -= OnChanged; board.UnitRemoved -= OnRemoved; ThrowableCard.Launched -= OnLaunched; ThrowableCard.Arrived -= OnArrived;
        if (!on) return;
        board.UnitPlaced += OnPlaced; board.UnitChanged += OnChanged; board.UnitRemoved += OnRemoved; ThrowableCard.Launched += OnLaunched; ThrowableCard.Arrived += OnArrived;
    }

    private static Animator Anim(Fighter f) { Animator a = f.GetComponent<Animator>(); return a != null && a.runtimeAnimatorController != null ? a : null; }
    private void OnArrived(ThrowableCard c, Vector3 at) => Burst(at, functionColors[(int)c.Data.function], 4f);
    private void OnFighter(Fighter f) { if (Anim(f) != null) Anim(f).speed = f.Has(StatusType.Skip) && !f.IsDown ? 0f : 1f; }

    private void OnLaunched(ThrowableCard c)
    {
        if (throwSound == null) return;
        throwSound.transform.position = c.transform.position;
        throwSound.pitch = Random.Range(0.9f, 1.1f);
        throwSound.Play();
    }

    private void OnPlaced(FieldUnit u)
    {
        ThrowableCard v = Instantiate(cardPrefab, u.at + Vector3.up * standHeight, Quaternion.identity);
        v.transform.localScale *= fieldScale;
        Vector3 look = Vector3.ProjectOnPlane(v.transform.position - (Camera.main != null ? Camera.main.transform.position : u.at), Vector3.up);
        if (look.sqrMagnitude > 0.001f) v.transform.rotation = Quaternion.LookRotation(look);
        v.Setup(u.card, faceTemplate, u.card.art);
        shown[u] = v;
        OnChanged(u);
        Burst(v.transform.position, functionColors[(int)u.card.function], 3f);
    }

    private void OnChanged(FieldUnit u)
    {
        if (!shown.TryGetValue(u, out ThrowableCard v)) return;
        bool mine = u.owner == player;
        if (u.card.kind == CardKind.Field) v.Show(u.card.art, u.card.cardName + "  ·  " + u.resolve + (u.card.guard ? " GUARD" : " HP"));
        else v.Show(u.revealed || mine ? u.card.art : cardBack, u.revealed || mine ? u.card.cardName + "  ·  hidden" : "HIDDEN CARD");
        v.Tint(u.card.kind == CardKind.Field && u.card.resolve > 0 && u.resolve * 2 <= u.card.resolve ? new Color(1f, 0.6f, 0.6f) : Color.white);
        if (u.card.kind == CardKind.Field && u.resolve < u.card.resolve && Anim(u.owner) != null) Anim(u.owner).SetTrigger("Block");
    }

    private void OnRemoved(FieldUnit u)
    {
        if (!shown.TryGetValue(u, out ThrowableCard v)) return;
        shown.Remove(u);
        Burst(v.transform.position, functionColors[(int)u.card.function], 5f);
        Destroy(v.gameObject);
    }

    private void OnDamaged(Fighter f, float amount)
    {
        if (Anim(f) != null && !f.IsDown) Anim(f).SetTrigger(amount >= 15f ? "Knockback" : "Hit");
        bool me = f == player && Camera.main != null;
        if (!me) Burst(f.transform.position + Vector3.up * 1.2f, new Color(1f, 0.4f, 0.35f), amount * 0.2f);
        if (me)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, pads);
            foreach (InputDevice d in pads) d.SendHapticImpulse(0u, 0.8f, 0.25f);
        }
        if (amount < 1f) return;
        TextMeshPro p = popups[nextPopup];
        popupTimes[nextPopup] = Time.time;
        nextPopup = (nextPopup + 1) % popups.Count;
        p.text = "-" + Mathf.RoundToInt(amount);
        p.color = me ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.85f, 0.45f);
        p.transform.position = me ? Camera.main.transform.position + Camera.main.transform.forward * 0.9f + Vector3.down * 0.15f : f.transform.position + Vector3.up * 2.1f;
        p.transform.localScale = Vector3.one * (me ? 0.35f : 1f);
        p.gameObject.SetActive(true);
    }

    private void OnDefeated(Fighter f)
    {
        if (Anim(f) != null) Anim(f).SetTrigger("Die");
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
            popups[i].alpha = 1f - age * 0.6f;
            if (age > 1.6f) popups[i].gameObject.SetActive(false);
        }
    }

    private void Burst(Vector3 at, Color color, float speed)
    {
        ParticleSystem p = bursts[nextBurst];
        nextBurst = (nextBurst + 1) % bursts.Count;
        p.transform.position = at;
        ParticleSystem.MainModule main = p.main;
        main.startColor = color;
        main.startSpeed = Mathf.Clamp(speed, 1.5f, 6f);
        p.Clear();
        p.Play();
        if (hitSound == null) return;
        hitSound.transform.position = at;
        hitSound.pitch = Random.Range(0.9f, 1.15f);
        hitSound.Play();
    }
}
