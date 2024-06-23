// FormatLmpPalette.cs
// Handles the loading and parsing of palette.lmp, the Quake color palette of index colors

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
// https://quakewiki.org/wiki/Quake_palette#palette.lmp
// "The palette is stored in gfx/palette.lmp. It consists of 256 RGB values using one byte per component, coming out to 768 bytes in total. "

public class FormatLmpPalette : FormatBaseClass
{
	public struct QuakeColor
	{
		public byte red, green, blue;
	}

	// Array of 256 colors, each an array of values, red, blue, green
	public const int NUM_COLORS = 256;
	public QuakeColor[] colors;

	new public bool Unload()
	{
		base.Unload();
		colors = null;
		return true;
	}

	const int LENGTH_OF_QUAKE1_PALETTE_FILE = 768; // COULDDO: this is a magic number, but feels necessary to help with load/unload/verify/etc.

	// Do the parsing of the format
	public bool Parse()
	{
		TimerHelper timer = new TimerHelper(this.GetType() + ".Parse()");

		if (!isLoaded)
		{
			return false;
		}

		if (fileContent.Length != LENGTH_OF_QUAKE1_PALETTE_FILE)
		{
			Debug.LogError("Palette data is not expected number of bytes: 768");
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

		// Load the colors! ///////////////////////////////////////////////////////////////////

		colors = new QuakeColor[NUM_COLORS];
		for (i = 0; i < NUM_COLORS; i++)
		{
			colors[i].red = ReadByte();
			colors[i].green = ReadByte();
			colors[i].blue = ReadByte();
		}

		isParsed = true;

		timer.Stop();
		return isParsed;

	} // end Parse();

} // end of FormatLmpPalette.cs class
