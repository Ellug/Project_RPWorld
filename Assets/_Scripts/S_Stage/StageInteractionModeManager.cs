using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Stage 인터랙션 모드 관리. 핫키: T(타일), B(건물), U(유닛), Tab(순환)
public class StageInteractionModeManager : MonoBehaviour
{
    [Header("Modes")]
    [SerializeField] private StageInteractionMode _startingMode = StageInteractionMode.TilePlacement;
    [SerializeField] private StageModeController[] _modes;

    [Header("Hotkeys")]
    [SerializeField] private Key _tileModeKey = Key.T;
    [SerializeField] private Key _buildingModeKey = Key.B;
    [SerializeField] private Key _unitModeKey = Key.U;
    [SerializeField] private Key _cycleModeKey = Key.Tab;
    [SerializeField] private bool _ignoreWhenTypingInUI = true;
    [SerializeField] private bool _logDebug = false;

    private readonly Dictionary<StageInteractionMode, StageModeController> _modeLookup = new();

    private static readonly StageInteractionMode[] ModeOrder =
    {
        StageInteractionMode.TilePlacement,
        StageInteractionMode.BuildingPlacement,
        StageInteractionMode.UnitControl
    };

    public StageInteractionMode CurrentMode { get; private set; }

    // 모드 변경 시 발생
    public event Action<StageInteractionMode> ModeChanged;

    private Keyboard Keyboard => Keyboard.current;

    private void Awake()
    {
        RebuildModeLookup();
    }

    private void Start()
    {
        if (_modeLookup.Count == 0)
        {
            if (_logDebug)
                Debug.LogWarning("[StageInteractionModeManager] No mode controllers found.");
            return;
        }

        if (!_modeLookup.ContainsKey(_startingMode))
            _startingMode = GetFirstAvailableMode();

        SetMode(_startingMode, true);
    }

    private void Update()
    {
        if (Keyboard == null)
            return;

        if (_ignoreWhenTypingInUI && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            return;

        if (Keyboard[_tileModeKey].wasPressedThisFrame)
            SetMode(StageInteractionMode.TilePlacement);
        else if (Keyboard[_buildingModeKey].wasPressedThisFrame)
            SetMode(StageInteractionMode.BuildingPlacement);
        else if (Keyboard[_unitModeKey].wasPressedThisFrame)
            SetMode(StageInteractionMode.UnitControl);
        else if (Keyboard[_cycleModeKey].wasPressedThisFrame)
            CycleMode();
    }

    public void SetMode(StageInteractionMode mode)
    {
        SetMode(mode, false);
    }

    private void SetMode(StageInteractionMode mode, bool force)
    {
        if (!force && CurrentMode == mode)
            return;

        if (!_modeLookup.TryGetValue(mode, out var target))
        {
            Debug.LogWarning($"[StageInteractionModeManager] Mode {mode} has no controller.");
            return;
        }

        foreach (var pair in _modeLookup)
        {
            if (pair.Value == null)
                continue;

            pair.Value.SetActive(pair.Key == mode);
        }

        CurrentMode = mode;

        if (_logDebug)
            Debug.Log($"[StageInteractionModeManager] Mode changed -> {mode}");

        ModeChanged?.Invoke(mode);
    }

    private void CycleMode()
    {
        if (_modeLookup.Count == 0)
            return;

        var next = GetNextAvailableMode(CurrentMode);
        SetMode(next);
    }

    private StageInteractionMode GetNextAvailableMode(StageInteractionMode current)
    {
        var index = Array.IndexOf(ModeOrder, current);
        if (index < 0)
            index = 0;

        for (var i = 1; i <= ModeOrder.Length; i++)
        {
            var candidate = ModeOrder[(index + i) % ModeOrder.Length];
            if (_modeLookup.ContainsKey(candidate))
                return candidate;
        }

        return current;
    }

    private StageInteractionMode GetFirstAvailableMode()
    {
        foreach (var mode in ModeOrder)
        {
            if (_modeLookup.ContainsKey(mode))
                return mode;
        }

        foreach (var pair in _modeLookup)
            return pair.Key;

        return StageInteractionMode.TilePlacement;
    }

    private void RebuildModeLookup()
    {
        _modeLookup.Clear();

        if (_modes == null || _modes.Length == 0)
            _modes = FindObjectsByType<StageModeController>(FindObjectsSortMode.None);

        foreach (var mode in _modes)
        {
            if (mode == null)
                continue;

            if (_modeLookup.ContainsKey(mode.Mode))
            {
                Debug.LogWarning($"[StageInteractionModeManager] Duplicate mode controller for {mode.Mode} on {mode.name}. Keeping first.");
                continue;
            }

            _modeLookup.Add(mode.Mode, mode);
        }
    }
}
