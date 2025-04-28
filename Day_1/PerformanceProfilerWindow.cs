using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using System.Collections.Generic;
using System.Linq;

public class PerformanceProfilerWindow : EditorWindow
{
    private enum Tab
    {
        Performance,
        Heatmap,
        AssetBudget,
        Scenario,
        Warnings,
        KPI,
        Reports
    }
    private Tab currentTab = Tab.Performance;

    // 샘플링 버퍼
    private const float SampleInterval = 0.5f;
    private double lastSampleTime;
    private readonly List<float> fpsHistory = new List<float>();
    private readonly List<long> memHistory = new List<long>();
    private readonly List<int> drawCallHistory = new List<int>();
    private readonly List<int> staticBatchHistory = new List<int>();
    private readonly List<int> dynamicBatchHistory = new List<int>();

    // KPI 설정
    private float fpsThreshold = 30f;
    private long memoryThreshold = 500 * 1024 * 1024;
    private int drawCallThreshold = 100;
    private int staticBatchThreshold = 50;
    private int dynamicBatchThreshold = 100;
    private Dictionary<string, float> customKPIs = new Dictionary<string, float>();

    // 에셋 버짓 데이터
    private Dictionary<string, AssetStats> assetStats = new Dictionary<string, AssetStats>();

    // 시나리오 목록
    private string[] scenarios = new string[] { "Level1", "BossStage", "TownScene" };
    private int selectedScenario = 0;

    [MenuItem("Tools/Performance Profiler")]
    public static void ShowWindow()
    {
        var window = GetWindow<PerformanceProfilerWindow>();
        window.titleContent = new GUIContent("Perf Toolkit");
        window.minSize = new Vector2(600, 400);
    }

    private void OnEnable()
    {
        lastSampleTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += UpdateSampling;
        AnalyzeAssets(); // 초기 에셋 데이터 분석
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdateSampling;
        fpsHistory.Clear(); memHistory.Clear(); drawCallHistory.Clear();
        staticBatchHistory.Clear(); dynamicBatchHistory.Clear();
    }

    private void UpdateSampling()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - lastSampleTime < SampleInterval) return;

        fpsHistory.Add(1f / Time.unscaledDeltaTime);
        memHistory.Add(Profiler.GetTotalAllocatedMemoryLong());
        drawCallHistory.Add(UnityEditor.UnityStats.drawCalls);
        staticBatchHistory.Add(UnityEditor.UnityStats.staticBatches);
        dynamicBatchHistory.Add(UnityEditor.UnityStats.dynamicBatches);

        lastSampleTime = now;
        Repaint();
    }

    private void OnGUI()
    {
        // 탭 바
        currentTab = (Tab)GUILayout.Toolbar((int)currentTab, System.Enum.GetNames(typeof(Tab)));
        GUILayout.Space(5);
        switch (currentTab)
        {
            case Tab.Performance: DrawPerformance(); break;
            case Tab.Heatmap: DrawHeatmap(); break;
            case Tab.AssetBudget: DrawAssetBudget(); break;
            case Tab.Scenario: DrawScenario(); break;
            case Tab.Warnings: DrawWarnings(); break;
            case Tab.KPI: DrawKPISettings(); break;
            case Tab.Reports: DrawReports(); break;
        }
    }

    #region Performance
    private void DrawPerformance()
    {
        GUILayout.Label("=== Real-time Performance ===", EditorStyles.boldLabel);
        GUILayout.Label("FPS: " + LastValue(fpsHistory));
        GUILayout.Label("Memory: " + (LastValueLong(memHistory) / (1024f * 1024f)).ToString("F1") + " MB");
        GUILayout.Label("Draw Calls: " + LastValue(drawCallHistory));
        GUILayout.Label("Static Batches: " + LastValue(staticBatchHistory));
        GUILayout.Label("Dynamic Batches: " + LastValue(dynamicBatchHistory));
    }
    #endregion

    #region Heatmap
    private void DrawHeatmap()
    {
        GUILayout.Label("=== Section Heatmap ===", EditorStyles.boldLabel);
        // 타임라인에서 FPS 드롭 구간 색상 표시 (녹→적)
        Rect r = GUILayoutUtility.GetRect(500, 50);
        for (int i = 0; i < fpsHistory.Count; i++)
        {
            float t = Mathf.InverseLerp(0, fpsThreshold * 2, fpsHistory[i]);
            Color c = Color.Lerp(Color.red, Color.green, t);
            EditorGUI.DrawRect(new Rect(r.x + i * (r.width / fpsHistory.Count), r.y, r.width / fpsHistory.Count, r.height), c);
        }
    }
    private void AnalyzeAssets()
    {
        assetStats.Clear();
        // 씬에 존재하는 모든 MeshFilter 기준 집계 (DrawCall, Memory 등)  
        foreach (var mf in FindObjectsOfType<MeshFilter>())
        {
            string key = mf.sharedMesh.name;
            if (!assetStats.ContainsKey(key)) assetStats[key] = new AssetStats(key);
            assetStats[key].drawCalls += 1;
            assetStats[key].memory += Profiler.GetRuntimeMemorySizeLong(mf.sharedMesh);
        }
    }

    private void DrawAssetBudget()
    {
        GUILayout.Label("=== Asset Budget Dashboard ===", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Asset", GUILayout.Width(200));
        GUILayout.Label("Draw Calls", GUILayout.Width(100));
        GUILayout.Label("Memory (MB)", GUILayout.Width(100));
        GUILayout.EndHorizontal();
        foreach (var stat in assetStats.Values.OrderByDescending(a => a.drawCalls).Take(10))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(stat.name, GUILayout.Width(200));
            GUILayout.Label(stat.drawCalls.ToString(), GUILayout.Width(100));
            GUILayout.Label((stat.memory / (1024f * 1024f)).ToString("F1"), GUILayout.Width(100));
            GUILayout.EndHorizontal();
        }
    }

    private class AssetStats
    {
        public string name;
        public int drawCalls;
        public long memory;
        public AssetStats(string n) { name = n; drawCalls = 0; memory = 0; }
    }
    #endregion

    #region Scenario
    private void DrawScenario()
    {
        GUILayout.Label("=== Scenario Test Mode ===", EditorStyles.boldLabel);
        selectedScenario = EditorGUILayout.Popup("Scenario", selectedScenario, scenarios);
        if (GUILayout.Button("Run Scenario"))
        {
            // 예: 씬 로드 후 일정 시간 측정 자동 실행
            EditorUtility.DisplayDialog("Scenario", scenarios[selectedScenario] + " started.", "OK");
        }
    }
    #endregion

    #region Warnings
    private void DrawWarnings()
    {
        GUILayout.Label("=== Designer Warnings ===", EditorStyles.boldLabel);
        if (LastValueFPS() < fpsThreshold)
            EditorGUILayout.HelpBox("FPS dropped below " + fpsThreshold + ". Optimize assets or code.", MessageType.Warning);
        if (LastValue(drawCallHistory) > drawCallThreshold)
            EditorGUILayout.HelpBox("Draw Calls exceed " + drawCallThreshold + ". Consider batching.", MessageType.Warning);
        // 추가 경고...
    }

    private float LastValue(List<float> list) { return list.Count > 0 ? list[list.Count - 1] : 0; }
    private long LastValueLong(List<long> list) { return list.Count > 0 ? list[list.Count - 1] : 0; }
    private int LastValue(List<int> list) { return list.Count > 0 ? list[list.Count - 1] : 0; }
    private float LastValueFPS() { return LastValue(fpsHistory); }
    #endregion

    #region KPI
    private void DrawKPISettings()
    {
        GUILayout.Label("=== KPI Settings ===", EditorStyles.boldLabel);
        fpsThreshold = EditorGUILayout.FloatField("FPS Threshold", fpsThreshold);
        memoryThreshold = EditorGUILayout.LongField("Memory Threshold", memoryThreshold);
        // 사용자 정의 KPI 추가
        GUILayout.Space(5);
        if (GUILayout.Button("Add Custom KPI")) customKPIs.Add("NewKPI", 0f);
        foreach (var key in customKPIs.Keys.ToList())
        {
            customKPIs[key] = EditorGUILayout.FloatField(key, customKPIs[key]);
        }
    }
    #endregion

    #region Reports
    private void DrawReports()
    {
        GUILayout.Label("=== Reports Export ===", EditorStyles.boldLabel);
        if (GUILayout.Button("Export CSV"))
        {
            string path = EditorUtility.SaveFilePanel("Save CSV", "", "report.csv", "csv");
            if (!string.IsNullOrEmpty(path))
                ExportCSV(path);
        }
    }

    private void ExportCSV(string path)
    {
        var lines = new List<string> { "Metric,Value" };
        lines.Add("FPS," + LastValue(fpsHistory));
        lines.Add("Memory," + LastValueLong(memHistory));
        System.IO.File.WriteAllLines(path, lines);
        EditorUtility.DisplayDialog("Export", "CSV saved to " + path, "OK");
    }
    #endregion
}
