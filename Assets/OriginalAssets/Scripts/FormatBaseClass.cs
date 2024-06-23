// FormatBaseClass.cs
// Meant to store as much of the file-system specific code as possible
// Then be extended by a class file for each specific file format we're supporting

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
#if UNITY_EDITOR || UNITY_5 || UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

public class FormatBaseClass
{
	// For where we search for the files we want to load
	public static string baseContentDirectory = "id1";

	// The raw contents of the file
	protected byte[] fileContent;
	protected bool isLoaded = false;
	protected bool isParsed = false;

	// Used to read and parse the raw content
	protected MemoryStream memStream;
	protected BinaryReader reader;

	// This object can be aware of one .pak file at a time for now
	public FormatPak pak;

	// Load the file into memory
	public bool Load(string pathToFile)
	{
		TimerHelper timer = new TimerHelper(this.GetType() + ".Load(\"" + pathToFile + "\")");

		// See if the flat file exists on drive
		if (File.Exists(baseContentDirectory + "/" + pathToFile))
		{
			pathToFile = baseContentDirectory + "/" + pathToFile;
			// Debug.Log("Found flat file outside of .pak file: " + pathToFile);
		}
		else if (!File.Exists(pathToFile))
		{
			// If the file doesn't exist on drive, check in the .pak file
			// COULDDO: give this code the ability to check multiple .pak files? // P4
			// COULDDO: give this code the ability to check multiple game directories, not just default "id1" // P4
			if (pak != null && pak.CheckIfLoaded())
			{
				byte[] fileInPak = pak.GetFile(pathToFile);
				if (fileInPak != null && Load(fileInPak))
				{
					return true;
				}
			}
			// File doesn't exist on drive or in pak file
			Debug.LogError("File does not exist: " + pathToFile);
			return false;
		}

		// https://stackoverflow.com/questions/6227373/how-to-open-a-file-in-memory#6227412
		fileContent = File.ReadAllBytes(pathToFile);

		// A totally empty file
		if (fileContent.Length <= 0)
		{
			Debug.LogError("File is empty: " + pathToFile);
			return false;
		}

		Debug.Log("File loaded: " + pathToFile);
		isLoaded = true;

		timer.Stop();

		return true;
	}

	// Load the file into memory from elsewhere (aka hydrate)
	public bool Load(byte[] useTheseBytes)
	{
		TimerHelper timer = new TimerHelper(this.GetType() + ".Load( ... from bytes ... )");

		fileContent = useTheseBytes;

		// A totally empty file
		if (fileContent.Length <= 0)
		{
			Debug.LogError("File is empty!");
			return false;
		}

		Debug.Log("File loaded");
		isLoaded = true;

		timer.Stop();

		return true;
	}

	public bool Unload()
	{
		fileContent = null;
		isLoaded = false;
		isParsed = false;
		memStream = null;
		reader = null;
		pak = null;
		return true;
	}

	public void SetPak(FormatPak newPak)
	{
		pak = newPak;
	}

	public bool CheckIfLoaded()
	{
		return isLoaded;
	}

	public bool CheckIfParsed()
	{
		return isParsed;
	}


	protected byte ReadByte()
	{
		return reader.ReadByte();
	}

	protected ushort ReadUShort()
	{
		return reader.ReadUInt16();
	}

	protected short ReadShort()
	{
		return reader.ReadInt16();
	}

	protected uint ReadUInt()
	{
		return reader.ReadUInt32();
	}

	protected int ReadInt()
	{
		return reader.ReadInt32();
	}

	protected float ReadFloat()
	{
		// COULDDO: not sure how big float actually was in Quake 1 code
		return BitConverter.ToSingle(reader.ReadBytes(4), 0);
	}

	protected string ReadString(int characterLength)
	{
		return new ASCIIEncoding().GetString(reader.ReadBytes(characterLength)).Trim();
	}

} // end of FormatBaseClass.cs

#if !UNITY_EDITOR && !UNITY_5 && !UNITY_5_3_OR_NEWER
class Debug
{
	static bool Log(string msg)
	{

	}

	static bool LogWarning(string msg)
	{
		
	}

	static bool LogError(string msg)
	{
		
	}

}
#endif
