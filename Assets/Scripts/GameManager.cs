using System;
using System.Collections.Generic;
using UnityEngine;

public enum FloorState
{
    Menu,
    Archive,
    Settings,
    Hall,
    Pairing,
    Trial,
    Cut,
    Reward
}

[Serializable]
public class StateView
{
    public FloorState state;
    public GameObject view;
}

public class GameManager : MonoBehaviour
{
    [SerializeField] private FloorState startState = FloorState.Menu;
    [SerializeField] private List<StateView> views = new List<StateView>();
    [SerializeField] private ArchiveDraft archive;
    [SerializeField] private CardOrbit orbit;
    [SerializeField] private AudioSource clickSound;

    public event Action<FloorState> StateChanged;

    public FloorState State { get; private set; }
    public List<CardData> Orbit { get; private set; } = new List<CardData>();
    public int AimAssist { get; private set; }
    public bool Vignette { get; private set; }

    private void Awake()
    {
        AimAssist = PlayerPrefs.GetInt("AimAssist", 1);
        Vignette = PlayerPrefs.GetInt("Vignette", 1) == 1;
        AudioListener.volume = PlayerPrefs.GetFloat("Volume", 0.8f);
    }

    private void OnEnable()
    {
        if (archive != null) archive.DraftDone += OnDraftDone;
    }

    private void OnDisable()
    {
        if (archive != null) archive.DraftDone -= OnDraftDone;
    }

    private void Start()
    {
        SetState(startState);
    }

    public void SetState(FloorState next)
    {
        State = next;
        foreach (StateView v in views) if (v.view != null) v.view.SetActive(views.Exists(w => w.view == v.view && w.state == next));
        StateChanged?.Invoke(next);
    }

    public void BeginClimb()
    {
        Click();
        SetState(Orbit.Count > 0 ? FloorState.Hall : FloorState.Archive);
    }

    public void OpenArchive()
    {
        Click();
        SetState(FloorState.Archive);
    }

    public void OpenSettings()
    {
        Click();
        SetState(FloorState.Settings);
    }

    public void OpenMenu()
    {
        Click();
        SetState(FloorState.Menu);
    }

    public void LeaveTower()
    {
        Click();
        Application.Quit();
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("Volume", value);
    }

    public void SetAimAssist(int level)
    {
        Click();
        AimAssist = level;
        PlayerPrefs.SetInt("AimAssist", level);
    }

    public void SetVignette(bool on)
    {
        Vignette = on;
        PlayerPrefs.SetInt("Vignette", on ? 1 : 0);
    }

    private void OnDraftDone(List<CardData> picked)
    {
        Orbit = new List<CardData>(picked);
        if (orbit != null) orbit.Fill(Orbit);
        SetState(FloorState.Hall);
    }

    private void Click()
    {
        if (clickSound != null) clickSound.Play();
    }
}
