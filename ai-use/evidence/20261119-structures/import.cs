using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

var results = new List<object>();
foreach (string name in new[] { "CozyWindmillCottage", "ToppledStoneTower" })
{
    string folder = "Assets/05_Art/05_Meshes/Environment/" + name;
    string modelPath = folder + "/" + name + ".fbx";
    var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
    importer.importAnimation = false;
    importer.materialImportMode = ModelImporterMaterialImportMode.None;
    importer.SaveAndReimport();
    var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    material.name = name;
    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_BaseColor.png"));
    material.SetColor("_BaseColor", Color.white);
    material.SetFloat("_Smoothness", 0.15f);
    material.SetFloat("_Metallic", 0f);
    AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
    var root = new GameObject(name + "_2x2");
    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
    model.transform.SetParent(root.transform, false);
    var renderers = model.GetComponentsInChildren<Renderer>();
    foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
    Bounds bounds = renderers[0].bounds;
    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
    var originalSize = bounds.size;
    // Runtime FitObstacle fits by X width: place the wider horizontal axis along X.
    if (bounds.size.z > bounds.size.x) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
    bounds = renderers[0].bounds;
    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
    model.transform.localScale *= 1.92f / bounds.size.x;
    bounds = renderers[0].bounds;
    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
    model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    bounds = renderers[0].bounds;
    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
    var collider = root.AddComponent<BoxCollider>();
    collider.size = new Vector3(1.92f, bounds.size.y, 1.92f);
    collider.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
    string prefabPath = "Assets/03_Prefabs/04_World/" + name + "_2x2.prefab";
    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    results.Add(new { name, prefabPath, originalSize, size = bounds.size, center = bounds.center, bottom = bounds.min.y, colliderSize = collider.size, texture = material.mainTexture.name, shader = material.shader.name });
    UnityEngine.Object.DestroyImmediate(root);
}
AssetDatabase.SaveAssets();
return results;
