# 단풍 후처리 1차 조정

- 적용 대상: `ProjectBeViolenceDefense/Assets/Settings/Stage/Stage_GoldenMaple_Volume.asset`의 Color Adjustments. Contrast 20→6, Saturation +12→-8. 이 두 값만 수정했다. 같은 프로필을 쓰는 단풍 프리팹과 기존 단풍 테스트 씬에 반영된다.
- Unity 6000.3.28f1의 복제 프로젝트 `/tmp/be-maple-grade-validation`에서 같은 프레임의 후처리 값만 바꾸어 전후 촬영했다. 원본 카메라의 준비/전투(600틱) 화면, 바닥만, 4종 유닛과 4색 화살표의 통제 비교, 경로 바닥 비교 등 총 5쌍이다.
- 통제 컷은 직교 크기 1.8, 같은 각도/배치/조명/모델을 사용했다. 비교를 위해 UI·기존 전투 유닛·임시 효과·날씨 파티클을 숨겼다. 경로 바닥 비교는 실제 경로 강조 재질을 바닥 전체에 사용한 검사 장면이다.
- 실제 URP 렌더 원본은 `captures/`에 있다. 비교 이미지는 크롭/리사이즈/라벨만 적용했으며 색 보정은 하지 않았다. `render-differences.txt`는 전후 렌더가 달라졌음을 확인하는 값이며 가독성 점수가 아니다.
- 실제 저장된 에셋을 복제본에 복사해 Unity로 다시 불러왔으며, 대비 6/채도 -8/노출 0.3과 프리팹 참조 보존을 확인했다. `profile-before.asset.txt`, `profile-after.asset.txt`에 변경 전후를 보관했다.
- `MapleGradeCapture.cs.txt`: 촬영과 에셋 검사 코드. 재현 시 before 에셋으로 Run을 호출하고, after 에셋으로 CheckSavedProfile을 호출한다. 검증 코드는 원본 Assets에 추가하지 않았다.
- 관찰: 강한 주황색과 명암 자극이 완화됐다. 바닥 질감·어두운 격자선과 일부 검은 유닛 디테일 문제는 남는다. 화면 전체에 적용되는 색 보정이므로 화살표와 장식 채도도 함께 낮아진다.
- 컴파일/촬영/에셋 검증 결과는 `results.txt`에 기록했다. Windows 빌드와 모든 웨이브/위치의 가독성은 미확인이다. 다른 맵의 설정은 수정하지 않았다.
