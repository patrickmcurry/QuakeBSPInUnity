// FormatPak.cs
// Handles the loading and parsing of .pak files, Quake's concept for uncompressed .zip archives
// Helpful: https://quakewiki.org/wiki/.pak

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
#if UNITY_EDITOR || UNITY_5 || UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

public class FormatPak : FormatBaseClass
{
	const int NAME_LENGTH_MAGIC_NUMER = 56; // WARNING: magic number warning
	private pak_file_s[] files;
	private int num_files;
	private int[] bspFileIndexArray;
	private int bspFilesCount = 0;

	pak_header_s pak_header;

    struct pak_header_s
    {
        public string id;
        public int offset;
        public int size;
    }

    struct pak_file_s
    {
        public string name;
        public int offset;
        public int size;
    }

	new public bool Unload()
	{
		base.Unload();
		files = null;
		pak_header = new pak_header_s();
		return true;
	}

	// Do the parsing of the format
	public bool Parse()
	{
		// GET READY ///////////////////////////////////////////////////////////////////

		TimerHelper timer = new TimerHelper(this.GetType() + ".Parse()");

		if (!isLoaded)
		{
			return false;
		}

		// Setup the tools to read the file format from memory
		memStream = new MemoryStream(fileContent);
		reader = new BinaryReader(memStream);
		memStream.Seek(0, 0);

		// For iteration below
		int i;

		// Just here to avoid compiler warnings.
		i = 0;
		Debug.Log("Debug log message to avoid warnings. " + i);

		// DO THE PARSING ///////////////////////////////////////////////////////////////////

		Debug.Log("Pak file is of size " + fileContent.Length + " bytes.");

		// Very start of the file
		memStream.Seek(0, 0);
        pak_header = new pak_header_s();
        pak_header.id = ReadString(4);
        pak_header.offset = ReadInt();
        pak_header.size = ReadInt();

        // Make sure the first four bytes of the file spell out "PACK"
		// Debug.Log(".pak file begins with " + pak_header.id);
        if (pak_header.id == "PACK")
        {
            // Going good so far
        }
        else
        {
            // COULDDO: better error handling
			Debug.LogError("Beginning of .pak file != PACK");
            return false;
        }

        num_files = pak_header.size / 64; // WARNING: magic number warning
		Debug.Log(".pak file contains " + pak_header.size + " bytes of file data");
		Debug.Log(".pak file contains " + num_files + " files.");
		files = new pak_file_s[num_files];
		bspFileIndexArray = new int[num_files];
		bspFilesCount = 0;

		memStream.Seek(pak_header.offset, 0);

        for (i = 0; i < num_files; i++)
        {
			files[i] = new pak_file_s();
			files[i].name = ReadString(NAME_LENGTH_MAGIC_NUMER).Trim();
			files[i].offset = ReadInt();
			files[i].size = ReadInt();
			// Debug.Log("file #" + i + " = " + files[i].name);

			// Make additional list of BSP map files
			if (files[i].name.Contains(".bsp") && !files[i].name.Contains("/b_"))
			{
				bspFileIndexArray[bspFilesCount] = i;
				bspFilesCount++;
			}
		}

        // We think we're done ///////////////////////////////////////////////////////////////////

		isParsed = true;

		timer.Stop();

		return isParsed;

	} // end Parse();

	int j;
	public byte[] GetFile(string filename)
	{
		for (j = 0; j < num_files; j++)
		{
			if (files[j].name.Contains(filename))
			{
				Debug.Log("Found file in pak: " + files[j].name);
				return GetFile(j);

			}
		}
		Debug.LogWarning("No file found in pak matching '" + filename + "'");
		return null;
	} // end LoadFile(string)

	public byte[] GetFile(int index)
	{
		byte[] fileData = new byte[files[index].size];
		Buffer.BlockCopy(fileContent, files[index].offset, fileData, 0, files[index].size);
		return fileData;
	} // end LoadFile(int)

	public string[] GetListOfBspFiles()
	{
		string[] returnArray = new string[bspFilesCount];
		for (int i=0; i < bspFilesCount; i++)
		{
			// Debug.Log(files[bspFileIndexArray[i]].name);
			returnArray[i] = files[bspFileIndexArray[i]].name;
		}
		return returnArray;
	}

} // end of FormatPak.cs class
