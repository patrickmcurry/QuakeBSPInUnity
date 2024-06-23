// ObjExporter.cs
// From: https://web.archive.org/web/20210829201518/https://wiki.unity3d.com/index.php/ObjExporter
// Updated by Unity 5.6 editor to then-relevant APIs
// PMC also did a good amount of hacking in this file
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

struct ObjMaterial
{
	public string name;
	public string textureName;
}
 
public class ObjExporter 
{
 	private static int vertexOffset = 0;
	private static int normalOffset = 0;
	private static int uvOffset = 0;
 
 
    public static string MeshToString(MeshFilter mf) {
        Mesh m = mf.mesh;
        Material[] mats = mf.GetComponent<Renderer>().sharedMaterials;
 
        StringBuilder sb = new StringBuilder();
 
        sb.Append("g ").Append(mf.name).Append("\n");
        foreach(Vector3 v in m.vertices) {
            sb.Append(string.Format("v {0} {1} {2}\n",v.x,v.y,v.z));
        }
        sb.Append("\n");
        foreach(Vector3 v in m.normals) {
            sb.Append(string.Format("vn {0} {1} {2}\n",v.x,v.y,v.z));
        }
        sb.Append("\n");
        foreach(Vector3 v in m.uv) {
            sb.Append(string.Format("vt {0} {1}\n",v.x,v.y));
        }
        for (int material=0; material < m.subMeshCount; material ++) {
            // PMC
			if (material >= mats.Length)
			{
				Debug.LogWarning("Whoops trying to access a material that doesn't exist!");
				continue;
			}
			// End PMC

            sb.Append("\n");
            sb.Append("usemtl ").Append(mats[material].name).Append("\n");
            sb.Append("usemap ").Append(mats[material].name).Append("\n");
 
            int[] triangles = m.GetTriangles(material);
            for (int i=0;i<triangles.Length;i+=3) {
                sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", 
                    triangles[i]+1, triangles[i+1]+1, triangles[i+2]+1));
            }
        }
        return sb.ToString();
    }
 
 	private static string MeshToString(MeshFilter mf, Dictionary<string, ObjMaterial> materialList) 
	{
		Mesh m = mf.sharedMesh;
		Material[] mats = mf.GetComponent<Renderer>().sharedMaterials;
 
		StringBuilder sb = new StringBuilder();
 
		sb.Append("g ").Append(mf.name).Append("\n");
		foreach(Vector3 lv in m.vertices) 
		{
			Vector3 wv = mf.transform.TransformPoint(lv);
 
			//This is sort of ugly - inverting x-component since we're in
			//a different coordinate system than "everyone" is "used to".
			sb.Append(string.Format("v {0} {1} {2}\n",-wv.x,wv.y,wv.z));
		}
		sb.Append("\n");
 
		foreach(Vector3 lv in m.normals) 
		{
			Vector3 wv = mf.transform.TransformDirection(lv);
 
			sb.Append(string.Format("vn {0} {1} {2}\n",-wv.x,wv.y,wv.z));
		}
		sb.Append("\n");
 
		foreach(Vector3 v in m.uv) 
		{
			sb.Append(string.Format("vt {0} {1}\n",v.x,v.y));
		}
 
		for (int material=0; material < m.subMeshCount; material ++) {
			// PMC
			if (material >= mats.Length)
			{
				// Debug.LogWarning("Whoops trying to access a material that doesn't exist!");
				continue;
			}
			// End PMC
			sb.Append("\n");
			sb.Append("usemtl ").Append(mats[material].name).Append("\n");
			sb.Append("usemap ").Append(mats[material].name).Append("\n");
 
			//See if this material is already in the materiallist.
			try
			{
				ObjMaterial objMaterial = new ObjMaterial();
 
				objMaterial.name = mats[material].name;

				/*** PMC ***
				if (mats[material].mainTexture)
					objMaterial.textureName = AssetDatabase.GetAssetPath(mats[material].mainTexture);
				else 
				*** End PMC ***/
					objMaterial.textureName = objMaterial.name; // also modded by PMC

				materialList.Add(objMaterial.name, objMaterial);
			}
			catch (ArgumentException)
			{
				//Already in the dictionary
			}
 
 
			int[] triangles = m.GetTriangles(material);
			for (int i=0;i<triangles.Length;i+=3) 
			{
				//Because we inverted the x-component, we also needed to alter the triangle winding.
				sb.Append(string.Format("f {1}/{1}/{1} {0}/{0}/{0} {2}/{2}/{2}\n", 
					triangles[i]+1 + vertexOffset, triangles[i+1]+1 + normalOffset, triangles[i+2]+1 + uvOffset));
			}
		}
 
		vertexOffset += m.vertices.Length;
		normalOffset += m.normals.Length;
		uvOffset += m.uv.Length;
 
		return sb.ToString();
	}

    public static void MeshToFile(MeshFilter mf, string filename) {
        using (StreamWriter sw = new StreamWriter(filename)) 
        {
            sw.Write(MeshToString(mf));
        }
    }

	public static void MeshesToFile(MeshFilter[] mf, string folder, string filename) 
	{
		Dictionary<string, ObjMaterial> materialList = PrepareFileWrite();
 
		using (StreamWriter sw = new StreamWriter(folder + "/" + filename + ".obj")) 
		{
			sw.Write("mtllib ./" + filename + ".mtl\n");
 
			for (int i = 0; i < mf.Length; i++)
			{
				sw.Write(MeshToString(mf[i], materialList));
			}
		}
 
		MaterialsToFile(materialList, folder, filename);
	}

	private static void Clear()
	{
		vertexOffset = 0;
		normalOffset = 0;
		uvOffset = 0;
	}
 
	private static Dictionary<string, ObjMaterial> PrepareFileWrite()
	{
		Clear();
 
		return new Dictionary<string, ObjMaterial>();
	}

	private static void MaterialsToFile(Dictionary<string, ObjMaterial> materialList, string folder, string filename)
	{
		using (StreamWriter sw = new StreamWriter(folder + "/" + filename + ".mtl")) 
		{
			foreach( KeyValuePair<string, ObjMaterial> kvp in materialList )
			{
				sw.Write("\n");
				sw.Write("newmtl {0}\n", kvp.Key);
				sw.Write("Ka  0.6 0.6 0.6\n");
				sw.Write("Kd  0.6 0.6 0.6\n");
				sw.Write("Ks  0.9 0.9 0.9\n");
				sw.Write("d  1.0\n");
				sw.Write("Ns  0.0\n");
				sw.Write("illum 2\n");
 
                // string destinationFile;
                string relativeFile;

                /*** PMC ***
				if (kvp.Value.textureName != null)
				{
					destinationFile = kvp.Value.textureName;
 
 
					int stripIndex = destinationFile.LastIndexOf(Path.PathSeparator);
 
					if (stripIndex >= 0)
						destinationFile = destinationFile.Substring(stripIndex + 1).Trim();
 
 
					relativeFile = destinationFile;
 
					destinationFile = folder + "/" + destinationFile;
 
					Debug.Log("Copying texture from " + kvp.Value.textureName + " to " + destinationFile);
 
					try
					{
						//Copy the source file
						File.Copy(kvp.Value.textureName, destinationFile);
					}
					catch
					{
 
					}	
                } 
                ***/
                // PMC
                if (false)
                {
                    // do nothing
                }
                // More PMC
                else
                {
                    relativeFile = "Textures/" + kvp.Key.Replace("*","").Replace("+","") + ".png";
                }
                sw.Write("map_Kd {0}", relativeFile);
				// } End PMC
 
				sw.Write("\n\n\n");
			}
		}
	}

}
