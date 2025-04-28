![Image](https://github.com/user-attachments/assets/27193c0b-2efa-43f3-8c27-904d9c03619d)

레딧에 올려서 다양한 의견을 받아서 기능을 추가 해보았습니다.

스팀덱 등 조금 더 다양한 플래폼에서도 하드웨어 성능을 테스트 할 수 있도록 추가했습니다.

데이터를 수집해 현재 선택된 플랫폼과 비교해 디바이스의 한계를 테스트를 해볼 수 있습니다.

```ruby
  private class HardwareProfile
  {
      public string name;
      public float targetFPS;
      public float memLimitMB;
      public int maxDrawCalls;
      public int maxStaticBatches;
      public List<string> unsupportedFeatures;
      public HardwareProfile(string n, float fps, float mem, int dc, int sb, List<string> uf)
      {
          name = n; targetFPS = fps; memLimitMB = mem;
          maxDrawCalls = dc; maxStaticBatches = sb;
          unsupportedFeatures = uf;
      }
  }
  
  private List<HardwareProfile> hwProfiles = new List<HardwareProfile>
  {
      new HardwareProfile("Nintendo Switch", 30f, 2048f, 500, 200, new List<string>{"Realtime GI", "MSAA"}),
      new HardwareProfile("Steam Deck",      60f, 4096f, 800, 300, new List<string>{"HDR", "Volumetric Fog"}),
      new HardwareProfile("Generic Mobile",  30f, 1024f, 300, 100, new List<string>{"Shadows", "PostProcessing"})
  };
  private int selectedHw = 0;
  private void DrawHardwareTab()
  {
      GUILayout.Label("▶ Hardware Simulation", EditorStyles.boldLabel);
  
      // 1) 프로파일 선택
      selectedHw = EditorGUILayout.Popup(
          "Profile",
          selectedHw,
          hwProfiles.Select(p => p.name).ToArray()
      );
      var prof = hwProfiles[selectedHw];
  
      // 2) 프로파일 스펙 표시
      EditorGUILayout.LabelField($"Target FPS:         {prof.targetFPS}");
      EditorGUILayout.LabelField($"Memory Limit (MB):  {prof.memLimitMB}");
      EditorGUILayout.LabelField($"Max Draw Calls:     {prof.maxDrawCalls}");
      EditorGUILayout.LabelField($"Max Static Batches: {prof.maxStaticBatches}");
  
      // 3) 현재 샘플과 비교해서 경고
      if (samples.Count > 0)
      {
          var last = samples[samples.Count - 1];
          if (last.fps < prof.targetFPS)
              EditorGUILayout.HelpBox($"[Warning] FPS ({last.fps:F1}) below {prof.targetFPS}", MessageType.Warning);
          if (last.memoryMB > prof.memLimitMB)
              EditorGUILayout.HelpBox($"[Warning] Memory ({last.memoryMB:F1} MB) exceeds {prof.memLimitMB} MB", MessageType.Warning);
          if (last.drawCalls > prof.maxDrawCalls)
              EditorGUILayout.HelpBox($"[Warning] DrawCalls ({last.drawCalls}) exceeds {prof.maxDrawCalls}", MessageType.Warning);
          if (last.staticBatches > prof.maxStaticBatches)
              EditorGUILayout.HelpBox($"[Warning] StaticBatches ({last.staticBatches}) exceeds {prof.maxStaticBatches}", MessageType.Warning);
      }
  
      GUILayout.Space(6);
  
      // 4) 지원하지 않는 기능 안내
      EditorGUILayout.LabelField("Unsupported Features:", EditorStyles.boldLabel);
      foreach (var feat in prof.unsupportedFeatures)
          EditorGUILayout.LabelField($"• {feat}", EditorStyles.helpBox);
  }
  #endregion
```
