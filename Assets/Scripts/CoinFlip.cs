using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class CoinFlip : MonoBehaviour
{
    [SerializeField] private Transform coin;
    [SerializeField] private TMP_Text face;
    [SerializeField] private GameObject choices;
    [SerializeField] private Button heads, tails;
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private AudioSource spinSound;
    [SerializeField] private float chooseTime = 8f, spinTime = 1.8f, distance = 1.1f, toss = 0.35f, showTime = 1.4f;
    [SerializeField] private int turns = 7;

    public event Action<Fighter, bool> Called;
    public event Action<Fighter, bool, bool> Flipped;

    private int pick = -1;

    private void Awake()
    {
        heads.onClick.AddListener(() => pick = 1);
        tails.onClick.AddListener(() => pick = 0);
        choices.SetActive(false);
        coin.gameObject.SetActive(false);
    }

    public IEnumerator Flip(Fighter caller, bool human, Action<bool> done)
    {
        pick = -1;
        if (human)
        {
            choices.SetActive(true);
            for (float t = chooseTime; t > 0f && pick < 0; t -= Time.deltaTime)
            {
                prompt.text = "CALL IT  ·  " + Mathf.CeilToInt(t);
                yield return null;
            }
            choices.SetActive(false);
        }
        if (pick < 0) pick = Random.value < 0.5f ? 1 : 0;
        Called?.Invoke(caller, pick == 1);
        bool landed = Random.value < 0.5f;
        yield return Spin(landed);
        bool right = landed == (pick == 1);
        Flipped?.Invoke(caller, landed, right);
        yield return new WaitForSeconds(showTime);
        coin.gameObject.SetActive(false);
        done(right);
    }

    private IEnumerator Spin(bool landedHeads)
    {
        Transform cam = Camera.main != null ? Camera.main.transform : transform;
        Vector3 flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 basePos = cam.position + flat * distance + Vector3.down * 0.1f;
        Quaternion facing = Quaternion.LookRotation(flat);
        Vector3 axis = Vector3.Cross(Vector3.up, flat);
        coin.gameObject.SetActive(true);
        if (spinSound != null) spinSound.Play();
        float end = turns * 360f + (landedHeads ? 0f : 180f);
        for (float t = 0f; t < spinTime; t += Time.deltaTime)
        {
            float k = t / spinTime;
            coin.position = basePos + Vector3.up * Mathf.Sin(k * Mathf.PI) * toss;
            coin.rotation = Quaternion.AngleAxis(Mathf.Lerp(0f, end, 1f - (1f - k) * (1f - k)), axis) * facing;
            ShowFace(cam);
            yield return null;
        }
        coin.SetPositionAndRotation(basePos, Quaternion.AngleAxis(end, axis) * facing);
        ShowFace(cam);
    }

    private void ShowFace(Transform cam)
    {
        bool front = Vector3.Dot(coin.forward, coin.position - cam.position) > 0f;
        face.text = front ? "HEADS" : "TAILS";
        face.transform.localRotation = front ? Quaternion.identity : Quaternion.Euler(180f, 0f, 0f);
    }
}
