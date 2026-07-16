using UnityEngine;
using System.Collections.Generic;

public class OnScreenConsole : MonoBehaviour
{
    private struct LogEntry
    {
        public string message;
        public string stackTrace;
        public LogType type;
    }

    private List<LogEntry> _logs = new List<LogEntry>();
    private bool _showConsole = true;
    private Vector2 _scrollPos;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var go = new GameObject("OnScreenConsole");
        DontDestroyOnLoad(go);
        go.AddComponent<OnScreenConsole>();
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        _logs.Add(new LogEntry { message = logString, stackTrace = stackTrace, type = type });
        if (_logs.Count > 80) _logs.RemoveAt(0);
        _scrollPos.y = float.MaxValue; // Auto-scroll to bottom
    }

    private void OnGUI()
    {
        // Initialize styles with larger fonts for mobile screens
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.02f);
            
            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.025f);
        }

        // Toggle button in the top right corner
        float btnWidth = Screen.width * 0.2f;
        float btnHeight = Screen.height * 0.05f;
        if (GUI.Button(new Rect(Screen.width - btnWidth - 10, 10, btnWidth, btnHeight), _showConsole ? "Hide Log" : "Show Log", _buttonStyle))
        {
            _showConsole = !_showConsole;
        }

        if (!_showConsole) return;

        // Display panel at the bottom half of the screen
        float panelHeight = Screen.height * 0.45f;
        float panelWidth = Screen.width - 20f;
        float panelY = Screen.height - panelHeight - 10f;

        GUILayout.BeginArea(new Rect(10, panelY, panelWidth, panelHeight), GUI.skin.box);
        
        _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Width(panelWidth - 10), GUILayout.Height(panelHeight - 15));
        
        foreach (var log in _logs)
        {
            Color originalColor = GUI.contentColor;
            if (log.type == LogType.Error || log.type == LogType.Exception)
                GUI.contentColor = Color.red;
            else if (log.type == LogType.Warning)
                GUI.contentColor = Color.yellow;
            else
                GUI.contentColor = Color.white;

            GUILayout.Label($"[{log.type}] {log.message}", _labelStyle);
            if (log.type == LogType.Exception && !string.IsNullOrEmpty(log.stackTrace))
            {
                GUI.contentColor = new Color(1f, 0.6f, 0.6f);
                GUILayout.Label(log.stackTrace, _labelStyle);
            }
            
            GUI.contentColor = originalColor;
        }
        
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
