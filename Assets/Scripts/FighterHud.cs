using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FighterHud : MonoBehaviour
{
    [SerializeField] private Fighter fighter;
    [SerializeField] private Image bodyFill;
    [SerializeField] private Image[] libidoPips = new Image[0];
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text libidoText;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Transform head;
    [SerializeField] private bool billboard;
    [SerializeField] private bool alwaysShow;
    [SerializeField] private float showAngle = 0.55f;
    [SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private float barSpeed = 3f;
    [SerializeField] private Color pipOn = new Color(1f, 0.82f, 0.35f);
    [SerializeField] private Color pipOff = new Color(0.25f, 0.2f, 0.3f);

    private float body = 1f;

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

    public void Bind(Fighter f)
    {
        if (fighter != null) fighter.Changed -= Show;
        fighter = f;
        if (fighter == null) return;
        fighter.Changed += Show;
        Show(fighter);
    }

    private void Show(Fighter f)
    {
        SetValues(f.BodyRatio, f.Libido);
        if (nameText != null) nameText.text = f.DisplayName.ToUpper();
        if (bodyText != null) bodyText.text = "BODY  " + Mathf.CeilToInt(f.Body);
        if (libidoText != null) libidoText.text = "LIBIDO  " + f.Libido;
    }

    public void SetValues(float bodyValue, int libidoValue)
    {
        body = Mathf.Clamp01(bodyValue);
        for (int i = 0; i < libidoPips.Length; i++) libidoPips[i].color = i < libidoValue ? pipOn : pipOff;
    }

    private void Update()
    {
        float step = Time.deltaTime * barSpeed;
        if (bodyFill != null) bodyFill.fillAmount = Mathf.MoveTowards(bodyFill.fillAmount, body, step);
        if (head == null || alwaysShow) return;
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
