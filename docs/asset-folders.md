# Assets 폴더 정리 (2026-11-18)

LuckyNumber의 번호·역할별 분류를 참고했다. Addressables 폴더·패키지는 추가하지 않았다.

- 01_Scenes: Bootstrap / Menu / Gameplay / Test
- 02_Scripts: Core / Input / Player / Enemies / Combat / World / UI
- 03_Prefabs: Core / Characters / Enemies / Combat / World / UI
- 04_Data: Config / Items / Enemies / Balance
- 05_Art: Sprites / Animations / Materials / VFX / Tiles / Meshes / Textures
- 06_Audio: Music / SFX / Ambience
- 07_UI: Fonts / Icons / Layouts
- 08_Input: Input Actions
- 09_Tests: Validation / Editor(EditMode 테스트)

Resources, Settings, TextMesh Pro, TutorialInfo, ai.meshy는 기존 위치를 유지한다. Resources 내부 경로와 로딩 키는 동일하다. 캐릭터의 모델·텍스처·애니메이션·재질은 모델별 묶음을 유지한다. 빈 분류는 Unity 폴더 메타로 버전 관리한다.

## 이동 목록

| 이전 경로 | 현재 경로 |
| --- | --- |
| ProjectBeViolenceDefense/Assets/Art | ProjectBeViolenceDefense/Assets/05_Art |
| ProjectBeViolenceDefense/Assets/Fonts | ProjectBeViolenceDefense/Assets/07_UI/00_Fonts |
| ProjectBeViolenceDefense/Assets/InputSystem_Actions.inputactions | ProjectBeViolenceDefense/Assets/08_Input/InputSystem_Actions.inputactions |
| ProjectBeViolenceDefense/Assets/Scenes | ProjectBeViolenceDefense/Assets/01_Scenes |
| ProjectBeViolenceDefense/Assets/Scripts | ProjectBeViolenceDefense/Assets/02_Scripts |
| ProjectBeViolenceDefense/Assets/Art/EncounterIcon.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/EncounterIcon.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies |
| ProjectBeViolenceDefense/Assets/Art/Heroes | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes |
| ProjectBeViolenceDefense/Assets/Art/Hud | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud |
| ProjectBeViolenceDefense/Assets/Art/RouteChevron.png | ProjectBeViolenceDefense/Assets/05_Art/00_Sprites/RouteChevron.png |
| ProjectBeViolenceDefense/Assets/Art/WoodBlock_BaseColor.png | ProjectBeViolenceDefense/Assets/05_Art/06_Textures/WoodBlock/WoodBlock_BaseColor.png |
| ProjectBeViolenceDefense/Assets/Art/WoodBlock_Normal.png | ProjectBeViolenceDefense/Assets/05_Art/06_Textures/WoodBlock/WoodBlock_Normal.png |
| ProjectBeViolenceDefense/Assets/Art/WoodBlock.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/WoodBlock.fbx |
| ProjectBeViolenceDefense/Assets/Art/WoodBlock.mat | ProjectBeViolenceDefense/Assets/05_Art/02_Materials/WoodBlock.mat |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt_1500 | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500 |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Skeleton | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Skeleton |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Werewolf | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Werewolf |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Wolf | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Wolf |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Zombie | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Zombie |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt/Goblin_Grunt_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt/Goblin_Grunt_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt/Goblin_Grunt_Run.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt/Goblin_Grunt_Run.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt/Goblin_Grunt_Walk.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt/Goblin_Grunt_Walk.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt/Goblin_Grunt.controller | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt/Goblin_Grunt.controller |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt/Goblin_Grunt.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt/Goblin_Grunt.mat |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_Combat.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_Combat.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_HitAlt.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_HitAlt.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_Run.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_Run.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_Walk.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500_Walk.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500.controller | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500.controller |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500.mat |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor/LivingArmor_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor_Combat.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor/LivingArmor_Combat.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor_Sword_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor/LivingArmor_Sword_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor_Sword.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor/LivingArmor_Sword.mat |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor_Walk.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor/LivingArmor_Walk.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor_WithSword.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor/LivingArmor_WithSword.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor.controller | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor/LivingArmor.controller |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/LivingArmor/LivingArmor.mat |
| ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/LivingArmor.prefab | ProjectBeViolenceDefense/Assets/03_Prefabs/02_Enemies/LivingArmor.prefab |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Skeleton/Skeleton_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Skeleton/Skeleton_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Skeleton/Skeleton_Combat.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Skeleton/Skeleton_Combat.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Skeleton/Skeleton_Walk.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Skeleton/Skeleton_Walk.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Skeleton/Skeleton.controller | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Skeleton/Skeleton.controller |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Skeleton/Skeleton.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Skeleton/Skeleton.mat |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Werewolf/Werewolf_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Werewolf/Werewolf_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Werewolf/Werewolf_Combat.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Werewolf/Werewolf_Combat.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Werewolf/Werewolf_Walk.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Werewolf/Werewolf_Walk.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Werewolf/Werewolf.controller | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Werewolf/Werewolf.controller |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Werewolf/Werewolf.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Werewolf/Werewolf.mat |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Wolf/Textures | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Wolf/Textures |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Wolf/Wolf.controller | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Wolf/Wolf.controller |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Wolf/Wolf.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Wolf/Wolf.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Wolf/Wolf.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Wolf/Wolf.mat |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Wolf/Textures/Wolf_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Wolf/Textures/Wolf_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Wolf/Textures/Wolf_Rigged.fbm | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Wolf/Textures/Wolf_Rigged.fbm |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Zombie/Zombie_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Zombie/Zombie_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Zombie/Zombie_Combat.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Zombie/Zombie_Combat.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Zombie/Zombie_Walk.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Zombie/Zombie_Walk.fbx |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Zombie/Zombie.controller | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Zombie/Zombie.controller |
| ProjectBeViolenceDefense/Assets/Art/Enemies/Zombie/Zombie.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Zombie/Zombie.mat |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster_Staff | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster_Staff |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster/Caster_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster/Caster_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster/Caster_Combat.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster/Caster_Combat.fbx |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster/Caster_Walk.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster/Caster_Walk.fbx |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster/Caster_WithStaff.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster/Caster_WithStaff.fbx |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster/Caster.controller | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster/Caster.controller |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster/Caster.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster/Caster.mat |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster/Caster.prefab | ProjectBeViolenceDefense/Assets/03_Prefabs/01_Characters/Caster.prefab |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster_Staff/Caster_Staff_Color.png | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster_Staff/Caster_Staff_Color.png |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster_Staff/Caster_Staff.fbx | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster_Staff/Caster_Staff.fbx |
| ProjectBeViolenceDefense/Assets/Art/Heroes/Caster_Staff/Caster_Staff.mat | ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Heroes/Caster_Staff/Caster_Staff.mat |
| ProjectBeViolenceDefense/Assets/Art/Hud/AttackBuff.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/AttackBuff.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Button.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Button.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Core.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Core.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Enemy.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Enemy.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Heal.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Heal.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Lock.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Lock.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Mana.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Mana.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/MoveSpeedBuff.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/MoveSpeedBuff.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Panel.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Panel.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Restart.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Restart.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Round.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Round.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Start.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Start.png |
| ProjectBeViolenceDefense/Assets/Art/Hud/Wall.png | ProjectBeViolenceDefense/Assets/07_UI/01_Icons/Hud/Wall.png |
| ProjectBeViolenceDefense/Assets/Fonts/netmarble_font.zip | ProjectBeViolenceDefense/Assets/07_UI/00_Fonts/netmarble_font.zip |
| ProjectBeViolenceDefense/Assets/Fonts/netmarbleM.ttf | ProjectBeViolenceDefense/Assets/07_UI/00_Fonts/netmarbleM.ttf |
| ProjectBeViolenceDefense/Assets/Scenes/GameScene.unity | ProjectBeViolenceDefense/Assets/01_Scenes/02_Gameplay/GameScene.unity |
| ProjectBeViolenceDefense/Assets/Scripts/AssetLoading | ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/AssetLoading |
| ProjectBeViolenceDefense/Assets/Scripts/DefenseBattle.cs | ProjectBeViolenceDefense/Assets/02_Scripts/04_Combat/DefenseBattle.cs |
| ProjectBeViolenceDefense/Assets/Scripts/DefenseBoardView.cs | ProjectBeViolenceDefense/Assets/02_Scripts/05_World/DefenseBoardView.cs |
| ProjectBeViolenceDefense/Assets/Scripts/DefenseGrid.cs | ProjectBeViolenceDefense/Assets/02_Scripts/05_World/DefenseGrid.cs |
| ProjectBeViolenceDefense/Assets/Scripts/DefenseHud.cs | ProjectBeViolenceDefense/Assets/02_Scripts/06_UI/DefenseHud.cs |
| ProjectBeViolenceDefense/Assets/Scripts/EnemyUnit.cs | ProjectBeViolenceDefense/Assets/02_Scripts/03_Enemies/EnemyUnit.cs |
| ProjectBeViolenceDefense/Assets/Scripts/UI | ProjectBeViolenceDefense/Assets/02_Scripts/06_UI |
| ProjectBeViolenceDefense/Assets/Scripts/VerifyEnemyGroupsOnce.cs | ProjectBeViolenceDefense/Assets/09_Tests/Validation/VerifyEnemyGroupsOnce.cs |
| ProjectBeViolenceDefense/Assets/Scripts/VoxelDefensePrototype.cs | ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/Flow/VoxelDefensePrototype.cs |
| ProjectBeViolenceDefense/Assets/Scripts/AssetLoading/AssetLoader.cs | ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/AssetLoading/AssetLoader.cs |
| ProjectBeViolenceDefense/Assets/Scripts/AssetLoading/AssetPaths.g.cs | ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/AssetLoading/AssetPaths.g.cs |
| ProjectBeViolenceDefense/Assets/Scripts/AssetLoading/AssetScope.cs | ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/AssetLoading/AssetScope.cs |
| ProjectBeViolenceDefense/Assets/Scripts/AssetLoading/Editor | ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/AssetLoading/Editor |
| ProjectBeViolenceDefense/Assets/Scripts/AssetLoading/Editor/AssetLoaderWindow.cs | ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/AssetLoading/Editor/AssetLoaderWindow.cs |
| ProjectBeViolenceDefense/Assets/Scripts/AssetLoading/Editor/AssetPathsGenerator.cs | ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/AssetLoading/Editor/AssetPathsGenerator.cs |
| ProjectBeViolenceDefense/Assets/Scripts/UI/UIManager.cs | ProjectBeViolenceDefense/Assets/02_Scripts/06_UI/UIManager.cs |
| ProjectBeViolenceDefense/Assets/Scripts/UI/UIPalette.cs | ProjectBeViolenceDefense/Assets/02_Scripts/06_UI/UIPalette.cs |
