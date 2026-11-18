using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class ThrowableCard : MonoBehaviour
{
    private enum CardState { Slot, Held, Flying, Spent }

    [SerializeField] private float throwScale = 1.7f;
    [SerializeField] private float gravity = 2.5f;
    [SerializeField] private float minSpeed = 1.2f;
    [SerializeField] private float maxLife = 3.5f;
    [SerializeField] private float spin = 30f;
    [SerializeField] private Renderer face;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TrailRenderer trail;

    public event Action<ThrowableCard, Collider, float> Landed;
    public event Action<ThrowableCard> Thrown;

    public CardData Data { get; private set; }
    public bool Floating => state == CardState.Slot || state == CardState.Spent;

    private XRGrabInteractable grab;
    private Rigidbody rb;
    private Material faceMat;
    private CardState state;
    private Vector3 lastPos;
    private Vector3 handVelocity;
    private float life;
    private float lastSpeed;
    private float rechargeAt;
    private float recharge;
    private float assist;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
        if (trail != null) trail.emitting = false;
    }

    public void Setup(CardData data, Material template, float aimAssist, float rechargeTime)
    {
        Data = data;
        assist = aimAssist;
        recharge = rechargeTime;
        faceMat = new Material(template);
        faceMat.SetTexture("_BaseMap", data.art);
        faceMat.SetTexture("_EmissionMap", data.art);
        faceMat.SetColor("_EmissionColor", Color.white * 0.6f);
        Material[] mats = face.sharedMaterials;
        mats[0] = faceMat;
        face.sharedMaterials = mats;
        label.text = data.cardName + "  ·  " + data.cost;
        state = CardState.Slot;
    }

    public void Hold(Vector3 pos, Quaternion rot, float follow)
    {
        if (!Floating) return;
        float t = 1f - Mathf.Exp(-follow * Time.deltaTime);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, t), Quaternion.Slerp(transform.rotation, rot, t));
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        state = CardState.Held;
        lastPos = transform.position;
        handVelocity = Vector3.zero;
        Buzz(args.interactorObject, 0.25f, 0.05f);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (handVelocity.magnitude < minSpeed)
        {
            state = CardState.Slot;
            rb.isKinematic = true;
            return;
        }
        Buzz(args.interactorObject, 0.6f, 0.08f);
        Vector3 v = handVelocity * throwScale;
        Collider target = assist > 0f ? CardOrbit.FindTarget(transform.position, v, assist > 1.5f ? 22f : 10f) : null;
        if (target != null) v = Vector3.Slerp(v.normalized, (target.bounds.center - transform.position).normalized, assist > 1.5f ? 0.85f : 0.5f) * v.magnitude;
        state = CardState.Flying;
        life = 0f;
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearVelocity = v;
        rb.angularVelocity = transform.up * spin;
        if (trail != null) trail.emitting = true;
        Thrown?.Invoke(this);
    }

    private void Update()
    {
        if (state == CardState.Held && Time.deltaTime > 0f)
        {
            handVelocity = Vector3.Lerp(handVelocity, (transform.position - lastPos) / Time.deltaTime, 0.5f);
            lastPos = transform.position;
        }
        if (state == CardState.Spent && Time.time >= rechargeAt)
        {
            state = CardState.Slot;
            grab.enabled = true;
            faceMat.SetColor("_BaseColor", Color.white);
        }
    }

    private void FixedUpdate()
    {
        if (state != CardState.Flying) return;
        rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        lastSpeed = rb.linearVelocity.magnitude;
        life += Time.fixedDeltaTime;
        if (life > maxLife) Spend();
    }

    private void OnCollisionEnter(Collision c)
    {
        if (state != CardState.Flying) return;
        Landed?.Invoke(this, c.collider, lastSpeed);
        Spend();
    }

    private void Spend()
    {
        state = CardState.Spent;
        rb.isKinematic = true;
        grab.enabled = false;
        rechargeAt = Time.time + recharge;
        if (trail != null) trail.emitting = false;
        faceMat.SetColor("_BaseColor", new Color(0.3f, 0.3f, 0.35f));
    }

    private static void Buzz(IXRInteractor who, float amp, float dur)
    {
        XRBaseInputInteractor input = who as XRBaseInputInteractor;
        if (input != null) input.SendHapticImpulse(amp, dur);
    }
}
