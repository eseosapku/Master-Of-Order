using UnityEngine;
using UnityEngine.UI;

public class FighterHud : MonoBehaviour
{
    [SerializeField] private Fighter fighter;
    [SerializeField] private Image bodyFill;
    [SerializeField] private Image egoFill;
    [SerializeField] private Image shadowFill;
    [SerializeField] private Image[] libidoPips = new Image[0];
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Transform head;
    [SerializeField] private bool billboard;
    [SerializeField] private float showAngle = 0.55f;
    [SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private float barSpeed = 3f;
    [SerializeField] private Color pipOn = new Color(1f, 0.82f, 0.35f);
    [SerializeField] private Color pipOff = new Color(0.25f, 0.2f, 0.3f);

    private float body = 1f;
    private float ego = 1f;
    private float shadow;

    private void OnEnable()
    {
        if (fighter != null) fighter.Changed += Show;
    }

    private void OnDisable()
    {
        if (fighter != null) fighter.Changed -= Show;
    }

    private void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (fighter != null) Show(fighter);
    }

    private void Show(Fighter f)
    {
        SetValues(f.BodyRatio, f.EgoRatio, f.ShadowRatio, f.Libido);
    }

    public void SetValues(float bodyValue, float egoValue, float shadowValue, int libidoValue)
    {
        body = Mathf.Clamp01(bodyValue);
        ego = Mathf.Clamp01(egoValue);
        shadow = Mathf.Clamp01(shadowValue);
        for (int i = 0; i < libidoPips.Length; i++) libidoPips[i].color = i < libidoValue ? pipOn : pipOff;
    }

    private void Update()
    {
        float step = Time.deltaTime * barSpeed;
        if (bodyFill != null) bodyFill.fillAmount = Mathf.MoveTowards(bodyFill.fillAmount, body, step);
        if (egoFill != null) egoFill.fillAmount = Mathf.MoveTowards(egoFill.fillAmount, ego, step);
        if (shadowFill != null) shadowFill.fillAmount = Mathf.MoveTowards(shadowFill.fillAmount, shadow, step);
        if (head == null) return;
        if (billboard)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - head.position);
            return;
        }
        Vector3 toHead = (head.position - transform.position).normalized;
        bool facing = Vector3.Dot(-transform.forward, toHead) > showAngle;
        group.alpha = Mathf.MoveTowards(group.alpha, facing ? 1f : 0f, Time.deltaTime * fadeSpeed);
    }
}
