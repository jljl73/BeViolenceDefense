# Netmarble Medium TMP 적용 결과물

아래 프로젝트 경로는 `ProjectBeViolenceDefense/` 기준이다. 파일과 Unity 메타데이터는 워킹 트리에 적용했다.

| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `Assets/Fonts/netmarbleM.ttf` 및 `.meta` | Codex, Unity Editor | 생성 | 제공한 ZIP의 Medium 파일을 그대로 추출하고 Unity에서 임포트했다. SDF 원본으로 사용 |
| `Assets/TextMesh Pro/Resources/Fonts & Materials/Netmarble Medium SDF.asset` 및 `.meta` | Unity Editor | 생성 | 동적 SDF 폰트, 아틀라스, 머티리얼. 기본 글꼴로 사용 |
| `Assets/TextMesh Pro/Resources/TMP Settings.asset` 및 `.meta` | Codex, Unity Editor | 생성 | Unity 제공 설정에서 Netmarble Medium SDF를 프로젝트 기본으로 지정 |
| `Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset` 및 `.meta` | Codex | 생성 | Unity 제공 기본 스타일 시트. TMP Settings에 연결 |
| `Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt` 및 `.meta` | Codex | 생성 | Unity 제공 줄바꿈 문자 리소스. TMP Settings에 연결 |
| `Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt` 및 `.meta` | Codex | 생성 | Unity 제공 줄바꿈 문자 리소스. TMP Settings에 연결 |
| `Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader` 및 `.meta` | Codex | 생성 | Unity 제공 SDF 셰이더. 기본 글꼴 머티리얼에서 사용 |
| `Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc` 및 `.meta` | Codex | 생성 | Unity 제공 SDF 셰이더의 의존 파일 |
| `Assets/TextMesh Pro.meta`, `Assets/TextMesh Pro/Resources.meta`, `Resources/Fonts & Materials.meta`, `Resources/Style Sheets.meta`, `Shaders.meta` | Codex | 생성 | Unity 제공 TMP 폴더 메타데이터 |
| `Assets/Scripts/DefenseHud.cs` | Codex | 수정 | HUD 라벨을 TextMeshProUGUI로 전환하고 프로젝트 기본 글꼴을 적용 |
| `Assets/Scripts/AssetLoading/AssetPaths.g.cs` | Unity Editor, 프로젝트 AssetPathsGenerator | 수정 | TMP Resources 경로 자동 반영 |
