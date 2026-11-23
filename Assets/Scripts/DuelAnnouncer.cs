using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DuelAnnouncer : MonoBehaviour
{
    [SerializeField] private TrialManager trials;
    [SerializeField] private Board board;
    [SerializeField] private CoinFlip coin;
    [SerializeField] private CardHand hand;
    [SerializeField] private FighterHud foeHud;
    [SerializeField] private TMP_Text title, logText;
    [SerializeField] private AudioSource voice, gong;
    [SerializeField] private AudioClip[] lines;
    [SerializeField] private GameObject resultPanel, continueButton;
    [SerializeField] private TMP_Text resultTitle, resultText;
    [SerializeField] private Transform head;
    [SerializeField] private int logLines = 8;
    [SerializeField] private float panelDistance = 1.3f;

    private readonly List<string> log = new List<string>();
    private readonly Queue<AudioClip> queue = new Queue<AudioClip>();
    private Fighter actor;
    private Fighter[] fighters = new Fighter[0];
    private int shownClock = -1;

    private void Awake() => fighters = FindObjectsByType<Fighter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    private static string Key(Fighter f) => f.DisplayName.Split(' ')[0];
    private static string Pascal(string n) => string.Concat(System.Array.ConvertAll(n.Replace("-", " ").Replace("'", "").Split(' '), w => w.Length > 0 ? char.ToUpper(w[0]) + w.Substring(1) : ""));
    private void OnHurt(Fighter f, float amount) => Say("Hurt" + Key(f) + Mathf.RoundToInt(amount));

    private void OnEnable() => Listen(true);
    private void OnDisable() => Listen(false);

    private void Listen(bool on)
    {
        trials.Message -= OnMessage; trials.BoutStarted -= OnBoutStarted; trials.TurnStarted -= OnTurnStarted;
        trials.TurnResolved -= OnResolved; trials.BoutEnded -= OnBoutEnded; trials.ChoiceMade -= Hide;
        board.Note -= OnNote; board.Landed -= OnLanded; board.IntentRevealed -= OnIntent; coin.Called -= OnCalled; coin.Flipped -= OnFlipped;
        foreach (Fighter f in fighters) { f.Damaged -= OnHurt; if (on) f.Damaged += OnHurt; }
        if (!on) return;
        trials.Message += OnMessage; trials.BoutStarted += OnBoutStarted; trials.TurnStarted += OnTurnStarted;
        trials.TurnResolved += OnResolved; trials.BoutEnded += OnBoutEnded; trials.ChoiceMade += Hide;
        board.Note += OnNote; board.Landed += OnLanded; board.IntentRevealed += OnIntent; coin.Called += OnCalled; coin.Flipped += OnFlipped;
    }

    private void Update()
    {
        int clock = hand != null ? Mathf.CeilToInt(hand.Clock) : -1;
        if (clock >= 0 && clock != shownClock) title.text = "YOUR TURN  ·  <color=" + (clock <= 5 ? "#FF6B6B" : "#FFFFFF") + ">" + clock + "s</color>";
        shownClock = clock;
        if (voice == null || voice.isPlaying || queue.Count == 0) return;
        voice.clip = queue.Dequeue();
        voice.Play();
    }

    private void Say(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        foreach (AudioClip c in lines) if (c != null && c.name == "Voice" + key) { queue.Enqueue(c); return; }
    }

    private void Log(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        log.Add(line);
        while (log.Count > logLines) log.RemoveAt(0);
        logText.text = string.Join("\n", log);
    }

    private void OnMessage(string head, string sub) { title.text = head; Log(sub); }
    private void OnNote(string text, string key) { Log(text); Say(text.EndsWith("a hidden card") && actor != null ? "Set" + Key(actor) : key); }
    private void OnCalled(Fighter f, bool heads) => Log(f.DisplayName + (f == trials.Player ? " call " : " calls ") + (heads ? "HEADS" : "TAILS"));
    private void Hide() => resultPanel.SetActive(false);

    private void OnFlipped(Fighter f, bool heads, bool right)
    {
        Log("The coin lands <b>" + (heads ? "HEADS" : "TAILS") + "</b>");
        Say(heads ? "Heads" : "Tails");
    }

    private void OnLanded(CardData c)
    {
        Log("<color=#E8C86A>" + (actor == trials.Player ? "You play " : actor.DisplayName + " plays ") + c.cardName.ToUpper() + "</color>");
        Say("Played" + Key(actor) + Pascal(c.cardName));
    }

    private void OnBoutStarted(Fighter foe, int index)
    {
        foeHud.Bind(foe);
        log.Clear();
        queue.Clear();
        Hide();
        title.text = index > 0 ? "DUEL " + index + "  ·  " + foe.DisplayName.ToUpper() : "FINAL DUEL  ·  " + foe.DisplayName.ToUpper();
        Log("You play first. One card per turn, 20 seconds to choose.");
        if (gong != null) gong.Play();
    }

    private void OnTurnStarted(Fighter f, bool skipped)
    {
        actor = f;
        title.text = f == trials.Player ? "YOUR TURN" : f.DisplayName.ToUpper() + "'S TURN";
        if (!skipped) return;
        Log(f.DisplayName + (f == trials.Player ? " lose" : " loses") + " this turn");
        Say("TurnLost");
    }

    private void OnIntent(Fighter f)
    {
        FighterAI ai = f.GetComponent<FighterAI>();
        Log(ai != null && ai.Intent != null ? f.DisplayName + " plans to play <b>" + ai.Intent.cardName.ToUpper() + "</b>" : f.Possessive + " hidden cards are revealed");
    }

    private void OnResolved(Fighter f, CardData c, Fighter foe, float body, float foeBody)
    {
        if (c == null) Log(f == trials.Player ? "You pass" : f.DisplayName + " passes");
        if (c == null && f == trials.Player) Say("TimeUp");
    }

    private void OnBoutEnded(int index, bool won)
    {
        Fighter foe = trials.SideB.Length > 0 ? trials.SideB[0] : null;
        resultTitle.text = won ? "VICTORY" : "DEFEAT";
        resultText.text = foe == null ? "" : won ? foe.DisplayName + " falls.  You stand with " + Mathf.CeilToInt(trials.Player.Body) + " Body." : foe.DisplayName + " wins with " + Mathf.CeilToInt(foe.Body) + " Body left.";
        continueButton.SetActive(won);
        Transform h = head != null ? head : Camera.main.transform;
        Vector3 flat = Vector3.ProjectOnPlane(h.forward, Vector3.up).normalized;
        resultPanel.transform.SetPositionAndRotation(h.position + flat * panelDistance, Quaternion.LookRotation(flat));
        resultPanel.SetActive(true);
        Say(won ? "Victory" : "Defeat");
    }
}
