using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class StagePrefabMigration
{
    const string GameScene="Assets/01_Scenes/02_Gameplay/GameScene.unity";
    const string Folder="Assets/03_Prefabs/05_Stages";
    static readonly string[] Names={"Default","GoldenMaple","Snowfield","Graveyard"};
    static readonly string[] Keys={"default","maple","snow","graveyard"};
    static void Set(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var prefabs=new StageContent[4];
            for(int i=0;i<4;i++)prefabs[i]=Export(i);
            var scene=EditorSceneManager.OpenScene(GameScene);
            var flow=Object.FindFirstObjectByType<VoxelDefensePrototype>();
            var source=new SerializedObject(flow);
            var ui=(UIManager)source.FindProperty("ui").objectReferenceValue;
            foreach(var root in scene.GetRootGameObjects())
                if(root.name=="Main Camera"||root.name=="Directional Light"||root.name=="Global Volume")Object.DestroyImmediate(root);
            var selectionCamera=new GameObject("Stage Selection Camera",typeof(Camera),typeof(AudioListener));
            var camera=selectionCamera.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=UIPalette.Background;camera.cullingMask=0;
            source.Update();source.FindProperty("waitForStageSelection").boolValue=true;Set(source,"boardCamera",null);Set(source,"stage",null);Set(source,"stageAmbience",null);source.ApplyModifiedPropertiesWithoutUndo();
            var popupPrefab=BuildPopup();
            var panel=(GameObject)PrefabUtility.InstantiatePrefab(popupPrefab,ui.PopupLayer);
            var controller=new GameObject("Stage Selection").AddComponent<StageSelectionController>();
            var setup=new SerializedObject(controller);
            var stages=setup.FindProperty("stages");stages.arraySize=4;
            for(int i=0;i<4;i++)stages.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Stage_{Names[i]}.prefab").GetComponent<StageContent>();
            Set(setup,"game",flow);Set(setup,"ui",ui);Set(setup,"popup",panel.GetComponent<StageSelectionPopup>());Set(setup,"selectionCamera",selectionCamera);setup.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("STAGE_MIGRATION_PASS four content prefabs, three explicit default-board assets, one popup prefab and shared GameScene saved");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void RepairReferences()
    {
        var scene=EditorSceneManager.OpenScene(GameScene);
        var controller=Object.FindFirstObjectByType<StageSelectionController>();
        var so=new SerializedObject(controller);var entries=so.FindProperty("stages");
        for(int i=0;i<4;i++)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Stage_{Names[i]}.prefab");
            var content=prefab.GetComponent<StageContent>();
            if(content==null)throw new Exception("Missing stage "+Names[i]);
            entries.GetArrayElementAtIndex(i).objectReferenceValue=content;
        }
        so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("STAGE_REBIND_PASS four persistent prefab references saved");EditorApplication.Exit(0);
    }
    static StageContent Export(int i)
    {
        string scenePath=i==0?GameScene:$"Assets/01_Scenes/03_Test/Stage_{Names[i]}.unity";
        var scene=EditorSceneManager.OpenScene(scenePath);
        var flow=Object.FindFirstObjectByType<VoxelDefensePrototype>();var source=new SerializedObject(flow);
        var root=new GameObject("Stage_"+Names[i]);var content=root.AddComponent<StageContent>();var data=new SerializedObject(content);
        data.FindProperty("displayNameKey").stringValue="stage_select."+Keys[i];
        var stage=(StageData)source.FindProperty("stage").objectReferenceValue;
        if(stage==null)
        {
            stage=ScriptableObject.CreateInstance<StageData>();stage.displayNameKey="stage_select."+Keys[i];
            var grid=new DefenseGrid();
            for(int s=0;s<grid.SpawnCells.Count;s++)stage.spawns.Add(new StageData.Spawn{cell=grid.SpawnCells[s],faction=grid.SpawnFactions[s]});
            foreach(var cell in grid.FixedWalls.OrderBy(c=>c.y).ThenBy(c=>c.x))stage.obstacles.Add(new StageData.Obstacle{cell=cell,size=Vector2Int.one});
            AssetDatabase.CreateAsset(stage,$"Assets/04_Data/04_Stages/Stage_{Names[i]}.asset");
        }
        Set(data,"stage",stage);data.FindProperty("theme").boxedValue=source.FindProperty("boardTheme").boxedValue;
        Set(data,"boardCamera",source.FindProperty("boardCamera").objectReferenceValue);
        Set(data,"ambience",source.FindProperty("stageAmbience").objectReferenceValue);
        Set(data,"sun",RenderSettings.sun);Set(data,"skybox",RenderSettings.skybox);
        data.FindProperty("ambientMode").intValue=(int)RenderSettings.ambientMode;
        data.FindProperty("ambientSky").colorValue=RenderSettings.ambientSkyColor;
        data.FindProperty("ambientEquator").colorValue=RenderSettings.ambientEquatorColor;
        data.FindProperty("ambientGround").colorValue=RenderSettings.ambientGroundColor;
        data.FindProperty("ambientIntensity").floatValue=RenderSettings.ambientIntensity;
        data.FindProperty("reflectionIntensity").floatValue=RenderSettings.reflectionIntensity;
        data.FindProperty("fog").boolValue=RenderSettings.fog;data.FindProperty("fogColor").colorValue=RenderSettings.fogColor;
        data.FindProperty("fogMode").intValue=(int)RenderSettings.fogMode;
        data.FindProperty("fogDensity").floatValue=RenderSettings.fogDensity;data.FindProperty("fogStart").floatValue=RenderSettings.fogStartDistance;data.FindProperty("fogEnd").floatValue=RenderSettings.fogEndDistance;
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach(var go in scene.GetRootGameObjects())
            if(go.name=="Main Camera"||go.name=="Directional Light"||go.name=="Global Volume"||go.name=="Stage Environment")go.transform.SetParent(root.transform,true);
        // Fail rather than silently dropping a scene reference when saving a standalone prefab.
        foreach(var component in root.GetComponentsInChildren<Component>(true))
        {
            if(component==null)throw new Exception("Missing script in "+root.name);
            var serialized=new SerializedObject(component);var prop=serialized.GetIterator();
            while(prop.Next(true))
            {
                if(prop.propertyType!=SerializedPropertyType.ObjectReference||prop.objectReferenceValue==null)continue;
                var target=prop.objectReferenceValue;
                Transform transform=target is Component c?c.transform:target is GameObject g?g.transform:null;
                if(transform!=null&&!EditorUtility.IsPersistent(target)&&!transform.IsChildOf(root.transform)&&transform!=root.transform)
                    throw new Exception($"External scene reference: {component.name}.{prop.propertyPath} -> {target.name}");
            }
        }
        var saved=PrefabUtility.SaveAsPrefabAsset(root,$"{Folder}/Stage_{Names[i]}.prefab");
        Debug.Log($"STAGE_EXPORTED {Names[i]} board={stage.width}x{stage.depth} camera={content.BoardCamera!=null} environmentChildren={root.transform.childCount}");
        return saved.GetComponent<StageContent>();
    }
    static GameObject BuildPopup()
    {
        var root=Rect("Stage Selection Popup",null,new Vector2(980,570));
        var popup=root.gameObject.AddComponent<StageSelectionPopup>();
        var panel=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("71d454b5c2b146bb850d3f0c894ac591"));
        Image(root,UIPalette.Outline,panel);
        var inner=Rect("Panel",root,new Vector2(972,562));Image(inner,UIPalette.Panel,panel);
        var title=Text("Title",root,new Vector2(0,207),new Vector2(860,60),42);title.text="스테이지 선택";
        var subtitle=Text("Subtitle",root,new Vector2(0,147),new Vector2(860,40),23);subtitle.color=UIPalette.TextSecondary;subtitle.text="전장을 선택하세요";
        var buttons=new Button[4];var labels=new TextMeshProUGUI[4];
        for(int i=0;i<4;i++)
        {
            var card=Rect("Stage "+(i+1),root,new Vector2(418,120));card.anchoredPosition=new Vector2(i%2==0?-222:222,i/2==0?37:-111);
            var image=Image(card,UIPalette.ButtonSecondary,panel);
            var button=card.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=UIPalette.OutlineStrong;colors.selectedColor=UIPalette.OutlineFocus;colors.pressedColor=UIPalette.TextSecondary;button.colors=colors;
            // Neutral image tint lets the palette drive actual button colors without multiplying two dark colors.
            image.color=Color.white;colors.normalColor=UIPalette.ButtonSecondary;colors.highlightedColor=UIPalette.PanelBrown;colors.selectedColor=UIPalette.PanelBrown;colors.pressedColor=UIPalette.PanelRaised;button.colors=colors;
            var number=Text("Number",card,new Vector2(-159,0),new Vector2(68,60),30);number.text=(i+1).ToString("00");number.color=UIPalette.OutlineFocus;
            labels[i]=Text("Label",card,new Vector2(37,0),new Vector2(280,66),34);labels[i].text=new[]{"기본 전장","단풍","설원","묘지"}[i];
            buttons[i]=button;
        }
        var setup=new SerializedObject(popup);Set(setup,"title",title);Set(setup,"subtitle",subtitle);
        foreach(string name in new[]{"buttons","labels"})
        {
            var array=setup.FindProperty(name);array.arraySize=4;
            for(int i=0;i<4;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=name=="buttons"?(Object)buttons[i]:labels[i];
        }
        setup.ApplyModifiedPropertiesWithoutUndo();root.gameObject.SetActive(false);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"/StageSelectionPopup.prefab");Object.DestroyImmediate(root.gameObject);return prefab;
    }
    static RectTransform Rect(string name,Transform parent,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;return rect;
    }
    static Image Image(RectTransform rect,Color color,Sprite sprite){var image=rect.gameObject.AddComponent<Image>();image.color=color;image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;return image;}
    static TextMeshProUGUI Text(string name,Transform parent,Vector2 position,Vector2 size,int fontSize)
    {
        var rect=Rect(name,parent,size);rect.anchoredPosition=position;var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Netmarble Medium SDF.asset");text.fontSize=fontSize;text.color=UIPalette.TextPrimary;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;
    }
}
