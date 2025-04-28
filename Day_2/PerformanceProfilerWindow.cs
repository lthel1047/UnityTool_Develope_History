using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class PerformanceProfilerWindow : EditorWindow
{
    // 탭 정의
    private enum Tab
    {
        Performance,
        Heatmap,
        AssetBudget,
        Scenario,
        Recording,
        Comparison,
        AutoOptimize,
        KPI,
        Reports
    }
    private Tab currentTab = Tab.Performance;

    [MenuItem("Tools/Performance Profiler")]
    public static void ShowWindow()
    {
        var window = GetWindow<PerformanceProfilerWindow>();
        window.titleContent = new GUIContent("PerformanceProfiler");
        window.minSize = new Vector2(600, 400);
    }

    #region GUI
    private void OnEnable()
    {
        // 에디터가 업데이트 될 때마다 성능 샘플링
        EditorApplication.update += SamplePerformance;
    }

    private void OnDisable()
    {
        EditorApplication.update -= SamplePerformance;
    }

    private void OnGUI()
    {
        DrawTabBar();
        GUILayout.Space(8);

        // 탭별 그리기 호출
        switch (currentTab)
        {
            case Tab.Performance: DrawPerformanceTab(); break;
            case Tab.Heatmap: DrawHeatmapTab(); break;
            case Tab.AssetBudget: DrawAssetBudgetTab(); break;
            case Tab.Scenario: DrawScenarioTab(); break;
            case Tab.Recording: DrawRecordingTab(); break;
            case Tab.Comparison: DrawComparisonTab(); break;
            case Tab.AutoOptimize: DrawAutoOptimizeTab(); break;
            case Tab.Reports: DrawReportsTab(); break;
        }
    }
    private void DrawTabBar()
    {
        // 탭 버튼을 툴바 스타일로 나열
        var names = System.Enum.GetNames(typeof(Tab));
        currentTab = (Tab)GUILayout.Toolbar((int)currentTab, names, EditorStyles.toolbarButton);
    }
    #endregion

    #region Performance Tab
    // Performance 탭: 최신 샘플을 화면에 표시합니다.
    private void DrawPerformanceTab()
    {
        if (samples.Count == 0)
        {
            GUILayout.Label("No performance data sampled yet.");
            return;
        }

        var last = samples[samples.Count - 1];
        GUILayout.Label($"FPS:           {last.fps:F1}");
        GUILayout.Label($"Memory:        {last.memoryMB:F1} MB");
        GUILayout.Label($"Draw Calls:    {last.drawCalls}");
        GUILayout.Label($"Static Batches:{last.staticBatches}");

        // — Warnings 탭에서 가져온 경고 로직:
        if (last.fps < fpsThreshold)
            EditorGUILayout.HelpBox(
                $"FPS dropped below threshold ({fpsThreshold} FPS)!",
                MessageType.Warning
            );  // :contentReference[oaicite:0]{index=0}&#8203;:contentReference[oaicite:1]{index=1}
    }
    #endregion

    #region Heatmap Tab
    // Heatmap 탭: 시간 축별 히트맵 뷰 (현재 기본 자리 표시)
    private void DrawHeatmapTab()
    {
        GUILayout.Label("Heatmap view is under development.");
        // TODO: 타임라인 기반 프레임 드롭 히트맵 구현
    }
    #endregion

    #region AssetBudget Tab
    // AssetBudget 탭: 씬 내 에셋별 DrawCall·메모리 순위
    private void DrawAssetBudgetTab()
    {
        if (GUILayout.Button("Analyze Assets"))
            AnalyzeAssets();  // :contentReference[oaicite:0]{index=0}&#8203;:contentReference[oaicite:1]{index=1}

        // DrawCall 순으로 상위 10개만 표시
        foreach (var stat in assetStats.Values
                 .OrderByDescending(a => a.drawCalls)
                 .Take(10))
        {
            // memory는 KB 단위로 표시
            GUILayout.Label($"{stat.name}: DrawCalls={stat.drawCalls}, Mem={(stat.memory / 1024f):F1} KB");
        }
    }
    #endregion

    #region Sampling
    // 샘플링 간격 및 버퍼 정의
    private const float SampleInterval = 0.5f;
    private double lastSampleTime;
    private readonly List<PerfSample> samples = new List<PerfSample>();

    private void SamplePerformance()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - lastSampleTime < SampleInterval) return;
        var sample = new PerfSample
        {
            time = (float)now,
            fps = 1f / Time.unscaledDeltaTime,
            memoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
            drawCalls = UnityStats.drawCalls,
            staticBatches = UnityStats.staticBatches
        };
        samples.Add(sample);
        if (isRecording) recordBuffer.Add(sample);
        lastSampleTime = now;
        Repaint();
    }
    #endregion

    #region AssetBudget
    // 씬 내 메쉬 사용량 기반 에셋 집계
    private Dictionary<string, AssetStats> assetStats = new Dictionary<string, AssetStats>();

    private void AnalyzeAssets()
    {
        assetStats.Clear();
        foreach (var mf in FindObjectsOfType<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var key = mf.sharedMesh.name;
            if (!assetStats.ContainsKey(key)) assetStats[key] = new AssetStats(key);
            assetStats[key].drawCalls++;
            assetStats[key].memory += Profiler.GetRuntimeMemorySizeLong(mf.sharedMesh);
        }
    }
    #endregion

    #region Scenario
    private List<SceneAsset> scenarioAssets = new List<SceneAsset>();
    private int selectedScenarioIndex = 0;
    private bool isScenarioRunning = false;
    private float scenarioStartTime;
    private float scenarioDuration = 10f;
    private List<PerfSample> scenarioSamples = new List<PerfSample>();

    private void DrawScenarioTab()
    {
        GUILayout.Label("▶ Scenario Manager", EditorStyles.boldLabel);

        // 1) Duration 설정
        scenarioDuration = EditorGUILayout.FloatField("Duration (sec)", scenarioDuration);

        // 2) 리스트 편집 UI
        for (int i = 0; i < scenarioAssets.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            // SceneAsset ObjectField 로 씬 에셋 지정
            scenarioAssets[i] = (SceneAsset)EditorGUILayout.ObjectField(
                scenarioAssets[i], typeof(SceneAsset), false);

            // 삭제 버튼
            if (GUILayout.Button("－", GUILayout.Width(20)))
            {
                scenarioAssets.RemoveAt(i);
                if (selectedScenarioIndex >= scenarioAssets.Count)
                    selectedScenarioIndex = Mathf.Max(0, scenarioAssets.Count - 1);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        // 항목 추가 버튼
        if (GUILayout.Button("+ Add Scenario"))
        {
            scenarioAssets.Add(null);
        }

        GUILayout.Space(6);

        // 3) 선택 팝업 및 실행
        var names = scenarioAssets
            .Select(a => a != null ? a.name : "(None)")
            .ToArray();
        selectedScenarioIndex = EditorGUILayout.Popup("Select Scenario", selectedScenarioIndex, names);

        if (!isScenarioRunning)
        {
            if (GUILayout.Button("▶ Run Scenario Test"))
            {
                var asset = scenarioAssets[selectedScenarioIndex];
                if (asset == null)
                {
                    EditorUtility.DisplayDialog("Error", "씬 에셋을 선택하세요.", "OK");
                }
                else
                {
                    // 씬 로드
                    var path = AssetDatabase.GetAssetPath(asset);
                    EditorSceneManager.OpenScene(path);

                    // 샘플 초기화 및 시작
                    scenarioSamples.Clear();
                    isScenarioRunning = true;
                    scenarioStartTime = (float)EditorApplication.timeSinceStartup;
                }
            }
        }
        else
        {
            // 진행 상태 표시 및 샘플 수집
            float elapsed = (float)(EditorApplication.timeSinceStartup - scenarioStartTime);
            EditorGUILayout.LabelField($"Running: {elapsed:F1}s / {scenarioDuration:F1}s");
            if (samples.Any()) scenarioSamples.Add(samples.Last());

            // 완료 처리
            if (elapsed >= scenarioDuration)
            {
                isScenarioRunning = false;
                float avgFps = scenarioSamples.Average(s => s.fps);
                float avgMem = scenarioSamples.Average(s => s.memoryMB);
                EditorUtility.DisplayDialog(
                    "Scenario Test Complete",
                    $"Scene: {scenarioAssets[selectedScenarioIndex].name}\n" +
                    $"Avg FPS: {avgFps:F1}\nAvg Mem: {avgMem:F1} MB",
                    "OK"
                );
            }
        }
    }
    #endregion

    #region Recording
    // 녹화 및 재생
    private bool isRecording;
    private List<PerfSample> recordBuffer = new List<PerfSample>();
    private int replayIndex;
    private bool isReplaying;

    private void DrawRecordingTab()
    {
        if (!isRecording)
        {
            if (GUILayout.Button("Start Recording")) { recordBuffer.Clear(); isRecording = true; }
        }
        else if (GUILayout.Button("Stop Recording")) isRecording = false;

        if (recordBuffer.Any())
        {
            if (!isReplaying && GUILayout.Button("Start Replay")) { isReplaying = true; replayIndex = 0; }
            if (isReplaying)
            {
                var s = recordBuffer[replayIndex++ % recordBuffer.Count];
                GUILayout.Label($"Replay FPS: {s.fps:F1}, Mem: {s.memoryMB:F1}MB");
                if (GUILayout.Button("Stop Replay")) isReplaying = false;
            }
        }
    }
    #endregion

    #region Comparison
    // 멀티플랫폼 CSV 비교
    private Dictionary<string, List<PerfSample>> platformData = new Dictionary<string, List<PerfSample>>();

    private void DrawComparisonTab()
    {
        if (GUILayout.Button("Load CSV File"))
        {
            var path = EditorUtility.OpenFilePanelWithFilters("Select CSV", "", new[] { "CSV", "csv" });
            if (!string.IsNullOrEmpty(path))
            {
                platformData.Clear();
                var name = Path.GetFileNameWithoutExtension(path);
                platformData[name] = LoadCsv(path);
            }
        }
        foreach (var kv in platformData)
            GUILayout.Label($"{kv.Key}: {kv.Value.Last().fps:F1} FPS");
    }
    #endregion

    #region AutoOptimize
    // 자동 최적화 권고
    private List<string> optimizeRecommendations = new List<string>();

    private void DrawAutoOptimizeTab()
    {
        if (GUILayout.Button("Run Scan"))
        {
            optimizeRecommendations.Clear();
            foreach (var stat in assetStats.Values.Where(a => a.drawCalls > 50))
                optimizeRecommendations.Add($"Consider static batching for {stat.name}");
        }
        foreach (var rec in optimizeRecommendations)
            EditorGUILayout.HelpBox(rec, MessageType.Info);
    }
    #endregion

    #region Warnings
    // KPI 미달 경고
    private float fpsThreshold = 30f;

    private void DrawWarningsTab()
    {
        if (samples.Any() && samples.Last().fps < fpsThreshold)
            EditorGUILayout.HelpBox("FPS dropped below threshold!", MessageType.Warning);
    }
    #endregion

    #region Reports
    // 데이터 CSV 내보내기
    private void DrawReportsTab()
    {
        if (GUILayout.Button("Export Samples CSV"))
        {
            var path = EditorUtility.SaveFilePanel("Export CSV", "", "samples.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                var lines = new List<string> { "time,fps,memMB,drawCalls,staticBatches" };
                lines.AddRange(samples.Select(s => $"{s.time:F2},{s.fps:F1},{s.memoryMB:F1},{s.drawCalls},{s.staticBatches}"));
                File.WriteAllLines(path, lines);
                EditorUtility.DisplayDialog("Export", "Saved to " + path, "OK");
            }
        }
    }
    #endregion

    // CSV 로드 유틸
    private List<PerfSample> LoadCsv(string path)
    {
        var list = new List<PerfSample>();
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length > 1 && float.TryParse(parts[1], out float fps))
                list.Add(new PerfSample { fps = fps });
        }
        return list;
    }

    // 데이터 구조
    private class PerfSample { public float time, fps, memoryMB; public int drawCalls, staticBatches; }
    private class AssetStats { public string name; public int drawCalls; public long memory; public AssetStats(string n) { name = n; } }


}
