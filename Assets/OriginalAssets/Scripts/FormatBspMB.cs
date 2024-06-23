// FormatBspMB.cs
// Handles the Unity-specific / MonoBehaviour elements of bringing BSPs into Unity
// Is not actually a file-format class, like FormatBsp.cs or FormatLmpPalette.cs
// Just an old file-name I don't want to change.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

public class FormatBspMB : MonoBehaviour
{
	// PUBLIC VARIABLES ///////////////////////////////////////////////
	
	public string pathToPak = "";
	public string pathToBsp = "maps/start.bsp"; // Note: only start and episode 1 (e1) maps included!
	public string pathToPalette = "gfx/palette.lmp";

	public Material defaultMaterial;
	public GameObject playerObject;

	public bool debuggingMaterials;
	public bool debuggingLightmaps;
	public bool useLightmapUvsForBase;
	public int debuggingLightmapIndex;
	public bool useLightmapAtlas = true;
	public bool debuggingLightmapAtlas;
	public bool lightmapSmoothing = true;
	public float lightmapGammaAdjustment = 3;
	public float atlasBorderWidth = 2;
	public bool visualized = false;
	public bool saveFiles = false;

	// PRIVATE VARIABLES //////////////////////////////////////////////

	private FormatBsp bsp;
	private FormatLmpPalette palette;

	private string pathToBspLoaded;
	private string bspFileNameNoExtension;

	private bool previousDebuggingMaterials;
	private bool previousDebuggingLightmaps;
	private bool previousUseLightmapUvsForBase;

	// Used to render so many edges per frame	
	int startIndex = 0;

	// Third-party, used for debug rendering
	// LineDrawer lineDrawer;
	System.Random rnd; // random number generator

	// Game Object's renderer
	Renderer goRenderer;

	// Array of Quake indexed colors to Unity brand Color objects
	Color[] QuakeColorsInUnity;
	bool haveGeneratedQuakePalette = false;

	// Array of Unity Materials pointing to QuakeTextures in Unity
	Material[] QuakeMaterialsInUnity;
	bool haveGeneratedQuakeMaterials = false;

	// Array of Quake textures converted into Unity brand textures
	Texture2D[] QuakeTexturesInUnity;
	bool haveGeneratedQuakeTextures = false;

	// Materials to house lightmaps as textures (mostly for debugging)
	Material[] QuakeLightmapMaterialsInUnity;
	bool haveGeneratedQuakeLightmapMaterials = false;

	// Array of Quake lightmap textures converted into Unity brand textures
	Texture2D[] QuakeLightmapTexturesInUnity;
	bool haveGeneratedQuakeLightmapTextures = false;

	// C# string-to-int map example: https://www.dotnetperls.com/map
	Dictionary<string, int> QuakeTextureNameToIndex;

	// Material that's used to render and modified in realtime
	Material bspMaterial;

	// Mesh Renderer which contains references to materials
	// -- unused -- MeshRenderer meshRenderer;

	// Variables used for triangle and UV generation
	Vector3[] unityPoints;
	Vector3[] usedUnityPoints;
	// bool[] unityPointConverted;
	int usedUnityPointsCount;
	Vector2[] unityUVs;
	Vector2[] unityUVs2; // for lightmaps
	bool visualizeFacesIsRunning = false;
	bool visualizeFacesFinished = false;

	// Variables for lightmap generation
	float[][] quakeFacesBoundingBox;
	int quakeFacesBoundingBoxCount;
	Vector2[][] unityUVsForFaces;
	// Disabled due to being unused
	// GameObject tempGO;
	// GameObject tempGO2;
	// PolygonCollider2D polyCollider;
	// PolygonCollider2D polyCollider2;
	// Vector2[] squarePoints;

	// Variables used by lightmap/atlas rendering
	bool GenerateQuakeLightmapTexturesIsRunning = false;
	Material lightmapAtlasMaterial;
	// COULDDO: rename this lightmapAtlasShader variable name, it's confusing
	Shader lightmapAtlasShader;
	Shader lightmapAtlasShaderUnlit;
	Shader lightmapAtlasAsDiffuseShader;
	Shader solidWhiteShader;
	Shader shadedWireframeShader;
	Shader skyboxPunchThroughShader;
	Texture2D lightmapAtlasTexture;
	Texture2D tinyFullbrightLightmapAtlasTexture;
	float atlasUnit = 16f; // max height/width of a lightmap texture / lightmap swatch
	float atlasUnitWithBorder; // should always be atlasUnit+(2*atlasBorderWidth)
	float maxAtlasPos = 2048f; // *4; // 1024*16; // 1024*16; // was 2048
	int atlasPosX = 0;
	int atlasPosY = 0;
	// -- unused -- float lightmapsPerX;
	FormatBsp.dface_t face; 
	Color defaultLightingColor;
	Color fullbrightLightingColor;

	// TODO: more shades of grey for premade lightmap swatches // P4
	int numberOfPreMadeLightmaps = 2; // making solid white and black beforehand

	// Aware of at least one .pak file
	FormatPak pak;

	// Keeps list of all brushes/models that are actually teleporters
	List<int> teleporterBrushIds;

	// Start the Unity GameObject
	void Start()
	{
		// Note: this has little to do with Quake. Put elsewhere?
		// Turn vsync off
		QualitySettings.vSyncCount = 0;

		// Do debug rendering setup
		// lineDrawer = new LineDrawer();
		rnd = new System.Random();

		// Prep the materials/shaders
		// COULDO: put these in their own method, make helper method for less duplicate text
		lightmapAtlasShader = Shader.Find("Legacy Shaders/Lightmapped/Diffuse");
		if (lightmapAtlasShader == null)
		{
			Debug.LogError("Shader.Find(\"Legacy Shaders/Lightmapped/Diffuse\") failed!");
		}
		lightmapAtlasShaderUnlit = Shader.Find("Legacy Shaders/Self-Illumin/Diffuse");
		if (lightmapAtlasShaderUnlit == null)
		{
			Debug.LogError("Shader.Find(\"Legacy Shaders/Self-Illumin/Diffuse\") failed!");
		}
		lightmapAtlasAsDiffuseShader = Shader.Find("Custom/LightmapAsDiffuse");
		if (lightmapAtlasAsDiffuseShader == null)
		{
			Debug.LogError("Shader.Find(\"Custom/LightmapAsDiffuse\") failed!");
		}		
		skyboxPunchThroughShader = Shader.Find("Custom/DepthMask");
		if (skyboxPunchThroughShader == null)
		{
			Debug.LogError("Shader.Find(\"Custom/DepthMask\") failed!");
		}
		solidWhiteShader = Shader.Find("Custom/White");
		if (solidWhiteShader == null)
		{
			Debug.LogError("Shader.Find(\"Custom/White\") failed!");
		}
		shadedWireframeShader = Shader.Find("Custom/WireframeFromDump");
		if (shadedWireframeShader == null)
		{
			Debug.LogError("Shader.Find(\"Custom/WireframeFromDump\") failed!");
		}

		// COULDDO: could put this in its own function
		// To actually include this shader in a build had to mess with project settings AND turn off stripping of shaders
		// More details here: https://answers.unity.com/questions/893182/why-does-my-unity-application-cant-find-the-specul.html
		// COULDDO: could we write code that guarantees certain shaders are in the project?

		// Prep the lightmap atlas border calculations
		atlasUnitWithBorder = atlasUnit + (atlasBorderWidth * 2);
		// TODO: some more frequently used ratios can be prepped here // P4

		// Allocate this atlas texture early so we can link it to materials/shaders
		setDefaultLightingColors();
		if (useLightmapAtlas)
		{
			if (lightmapAtlasTexture == null)
			{
				lightmapAtlasTexture = new Texture2D((int)maxAtlasPos, (int)maxAtlasPos);
				// fill this entire texture with white
				FillWithColorTexture2D(fullbrightLightingColor, (int)maxAtlasPos, (int)maxAtlasPos, lightmapAtlasTexture, 0, 0);
				// Apply to save this change
				lightmapAtlasTexture.Apply();

				// Small texture to use as alternate lightmap when turning "off" lightmap
				int sizeOfTinyLightmap = 8;
				tinyFullbrightLightmapAtlasTexture = new Texture2D(sizeOfTinyLightmap, sizeOfTinyLightmap);
				FillWithColorTexture2D(fullbrightLightingColor, sizeOfTinyLightmap, sizeOfTinyLightmap, tinyFullbrightLightmapAtlasTexture, 0, 0);
			}
		}

		previousDebuggingMaterials = debuggingMaterials;
		previousDebuggingLightmaps = debuggingLightmaps;
		previousUseLightmapUvsForBase = useLightmapUvsForBase;

		// setup all multiples of 16 / sixteeen
		multiplesOfSixteen = new float[100];
		for (int i=0; i < 100; i++)
		{
			multiplesOfSixteen[i] = i * 16f;
		}
		// setup all multiples of 18 / eighteen
		multiplesOfEighteen = new float[100];
		for (int i=0; i < 100; i++)
		{
			multiplesOfEighteen[i] = i * 18f;
		}

		// Starting loading on second frame via FixedUpdate()
	}

	public void SetPak(FormatPak newPak)
	{
		pak = newPak;
	}

	void setDefaultLightingColors()
	{
		// Set two default colors:
		defaultLightingColor = new Color(0, 0, 0, 255); // should be black == new Color(0, 0, 0, 255); // magenta / hot pint == new Color(255, 0, 255, 255);
		fullbrightLightingColor = new Color(255, 255, 255, 255); // should be white == new Color(255, 255, 255, 255); // red == new Color(255, 0, 0, 255); 
	}

	void loadAndGeneratePak()
	{
		// Load .pak palette file
		FormatPak pak = new FormatPak();
		if (pak.Load(pathToPak))
		{
			Debug.Log("Pak loaded.");
			if (pak.Parse())
			{
				Debug.Log("Pak parsed.");
			}
		}
	}

	void loadAndGeneratePalette()
	{
		// Load .lmp palette file
		palette = new FormatLmpPalette();
		palette.SetPak(pak);
		if (palette.Load(pathToPalette))
		{
			// Debug.Log("Palette loaded.");
			if (palette.Parse())
			{
				Debug.Log("Palette parsed.");

				// Testing if the palette loadeded correctly
				GeneratePaletteTexture();
			}
		}
	}

	// Unloads all of the elements used to generate the Unity mesh, but keeps the Unity mesh, textures, lightmap, etc.
	public void UnloadUnusedDependencies()
	{
		// Done with the overall .pak file
		if (pak != null)
		{
			pak.Unload();
			pak = null;
		}
		// Done with the raw BSP data
		if (bsp != null)
		{
			bsp.Unload();
			bsp = null;
		}
		// Done with the palette to generate textures
		if (palette != null)
		{
			palette.Unload();
			palette = null;
		}
		
		// Null out a town of stuff
		goRenderer = null;

		QuakeColorsInUnity = null;
		haveGeneratedQuakePalette = false;

		// Decided to not garbage collect these, because we refer back to them when swapping render-modes
		// QuakeMaterialsInUnity = null;
		// haveGeneratedQuakeMaterials = false;

		QuakeTexturesInUnity = null;
		haveGeneratedQuakeTextures = false;

		QuakeLightmapMaterialsInUnity = null;
		haveGeneratedQuakeLightmapMaterials = false;

		QuakeLightmapTexturesInUnity = null;
		haveGeneratedQuakeLightmapTextures = false;

		QuakeTextureNameToIndex = null;

		bspMaterial = null;

		// -- unused -- meshRenderer = null;

		unityPoints = null;
		usedUnityPoints = null;
		// unityPointConverted = null;
		usedUnityPointsCount = 0;
		unityUVs = null;
		unityUVs2 = null;

		quakeFacesBoundingBox = null;
		quakeFacesBoundingBoxCount = 0;
		unityUVsForFaces = null;
		// Disabled due to being unused
		// tempGO = null;
		// tempGO2 = null;
		// polyCollider = null;
		// polyCollider2 = null;
		// squarePoints = null;

		pak = null;

	}

	void loadAndVisualizeBsp()
	{
		TimerHelper timer = new TimerHelper(this.GetType() + " Running loadAndVisualizeBSP()");

		// Reset materials and textures
		haveGeneratedQuakeMaterials = false;
		haveGeneratedQuakeTextures = false;
		haveGeneratedQuakeLightmapMaterials = false;
		haveGeneratedQuakeLightmapTextures = false;
		QuakeMaterialsInUnity = null;
		QuakeLightmapMaterialsInUnity = null;
		QuakeSkyboxLoaded = false;
		QuakeSkyboxMaterial = null;
		QuakeSkyboxPunchThroughMaterial = null;

		// Load .bsp file
		bsp = new FormatBsp();
		bsp.SetPak(pak);
		if (bsp.Load(pathToBsp))
		{
			pathToBspLoaded = pathToBsp;
			var pathToBspSplit = pathToBspLoaded.Split('/');
			bspFileNameNoExtension = pathToBspSplit[pathToBspSplit.Length-1].Replace(".bsp","");
			// Debug.Log("bspFileNameNoExtension = " + bspFileNameNoExtension);

			if (bsp.Parse())
			{
				// Do something with the loaded, and parsed BSP data...

				// Get all the teleporters in the level
				FindTeleporters();

				// Make the list of materials
				GenerateQuakeMaterials();

				// Testing code-generated textures
				// GenerateTextureTest();

				GenerateQuakeTextures();

				// VisualizeVerts();
				// VisualizeEdges(); // this is the old editor-only wireframe renderer
				VisualizeSky();
				VisualizeFaces();
				PlacePlayer();

				// Do the real lightmap loading and generation
				GenerateQuakeLightmapTextures();
				GenerateQuakeLightmapMaterials();
				if (useLightmapAtlas && debuggingLightmapAtlas)
				{
					SetAllQuakeMaterialsToLightmapAtlas();
				}
				else if (debuggingLightmaps)
				{
					SetAllQuakeMaterialsToLightmap(0);
				}

				visualized = true;
			}
		}

		timer.Stop();
	}

	void VisualizeVerts()
	{
		if (bsp != null &&
			bsp.CheckIfParsed() &&
			bsp.dvertexes != null)
		{
			for (int i = 0; i < bsp.dvertexes.Length; i++)
			{
				if (bsp.dvertexes[i].point != null)
				{
					// Debug.Log(bsp.dvertexes[i].point[0]);
					VisualizeVert(bsp.dvertexes[i].point[0], bsp.dvertexes[i].point[1], bsp.dvertexes[i].point[2]);
				}
			}
		}
	}

	void VisualizeEdges()
	{
		// WARNING: this function relies on the raw BSP data, but that data can be released before it draws everything
		// Also this method of drawing the wireframes only works in the scene view, NOT in the game view
		// See RenderModes.cs for the runtime wireframe rendering
		if (bsp == null ||
			bsp.dedges == null ||
			!bsp.CheckIfParsed()
		)
		{
			return;
		}

		if (startIndex >= bsp.dedges.Length)
		{
			return;
		}

		// Render a random subset of the edges
		// startIndex = 0; // don't reset between frames
		int endIndex;
		int maxEdgesToRender = 1000; // 100 wasn't enough edges to draw them all pre-garbage collection
		int edgesRendered = 0;

		// COULDDO: can remove when optimizing
		int randomEdgeIndex = rnd.Next(0, bsp.dedges.Length - maxEdgesToRender);
		if (randomEdgeIndex > 0)
		{
			// DO NOTHING
		}

		endIndex = startIndex + maxEdgesToRender;

		for (int i = startIndex; i < endIndex; i++)
		{
			if (i >= bsp.dedges.Length)
			{
				// do nothing, we're out of bounds
				break;
			}
			else if (bsp.dedges[i].v != null)
			{
				int tempIndex1 = bsp.dedges[i].v[0];
				int tempIndex2 = bsp.dedges[i].v[1];

				// Debug.Log("Attempting to draw edge # " + i + " between verts # " + tempIndex1 + " and " + tempIndex2);
				if (tempIndex2 >= bsp.dvertexes.Length)
				{
					// COULDDO: investigate and fix this -- it's an out-of-bounds error
					Debug.LogWarning("Trying to render out-of-bounds vertex.");
				}
				else
				{
					var startPoint = bsp.dvertexes[tempIndex1].point;
					var endPoint = bsp.dvertexes[tempIndex2].point;
					VisualizeEdge(startPoint[0], startPoint[1], startPoint[2], endPoint[0], endPoint[1], endPoint[2]);
					edgesRendered++;
				}
			}
		}

		// Debug.Log("VisualizeEdges(): rendered " + edgesRendered + " edges.");

		startIndex = endIndex;
		if (startIndex > bsp.dedges.Length)
		{
			// we think we've rendered them all
		}
	}

	Shader quakeSkyboxShader;
	void VisualizeSky()
	{
		// return; // If we skip this code, the skybox will render as solid black 

		// This enabled code path uses a Unity-style six-sided skybox for now...

		// Tell the material that's used to draw the sky polygons to draw with the skybox shader
		if (QuakeSkyboxMaterial != null)
		{
			return;
		}
		quakeSkyboxShader = Shader.Find("Retro/QuakeSkybox"); // "Skybox/6 Sided");
		if (quakeSkyboxShader == null)
		{
			Debug.LogError("Shader.Find(\"Retro/QuakeSkybox\") failed!");
		}
		QuakeSkyboxMaterial = new Material(quakeSkyboxShader);
		RenderSettings.skybox = QuakeSkyboxMaterial;

		return;

		/************

		// THE BELOW CODE PATH CREATES A SINGLE QUAD (TWO TRIS) IN THE SKY TO BE THE SKY
		// An interesting-enough technique that I'm leaving this code here, commented-out

		// Just Prep and Setup /////////////////////////////

		// Game object to hold the sky
		GameObject skyMeshGameObject = new GameObject();
		skyMeshGameObject.name = "quakeSkyMesh";
		skyMeshGameObject.transform.SetParent(gameObject.transform);

		// Make sure we have a meshFilter
		MeshFilter meshFilter = skyMeshGameObject.GetComponent<MeshFilter>();
		if (meshFilter == null)
		{
			meshFilter = skyMeshGameObject.AddComponent<MeshFilter>();
			GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			meshFilter.mesh = go.GetComponent<MeshFilter>().mesh;
			meshFilter.gameObject.AddComponent<MeshRenderer>();
			Destroy(go);
		}

		// Make sure we have an empty mesh to drop triangles into
		Mesh mesh = meshFilter.sharedMesh;
		if (mesh == null)
		{
			meshFilter.mesh = new Mesh();
			mesh = meshFilter.sharedMesh;
		}
		mesh.Clear();

		// Get verts ready ///////////////////////////////////

		Vector3[] skyVerts = new Vector3[4];
		Vector2[] skyUVs   = new Vector2[skyVerts.Length];
		int[]     skyTris;

		int unityWorldMax = 1000;
		int unitySkyHeight = 100;

		// Set the verts ///////////////////////////////////

		skyVerts[0] = new Vector3(-unityWorldMax, unitySkyHeight, -unityWorldMax);
		skyVerts[1] = new Vector3(-unityWorldMax, unitySkyHeight,  unityWorldMax);
		skyVerts[2] = new Vector3( unityWorldMax, unitySkyHeight,  unityWorldMax);
		skyVerts[3] = new Vector3( unityWorldMax, unitySkyHeight, -unityWorldMax);

		// Set the trianges -- just a list of verts, 3 per tri, in order ///////////////////////////////////

		skyTris = new int[] { 3,2,0,2,1,0 };

		// Set the UVs
		skyUVs[0] = new Vector2(0,0);
		skyUVs[1] = new Vector2(0,1);
		skyUVs[2] = new Vector2(1,1);
		skyUVs[3] = new Vector2(1,0);

		// Glue it all together ///////////////////////////////////

		// Assign mesh to use smaller list of verts
		mesh.vertices = skyVerts;

		// Add the triangles to a submesh. Each submesh has its own corresponding materials
		mesh.subMeshCount = 1;
		mesh.SetTriangles(skyTris, 0); // second parameter defines which submesh to attach these triangles to
		mesh.uv = skyUVs;

		Renderer goRenderer = skyMeshGameObject.GetComponent<Renderer>();
		goRenderer.materials = new Material[] { QuakeSkyMaterial };

		************/
	}

	bool[] processedLightmapUVs;
	// Accepts Quake coordinates, shows them in Unity coordinates
	// inspired by this boy: https://blog.nobel-joergensen.com/2010/12/25/procedural-generated-mesh-in-unity/
	void VisualizeFaces()
	{
		if (visualizeFacesIsRunning)
		{
			return;
		}
		visualizeFacesIsRunning = true;
		visualizeFacesFinished = false;

		// Reusable variables
		int i, j, k, l;

		int quakeModelIndex = 0;
		bool[] usedQuakeMaterials;
		int usedQuakeMaterialCount;

		// Face bounding box tracking
		float uMin, uMax, vMin, vMax;
		float xMin, xMax, yMin, yMax;

		// Lots of variables we'll use later
		FormatBsp.dface_t[] faces;
		int[] surfaceEdges;
		FormatBsp.dedge_t[] edges;
		int currentMaterialIndex = 0;
		int[] unityTriangles;
		int unityTrianglesCount;
		int unityTriangleIndex;
		int quakeFaces;
		int quakeFacesValid;
		bool quakeFaceIsValid;
		int firstEdgeIndex;
		GameObject meshGameObject;
		int unityTrianglesInModelCount;
		bool allMaterialsAreTrigger;

		// Debug.Log("bsp.num_models: " + bsp.num_models);
		// Debug.Log("Quake sub-model " + quakeModelIndex + " has " + bsp.dmodels[quakeModelIndex].numfaces + " faces.");

		// How large is the texture for the current material we care about
		float textureWidth = (float)bsp.miptextures[currentMaterialIndex].width;
		float textureHeight = (float)bsp.miptextures[currentMaterialIndex].height;
		String textureName;
		bool textureIsFullbright = false;
		// Do this calculation once early in the process
		// -- unused -- lightmapsPerX = RoundDown(maxAtlasPos / atlasUnitWithBorder);

		int firstface = bsp.dmodels[quakeModelIndex].firstface;
		int numfaces = bsp.dmodels[quakeModelIndex].numfaces;

		int trianglesPerFace = 0;
		int edgeIndex = -1;

		if (!bsp.CheckIfParsed())
		{
			Debug.LogWarning("BSP not parsed.");
			return;
		}
		if (bsp == null)
		{
			// We don't have a valid BSP object to play with, return out early
			Debug.LogWarning("BSP object is null.");
			return;
		}

		// Since faces are one array, init this once before diving into per-face code
		quakeFacesBoundingBox = new float[FormatBsp.MAX_MAP_FACES][];
		quakeFacesBoundingBoxCount = 0;
		unityUVsForFaces = new Vector2[FormatBsp.MAX_MAP_FACES][];

		// Setup lightmap atlas
		float faceAtlasPosX = 0;
		float faceAtlasPosY = 0;

		for (quakeModelIndex = 0; quakeModelIndex < bsp.num_models; quakeModelIndex++)
		{
			// Do some Unity setup before diving into Quake data

			// Initialize global list of points
			unityPoints = new Vector3[bsp.dvertexes.Length]; // was [65000] which is Unity's limit
			usedUnityPoints = new Vector3[bsp.dvertexes.Length]; // was [65000] which is Unity's limit
			processedLightmapUVs = new bool[bsp.dvertexes.Length];
			usedUnityPointsCount = 0;
			// Vector3 root = new Vector3(0, 0, 0);
			unityUVs = new Vector2[unityPoints.Length];
			unityUVs2 = new Vector2[unityPoints.Length];
			unityTrianglesInModelCount = 0;

			// Make a new sub-game-object for each Quake model contained in the BSP. 0 = base level, 1+ = triggers and movers
			meshGameObject = new GameObject();
			meshGameObject.name = "quakeMesh" + quakeModelIndex;
			meshGameObject.transform.SetParent(gameObject.transform);

			// Hide brushes that are teleporters
			if (teleporterBrushIds.Contains(quakeModelIndex))
			{
				meshGameObject.SetActive(false);
			}

			// Make sure we have a meshFilter
			// from https://stackoverflow.com/questions/32989808/adding-a-meshfilter-mesh-via-script
			MeshFilter meshFilter = meshGameObject.GetComponent<MeshFilter>();
			if (meshFilter == null)
			{
				// Debug.Log("MeshFilter not found, creating one.");
				meshFilter = meshGameObject.AddComponent<MeshFilter>();
				GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
				meshFilter.mesh = go.GetComponent<MeshFilter>().mesh;
				meshFilter.gameObject.AddComponent<MeshRenderer>();
				Destroy(go);
			}

			// Make sure we have an empty mesh to drop triangles into
			Mesh mesh = meshFilter.sharedMesh;
			if (mesh == null)
			{
				meshFilter.mesh = new Mesh();
				mesh = meshFilter.sharedMesh;
			}
			mesh.Clear();

			// Track which materials this particular Quake model uses or not
			usedQuakeMaterials = new bool[QuakeMaterialsInUnity.Length];
			usedQuakeMaterialCount = 0;

			// Setup pointers to the Quake data

			// Array of faces
			faces = bsp.dfaces;
			// Maps surfaces to edges
			surfaceEdges = bsp.surfedges;
			// Array of edges
			edges = bsp.dedges;

			// Debug.Log("bsp.num_models: " + bsp.num_models);
			// Debug.Log("Quake sub-model " + quakeModelIndex + " has " + bsp.dmodels[quakeModelIndex].numfaces + " faces.");

			// tracks if this mesh has more than just trigger texture on them
			allMaterialsAreTrigger = true;

			// Iterate across list of materials, make a sub-mesh for each unique materials
			for (currentMaterialIndex = 0; currentMaterialIndex < QuakeMaterialsInUnity.Length; currentMaterialIndex++)
			{
				// Some initial Unity data structures
				unityTriangles = new int[100000]; // COULDDO: this number is too big
				unityTrianglesCount = 0;
				unityTriangleIndex = 0; // used for counting by 3
				quakeFaces = 0;
				quakeFacesValid = 0;
				quakeFaceIsValid = false;

				// How large is the texture for the current material we care about
				textureWidth = (float)bsp.miptextures[currentMaterialIndex].width;
				textureHeight = (float)bsp.miptextures[currentMaterialIndex].height;
				textureName = bsp.miptextures[currentMaterialIndex].name;

				// make some lighting decisions very early
				// COULDDO -- I feel like these texture names could have dangling whitespace at the end of them
				textureIsFullbright = false;
				// This line of code was crashing because the texture name was less than the substring length I was searching for (crashing when loading e1m2.bsp in particular)
				if (textureName != null && !textureName.Equals("") && (textureName.Substring(0,1) == "*" || (textureName.Length >= 3 && textureName.Substring(0,3) == "sky") || textureName.StartsWith("trigger")))
				{
					textureIsFullbright = true;
				}

				firstface = bsp.dmodels[quakeModelIndex].firstface;
				numfaces = bsp.dmodels[quakeModelIndex].numfaces;

				trianglesPerFace = 0;
				edgeIndex = -1;

				if (faces == null)
				{
					Debug.LogWarning("faces is null.");
					return;
				}

				FormatBsp.dface_t currentFace;

				// Go through all of the faces in the BSP, and convert them into Unity formats
				for (i = firstface; i < firstface + numfaces; i++)
				{
					currentFace = faces[i];
					quakeFaces++;
					quakeFaceIsValid = false;

					// Tracking size/dimensions of each face
					uMin = 1000000;
					uMax = -1000000;
					vMin = 1000000;
					vMax = -1000000;
					// These numbers MAY need to be larger
					xMin = 1000000;
					xMax = -1000000;
					yMin = 1000000;
					yMax = -1000000;

					// Only add the triangle data if we're looking at the right material
					if (bsp.texinfos[currentFace.texinfo].miptex != currentMaterialIndex)
					{
						continue;
					}

					// Tracks if a mesh is only using trigger materials/textures, such that we can hide this mesh/gameobject later
					if (!textureName.Contains("trigger"))
					{
						allMaterialsAreTrigger = false;
					}


					// Look up the first edge
					firstEdgeIndex = currentFace.firstedge;

					trianglesPerFace = 0;

					// Debug.Log("Attempting to triangulate Quake face...");
					// Debug.Log(" - with " + currentFace.numedges + " edges...");

					int[] localVerts = new int[3];

					// Find the first point, which we'll use for all triangles
					edgeIndex = surfaceEdges[firstEdgeIndex];
					if (edgeIndex > 0)
					{
						localVerts[0] = edges[edgeIndex].v[0];
					}
					else
					{
						localVerts[0] = edges[-edgeIndex].v[1];
					}

					// Remember a 2D UV only shape of this surface, which we'll use to render the lightmap "into"
					unityUVsForFaces[i] = new Vector2[currentFace.numedges];

					// Fan out from the first vert localVerts[0] to make triangles of the rest of the face
					// Turn all the edge data into an array of verts/points
					for (j = 1; j < currentFace.numedges - 1; j++)
					{
						if (currentFace.numedges < 3)
						{
							break;
						}

						edgeIndex = surfaceEdges[firstEdgeIndex + j];

						// Handle case where edgeIndex is inverted, and thus we're walking the edge "backwards"
						if (edgeIndex > 0)
						{
							localVerts[1] = edges[edgeIndex].v[0];
							localVerts[2] = edges[edgeIndex].v[1];
						}
						else
						{
							localVerts[1] = edges[-edgeIndex].v[1];
							localVerts[2] = edges[-edgeIndex].v[0];
						}

						// Track all of the points we discover
						// COULDDO: can cache this data for optimizations
						// Can point to the same Vector3 multiple times, cache the conversions, etc.
						// Also goes for UV data, which is generated below
						usedUnityPoints[usedUnityPointsCount + 0] = QuakeToUnityVector3(bsp.dvertexes[localVerts[0]]);
						usedUnityPoints[usedUnityPointsCount + 1] = QuakeToUnityVector3(bsp.dvertexes[localVerts[1]]);
						usedUnityPoints[usedUnityPointsCount + 2] = QuakeToUnityVector3(bsp.dvertexes[localVerts[2]]);

						// Point our triangles array to those 
						unityTriangles[unityTriangleIndex + 0] = usedUnityPointsCount + 0; // unityTriangleIndex + 0; // localVerts[0];
						unityTriangles[unityTriangleIndex + 1] = usedUnityPointsCount + 1; // unityTriangleIndex + 1; // localVerts[1];
						unityTriangles[unityTriangleIndex + 2] = usedUnityPointsCount + 2; // unityTriangleIndex + 2; // localVerts[2];

						// Do our UV math in Quake worldspace
						// These variables are NOT converted to Unity world-space

						var tempVertQ0 = new Vector3(bsp.dvertexes[localVerts[0]].point[0], bsp.dvertexes[localVerts[0]].point[1], bsp.dvertexes[localVerts[0]].point[2]);
						var tempVertQ1 = new Vector3(bsp.dvertexes[localVerts[1]].point[0], bsp.dvertexes[localVerts[1]].point[1], bsp.dvertexes[localVerts[1]].point[2]);
						var tempVertQ2 = new Vector3(bsp.dvertexes[localVerts[2]].point[0], bsp.dvertexes[localVerts[2]].point[1], bsp.dvertexes[localVerts[2]].point[2]);

						// Can move this block out of the for loop
						var sVector = bsp.texinfos[currentFace.texinfo].vecs[0];
						var tVector = bsp.texinfos[currentFace.texinfo].vecs[1];
						var sVectorQ = new Vector3(sVector[0], sVector[1], sVector[2]);
						var tVectorQ = new Vector3(tVector[0], tVector[1], tVector[2]);

						// For world-space sizes
						var xyArray = new float[6];
						xyArray[0] = (Vector3.Dot(tempVertQ0, sVectorQ) + sVector[3]);
						xyArray[1] = (Vector3.Dot(tempVertQ1, sVectorQ) + sVector[3]);
						xyArray[2] = (Vector3.Dot(tempVertQ2, sVectorQ) + sVector[3]);
						xyArray[3] = -(Vector3.Dot(tempVertQ0, tVectorQ) + tVector[3]);
						xyArray[4] = -(Vector3.Dot(tempVertQ1, tVectorQ) + tVector[3]);
						xyArray[5] = -(Vector3.Dot(tempVertQ2, tVectorQ) + tVector[3]);

						// Quake format vectors -- for UVs
						// Note: rounding these made no different to lightmap right-sizing
						var stArray = new float[6];
						stArray[0] = xyArray[0] / textureWidth;
						stArray[1] = xyArray[1] / textureWidth;
						stArray[2] = xyArray[2] / textureWidth;
						stArray[3] = xyArray[3] / textureHeight;
						stArray[4] = xyArray[4] / textureHeight;
						stArray[5] = xyArray[5]	/ textureHeight;

						// Calculate / update bounding box of the entire face
						for (k = 0; k < 6; k++)
						{
							if (k < 3)
							{
								if (stArray[k] < uMin)
								{
									uMin = stArray[k];
									xMin = xyArray[k];
								}
								if (stArray[k] > uMax)
								{
									uMax = stArray[k];
									xMax = xyArray[k];
								}
							}
							else
							{
								if (stArray[k] < vMin)
								{
									vMin = stArray[k];
									yMin = xyArray[k];
								}
								if (stArray[k] > vMax)
								{
									vMax = stArray[k];
									yMax = xyArray[k];
								}
							}
						}

						/*** Just debugging ***
						if (sVector[3] != 0 || tVector[3] != 0)
						{
							Debug.Log("textureWidth = " + textureWidth + "; s0 = " + stArray[0] + "; t0 = " + stArray[3]);
						}
						*** end debugging ***/

						// Store the UVs in matching order to the verts!
						unityUVs[usedUnityPointsCount + 0] = new Vector2(stArray[0], stArray[3]);
						unityUVs[usedUnityPointsCount + 1] = new Vector2(stArray[1], stArray[4]);
						unityUVs[usedUnityPointsCount + 2] = new Vector2(stArray[2], stArray[5]);
						// End UV work

						// For lightmap loading and pre-rendering into atlas...
						// Store the three point's UVs in this per-face array too
						unityUVsForFaces[i][0]   = unityUVs[usedUnityPointsCount + 0];
						unityUVsForFaces[i][j]   = unityUVs[usedUnityPointsCount + 1];
						unityUVsForFaces[i][j+1] = unityUVs[usedUnityPointsCount + 2];
						
						// Ready for the next set of triangles etc
						usedUnityPointsCount += 3;
						unityTriangleIndex += 3;
						unityTrianglesCount++;
						trianglesPerFace++;
						quakeFaceIsValid = true;
					}
					// end of every edge in the face

					// if we think we made at least one triangle, say so!
					if (quakeFaceIsValid)
					{
						quakeFacesValid++;
						// Debug.Log("  -  into " + trianglesPerFace + " triangles");
					}

					// Calculate the bounding box width and height for this particular face
					float bbWidth, bbHeight, bbwWidth, bbwHeight;
					bbWidth   = Math.Abs(uMax - uMin);
					bbHeight  = Math.Abs(vMax - vMin);

					bbwWidth  = Math.Abs(xMax - xMin);
					bbwHeight = Math.Abs(yMax - yMin);
					// COULDDO: quakeFacesBoundingBox really should be its own struct
					// Debug.Log("Bounding Box for Face [" + i + "]: " + uMin + "," + vMin + " ; " + uMax + "," + vMax + " ; Width: " + bbWidth + ", Height: " + bbHeight);
					quakeFacesBoundingBox[i] = new float[] { uMin, vMin, uMax, vMax, bbWidth, bbHeight, bbwWidth, bbwHeight , xMin, yMin, xMax, yMax};
					quakeFacesBoundingBoxCount++;
					// end of code for a specific material

					// NOTE: out
					// top-left-u  = 0f
					// top-left-v  = 0f
					// low-right-u = 1f
					// low-right-v = 1f 

					// FIGURE OUT ATLAS COORDINATES BY LOOKING INTO THE FUTURE

					// Harmless to have outside of check
					faceAtlasPosX = -1;
					faceAtlasPosY = -1;

					// Decide which lightmap to use for this face
					float thisFaceLightmapIndex;
					if (textureIsFullbright)
					{
						// completely fullbright
						thisFaceLightmapIndex = 1; // white // COULDDO: make these enums instead of magic numbers
					}
					else if (currentFace.styles[0] == 0xFF)
					{
						// the face doesn't have a lightmap, set a fallback base color
						if (currentFace.styles[1] == 0)
						{
							// this seems backwards, but for whatever reason, in Quake, the lightmap baselight value of 0 = white, and 0xFF = black
							thisFaceLightmapIndex = 1; // white // COULDDO: make these enums instead of magic numbers
						}
						else if (currentFace.styles[1] == 0xFF)
						{
							thisFaceLightmapIndex = 0; // black // COULDDO: make these enums instead of magic numbers
						}
						else
						{
							// TODO: why are some sections of the Start.bsp level so full-brighty? // P1
							Debug.LogWarning("Unhandled lightmap base color");
							thisFaceLightmapIndex = 0; // this is clearly temp
						}
					}
					else
					{
						// the face has a lightmap
						thisFaceLightmapIndex = bsp.lightmapOffsetToIndexLookup[currentFace.lightofs] + numberOfPreMadeLightmaps;
					}

					if (useLightmapAtlas)
					{
						// Get the atlas coordinates for this swatch in a consistent manner
						Vector2 swatchPos = GetLightmapSwatchPositioninAtlas((int)thisFaceLightmapIndex);
						faceAtlasPosX = swatchPos.x; 
						faceAtlasPosY = swatchPos.y; 
						
						// Debugging
						if (faceAtlasPosX % 1 > 0 || faceAtlasPosY % 1 > 0)
						{
							Debug.LogWarning("Non-integer atlas positions: " + faceAtlasPosX + ":" + faceAtlasPosY );
						}
					}

					// COULDDO: (3 * trianglesPerFace) should be replaced once we're truly reusing verts between triangles of the same face!
					int vertIndex = usedUnityPointsCount - (3 * trianglesPerFace);
					for (; vertIndex < usedUnityPointsCount; vertIndex++)
					{
						unityUVs2[vertIndex] = new Vector2();

						// Each vert in this face...
						// Debug.Log("vertIndeX: " + vertIndex + "; unityTrianglesCount: " + unityTrianglesCount);
						if (!processedLightmapUVs[vertIndex])
						{
							if (bbWidth == 0 || bbHeight == 0)
							{
								unityUVs2[vertIndex].x = 0;
								unityUVs2[vertIndex].y = 0;
							}

							// Make the UVs 0-to-1 (doesn't take into account whitepace)
							unityUVs2[vertIndex].x = (unityUVs[vertIndex].x - uMin) / bbWidth;
							unityUVs2[vertIndex].y = (unityUVs[vertIndex].y - vMin) / bbHeight;

							if (useLightmapAtlas)
							{
								// Take white space into account
								unityUVs2[vertIndex].x = unityUVs2[vertIndex].x * (float)RoundUpToNearest(bbwWidth,  multiplesOfSixteen) / 16f;
								unityUVs2[vertIndex].y = unityUVs2[vertIndex].y * (float)RoundUpToNearest(bbwHeight, multiplesOfSixteen) / 16f;

								// Shrink into atlas size
								unityUVs2[vertIndex].x = unityUVs2[vertIndex].x / maxAtlasPos;
								unityUVs2[vertIndex].y = unityUVs2[vertIndex].y / maxAtlasPos;

								// Do translation into lightmap atlas space...
								unityUVs2[vertIndex].x = (unityUVs2[vertIndex].x) + (faceAtlasPosX / maxAtlasPos);
								unityUVs2[vertIndex].y = (unityUVs2[vertIndex].y) + (faceAtlasPosY / maxAtlasPos);
								// Debug.Log("unityUVs2[" + vertIndex + "].x = " + unityUVs2[vertIndex].x + " = (" + unityUVs2[vertIndex].x + " * " + atlasUnit + " / " + maxAtlasPos + ") + (" + faceAtlasPosX + " / " + maxAtlasPos + ")");
							}

							// DEBUGING
							if (unityUVs2[vertIndex].x < -1f || unityUVs2[vertIndex].x > 1f || unityUVs2[vertIndex].y < -1f || unityUVs2[vertIndex].y > 1f)
							{
								Debug.LogWarning("Lightmap UVs for vertIndex " + vertIndex + " is out of bounds.");
							}
							// END DEBUGGING

							processedLightmapUVs[vertIndex] = true;
						}
					}
				}
				// end of every face in BSP

				// Trim down the unityTriangles array to just our list
				if (usedUnityPointsCount != unityTrianglesCount * 3)
				{
					// COULDDO: is this check valid anymore?
					// Debug.LogWarning("Point count and triangle count don't match.");
				}
				Array.Resize(ref unityTriangles, unityTrianglesCount * 3);

				// If there are any triangles in this mesh's sub-mesh for this material
				if (unityTrianglesCount > 0)
				{
					// Debug.Log("Found " + quakeFaces + " Quake faces!");
					// Debug.Log("Found " + quakeFacesValid + " valid Quake faces!");
					// Debug.Log("Found " + unityTrianglesCount + " triangles!");

					// Track this number per model
					unityTrianglesInModelCount += unityTrianglesCount;

					// Make mesh not have 50k verts -- copy vectors into new smaller ones of just what's been used
					Vector3[] theseUnityPoints = new Vector3[usedUnityPointsCount];
					Array.Copy(usedUnityPoints, theseUnityPoints, usedUnityPointsCount); // COULDDO: trim out earlier unused points?
					Vector2[] theseUnityUVs  = new Vector2[usedUnityPointsCount];
					Vector2[] theseUnityUVs2 = new Vector2[usedUnityPointsCount];
					Array.Copy(unityUVs,  theseUnityUVs,  usedUnityPointsCount); // COULDDO: trim out earlier unused points?
					Array.Copy(unityUVs2, theseUnityUVs2, usedUnityPointsCount); // COULDDO: trim out earlier unused points?

					// Assign mesh to use smaller list of verts
					mesh.vertices = theseUnityPoints;

					// Add the triangles to a submesh. Each submesh has its own corresponding materials
					mesh.subMeshCount = QuakeMaterialsInUnity.Length;
					mesh.SetTriangles(unityTriangles, usedQuakeMaterialCount); // second parameter defines which submesh to attach these triangles to

					// Keep track of which and how many materials we've used
					usedQuakeMaterials[currentMaterialIndex] = true;
					usedQuakeMaterialCount++;

					// Actually assign the UVs
					if (!useLightmapUvsForBase)
					{
						mesh.uv = theseUnityUVs;
					}
					else
					{
						mesh.uv = theseUnityUVs2; // set the lightmap UVs to the diffuse for testing purposes
					}
					mesh.uv2 = theseUnityUVs2; // set the lightmap UVs?

				}
			} // End of For Loop across materials

			// If this mesh only has "trigger" texture on all faces, let's not draw it
			if (allMaterialsAreTrigger)
			{
				meshGameObject.SetActive(false);
			}

			// If this particular game object has no triangles, like if we've skipped it because it contains the trigger texture, then remove the game object
			if (unityTrianglesInModelCount == 0)
			{
				// COULDDO: if we want more robust trigger support, this would be a spot to work on it
				// Debug.LogWarning("Mesh with no triangles created. Perhaps a hidden trigger?");
				Destroy(mesh);
				Destroy(meshGameObject);
				continue;
			}

			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			// Make an array of the materials we actually used
			Material[] actuallyUsedQuakeMaterials = new Material[usedQuakeMaterialCount];
			int tempCounter = 0;
			for (l = 0; l < usedQuakeMaterials.Length; l++)
			{
				if (usedQuakeMaterials[l])
				{
					actuallyUsedQuakeMaterials[tempCounter] = QuakeMaterialsInUnity[l];
					tempCounter++;
				}
			}

			goRenderer = meshGameObject.GetComponent<Renderer>();
			goRenderer.materials = actuallyUsedQuakeMaterials;

		} // END of for each Quake model
		visualizeFacesFinished = true;

		// Save the whole map as an OBJ file
		if (saveFiles)
		{
			MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
			Directory.CreateDirectory(Application.persistentDataPath + "/ConvertedData");
			ObjExporter.MeshesToFile(meshFilters, Application.persistentDataPath + "/ConvertedData/", bspFileNameNoExtension);
		}

		visualizeFacesIsRunning = false;
	}
	// End VisualizeFaces()

	// Accepts Quake coordinates, shows them in Unity coordinates
	void VisualizeVert(float x, float y, float z)
	{
		GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		cube.transform.position = QuakeToUnityVector3(x, y, z);
		cube.transform.localScale *= 0.5f;
	}

	// Accepts Quake coordinates, shows them in Unity coordinates
	void VisualizeEdge(float x1, float y1, float z1, float x2, float y2, float z2)
	{
		Vector3 startPoint = QuakeToUnityVector3(x1, y1, z1);
		Vector3 endPoint = QuakeToUnityVector3(x2, y2, z2);
		// Ref: DrawLine(Vector3 start, Vector3 end, Color color = Color.white, float duration = 0.0f, bool depthTest = true); 
		Debug.DrawLine(startPoint, endPoint, Color.white, 1000, false);
		// lineDrawer.DrawLineInGameView(startPoint, endPoint, Color.blue);
	}

	// Find the teleporter entities
	void FindTeleporters()
	{
		// Allocate the list of all the teleporter brushes
		teleporterBrushIds = new List<int>();

		// Get the list of all entities in the BSP that represent teleporter data
		FormatBsp.entity_t[] teleporters = bsp.GetEntitiesWithKeyValue("classname", "trigger_teleport");

		// For every teleporter entity...
		foreach(FormatBsp.entity_t teleporter in teleporters)
		{
			// Debug.LogWarning("New Entity!");
			// Get all of the keys and values from the BSP...
			List<KeyValuePair<string, string>> kvps = bsp.GetKeyValuePairsFromEntity(teleporter);
			foreach(KeyValuePair<string, string> kvp in kvps)
			{
				// Debug.Log(kvp.Key + " = " + kvp.Value);
				// Save the list of teleporter brushes by number / ID so we don't draw them later
				if (kvp.Key == "model")
				{
					teleporterBrushIds.Add(Int32.Parse( kvp.Value.Replace("*", "")) );
				}
			}
		}
	}

	// Place the player/camera at a spawn point
	void PlacePlayer()
	{
		Debug.Log("PlacePlayer()");

		// COULDDO: can likely make this a generic process where all entities with a type of name are turned into Unity transforms (position + rotation)
		FormatBsp.entity_t[] dmSpawnPoints = bsp.GetEntitiesWithKeyValue("classname", "info_player_start"); // bsp.GetEntitiesWithKeyValue("classname", "info_player_deathmatch");
		if (dmSpawnPoints == null || dmSpawnPoints.Length == 0)
		{
			Debug.LogWarning("No spawn points found.");
			return;
		}

		// Pick a random spawn point
		int dmSpawnPointIndex = rnd.Next(0, dmSpawnPoints.Length - 1);
		if (dmSpawnPoints[dmSpawnPointIndex] != null)
		{
			var dmSpawnPointEntity = dmSpawnPoints[dmSpawnPointIndex];
			var angle = bsp.GetEntityValueFromKey(dmSpawnPoints[dmSpawnPointIndex], "angle");

			// Catch if a player object isn't explicitly set
			if (playerObject == null)
			{
				playerObject = GameObject.Find("CameraContainer");
			}

			if (playerObject != null)
			{
				playerObject.transform.position = QuakeToUnityVector3(dmSpawnPointEntity.origin.x, dmSpawnPointEntity.origin.y, dmSpawnPointEntity.origin.z);
				if (angle != null)
				{
					playerObject.transform.localEulerAngles = QuakeToUnityRotation(float.Parse(angle));
				}
				else
				{
					// No angle set in BSP so defaulting this to zero value
					playerObject.transform.localEulerAngles = QuakeToUnityRotation(0.0f);
				}

				// Reset the camera sub-object to this location too...
				GameObject mainCamera = GameObject.Find("Main Camera");
				if (mainCamera != null)
				{
					mainCamera.transform.localPosition = Vector3.zero;
					mainCamera.transform.localEulerAngles = Vector3.zero;
					SimpleSmoothMouseLook.needToResetMouseRotation = true;
				}

				GameObject placeholderPlayerObject = GameObject.Find("PlaceholderPlayer");
				if (placeholderPlayerObject != null)
				{
					placeholderPlayerObject.transform.position = playerObject.transform.position;
					placeholderPlayerObject.transform.localEulerAngles = playerObject.transform.localEulerAngles;
				}

			}
			return;
		}

		Debug.LogWarning("No spawn points found.");
	}

	// Update is called once per frame
	private float lastTempUVScale2;
	private RenderMode lastKnownRenderMode = RenderMode.Default;
	int previousDebuggingLightmapIndex;
	void Update()
	{
		// KEY INPUT
		// Debugging lightmaps
		if (Input.GetKeyDown(KeyCode.LeftBracket)) // '['
		{
			debuggingLightmapIndex--;
		}
		if (Input.GetKeyDown(KeyCode.RightBracket)) // ']'
		{
			debuggingLightmapIndex++;
		}
		if (Input.GetKeyDown(KeyCode.L)) // lightmap
		{
			ReGenerateQuakeLightmapTextures();
		}

		// If a new render mode is being requested, swap out some shaders
		if (lastKnownRenderMode != RenderModes.currentRenderMode)
		{
			// Change some stuff based on RenderMode
			if (RenderModes.currentRenderMode == RenderMode.Wireframe)
			{
				setMaterialsToWireframe();
			}
			else if (RenderModes.currentRenderMode == RenderMode.Unlit)
			{
				setLightmapAtlasToUnlit();
			}
			else if (RenderModes.currentRenderMode == RenderMode.LightingOnly)
			{
				setLightmapAtlasAsDiffuse();
			}
			else
			{
				setLightmapAtlasToLit();
			}
			lastKnownRenderMode = RenderModes.currentRenderMode;
		}

		// DO SOME UPDATES

		if (bsp != null)
		{
			// COULDDO: if we want to be able to tweak these things "realtime" this needs to be rethought
			// Debugging lightmaps -- use '[' and ']' to step through them
			if (haveGeneratedQuakeLightmapMaterials && haveGeneratedQuakeLightmapTextures)
			{
				if (debuggingLightmapIndex < 0)
				{
					debuggingLightmapIndex = bsp.num_lightmaps - 1;
				}
				else if (debuggingLightmapIndex >= bsp.num_lightmaps)
				{
					debuggingLightmapIndex = 0;
				}

				// This is in Update for max speed
				if (debuggingLightmaps && (
						previousDebuggingLightmapIndex != debuggingLightmapIndex
					))
				{
					SetAllQuakeMaterialsToLightmap(debuggingLightmapIndex);
					previousDebuggingLightmapIndex = debuggingLightmapIndex;
				}
			}
		}
	}

	// Do the debug rendering
	int fixedUpdateCount = 0;
	void FixedUpdate()
	{
		fixedUpdateCount++;

		// Do the loading on the second frame...
		if (fixedUpdateCount == 2)
		{
			loadAndGeneratePalette();
			loadAndVisualizeBsp();
			// loadAndGeneratePak();
			// Debug.LogWarning("Max lightmap brightness = " + maxLightmapBrightness);
		}
		else if (fixedUpdateCount > 2)
		{
			// This is the old editor-only wireframe rendering
			// VisualizeEdges();
		}

		// TODO: this section could be more active than intended // P4
		if (fixedUpdateCount % 120 == 0)
		{
			// Reload and re-visualize if needed
			if (pathToBspLoaded != pathToBsp || 
				previousDebuggingMaterials != debuggingMaterials || 
				previousDebuggingLightmaps != debuggingLightmaps || 
				previousUseLightmapUvsForBase != useLightmapUvsForBase
			)
			{
				if (!visualizeFacesIsRunning && visualizeFacesFinished)
				{
					previousDebuggingMaterials = debuggingMaterials;
					previousDebuggingLightmaps = debuggingLightmaps;
					previousUseLightmapUvsForBase = useLightmapUvsForBase;
					DestroyChildrenObjects();
					loadAndVisualizeBsp();
				}
			}
		}
	}

	void ReVisualizeFaces()
	{
		if (!visualizeFacesIsRunning)
		{
			DestroyChildrenObjects();
			// Re-Render
			VisualizeFaces();
		}
	}

	void DestroyChildrenObjects()
	{
		// Destory previous mesh
		var children = new List<GameObject>();
		foreach (Transform child in transform)
		{
			children.Add(child.gameObject);
		}
		children.ForEach(child => Destroy(child));
	}

	// Generates debug-quality UVs for a list of arbitary verts
	// Doesn't take triangles or surface orientation into account, strictly based on worldspace
	// Some surfaces will look more skewed/streched than others, but useful for quick visualization
	// Example usage: mesh.uv = GenerateWorldSpaceUVs(usedUnityPoints, 4.0f); // 4.0f is a magic number for testing purposes
	static Vector2[] GenerateWorldSpaceUVs(Vector3[] verts, float scale)
	{
		Vector2[] uv = new Vector2[verts.Length];
		for (int i = 0; i < verts.Length; i++)
		{
			uv[i][0] = (verts[i].x + verts[i].z) / scale;
			uv[i][1] = (verts[i].y + verts[i].z) / scale;
		}
		return uv;
	}

	// COULDDO: is this scaling down causing us problems elsewhere?
	float quakeToUnityWorldScale = 0.1f;

	// Translates Quake world space to Unity world space and scale
	Vector3 QuakeToUnityVector3(float x, float y, float z)
	{
		// IMPORTANT: Quake uses Z-up, Unity uses Y-up. Hopefully this is the only spot in the code to do the swap!
		return new Vector3(x * quakeToUnityWorldScale, z * quakeToUnityWorldScale, y * quakeToUnityWorldScale);
	}

	// Accepts a Quake style array of floats
	Vector3 QuakeToUnityVector3(float[] point)
	{
		return QuakeToUnityVector3(point[0], point[1], point[2]);
	}

	// Accepts a Quake style array of ints
	Vector3 QuakeToUnityVector3(int[] point)
	{
		return QuakeToUnityVector3(point[0], point[1], point[2]);
	}

	// Accepts a Quake style dvertex_t vertex
	Vector3 QuakeToUnityVector3(FormatBsp.dvertex_t quakeVertex)
	{
		return QuakeToUnityVector3(quakeVertex.point[0], quakeVertex.point[1], quakeVertex.point[2]);
	}

	// Accepts another Quake style vec3_t vertex
	Vector3 QuakeToUnityVector3(FormatBsp.vec3_t quakeVertex)
	{
		return QuakeToUnityVector3(quakeVertex.x, quakeVertex.y, quakeVertex.z);
	}

	// Translates Quake entity rotation into Unity Vector3 rotation
	Vector3 QuakeToUnityRotation(float quakeRotation)
	{
		return new Vector3(0, 90 - quakeRotation, 0);
	}

	// Example method from Unity barely changed to replace test texture in BSP level
	void GeneratePaletteTexture()
	{
		if (haveGeneratedQuakePalette)
		{
			return;
		}

		if (!palette.CheckIfParsed())
		{
			Debug.LogWarning("Palette isn't parsed yet.");
			return;
		}

		if (defaultMaterial == null)
		{
			defaultMaterial = new Material(Shader.Find("Legacy Shaders/Diffuse")); // was "Standard"
		}

		// Initialize the QuakeColors structure
		QuakeColorsInUnity = new Color[FormatLmpPalette.NUM_COLORS];

		// Assumes the palette is a square, and width and height are the same
		int widthEqualsHeight = (int)Math.Sqrt(FormatLmpPalette.NUM_COLORS);

		Texture2D texture = new Texture2D(widthEqualsHeight, widthEqualsHeight);

		// Use the bspMaterial to hold/refer-to this texture
		if (bspMaterial == null)
		{
			bspMaterial = new Material(defaultMaterial);
			bspMaterial.mainTexture = texture;
		}

		int colorIndex = 0;
		float newRed, newGreen, newBlue;
		FormatLmpPalette.QuakeColor quakeColor;

		int x, y;
		for (y = 0; y < texture.height; y++)
		{
			for (x = 0; x < texture.width; x++)
			{
				// Current Quake color
				quakeColor = palette.colors[colorIndex];
				// Debug.Log("QuakeColor #" + colorIndex + ": " + quakeColor.red + ", " + quakeColor.green + ", " + quakeColor.blue);

				newRed = (float)quakeColor.red / 256;
				newGreen = (float)quakeColor.green / 256;
				newBlue = (float)quakeColor.blue / 256;

				// Debug.Log("Color #" + colorIndex + ": " + newRed + ", " + newGreen + ", " + newBlue);

				// Make Unity color
				Color color = new Color(newRed, newGreen, newBlue, 1);

				// Save the color in the array if it doesn't already exist
				QuakeColorsInUnity[colorIndex] = color;

				// Paint the texture
				texture.SetPixel(x, y, color);
				colorIndex++;
			}
		}
		texture.filterMode = FilterMode.Point;
		texture.Apply();
		haveGeneratedQuakePalette = true;
	}

	void GenerateQuakeMaterials()
	{
		if (haveGeneratedQuakeMaterials)
		{
			return;
		}

		if (!bsp.CheckIfParsed())
		{
			Debug.LogWarning("BSP isn't parsed yet.");
			return;
		}

		int i;
		if (QuakeMaterialsInUnity == null)
		{
			QuakeMaterialsInUnity = new Material[bsp.miptextures.Length];
			for (i = 0; i < QuakeMaterialsInUnity.Length; i++)
			{
				QuakeMaterialsInUnity[i] = new Material(lightmapAtlasShader);
			}
		}
		haveGeneratedQuakeMaterials = true;
	}

	void GenerateQuakeTextures()
	{
		if (haveGeneratedQuakeTextures)
		{
			return;
		}

		if (!bsp.CheckIfParsed())
		{
			Debug.LogWarning("BSP isn't parsed yet.");
			return;
		}

		// Run the pre-requisite methods
		GeneratePaletteTexture();
		GenerateQuakeMaterials();

		// Setup class variables
		QuakeTexturesInUnity = new Texture2D[bsp.miptextures.Length];
		QuakeTextureNameToIndex = new Dictionary<string, int>();

		int i;
		for (i = 0; i < QuakeTexturesInUnity.Length; i++)
		{
			GenerateQuakeTexture(i);
		}

		if (goRenderer != null)
		{
			goRenderer.materials = QuakeMaterialsInUnity;
		}
		else
		{
			// Debug.LogWarning("goRenderer isn't initiated yet.");
		}

		haveGeneratedQuakeTextures = true;
	}

	int MAX_TEXTURE_DIMENSION = 512;
	FormatBsp.miptex_t quakeTexture;
	Texture2D texture;
	void GenerateQuakeTexture(int index)
	{
		if (!bsp.CheckIfParsed())
		{
			Debug.LogWarning("BSP isn't parsed yet.");
			return;
		}

		// Pointer to current Quake texture
		quakeTexture = bsp.miptextures[index];

		// Handle sky textures differently
		if (quakeTexture.name.StartsWith("sky"))
		{
			// Debug.LogWarning("Texture name contains sky == " + quakeTexture.name);
			GenerateQuakeSkyTexture(index);
			return;
		}

		if (quakeTexture.width <= 0 || quakeTexture.width > MAX_TEXTURE_DIMENSION ||
			quakeTexture.height <= 0 || quakeTexture.height > MAX_TEXTURE_DIMENSION ||
			(int)quakeTexture.width <= 0 || (int)quakeTexture.width > MAX_TEXTURE_DIMENSION ||
			(int)quakeTexture.height <= 0 || (int)quakeTexture.height > MAX_TEXTURE_DIMENSION)
		{
			// COULDDO: this catches problem in e1m2.bsp -- symptom of something bigger
			Debug.LogWarning("Texture (index " + index + ") sizes are outside min/max. Width: " + quakeTexture.width + ", Height: " + quakeTexture.height);
			return;
		}

		// New Unity texture to fill with Quake data		
		texture = new Texture2D((int)quakeTexture.width, (int)quakeTexture.height);

		int pixelIndex = 0;
		int colorIndex = 0;
		int x, y;
		int th = texture.height - 1;
		// For every pixel
		for (y = 0; y < texture.height; y++)
		{
			for (x = 0; x < texture.width; x++)
			{
				// Get the color index for this pixel from the texture
				// COULDDO: currently hardcoded to the largest texture size, mipmap index 0
				colorIndex = quakeTexture.pixels[0][pixelIndex];

				// Paint the texture
				texture.SetPixel(x, th - y, QuakeColorsInUnity[colorIndex]);

				pixelIndex++;
			}
		}

		// Give the texture the crunchy no-smoothing, no-blur look of 1990s DOS games
		texture.filterMode = FilterMode.Point;
		texture.Apply();

		// Save the texture for later!
		if (QuakeTextureNameToIndex != null && QuakeTexturesInUnity != null)
		{
			// Handle case where we've already mapped the texture to an index
			if (!QuakeTextureNameToIndex.ContainsKey(quakeTexture.name))
			{
				QuakeTextureNameToIndex.Add(quakeTexture.name, index);
				QuakeTexturesInUnity[index] = texture;
			}

			// Export the texture as a PNG
			if (saveFiles)
			{
				texture.name = quakeTexture.name;
				SaveTextureAsPng(texture, quakeTexture.name);
			}

		}

		// New Unity Material to refer to this texture
		if (QuakeMaterialsInUnity != null)
		{
			QuakeMaterialsInUnity[index].name = quakeTexture.name;

			// Just debugging code
			if (debuggingMaterials)
			{
				// -- leave as default texture -- QuakeMaterialsInUnity[index].mainTexture = texture;
			}
			else
			{
				QuakeMaterialsInUnity[index].mainTexture = texture;
				QuakeMaterialsInUnity[index].SetTexture("_LightMap", lightmapAtlasTexture);
				// -- the "unlit" lightmap texture -- QuakeMaterialsInUnity[index].SetTexture("_LightMap", tinyFullbrightLightmapAtlasTexture);
			}
		}
		else
		{
			Debug.LogWarning("QuakeMaterialsInUnity is null.");
		}

	} // end of GeneratequakeTexture()

	void setLightmapAtlasToUnlit()
	{
		for (int i=0; i < QuakeMaterialsInUnity.Length; i++)
		{
			if (QuakeMaterialsInUnity[i] != null)
			{
				if (QuakeMaterialsInUnity[i].shader == lightmapAtlasShader ||
					QuakeMaterialsInUnity[i].shader == lightmapAtlasAsDiffuseShader ||
					QuakeMaterialsInUnity[i].shader == solidWhiteShader || 
					QuakeMaterialsInUnity[i].shader == shadedWireframeShader)
				{
					QuakeMaterialsInUnity[i].shader = lightmapAtlasShaderUnlit;
				}
			}
		}
	}

	void setLightmapAtlasToLit()
	{
		for (int i=0; i < QuakeMaterialsInUnity.Length; i++)
		{
			if (QuakeMaterialsInUnity[i] != null)
			{
				if (QuakeMaterialsInUnity[i].shader == lightmapAtlasShaderUnlit ||
					QuakeMaterialsInUnity[i].shader == lightmapAtlasAsDiffuseShader ||
					QuakeMaterialsInUnity[i].shader == solidWhiteShader ||
					QuakeMaterialsInUnity[i].shader == shadedWireframeShader)
				{
					QuakeMaterialsInUnity[i].shader = lightmapAtlasShader;
				}
			}
		}
	}

	void setMaterialsToWireframe()
	{
		// COULDDO: investigate this repo: https://github.com/MinaPecheux/unity-tutorials/tree/main/Assets/00-Shaders/CrossPlatformWireframe
		// COULDDO: also look into https://docs.unity3d.com/ScriptReference/MeshTopology.html

		// This along with setting wireframe mode in RenderModes.cs works ok so far
		setTexturesToWhite();

		// TODO: But this shaded wireframe shader doesn't work yet, likely problems in shader
		/***
		for (int i=0; i < QuakeMaterialsInUnity.Length; i++)
		{
			if (QuakeMaterialsInUnity[i] != null)
			{
				if (QuakeMaterialsInUnity[i].shader == lightmapAtlasShaderUnlit ||
					QuakeMaterialsInUnity[i].shader == lightmapAtlasAsDiffuseShader ||
					QuakeMaterialsInUnity[i].shader == lightmapAtlasShader ||
					QuakeMaterialsInUnity[i].shader == solidWhiteShader )
				{
					QuakeMaterialsInUnity[i].shader = shadedWireframeShader;
				}
			}
		}
		/***/
	}

	void setTexturesToWhite()
	{
		for (int i=0; i < QuakeMaterialsInUnity.Length; i++)
		{
			if (QuakeMaterialsInUnity[i] != null)
			{
				if (QuakeMaterialsInUnity[i].shader == lightmapAtlasShaderUnlit ||
					QuakeMaterialsInUnity[i].shader == lightmapAtlasAsDiffuseShader ||
					QuakeMaterialsInUnity[i].shader == lightmapAtlasShader ||
					QuakeMaterialsInUnity[i].shader == shadedWireframeShader)
				{
					QuakeMaterialsInUnity[i].shader = solidWhiteShader;
				}
			}
		}

	}

	void setLightmapAtlasAsDiffuse()
	{
		for (int i=0; i < QuakeMaterialsInUnity.Length; i++)
		{
			if (QuakeMaterialsInUnity[i] != null)
			{
				if (QuakeMaterialsInUnity[i].shader == lightmapAtlasShaderUnlit || 
					QuakeMaterialsInUnity[i].shader == lightmapAtlasShader ||
					QuakeMaterialsInUnity[i].shader == solidWhiteShader)
				{
					QuakeMaterialsInUnity[i].shader = lightmapAtlasAsDiffuseShader;
				}
			}
		}
	}

	Material QuakeSkyboxMaterial;
	Material QuakeSkyboxPunchThroughMaterial;
	bool QuakeSkyboxLoaded = false;
	void GenerateQuakeSkyTexture(int index)
	{
		if (!bsp.CheckIfParsed())
		{
			Debug.LogWarning("BSP isn't parsed yet.");
			return;
		}

		// If we've already loaded a skybox texture
		if (QuakeSkyboxLoaded)
		{
			// My research and Quake test-maps suggest that you can put multiple sky-textures into a .map, but when it comes to render in the actual game, only one sky texture is shown on all sky surfaces
			// In winquake.exe software renderer and glquake.exe hardware renderer, the last-encountered sky texture is shown on all surfaces
			// In Quake_x64_steam.exe the recent re-release version on Steam, the last-encountered sky texture is shown on all surfaces
			// In quakespasm-spiked-win64.exe renderer, first-encountered sky texture is shown on all surfaces
			// So I'm going with the official behavior of displaying the last-encountered sky texture
			Debug.LogWarning("This map has multiple sky textures. Only one will be displayed.");
		}

		// Pointer to current Quake texture
		quakeTexture = bsp.miptextures[index];

		// Sky texture is stored as two textures in a double-wide section
		int computedWidth  = (int)( quakeTexture.width);		// (int)( quakeTexture.width  / 2);
		int computedHeight = (int)( quakeTexture.height);		// (int)( quakeTexture.height / 2);

		if (quakeTexture.width <= 0 || quakeTexture.width > MAX_TEXTURE_DIMENSION ||
			quakeTexture.height <= 0 || quakeTexture.height > MAX_TEXTURE_DIMENSION ||
			(int)quakeTexture.width <= 0 || (int)quakeTexture.width > MAX_TEXTURE_DIMENSION ||
			(int)quakeTexture.height <= 0 || (int)quakeTexture.height > MAX_TEXTURE_DIMENSION)
		{
			// COULDDO: Added this to catch problem in e1m2.bsp // P1
			Debug.LogWarning("Texture (index " + index + ") sizes are outside min/max.");
			return;
		}

		// New Unity texture to fill with Quake data		
		texture = new Texture2D(computedWidth, computedHeight);

		int pixelIndex = 0;
		int colorIndex = 0;
		int x, y;
		int th = texture.height - 1;
		// For every pixel
		for (y = 0; y < computedHeight; y++)
		{
			for (x = 0; x < computedWidth; x++)
			{
				// Get the color index for this pixel from the texture
				// COULDDO: currently hardcoded to the largest texture size, mipmap index 0
				colorIndex = quakeTexture.pixels[0][pixelIndex];

				// Paint the texture
				texture.SetPixel(x, th - y, QuakeColorsInUnity[colorIndex]);

				pixelIndex++;
			}
		}

		// Give the texture the crunchy no-smoothing, no-blur look of 1990s DOS games
		texture.filterMode = FilterMode.Point;
		texture.Apply();

		// Export the texture as a PNG
		if (saveFiles)
		{
			SaveTextureAsPng(texture, quakeTexture.name);
		}

		// Split this loaded skybox texture into two smaller textures
		computedWidth = computedWidth/2;
		// Debug.LogWarning("computedWidth = " + computedWidth);
		Texture2D texture2 = new Texture2D(computedWidth, computedHeight);
		Texture2D texture3 = new Texture2D(computedWidth, computedHeight);
		
		// Copy the left half of the texture
		CopyTexture2D(texture, 0			, 0, 			  computedWidth, computedHeight, texture2, 0, 0);
		// Copy the right half of the texture
		CopyTexture2D(texture, computedWidth, computedHeight, computedWidth, computedHeight, texture3, 0, 0);

		// Make the foreground sky texture have transparent patches in it
		ChangeColorToAlphaTexture2D(texture2, Color.black);

		// Save these new textures
		texture2.filterMode = FilterMode.Point;
		texture2.Apply();
		texture3.filterMode = FilterMode.Point;
		texture3.Apply();

		// Save the texture for later!
		if (QuakeTextureNameToIndex != null && QuakeTexturesInUnity != null)
		{
			QuakeTextureNameToIndex.Add(quakeTexture.name, index);
			QuakeTexturesInUnity[index] = texture2; // Save the left side when it's a sky texture
		}

		// New Unity Material to refer to this texture
		if (QuakeMaterialsInUnity != null)
		{
			// Just debugging code
			if (debuggingMaterials)
			{
				QuakeMaterialsInUnity[index].name = quakeTexture.name;
				// -- leave as default texture -- QuakeMaterialsInUnity[index].mainTexture = texture;
			}
			else
			{
				// Turn this face's material into a material that can see the sky
				if (QuakeSkyboxPunchThroughMaterial == null)
				{
					QuakeSkyboxPunchThroughMaterial = new Material(skyboxPunchThroughShader);
					QuakeSkyboxPunchThroughMaterial.name = quakeTexture.name;
				}
				QuakeMaterialsInUnity[index] = QuakeSkyboxPunchThroughMaterial;
				
				if (QuakeSkyboxMaterial == null)
				{
					VisualizeSky();
				}

				// COULDDO: this is a lil redundant, and will need to revisit once we make more features toggleable
				// This redundant check is here in case we decide to make VisualizeSky() not actually finish configuring the sky
				if (QuakeSkyboxMaterial == null)
				{
					return;
				}

				/***
				// OLD and just included for reference this is very specific to having a six-sided skybox // P3
				QuakeSkyboxMaterial.SetTexture("_FrontTex",	texture2);
				QuakeSkyboxMaterial.SetTexture("_BackTex",	texture2);
				QuakeSkyboxMaterial.SetTexture("_LeftTex",	texture2);
				QuakeSkyboxMaterial.SetTexture("_RightTex",	texture2);
				QuakeSkyboxMaterial.SetTexture("_UpTex",	texture2);
				QuakeSkyboxMaterial.SetTexture("_DownTex",	texture2);
				***/

				// This is for the retro-style Quake shader
				QuakeSkyboxMaterial.SetTexture("_MainTex",			texture3);
				QuakeSkyboxMaterial.SetTexture("_SecondaryTex",		texture2);
				QuakeSkyboxMaterial.SetFloat("_MainTexSpeed",		0.1f);
				QuakeSkyboxMaterial.SetFloat("_SecondaryTexSpeed",	1.0f);
				QuakeSkyboxMaterial.SetFloat("_CutOff",				0.025f); // their old default of zero resulted in further sky not rendering
				QuakeSkyboxMaterial.SetFloat("_SphereSize",			1.0f);
			}

			QuakeSkyboxLoaded = true;
		}
		else
		{
			Debug.LogWarning("QuakeMaterialsInUnity is null.");
		}

	} // end of GenerateQuakeSkyTexture()

	void SaveTextureAsPng(Texture2D texture, String fileName)
	{
		byte[] bytes = texture.EncodeToPNG();
		Directory.CreateDirectory(Application.persistentDataPath + "/ConvertedData");
		Directory.CreateDirectory(Application.persistentDataPath + "/ConvertedData/Textures");
		// Cleanup the filename
		foreach (char c in System.IO.Path.GetInvalidFileNameChars())
		{
			string s = c + "";
			fileName = fileName.Replace(s, string.Empty);
		}
		fileName = fileName.Replace("*","").Replace("+","");
		File.WriteAllBytes(Application.persistentDataPath + "/ConvertedData/Textures/" + fileName + ".png", bytes);
	}

	// Example method from Unity barely changed to replace test texture in BSP level
	void GenerateTextureTest()
	{
		Texture2D texture = new Texture2D(128, 128);

		int x, y;
		for (y = 0; y < texture.height; y++)
		{
			for (x = 0; x < texture.width; x++)
			{
				Color color = ((x & y) != 0 ? Color.white : Color.gray);
				texture.SetPixel(x, y, color);
			}
		}
		texture.Apply();

		// New Unity Material to refer to this texture
		if (QuakeMaterialsInUnity != null)
		{
			QuakeMaterialsInUnity[0].name = "generated texture";
			QuakeMaterialsInUnity[0].mainTexture = texture;
		}
		else
		{
			Debug.LogWarning("QuakeMaterialsInUnity is null.");
		}

	}

	void ReGenerateQuakeLightmapTextures()
	{
		// Debug.Log("ReGenerateQuakeLightmapTextures()");
		haveGeneratedQuakeLightmapTextures = false;
		GenerateQuakeLightmapTextures();
		SetAllQuakeMaterialsToLightmap(debuggingLightmapIndex);
	}

	void GenerateQuakeLightmapTextures()
	{
		TimerHelper timer = new TimerHelper(this.GetType() + ".GenerateQuakeLightmapTextures()");

		// Early out if this is currently running or already been run!
		if (GenerateQuakeLightmapTexturesIsRunning)
		{
			return;
		}
		if (haveGeneratedQuakeLightmapTextures)
		{
			return;
		}
		GenerateQuakeLightmapTexturesIsRunning = true;

		Debug.Log("GenerateQuakeLightmapTextures() bsp.num_lightmaps: " + bsp.num_lightmaps + "; quakeFacesBoundingBoxCount: " + quakeFacesBoundingBoxCount);

		quakeLightmapTexturesCount = 0;
		QuakeLightmapTexturesInUnity = new Texture2D[bsp.num_lightmaps];
		
		atlasPosX = 0;
		atlasPosY = 0;

		/*** Beginning of section to skip lightmap atlast loading ***/

		if (useLightmapAtlas)
		{
			// TODO: likely need to allocate all of the possible solid shades of grey for the lightmap, NOT JUST white and black // P2
			// Create initial swatch for default/black
			FillWithColorTexture2D(defaultLightingColor, (int)atlasUnitWithBorder, (int)atlasUnitWithBorder, lightmapAtlasTexture, atlasPosX, atlasPosY);
			atlasPosX += (int)atlasUnitWithBorder; // Bump atlas position just once

			// Create second swatch for fullbright/white
			FillWithColorTexture2D(fullbrightLightingColor, (int)atlasUnitWithBorder, (int)atlasUnitWithBorder, lightmapAtlasTexture, atlasPosX, atlasPosY);
			atlasPosX += (int)atlasUnitWithBorder; // Bump atlas position just once

			// account for border around future swatches
			atlasPosX += (int)atlasBorderWidth;
			atlasPosY += (int)atlasBorderWidth;
		}

		Vector2 swatchPos;
		for (int i = 0; i < bsp.num_lightmaps; i++)
		{
			GenerateQuakeLightmapTexture(i);
			// -- unused -- totalPixelError += currentPixelError;

			// copy that texture into the atlas
			if (QuakeLightmapTexturesInUnity[i] != null)
			{
				if (useLightmapAtlas)
				{
					// Get the atlas coordinates for this swatch in a consistent manner
					swatchPos = GetLightmapSwatchPositioninAtlas(i + numberOfPreMadeLightmaps);

					// THIS WORKS...
					// Copy this lightmap into the atlas
					// CopyTexture2D(QuakeLightmapTexturesInUnity[i], 0, 0, QuakeLightmapTexturesInUnity[i].width, QuakeLightmapTexturesInUnity[i].height, lightmapAtlasTexture, (int)swatchPos.x, (int)swatchPos.y);
					// Extend the edges of this lightmap swatch in the atlas
					// ExtendBorderTexture2D(QuakeLightmapTexturesInUnity[i], 0, 0, QuakeLightmapTexturesInUnity[i].width, QuakeLightmapTexturesInUnity[i].height, lightmapAtlasTexture, (int)swatchPos.x, (int)swatchPos.y, (int)atlasBorderWidth);

					// BUT THIS FEELS MUCH TIGHTER IN A SINGLE FUNCTION!
					// Copy this lightmap into the atlas AND extend the edges by X pixels
					CopyTexture2DAndExtendBorders(QuakeLightmapTexturesInUnity[i], 0, 0, QuakeLightmapTexturesInUnity[i].width, QuakeLightmapTexturesInUnity[i].height, lightmapAtlasTexture, (int)swatchPos.x, (int)swatchPos.y, (int)atlasBorderWidth);

					// Delete the previous texture from memory
					Destroy(QuakeLightmapTexturesInUnity[i]);
				}
			}
		}

		// How large are the lightmaps we're doing with?
		Debug.Log("quakeLightmapTexturesCount: " + quakeLightmapTexturesCount + "; maxLightmapWidth: " + maxLightmapWidth + "; maxLightmapHeight: " + maxLightmapHeight);

		/*** End of section to skip lightmap atlast loading ***/

		if (useLightmapAtlas)
		{
			if (lightmapSmoothing)
			{
				lightmapAtlasTexture.filterMode = FilterMode.Bilinear;
			}
			else
			{
				// Give the texture the crunchy no-smoothing, no-blur look of 1990s DOS games
				lightmapAtlasTexture.filterMode = FilterMode.Point;
			}
			// Set this atlas material to use the atlas texture
			lightmapAtlasTexture.Apply();
			lightmapAtlasMaterial = new Material(defaultMaterial);
			lightmapAtlasMaterial.mainTexture = lightmapAtlasTexture;

			// Trying to be extra clear that we want to delete the small individual swatches
			QuakeLightmapTexturesInUnity = null;
		}

		haveGeneratedQuakeLightmapTextures = true;
		GenerateQuakeLightmapTexturesIsRunning = false;

		// Export the lightmap atlas as a PNG for testing purposes
		if (saveFiles && useLightmapAtlas)
		{
			SaveTextureAsPng(lightmapAtlasTexture, bspFileNameNoExtension + "_lightmaps");
		}

		Debug.Log("Finished generating lightmap textures");

		timer.Stop();
	}

	float[] multiplesOfSixteen;
	float[] multiplesOfEighteen;
	// -- unused -- float[] powerOfTwos = new float[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 }; // Believe 512 is the max width
	int maxLightmapWidth = 0;
	int maxLightmapHeight = 0;
	int quakeLightmapTexturesCount = 0;
	// Frequently used variables in lightmap texture generation
	byte[] quakeLightmap;
	int firstPixelIndex;
	int offset;
	int nextOffset;
	int faceIndex;
	// -- unused -- int expectedNumberOfPixels;
	float[] faceBB;
	float _minU;
	float _maxU;
	float _minV;
	float _maxV;
	int sizeWidth;
	int sizeHeight;
	int allocatedPixels;
	float totalPixels;
	int pixelIndex;
	int x, y, invY; // -- unused -- , invX;
	// -- unused -- int tw;
	int th;
	Color pixelColor;
	int validLightmapPixels;
	int lightmapPixelIndex;

	void GenerateQuakeLightmapTexture(int index)
	{
		if (!bsp.CheckIfParsed())
		{
			Debug.LogWarning("BSP isn't parsed yet.");
			return;
		}

		// Skip if we've already done the work
		if (QuakeLightmapTexturesInUnity[index] != null)
		{
			return;
		}

		/*** 
		// MAKE SURE WE'RE NOT IN A BAD STATE
		if (quakeLightmap == null)
		{
			Debug.LogWarning("quakeLightmap = null");
		}
		***/

		// GET THE SIZE OF THE FACE OF THIS LIGHTMAP

		// get the face index for this particular lightmap index
		offset = bsp.lightmapIndexToOffsetLookup[index];
		faceIndex = bsp.lightmapOffsetToFaceLookup[offset];

		/***
		// MAKE SURE WE'RE NOT DOING BAD LIGHTMAP-to-FACE LOOKUP
		FormatBsp.dface_t quakeFace = bsp.dfaces[faceIndex];
		if (quakeFace.styles[0] == 0xFF)
		{
			Debug.LogWarning("quakeFace.styles[0] = 0xFF");
		}
		if (quakeFace.lightofs != offset)
		{
			Debug.LogWarning("quakeFace.lightofs != offset");
		}
		if (bsp.lightmapOffsetToIndexLookup[quakeFace.lightofs] != index)
		{
			Debug.LogWarning("bsp.lightmapOffsetToIndexLookup[quakeFace.lightofs] != index");
		}
		if (faceIndex < 0 || faceIndex >= quakeFacesBoundingBoxCount)
		{
			Debug.LogWarning("Lightmap [" + index + "] 's faceIndex = " + faceIndex + "; could be out of bounds.");
		}
		// END DOUBLE CHECKING
		***/

		// get the dimentions of the face
		faceBB = quakeFacesBoundingBox[faceIndex];
		// for reference: quakeFacesBoundingBox[i] = new float[] { uMin, vMin, uMax, vMax, bbWidth, bbHeight, bbwWidth, bbwHeight , xMin, yMin, xMax, yMax};
		// --->													   0     1     2     3     4        5         6         7           8     9     10    11

		if (faceBB == null)
		{
			// these faces didn't get processed
			return;
		}

		// 2022-11-22 trying to figure out this lightmap swatch width problem
		// Found good community specs/code for Quake2 lightmaps: https://www.gamedev.net/forums/topic/538713-bspv38-quake-2-bsp-loader-lightmap-problem/4477340/
		// This is also useful to see it explained another way:  https://www.flipcode.com/archives/Quake_2_BSP_File_Format.shtml
		_minU = (float)RoundDown( RoundDown( faceBB[8])  / 16.0f );
		_maxU = (float)RoundUp(   RoundDown( faceBB[10]) / 16.0f );
		_minV = (float)RoundDown( RoundDown( faceBB[9])  / 16.0f );
		_maxV = (float)RoundUp(   RoundDown( faceBB[11]) / 16.0f );
		// sizeWidth and sizeHeight are used a bunch below
		sizeWidth =  (int)(_maxU - _minU + 1);
		sizeHeight = (int)(_maxV - _minV + 1);

		// COULDDO: looks like our atlasUnit is too small based on dm6 testing, or 16 is the hard limit?
		// Trying to reduce the overall size of the lightmap textures and atlas
		if (sizeWidth > atlasUnit)
		{
			// Debug.LogError("Trying to load a lightmap texture for atlas that's wider (" + sizeWidth + ") than atlasUnit (" + atlasUnit + ")");
			sizeWidth = (int)atlasUnit;
		}
		if (sizeHeight > atlasUnit)
		{
			// Debug.LogError("Trying to load a lightmap texture for atlas that's taller (" + sizeHeight + ") than atlasUnit (" + atlasUnit + ")");
			sizeHeight = (int)atlasUnit;
		}

		totalPixels = sizeWidth * sizeHeight;

		// COULDDO: figure out how to handle the case with zero pixels
		if (totalPixels == 0)
		{
			Debug.LogWarning("lightmap[" + index + "] has zero pixels");
			return;
		}

		// TRACK OVERALL SIZES OF LIGHTMAP SWATCHES

		if (sizeWidth > maxLightmapWidth)
		{
			maxLightmapWidth = (int)sizeWidth;
		}
		if (sizeHeight > maxLightmapHeight)
		{
			maxLightmapHeight = (int)sizeHeight;
		}

		// NOW THAT WE KNOW THE DIMENSIONS AND THUS TOTAL PIXELS, ACTUALLY PARSE THE LIGHTMAP DATA
		// GRAB THE LIGHTMAP PIXEL DATA
		bsp.ParseLightmap(index, (int)totalPixels);

		// Pointer to current Quake lightmap texture data
		quakeLightmap = bsp.lightmaps[index];
		firstPixelIndex = 0;

		// START THE UNITY ASSET SETUP PROCESS

		// Allocate a texture large enough to store these Quake lightmap pixels as-is.
		Texture2D lightmapTexture = new Texture2D(sizeWidth, sizeHeight);

		// Reset our working variables
		// -- unused -- tw = (int)sizeWidth  - 1;
		th = (int)sizeHeight - 1;
		validLightmapPixels = 0;
		pixelIndex = firstPixelIndex;

		for (y = 0; y < sizeHeight; y+=1)
		{
			invY = th-y;

			for (x = 0; x < sizeWidth; x+=1)
			{
				// -- unused -- invX = tw-x;
				pixelColor = Color.magenta; // Color.clear; // Color.white; // Default found no lightmap data for this pixel

				// If the pixel is within bounds
				if (pixelIndex < totalPixels && pixelIndex < quakeLightmap.Length)
				{
					// Get the color index for this pixel from the texture
					pixelColor = QuakeLightmapInUnity(quakeLightmap[pixelIndex]);
					// COULDDO: for debugging purposes, set a unique color per lightmap swatch, so I can tell them apart and help debug stuff
					pixelIndex++;
					validLightmapPixels++;
				}
				else
				{
					// setting this to black, mostly to help with debugging
					pixelColor = Color.black;
				}

				// Paint the lightmap texture
				lightmapTexture.SetPixel(x, invY, pixelColor);
			}
		}

		if (lightmapSmoothing)
		{
			lightmapTexture.filterMode = FilterMode.Bilinear;
		}
		else
		{
			// Give the texture the crunchy no-smoothing, no-blur look of 1990s DOS games
			lightmapTexture.filterMode = FilterMode.Point;
		}
		lightmapTexture.Apply();
		quakeLightmapTexturesCount++;

		// Debugging
		// Debug.Log("Valid lightmap pixels: " + validLightmapPixels + "; Expected number of pixels: " + expectedNumberOfPixels);
		// -- unused -- currentPixelError = expectedNumberOfPixels - validLightmapPixels;

		// Save a reference to this texture for later
		QuakeLightmapTexturesInUnity[index] = lightmapTexture;
	}

	void GenerateQuakeLightmapMaterials()
	{
		if (haveGeneratedQuakeLightmapMaterials)
		{
			return;
		}

		if (!bsp.CheckIfParsed())
		{
			Debug.LogWarning("BSP isn't parsed yet.");
			return;
		}

		if (!haveGeneratedQuakeLightmapTextures)
		{
			GenerateQuakeLightmapTextures();
		}

		int i;
		if (QuakeLightmapMaterialsInUnity == null)
		{
			QuakeLightmapMaterialsInUnity = new Material[bsp.num_lightmaps];
			for (i = 0; i < QuakeLightmapMaterialsInUnity.Length; i++)
			{
				QuakeLightmapMaterialsInUnity[i] = new Material(defaultMaterial);
				if (!useLightmapAtlas)
				{
					QuakeLightmapMaterialsInUnity[i].mainTexture = QuakeLightmapTexturesInUnity[i];
				}
			}
		}
		haveGeneratedQuakeLightmapMaterials = true;
	}

	void SetAllQuakeMaterialsToLightmap(int index)
	{
		Texture2D lightmapTexure = QuakeLightmapTexturesInUnity[index];
		if (lightmapTexure != null)
		{
			SetAllQuakeMaterialsToTexture(lightmapTexure);
			// Debug.Log("Set lightmap index to: " + debuggingLightmapIndex + "; width: " + lightmapTexure.width);
		}
		else
		{
			Debug.LogWarning("Set lightmap index to: " + debuggingLightmapIndex + "; BUT DOES NOT EXIST");
		}
	}

	void SetAllQuakeMaterialsToLightmapAtlas()
	{
		Texture2D lightmapTexure = lightmapAtlasTexture;
		if (lightmapTexure != null)
		{
			SetAllQuakeMaterialsToTexture(lightmapTexure);
		}
		else
		{
			Debug.LogWarning("Cannot set all quake materials to atlas -- atlas is null!");
		}
	}

	void SetAllQuakeMaterialsToTexture(Texture2D newTexture)
	{
		for (int i = 0; i < QuakeMaterialsInUnity.Length; i++)
		{
			if (QuakeMaterialsInUnity[i] != null)
			{
				QuakeMaterialsInUnity[i].mainTexture = newTexture;
			}
		}
	}

	void SetAllQuakeMaterialsToTexture(Material newMaterial)
	{
		for (int i = 0; i < QuakeMaterialsInUnity.Length; i++)
		{
			if (QuakeMaterialsInUnity[i] != null)
			{
				QuakeMaterialsInUnity[i].mainTexture = newMaterial.mainTexture;
			}
		}
	}

	Color QuakeLightmapInUnity(int value)
	{
		return QuakeLightmapInUnity((byte)value);
	}

	float maxLightmapBrightness = 0;

	Color QuakeLightmapInUnity(byte value)
	{
		int greyscaleInt = (int)value;
		float greyscaleFloat = (float)greyscaleInt / 255.0f;
		if (greyscaleFloat > maxLightmapBrightness)
		{
			maxLightmapBrightness = greyscaleFloat;
		}
		greyscaleFloat *= lightmapGammaAdjustment;
		if (greyscaleFloat > 1f) 
		{
			greyscaleFloat = 1f;
		}
		return new Color(greyscaleFloat, greyscaleFloat, greyscaleFloat);
	}

	Vector2[] lightmapSwatchPositions;
	bool[]    lightmapSwatchPositionsCached;
	Vector2 GetLightmapSwatchPositioninAtlas(int faceIndex)
	{
		// COULDDO: work on packing the lightmap MUCH MUCH tighter

		// Do not account for pre-existing lightmaps here

		if (lightmapSwatchPositions == null)
		{
			lightmapSwatchPositions = new Vector2[FormatBsp.MAX_MAP_FACES];
			lightmapSwatchPositionsCached = new Boolean[FormatBsp.MAX_MAP_FACES];
		}
		if (!lightmapSwatchPositionsCached[faceIndex])
		{
			Vector2 pos = new Vector2();
			pos.x = faceIndex * atlasUnitWithBorder;
			pos.y = 0;
			while (pos.x + atlasUnitWithBorder > maxAtlasPos)
			{
				pos.x -= maxAtlasPos;
				pos.y += atlasUnitWithBorder;
			}
			pos.x += atlasBorderWidth;
			pos.y += atlasBorderWidth;
			// Debug.Log("GetLightmapSwatchPositioninAtlas(" + faceIndex + ") calculating " + pos.x + ":" + pos.y);
			lightmapSwatchPositions[faceIndex] = pos;
			lightmapSwatchPositionsCached[faceIndex] = true;
		}
		// Debug.Log("GetLightmapSwatchPositioninAtlas(" + faceIndex + ") returning " + lightmapSwatchPositions[faceIndex].x + ":" + lightmapSwatchPositions[faceIndex].y);
		return lightmapSwatchPositions[faceIndex];
	}

	float Round(float roundMe)
	{
		return (float)Math.Round((double)roundMe);
	}

	float RoundDown(float roundMe)
	{
		return (float)Math.Floor((double)roundMe);
	}

	float RoundUp(float roundMe)
	{
		return (float)Math.Ceiling((double)roundMe);
	}

	double RoundUpToNearest(double roundMe, float[] possibleAnswers)
	{
		// COULDDO 2022-11-23 -- this looks suspiciously inefficient
		double returnVal = roundMe;
		double[] deltas = new double[possibleAnswers.Length];
		int nearestIndex = 0;

		for (int i = 0; i < possibleAnswers.Length; i++)
		{
			deltas[i] = Math.Abs(possibleAnswers[i] - roundMe);

			if (roundMe < possibleAnswers[i] && deltas[i] <= deltas[nearestIndex])
			{
				nearestIndex = i;
				returnVal = possibleAnswers[i];
			}
		}

		return returnVal;
	}

	double Sqrt(double doubleNumber)
	{
		return Math.Sqrt(doubleNumber);
	}

	float Sqrt(float floatNumber)
	{
		decimal dec = new decimal(floatNumber);
		double doub = (double)dec;
		double sqrt = Math.Sqrt(doub);

		float result = (float)sqrt;
		if (float.IsPositiveInfinity(result))
		{
			result = float.MaxValue;
		}
		else if (float.IsNegativeInfinity(result))
		{
			result = float.MinValue;
		}
		return result;
	}

	// was Factor()
	// COULDDO: Could be optimized to only go from 1 to the square root, but would then want the option to sort the results
	public List<int> GetFactors(int number)
	{
		List<int> factors = new List<int>();
		int max = number; // (int)Math.Sqrt(number);  //round down
		int factor;
		for (factor = 1; factor <= max; ++factor)
		{ //test from 1 to the square root, or the int below it, inclusive.
			if (number % factor == 0)
			{
				factors.Add(factor);
			}
		}
		return factors;
	}

	// Inspired by Graphics.CopyTexture, but just works on Texture2D
	void CopyTexture2D(Texture2D src, int srcX, int srcY, int srcWidth, int srcHeight, Texture2D dst, int dstX, int dstY)
	{
		if (src == null)
		{
			Debug.LogError("src is null");
			return;
		}
		else if (dst == null)
		{
			Debug.LogError("dst is null");
			return;
		}

		int getX, getY, setX, setY;
		getX = srcX;
		getY = srcY;
		setX = dstX;
		setY = dstY;
		Color clr;

		while ( getY < srcY + srcHeight )
		{
			while ( getX < (srcX + srcWidth) )
			{
				clr = src.GetPixel(getX, getY);
				dst.SetPixel(setX, setY, clr);

				getX++;
				setX++;
			}

			getX = srcX;
			setX = dstX;
			getY++;
			setY++;
		}

	}

	// Based on our CopyTexture2D
	void ExtendBorderTexture2D(Texture2D src, int srcX, int srcY, int srcWidth, int srcHeight, Texture2D dst, int dstX, int dstY, int borderWidth)
	{
		if (src == null)
		{
			Debug.LogError("src is null");
			return;
		}
		else if (dst == null)
		{
			Debug.LogError("dst is null");
			return;
		}

		int getX, getY, setX, setY, borderCount;
		getX = srcX;
		getY = srcY;
		setX = dstX;
		setY = dstY;
		Color clr;

		while ( getY < srcY + srcHeight )
		{
			while ( getX < (srcX + srcWidth) )
			{
				// Get the color
				clr = src.GetPixel(getX, getY);

				// CORNERS

				// Top left corner
				if (getX == srcX && getY == srcY)
				{
					FillWithColorTexture2D(clr, (int)borderWidth, (int)borderWidth, dst, setX-borderWidth, setY-borderWidth);
				}
				// Top right corner
				else if (getX == srcX + srcWidth -1 && getY == srcY)
				{
					FillWithColorTexture2D(clr, (int)borderWidth, (int)borderWidth, dst, setX+1, setY-borderWidth);
				}
				// Bottom left corner
				else if (getX == srcX && getY == srcY + srcHeight -1)
				{
					FillWithColorTexture2D(clr, (int)borderWidth, (int)borderWidth, dst, setX-borderWidth, setY+1);
				}
				// Bottom right corner
				else if (getX == srcX + srcWidth -1 && getY == srcY + srcHeight -1)
				{
					FillWithColorTexture2D(clr, (int)borderWidth, (int)borderWidth, dst, setX+1, setY+1);
				}

				// EDGES

				// Top row above the rectangle
				if (getY == srcY)
				{
					borderCount = 1;
					while (borderCount <= borderWidth)
					{
						dst.SetPixel(setX, setY-borderCount, clr);
						borderCount++;
					}
				}
				// Bottom row above the rectangle
				else if (getY == srcY + srcHeight -1)
				{
					borderCount = 1;
					while (borderCount <= borderWidth)
					{
						dst.SetPixel(setX, setY+borderCount, clr);
						borderCount++;
					}
				}
				// Left column of the rectangle
				if (getX == srcX)
				{
					borderCount = 1;
					while (borderCount <= borderWidth)
					{
						dst.SetPixel(setX-borderCount, setY, clr);
						borderCount++;
					}
				}
				// Right column of the rectangle
				else if (getX == srcX + srcWidth -1)
				{
					borderCount = 1;
					while (borderCount <= borderWidth)
					{
						dst.SetPixel(setX+borderCount, setY, clr);
						borderCount++;
					}
				}
				//clr = src.GetPixel(getX, getY);
				//dst.SetPixel(setX, setY, clr);

				getX++;
				setX++;
			}

			getX = srcX;
			setX = dstX;
			getY++;
			setY++;
		}

	}

	void CopyTexture2DAndExtendBorders(Texture2D src, int srcX, int srcY, int srcWidth, int srcHeight, Texture2D dst, int dstX, int dstY, int borderWidth)
	{
		if (src == null)
		{
			Debug.LogError("src is null");
			return;
		}
		else if (dst == null)
		{
			Debug.LogError("dst is null");
			return;
		}

		int getX, getY, setX, setY, borderCount;
		getX = srcX;
		getY = srcY;
		setX = dstX;
		setY = dstY;
		Color clr;

		while ( getY < srcY + srcHeight )
		{
			while ( getX < (srcX + srcWidth) )
			{
				// Get the color
				clr = src.GetPixel(getX, getY);

				// CORNERS

				// Top left corner
				if (getX == srcX && getY == srcY)
				{
					FillWithColorTexture2D(clr, (int)borderWidth, (int)borderWidth, dst, setX-borderWidth, setY-borderWidth);
				}
				// Top right corner
				else if (getX == srcX + srcWidth -1 && getY == srcY)
				{
					FillWithColorTexture2D(clr, (int)borderWidth, (int)borderWidth, dst, setX+1, setY-borderWidth);
				}
				// Bottom left corner
				else if (getX == srcX && getY == srcY + srcHeight -1)
				{
					FillWithColorTexture2D(clr, (int)borderWidth, (int)borderWidth, dst, setX-borderWidth, setY+1);
				}
				// Bottom right corner
				else if (getX == srcX + srcWidth -1 && getY == srcY + srcHeight -1)
				{
					FillWithColorTexture2D(clr, (int)borderWidth, (int)borderWidth, dst, setX+1, setY+1);
				}

				// EDGES

				// Top row above the rectangle
				if (getY == srcY)
				{
					borderCount = 1;
					while (borderCount <= borderWidth)
					{
						dst.SetPixel(setX, setY-borderCount, clr);
						borderCount++;
					}
				}
				// Bottom row above the rectangle
				else if (getY == srcY + srcHeight -1)
				{
					borderCount = 1;
					while (borderCount <= borderWidth)
					{
						dst.SetPixel(setX, setY+borderCount, clr);
						borderCount++;
					}
				}
				// Left column of the rectangle
				if (getX == srcX)
				{
					borderCount = 1;
					while (borderCount <= borderWidth)
					{
						dst.SetPixel(setX-borderCount, setY, clr);
						borderCount++;
					}
				}
				// Right column of the rectangle
				else if (getX == srcX + srcWidth -1)
				{
					borderCount = 1;
					while (borderCount <= borderWidth)
					{
						dst.SetPixel(setX+borderCount, setY, clr);
						borderCount++;
					}
				}

				// Also fill in the pixels in the rectangle
				clr = src.GetPixel(getX, getY);
				dst.SetPixel(setX, setY, clr);

				getX++;
				setX++;
			}

			getX = srcX;
			setX = dstX;
			getY++;
			setY++;
		}

	}

	// Draws a rectangle of solid color on a Texture2D
	void FillWithColorTexture2D(Color solidColor, int width, int height, Texture2D dst, int dstX, int dstY)
	{
		if (dst == null)
		{
			Debug.LogError("dst is null");
			return;
		}

		int setX, setY;
		setX = dstX;
		setY = dstY;
		while ( setY < dstY + height )
		{
			while ( setX < dstX + width )
			{
				dst.SetPixel(setX, setY, solidColor);
				setX++;
			}

			setX = dstX;
			setY++;
		}		
	}

	void ChangeColorToAlphaTexture2D(Texture2D texture, Color makeThisColorClear)
	{
		int x, y;
		Color transparent = new Color(0,0,0,0);
		for (x=0; x < texture.width; x++)
		{
			for (y=0; y < texture.height; y++)
			{
				if (CompareColorsIgnoreAlpha(texture.GetPixel(x, y), makeThisColorClear))
				{
					texture.SetPixel(x, y, transparent);
				}
			}
		}
	}

	bool CompareColorsIgnoreAlpha(Color a, Color b)
	{
		return (a.r == b.r && a.g == b.g && a.b == b.b);
	}

	// From https://gamedev.stackexchange.com/questions/132569/how-do-i-find-an-object-by-type-and-name-in-unity-using-c
	public List<UnityEngine.Object> FindObjectsOfTypeAndName<T>(string name) where T : MonoBehaviour
	{
		MonoBehaviour[] firstList = GameObject.FindObjectsOfType<T>();
		List<UnityEngine.Object> finalList = new List<UnityEngine.Object>();

		for(var i = 0; i < firstList.Length; i++)
		{
			if(firstList[i].name == name)
			{
				finalList.Add(firstList[i]);
			}
		}

		return finalList;
	}

} // end of FormatBspMB.cs class
