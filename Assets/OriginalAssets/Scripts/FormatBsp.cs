// FormatBsp.cs
// Handles the loading and parsing of .bsp files in an engine-independent way

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
#if UNITY_EDITOR || UNITY_5 || UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

// Helpful
// http://www.gamers.org/dEngine/quake/spec/quake-spec34/qkspec_4.htm
// https://www.convertdatatypes.com/Convert-byte-to-float-in-CSharp.html

public class FormatBsp : FormatBaseClass
{
	// Header we use to navigate BSP
	private dheader_t dheader;

	// Public data structures
	public dvertex_t[] dvertexes;
	public dedge_t[] dedges;
	public int[] surfedges;
	public dface_t[] dfaces;
	public dmodel_t[] dmodels;
	public entity_t[] entities;
	public miptex_t[] miptextures;
	public texinfo_t[] texinfos;
	public byte[][] lightmaps;
	public int[] lightmapOffsetToIndexLookup;
	public int[] lightmapIndexToOffsetLookup;
	public int[] lightmapOffsetToFaceLookup;

	// Public counts
	public int num_texinfos;
	public int num_models;
	public int num_lightmaps;

	public new bool Unload()
	{
		base.Unload();
		dvertexes = null;
		dedges = null;
		surfedges = null;
		dfaces = null;
		dmodels = null;
		entities = null;
		miptextures = null;
		texinfos = null;
		lightmaps = null;
		lightmapOffsetToIndexLookup = null;
		lightmapIndexToOffsetLookup = null;
		lightmapOffsetToFaceLookup = null;
		num_texinfos = 0;
		num_models = 0;
		num_lightmaps = 0;
		return true;
	}

	// Do the parsing of the format
	public bool Parse()
	{
		TimerHelper timer = new TimerHelper(this.GetType() + " FormatBsp.Parse()");

		if (!isLoaded)
		{
			return false;
		}

		Debug.Log("LUMP_LABLES[LUMP_ENTITIES] = " + LUMP_LABLES[LUMP_ENTITIES]);

		// Setup the tools to read the BSP format from memory
		memStream = new MemoryStream(fileContent);
		reader = new BinaryReader(memStream);

		// For iteration below
		int i, j, k;

		#if UNITY_EDITOR
		// Just here to avoid warnings.
		i = 0;
		j = 0;
		k = 0;
		Debug.Log("Debug log message to avoid warnings. " + i + j + k);
		#endif

		// Load the header ///////////////////////////////////////////////////////////////////

		dheader = new dheader_t(); // definition

		// Check the version number of the level
		dheader.version = ReadInt();
		Debug.Log("dheader.version: " + dheader.version);
		if (dheader.version != 29)
		{
			Debug.LogWarning("File format version mismatch. Expected dheader.version to be 29.");
			return false;
		}

		// Define all of the header lumps
		dheader.lumps = new lump_t[HEADER_LUMPS];
		for (i = 0; i < HEADER_LUMPS; i++)
		{
			dheader.lumps[i].fileofs = ReadInt();
			dheader.lumps[i].filelen = ReadInt();

			// Note: the order that the lumps are listed is consistent
			// The order that their data appears in file itself is NOT consistent

			/* Unnecessary logging now that this is working well
			Debug.Log("Lump Loaded: " + LUMP_LABLES[i] );
			Debug.Log("dheader.lumps[" + i + "].fileofs: " + dheader.lumps[i].fileofs);
			Debug.Log("dheader.lumps[" + i + "].filelen: " + dheader.lumps[i].filelen);
			*/
		}

		// Load LUMP_ENTITIES ///////////////////////////////////////////////////////////////////

		Debug.Log("Load LUMP_ENTITIES");

		// memStream.Seek(dheader.lumps[LUMP_ENTITIES].fileofs, 0);

		i = 0;
		int num_entities;
		entities = new entity_t[MAX_MAP_ENTITIES];

		byte[] entitiesMem = new byte[dheader.lumps[LUMP_ENTITIES].filelen];
		Buffer.BlockCopy(fileContent, dheader.lumps[LUMP_ENTITIES].fileofs, entitiesMem, 0, dheader.lumps[LUMP_ENTITIES].filelen);

		// This GetEncoding() required that we add two DLLs to the project: I18N.dll and I18N.West.dll
		// See details here: https://answers.unity.com/questions/42955/codepage-1252-not-supported-works-in-editor-but-no.html
		string entitiesString = Encoding.GetEncoding(437).GetString(entitiesMem);
		// Debug.Log("Giant entities string: " + entitiesString);
		string singleLine;
		epair_t currentKeyValuePair;
		epair_t previousKeyValuePair = null; // new epair_t(); // had to assign once
		string[] splitLine;
		using (StringReader stringReader = new StringReader(entitiesString))
		{
			while ((singleLine = stringReader.ReadLine()) != null)
			{
				// Debug.Log(singleLine);
				if (singleLine.StartsWith("{"))
				{
					entities[i] = new entity_t();
					entities[i].epairs = new epair_t();
					currentKeyValuePair = null;
					previousKeyValuePair = null;
					i++;
				}
				else if (singleLine.StartsWith("}"))
				{
					// nothing to do
				}
				else
				{
					splitLine = singleLine.Split('\"');
					if (splitLine[0] == "" && splitLine[2] == " " && splitLine[4] == "")
					{
						currentKeyValuePair = new epair_t();
						currentKeyValuePair.key = splitLine[1];
						currentKeyValuePair.value = splitLine[3];
						currentKeyValuePair.next = null;

						if (previousKeyValuePair == null)
						{
							// we're at the beginning of linked list
							entities[i - 1].epairs = currentKeyValuePair;
						}
						else
						{
							// tell the previous pair about us
							previousKeyValuePair.next = currentKeyValuePair;
						}

						// Now we become the previous, ready for the next one
						previousKeyValuePair = currentKeyValuePair;

						// Debug.Log("key: " + currentKeyValuePair.key + "; value: " + currentKeyValuePair.value);
						if (currentKeyValuePair.key == "origin")
						{
							splitLine = currentKeyValuePair.value.Split(' ');
							entities[i - 1].origin.x = float.Parse(splitLine[0]);
							entities[i - 1].origin.y = float.Parse(splitLine[1]);
							entities[i - 1].origin.z = float.Parse(splitLine[2]);
						}
					}
				}
			}
		}

		num_entities = i;
		Debug.Log("Parsed " + num_entities + " entities.");

		// Load LUMP_PLANES ///////////////////////////////////////////////////////////////////

		memStream.Seek(dheader.lumps[LUMP_PLANES].fileofs, 0);

		int num_planes;
		dplane_t[] dplanes = new dplane_t[MAX_MAP_PLANES];

		i = 0;
		while (memStream.Position < dheader.lumps[LUMP_PLANES].fileofs + dheader.lumps[LUMP_PLANES].filelen)
		{
			dplanes[i] = new dplane_t();
			dplanes[i].normal = new float[3];
			dplanes[i].normal[0] = ReadFloat();
			dplanes[i].normal[1] = ReadFloat();
			dplanes[i].normal[2] = ReadFloat();
			dplanes[i].dist = ReadFloat();
			dplanes[i].type = ReadInt();
			i++;
		}
		num_planes = i;
		Debug.Log("Parsed " + num_planes + " planes.");

		// Load LUMP_TEXTURES ///////////////////////////////////////////////////////////////////

		memStream.Seek(dheader.lumps[LUMP_TEXTURES].fileofs, 0);

		int num_textures;

		dmiptexlump_t miptexlump = new dmiptexlump_t();
		miptexlump.nummiptex = ReadInt();
		miptexlump.dataofs = new int[miptexlump.nummiptex];

		for (i = 0; i < miptexlump.nummiptex; i++)
		{
			miptexlump.dataofs[i] = ReadInt();
		}

		miptextures = new miptex_t[miptexlump.nummiptex];
		int numPixels = 0; // reusable variables
		int startingPointOfTextureData;

		for (i = 0; i < miptexlump.nummiptex; i++)
		{
			// Jump to beginning of texture data
			memStream.Seek(dheader.lumps[LUMP_TEXTURES].fileofs + miptexlump.dataofs[i], 0);

			miptextures[i].name = ReadString(16).Trim();
			miptextures[i].name = miptextures[i].name.Split('\0')[0]; // everything from before null character
			// Debug.Log("miptextures[" + i + "].name: " + miptextures[i].name);
			miptextures[i].width = ReadUInt();
			miptextures[i].height = ReadUInt();

			if (miptextures[i].width <= 0)
			{
				Debug.LogWarning("Texture " + miptextures[i].name + " has width = " + miptextures[i].width + ". Now set to zero.");
				miptextures[i].width = 0;
			}

			if (miptextures[i].height <= 0)
			{
				Debug.LogWarning("Texture " + miptextures[i].name + " has height = " + miptextures[i].height + ". Now set to zero.");
				miptextures[i].height = 0;
			}

			// Debug.Log("miptextures[" + i + "].width = " + miptextures[i].width);
			// Debug.Log("miptextures[" + i + "].height = " + miptextures[i].height);

			miptextures[i].offsets = new uint[4];
			miptextures[i].pixels = new byte[4][];

			for (j = 0; j < 4; j++)
			{
				miptextures[i].offsets[j] = ReadUShort();
				// Debug.Log("miptextures[" + i + "].offsets[" + j + "] = " + miptextures[i].offsets[j]);

				numPixels = (int)(miptextures[i].width * miptextures[i].height / (j + 1));
				// Debug.Log("numPixels: " + numPixels);

				// Read in the bytes for this version of the texture mip
				startingPointOfTextureData = (int)(dheader.lumps[LUMP_TEXTURES].fileofs + miptexlump.dataofs[i] + miptextures[i].offsets[j]);
				// Debug.Log("startingPointOfTextureData: " + startingPointOfTextureData);
				if (startingPointOfTextureData > fileContent.Length)
				{
					// COULDDO: investigate why we're getting this warning a bunch when loading the BSP.
					// Debug.LogWarning("Trying to start reading texture data that starts beyond the end of the .bsp file.");
				}
				else if (startingPointOfTextureData + numPixels > fileContent.Length)
				{
					// COULDDO: investigate why we're getting this warning a bunch when loading the BSP.
					// Debug.LogWarning("Trying to read texture data that ends beyond the end of the .bsp file.");
				}
				else
				{
					// Actually read the texture data
					memStream.Seek(startingPointOfTextureData, 0);
					if (numPixels <= 0)
					{
						// COULDDO: this warning is getting hit on e1m2.bsp, likely a sign of other problems
						Debug.LogWarning("numPixels = " + numPixels + "; Expected to be greater than zero.");
					}
					else
					{
						miptextures[i].pixels[j] = reader.ReadBytes(numPixels);
					}
				}
			}
		}

		num_textures = i;
		Debug.Log("Parsed " + num_textures + " miptex.");

		// Load LUMP_VERTEXES ///////////////////////////////////////////////////////////////////

		memStream.Seek(dheader.lumps[LUMP_VERTEXES].fileofs, 0);

		int numvertexes; // AKA verticies
		dvertexes = new dvertex_t[MAX_MAP_VERTS];

		i = 0;
		while (memStream.Position < dheader.lumps[LUMP_VERTEXES].fileofs + dheader.lumps[LUMP_VERTEXES].filelen)
		{
			dvertexes[i] = new dvertex_t();
			dvertexes[i].point = new float[3];
			dvertexes[i].point[0] = ReadFloat();
			dvertexes[i].point[1] = ReadFloat();
			dvertexes[i].point[2] = ReadFloat();
			i++;
		}
		numvertexes = i;
		Debug.Log("Parsed " + numvertexes + " vertexes.");

		// We're not using the visibility / PVS data from the Quake BSP levels yet. Probably don't need them for modern rendering.
		// Load LUMP_VISIBILITY ///////////////////////////////////////////////////////////////////
		// Load LUMP_NODES ///////////////////////////////////////////////////////////////////
		// Load LUMP_TEXINFO ///////////////////////////////////////////////////////////////////

		// Believe this data structure is the mapping from faces to textures
		memStream.Seek(dheader.lumps[LUMP_TEXINFO].fileofs, 0);

		texinfos = new texinfo_t[MAX_MAP_TEXINFO];

		i = 0;
		while (memStream.Position < dheader.lumps[LUMP_TEXINFO].fileofs + dheader.lumps[LUMP_TEXINFO].filelen)
		{
			texinfos[i] = new texinfo_t();
			texinfos[i].vecs = new float[2][]; // magic number
			for (j = 0; j < 2; j++)
			{
				texinfos[i].vecs[j] = new float[4]; // magic number
				for (k = 0; k < 4; k++)
				{
					texinfos[i].vecs[j][k] = ReadFloat();
				}
			}
			texinfos[i].miptex = ReadInt();
			texinfos[i].flags = ReadInt();
			i++;
		}
		num_texinfos = i;
		Debug.Log("Parsed " + num_texinfos + " tex[ture]infos.");

		// Load LUMP_FACES ///////////////////////////////////////////////////////////////////

		memStream.Seek(dheader.lumps[LUMP_FACES].fileofs, 0);

		int num_dfaces;
		dfaces = new dface_t[MAX_MAP_FACES];

		// COULDDO these massive arrays should be key value pairs or some memory-efficient sparse-arrays
		num_lightmaps = 0;
		int maxLightmapId = 0;
		int[] lightmapUseCount = new int[1000000];
		lightmapOffsetToIndexLookup = new int[1000000];
		lightmapIndexToOffsetLookup = new int[1000000];
		lightmapOffsetToFaceLookup = new int[1000000];

		i = 0;
		while (memStream.Position < dheader.lumps[LUMP_FACES].fileofs + dheader.lumps[LUMP_FACES].filelen)
		{
			dfaces[i] = new dface_t();
			dfaces[i].planenum = ReadShort();
			dfaces[i].side = ReadShort();
			dfaces[i].firstedge = ReadInt();
			dfaces[i].numedges = ReadShort();
			dfaces[i].texinfo = ReadShort();
			dfaces[i].styles = new byte[MAXLIGHTMAPS];
			for (j = 0; j < MAXLIGHTMAPS; j++)
			{
				dfaces[i].styles[j] = ReadByte();
				// Note to self:
				// styles[0] = type of lighting
				// styles[1] = base lighting
				// styles[2] = light mode?
				// styles[3] = another light mode
			}
			dfaces[i].lightofs = ReadInt();

			if (dfaces[i].styles[0] == 0xFF)
			{
				// do nothing -- we'll handle these cases in FormatBspMP.cs
			}
			else if (dfaces[i].lightofs == -1)
			{
				// do nothing 
			}
			else if (miptextures[ texinfos[ dfaces[i].texinfo ].miptex ].name[0] == '*')
			{
				// do nothing -- sky-ish texture
			}
			else
			{
				if (dfaces[i].lightofs > maxLightmapId)
				{
					maxLightmapId = dfaces[i].lightofs;
				}
				// Note: learned that each lightmap texture is used just once
				lightmapUseCount[dfaces[i].lightofs]++;
				// Be able to look up a face index based on the lightmap offset
				lightmapOffsetToFaceLookup[dfaces[i].lightofs] = i;
				// This is a new lightmap
				num_lightmaps++;
			}
			// Checkout out how many edges the typical face had
			// Debug.Log("face with " + dfaces[i].numedges + " edges.");
			i++;
		}

		num_dfaces = i;
		Debug.Log("Parsed " + num_dfaces + " dfaces.");

		// Order the offsets of all the discovered lightmaps in escalating order
		j = 0;
		// COULDDO: this array lightmapUseCount has lots of whitespace, can walk this list more efficiently
		for (i = 0; i <= maxLightmapId; i++)
		{
			if (lightmapUseCount[i] != 0)
			{
				// Store the lightmaps in escalating order so we can determine their sizes
				// Store a lookup value, so we can walk from face -> lightmapOffsetLookup -> lightmap[lightmapOffsetToIndexLookup[n]]
				// COULDDO: this data structure can be a key value pair
				lightmapIndexToOffsetLookup[j] = i;
				lightmapOffsetToIndexLookup[i] = j;
				j++;
			}
		}

		Debug.Log("Lightmap Count: " + num_lightmaps + "; Max Lightmap ID: " + maxLightmapId);

		// Load LUMP_LIGHTING ///////////////////////////////////////////////////////////////////

		// Just allocated the array because we can't LOAD the lightmap data until we process the sizes/dimenstions of the faces
		// See ParseLightmap(index) below
		lightmaps = new byte[num_lightmaps][];

		// We're not using the visibility / PVS data from the Quake BSP levels yet. Probably don't need them for modern rendering.
		// Load LUMP_CLIPNODES ///////////////////////////////////////////////////////////////////
		// Load LUMP_LEAFS ///////////////////////////////////////////////////////////////////
		// Load LUMP_MARKSURFACES ///////////////////////////////////////////////////////////////////

		// Load LUMP_EDGES ///////////////////////////////////////////////////////////////////

		memStream.Seek(dheader.lumps[LUMP_EDGES].fileofs, 0);

		int num_dedges;
		// dedge_t[] dedges = new dedge_t[MAX_MAP_EDGES];
		dedges = new dedge_t[MAX_MAP_EDGES];

		i = 0; // edge zero is never used (noted below?)
		while (memStream.Position < dheader.lumps[LUMP_EDGES].fileofs + dheader.lumps[LUMP_EDGES].filelen)
		{
			dedges[i] = new dedge_t();
			dedges[i].v = new ushort[2];
			dedges[i].v[0] = ReadUShort();
			dedges[i].v[1] = ReadUShort();
			i++;
		}
		num_dedges = i;
		Debug.Log("Parsed " + num_dedges + " dedges.");

		// Load LUMP_SURFEDGES ///////////////////////////////////////////////////////////////////

		memStream.Seek(dheader.lumps[LUMP_SURFEDGES].fileofs, 0);

		int num_surfedges;
		surfedges = new int[MAX_MAP_SURFEDGES];

		i = 0; // edge zero is never used (noted below?)
		while (memStream.Position < dheader.lumps[LUMP_SURFEDGES].fileofs + dheader.lumps[LUMP_SURFEDGES].filelen)
		{
			surfedges[i] = ReadInt();
			i++;
		}
		num_surfedges = i;
		Debug.Log("Parsed " + num_surfedges + " surfedges.");

		// Load LUMP_MODELS ///////////////////////////////////////////////////////////////////

		memStream.Seek(dheader.lumps[LUMP_MODELS].fileofs, 0);

		dmodels = new dmodel_t[MAX_MAP_MODELS];

		// for (i=0; i < MAX_MAP_MODELS; i++)
		i = 0;
		while (memStream.Position < dheader.lumps[LUMP_MODELS].fileofs + dheader.lumps[LUMP_MODELS].filelen)
		{
			dmodels[i] = new dmodel_t();
			dmodels[i].mins = new float[3];
			dmodels[i].maxs = new float[3];
			dmodels[i].origin = new float[3];
			dmodels[i].mins[0] = ReadFloat();
			dmodels[i].mins[1] = ReadFloat();
			dmodels[i].mins[2] = ReadFloat();
			dmodels[i].maxs[0] = ReadFloat();
			dmodels[i].maxs[1] = ReadFloat();
			dmodels[i].maxs[2] = ReadFloat();
			dmodels[i].origin[0] = ReadFloat();
			dmodels[i].origin[1] = ReadFloat();
			dmodels[i].origin[2] = ReadFloat();
			dmodels[i].headnode = new int[MAX_MAP_HULLS];
			for (j = 0; j < MAX_MAP_HULLS; j++)
			{
				dmodels[i].headnode[j] = ReadInt();
			}
			dmodels[i].visleafs = ReadInt();
			dmodels[i].firstface = ReadInt();
			dmodels[i].numfaces = ReadInt();
			i++;
		}
		num_models = i;
		Debug.Log("Parsed " + num_models + " dmodels.");

		Debug.Log("FormatBsp.Parse() Completed");
		isParsed = true;

		timer.Stop();

		return true;
	} // end Parse();

	private int start; // used for loading lightmaps
	public bool ParseLightmap(int index, int size)
	{
		// PARSE A SPECIFIC LIGHTMAP NOW THAT WE KNOW ITS NUMBER OF PIXELS
		if (!isParsed || lightmaps == null)
		{
			Debug.LogError("Trying to load lightmap data but lightmaps array isn't initialized.");
		}

		start = dheader.lumps[LUMP_LIGHTING].fileofs + lightmapIndexToOffsetLookup[index];
		memStream.Seek(start, 0);
		lightmaps[index] = reader.ReadBytes(size);

		// COULDDO: Do something to test if the loading worked?
		return true;
	}

	// Returns the first entity found with a specific key/name
	public entity_t GetEntityWithKey(string key)
	{
		if (!isParsed)
		{
			Debug.LogWarning("Cannot get entity -- BSP isn't parsed yet.");
			return null;
		}

		if (entities == null)
		{
			return null;
		}

		int i;
		for (i = 0; i < entities.Length; i++)
		{
			if (entities[i] != null)
			{
				epair_t pair = entities[i].epairs;
				while (pair != null)
				{
					if (pair.key == key)
					{
						return entities[i];
					}
					pair = pair.next;
				}
			}
			else
			{
				// Shouldn't have to be in else{} but it's throwing warnings otherwise
				break;
			}
		}
		return null;
	}

	// Returns an array of all entities found with a specific key/name
	public entity_t[] GetEntitiesWithKey(string key)
	{
		if (!isParsed)
		{
			Debug.LogWarning("Cannot get entity -- BSP isn't parsed yet.");
			return null;
		}

		if (entities == null)
		{
			return null;
		}

		entity_t[] foundEntities = new entity_t[MAX_MAP_ENTITIES];
		int foundEntityCount = 0;
		int i;
		for (i = 0; i < entities.Length; i++)
		{
			if (entities[i] != null)
			{
				epair_t pair = entities[i].epairs;
				while (pair != null)
				{
					if (pair.key == key)
					{
						foundEntities[foundEntityCount] = entities[i];
						foundEntityCount++;
						pair = pair.next;
						continue;
					}
					pair = pair.next;
				}
				continue;
			}
			break;
		}
		Array.Resize(ref foundEntities, foundEntityCount);
		return foundEntities;
	}

	// Returns an array of all entities found with a specific key and value
	public entity_t[] GetEntitiesWithKeyValue(string key, string value)
	{
		if (!isParsed)
		{
			// COULDDO: some error/explanation of why it's null
			return null;
		}

		entity_t[] foundEntities = new entity_t[MAX_MAP_ENTITIES];
		int foundEntityCount = 0;
		int i = 0;
		for (i = 0; i < entities.Length; i++)
		{
			if (entities[i] != null)
			{
				epair_t pair = entities[i].epairs;
				while (pair != null)
				{
					if (pair.key == key && pair.value == value)
					{
						var foundEntity = entities[i];
						// Debug.Log("foundEntityCount: " + foundEntityCount);
						foundEntities[foundEntityCount] = foundEntity;
						foundEntityCount++;
						pair = pair.next;
						continue;
					}
					if (pair == pair.next)
					{
						Debug.LogWarning("Infinite loop in epair_t.next");
						break;
					}
					pair = pair.next;
				}
				continue;
			}
			break;
		}
		Array.Resize(ref foundEntities, foundEntityCount);
		Debug.Log("foundEntityCount: " + foundEntityCount);
		return foundEntities;
	}

	// Get a C# list of all the key value pairs for a single entity
	public List<KeyValuePair<string, string>> GetKeyValuePairsFromEntity(entity_t entity)
	{
		List<KeyValuePair<string, string>> keyValuePairs = new List<KeyValuePair<string, string>>();

		if (entity != null)
		{
			// Walk the linked list of entity epairs
			epair_t pair = entity.epairs;
			while (pair != null && entity.epairs != null)
			{
				keyValuePairs.Add( new KeyValuePair<string, string>(pair.key, pair.value) ); // actually adds it to the list
				if (pair.next == pair)
				{
					Debug.LogWarning("Infinite loop in epair_t.next");
					break;
				}
				pair = pair.next;
			}
		}

		return keyValuePairs;
	}

	// Returns the value from a specific entity with this key
	public string GetEntityValueFromKey(entity_t entity, string key)
	{
		if (!isParsed)
		{
			Debug.LogWarning("Cannot get entity -- BSP isn't parsed yet.");
			return null;
		}

		if (entity == null)
		{
			return null;
		}
		epair_t pair = entity.epairs;
		while (pair != null)
		{
			if (pair.key == key)
			{
				return pair.value;
			}
			pair = pair.next;
		}
		return null;
	}

	// upper design bounds

	const int MAX_MAP_HULLS = 4;

	const int MAX_MAP_MODELS = 256;
	const int MAX_MAP_BRUSHES = 4096;
	const int MAX_MAP_ENTITIES = 1024;
	const int MAX_MAP_ENTSTRING = 65536;

	const int MAX_MAP_PLANES = 32767;
	const int MAX_MAP_NODES = 32767;
	const int MAX_MAP_CLIPNODES = 32767;
	const int MAX_MAP_LEAFS = 8192;
	const int MAX_MAP_VERTS = 65535;
	public const int MAX_MAP_FACES = 65535; // PMC: made this public
	const int MAX_MAP_MARKSURFACES = 65535;
	const int MAX_MAP_TEXINFO = 4096;
	const int MAX_MAP_EDGES = 256000;
	const int MAX_MAP_SURFEDGES = 512000;

	struct lump_t
	{
		public int fileofs, filelen;
	}

	// COULDDO: turn this into ENUM?
	const int LUMP_ENTITIES = 0;
	const int LUMP_PLANES = 1;
	const int LUMP_TEXTURES = 2;
	const int LUMP_VERTEXES = 3;
	const int LUMP_VISIBILITY = 4;
	const int LUMP_NODES = 5;
	const int LUMP_TEXINFO = 6;
	const int LUMP_FACES = 7;
	const int LUMP_LIGHTING = 8;
	const int LUMP_CLIPNODES = 9;
	const int LUMP_LEAFS = 10;
	const int LUMP_MARKSURFACES = 11;
	const int LUMP_EDGES = 12;
	const int LUMP_SURFEDGES = 13;
	const int LUMP_MODELS = 14;

	// PMC new structure to be able to debug labels
	string[] LUMP_LABLES = {
	"LUMP_ENTITIES",
	"LUMP_PLANES",
	"LUMP_TEXTURES",
	"LUMP_VERTEXES",
	"LUMP_VISIBILITY",
	"LUMP_NODES",
	"LUMP_TEXINFO",
	"LUMP_FACES",
	"LUMP_LIGHTING",
	"LUMP_CLIPNODES",
	"LUMP_LEAFS",
	"LUMP_MARKSURFACES",
	"LUMP_EDGES",
	"LUMP_SURFEDGES",
	"LUMP_MODELS"
	};

	const int HEADER_LUMPS = 15;

	public struct dmodel_t
	{
		public float[] mins, maxs;
		public float[] origin;
		public int[] headnode;
		public int visleafs;
		public int firstface, numfaces;
	}

	struct dheader_t
	{
		public int version;
		public lump_t[] lumps;
	}

	public struct dmiptexlump_t
	{
		public int nummiptex;
		public int[] dataofs;
	}

	public struct miptex_t
	{
		public string name;
		public uint width, height;
		public uint[] offsets;
		public byte[][] pixels;
	}

	public struct dvertex_t
	{
		public float[] point;
	}

	struct dplane_t
	{
		public float[] normal;
		public float dist;
		public int type;
	}

	public struct texinfo_t
	{
		public float[][] vecs;
		public int miptex;
		public int flags;
	}

	// note that edge 0 is never used, because negative edge nums are used for
	// counterclockwise use of the edge in a face
	public struct dedge_t
	{
		public ushort[] v;
	}

	const int MAXLIGHTMAPS = 4;

	public struct dface_t
	{
		public short planenum;
		public short side;

		public int firstedge;
		public short numedges;
		public short texinfo;

		// lighting info
		public byte[] styles;
		public int lightofs;
	}

	public struct vec3_t
	{
		public float x;
		public float y;
		public float z;
	}

	public class entity_t
	{
		public vec3_t origin;
		public int firstbrush;
		public int numbrushes;
		public epair_t epairs;
	}

	public class epair_t
	{
		public epair_t next;
		public string key;
		public string value;
	}

} // end of FormatBsp.cs class
