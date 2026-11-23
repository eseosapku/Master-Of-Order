using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ThrowableCard : MonoBehaviour
{
    [SerializeField] private Renderer face;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private float flightTime = 0.9f, arcHeight = 0.5f, riseTime = 0.45f;

    public CardData Data { get; private set; }

    private Material faceMat;

    private void Awake()
    {
        if (TryGetComponent(out XRGrabInteractable grab)) grab.enabled = false;
        if (TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (trail != null) trail.emitting = false;
    }

    public void Setup(CardData data, Material template, Texture art)
    {
        Data = data;
        faceMat = new Material(template);
        Show(art, data.cardName + "  ·  " + data.cost);
        Material[] mats = face.sharedMaterials;
        mats[0] = faceMat;
        face.sharedMaterials = mats;
    }

    public void Show(Texture art, string text)
    {
        faceMat.SetTexture("_BaseMap", art);
        faceMat.SetTexture("_EmissionMap", art);
        faceMat.SetColor("_EmissionColor", Color.white * 0.6f);
        label.text = text;
    }

    public void Tint(Color c) => faceMat.SetColor("_BaseColor", c);

    public IEnumerator Fly(Vector3 to, float endScale, Transform faceTowards)
    {
        Vector3 start = transform.position;
        Vector3 baseScale = transform.localScale;
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            transform.position = start + Vector3.up * 0.15f * (t / riseTime);
            yield return null;
        }
        start = transform.position;
        if (trail != null) trail.emitting = true;
        Quaternion from = transform.rotation;
        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / flightTime);
            transform.position = Vector3.Lerp(start, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arcHeight;
            transform.localScale = Vector3.Lerp(baseScale, baseScale * endScale, k);
            Vector3 look = faceTowards != null ? transform.position - faceTowards.position : to - start;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(from, Quaternion.LookRotation(look), k);
            yield return null;
        }
        transform.position = to;
        if (trail != null) trail.emitting = false;
    }
}
