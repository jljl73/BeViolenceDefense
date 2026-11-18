var prototype = UnityEngine.Object.FindFirstObjectByType<VoxelDefensePrototype>();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var view = typeof(VoxelDefensePrototype).GetField("view", flags).GetValue(prototype);
var grid = typeof(VoxelDefensePrototype).GetField("grid", flags).GetValue(prototype);
UnityEngine.Transform fixedWall = null;
foreach (UnityEngine.Transform child in prototype.transform) if (child.name.StartsWith("Fixed Wall ")) { fixedWall = child; break; }
if (fixedWall == null) throw new System.Exception("No fixed walls");
var cell = new UnityEngine.Vector2Int(1, 0);
var result = grid.GetType().GetMethod("TryPlace").Invoke(grid, new object[] { cell });
if (result.ToString() != "Placed") throw new System.Exception("Cannot place wall: " + result);
view.GetType().GetMethod("PlaceWall").Invoke(view, new object[] { cell });
var placed = prototype.transform.Find("Wall 1,0");
var fixedMesh = fixedWall.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.name;
var placedMesh = placed.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.name;
var block = new UnityEngine.MaterialPropertyBlock();
fixedWall.GetComponentInChildren<UnityEngine.Renderer>().GetPropertyBlock(block);
var tint = block.GetColor("_BaseColor");
var sceneName = prototype.gameObject.scene.name;
var wood = sceneName == "TestScene";
if (wood ? (fixedWall.Find("Cap") != null || placed.Find("Cap") != null) : (fixedWall.Find("Cap") == null || placed.Find("Cap") == null)) throw new System.Exception("Unexpected wall geometry");
if (wood && (fixedMesh != "WoodBlock" || placedMesh != "WoodBlock" || UnityEngine.Vector4.Distance(tint, (UnityEngine.Color)new UnityEngine.Color32(160,132,112,255)) > 0.01f)) throw new System.Exception("Wood mesh or fixed tint changed");
view.GetType().GetMethod("RemoveWall").Invoke(view, new object[] { cell });
var removed = grid.GetType().GetMethod("TryRemove").Invoke(grid, new object[] { cell });
return new { scene = sceneName, frame = UnityEngine.Time.frameCount, fixedMesh, placedMesh, fixedTint = tint.ToString(), removed, geometry = wood ? "WoodBlockWall" : "Block + Cap", passed = true };


