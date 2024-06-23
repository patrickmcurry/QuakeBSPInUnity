// QuakeBSPInUnity.cs
// Big wrapper script used to orchestrate this whole affair
// Handles the downloading, verification, extracting of Quake files
// Then hands things off to the FormatBspMB class

// Downloaded files will be in this directory on Windows:
//    C:\Users\[username]\AppData\LocalLow\patrickmcurry\QuakeBSPInUnity
//                  From Project Settings: company name \ product name

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using Force.Crc32;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class QuakeBSPInUnity : MonoBehaviour {

	public string bspFilePath = "maps/start.bsp"; // Note: only start and episode 1 (e1) maps included in shareware Quake 1
	private string baseContentDirectory = "id1";
	public bool saveFiles = false;
	public FormatPak pak;
	private SimpleZipDownloader downloader;
	private string mirrorFileName = "msdos_Quake106_shareware.zip.mirrors";
	private string mirrorFilePath;
	private string zipFilePath = "msdos_Quake106_shareware.zip";
	private string zipFileContentsPath = "msdos_Quake106_shareware";
	private string pakFilePath = "pak0.pak";
	private byte[] zipFileData;
	private int downloadAttemptCycles = 0;
	private int downloadAttemptCyclesMax = 3;
	private float secondsBetweenSteps = 0.1f;
	private string[] bspMapNames;
	private GameObject mapGameObject;
	// GUI variables
	private string displayGuiStatus = "Intitializing...";
	private int displayGuiMode = 0; // 0 = loading, 1 = visible, 2 = minimized, 3 = invisible


	// Use this for initialization
	void Start()
	{
		mirrorFilePath = Application.persistentDataPath + "/" + mirrorFileName;
		zipFilePath = Application.persistentDataPath + "/" + zipFilePath;
		zipFileContentsPath = Application.persistentDataPath + "/" + zipFileContentsPath;
		baseContentDirectory = zipFileContentsPath + "/" + baseContentDirectory;
		FormatBaseClass.baseContentDirectory = baseContentDirectory; // update our file-system code
		pakFilePath = baseContentDirectory + "/" + pakFilePath; // compounds with above path
		// If loading the .bsp file from inside a .pak file, then this path does not need to be modified
		// bspFilePath = bspFilePath;
		StartCoroutine(Orchestrate());
	}

	// Only used for keyboard input
	void Update()
	{
		// Handle GUI-specific keyboard input
        if (Input.GetKeyUp(KeyCode.Tab))
		{
			if (displayGuiMode != 0)
			{
				displayGuiMode++;
			}
			if (displayGuiMode > 3)
			{
				displayGuiMode = 1;
			}
		}
		if (Input.GetKeyUp(KeyCode.M))
		{
			if (displayGuiMode == 4)
			{
				displayGuiMode = 1;
			}
			else
			{
				SimpleSmoothMouseLook.setCursorLock(false);
				displayGuiMode = 4;
			}
		}
		if (Input.GetKeyUp(KeyCode.R))
		{
			RenderModes.CycleRenderMode();
		}
		if ( ( Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKey(KeyCode.P))
		{
			#if UNITY_EDITOR
				// EditorApplication.ExitPlaymode();
				EditorApplication.isPlaying = false;
			#else
				Application.Quit();
			#endif
		}
	}

	// Runs the program from a birds-eye view
	IEnumerator Orchestrate(bool reload=false)
	{
		if (reload)
		{
			Debug.Log("PHASE: LOAD NEW MAP");
			displayGuiStatus = "Loading new map...";

			// Destroy / delete the existing stuff
			Destroy(mapGameObject);
			// Force garbage collection
			System.GC.Collect();
			yield return new WaitForSeconds(1.0f); // actually waiting a full second here

			// Restart the map loading process since we know the pak has been downloaded at least once
			goto unpackFiles;
		}

		yield return new WaitForSeconds(secondsBetweenSteps);
		goto checkFiles;

		checkFiles:
			Debug.Log("PHASE: CHECK FOR FILES");
			displayGuiStatus = "Checking for local files...";

			// If the .bsp AND the .lmp exists in the file system, skip to loading
			// TODO: check for .bsp and .lmp as flat files on filesystem // P3

			// TODO: be able to support more than one .pak file // P2

			// If the .pak exists in the file system, skip to extraction
			if (File.Exists(pakFilePath))
			{
				Debug.Log("Found existing .pak file : " + pakFilePath);
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto unpackFiles;
			}

			// If the .zip exists in the file system, skip to extraction
			if (File.Exists(zipFilePath))
			{
				Debug.Log("Found existing .zip file : " + zipFilePath);
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto extractFiles;
			}

			yield return new WaitForSeconds(secondsBetweenSteps);
			goto processMirrorIni;

		processMirrorIni:
			Debug.Log("PHASE: PROCESS MIRROR INI FILE");
			displayGuiStatus = "Processing mirror file file...";
			// Parse the list of possible .zip download URLs

			// TODO: this implementation requires that you delete the existing .mirrors file from user directory if you want to edit the .mirrors file that's in the Unity project StreamingAssets directory. // P1
			bool copiedIni = false;
			if (!File.Exists(mirrorFilePath))
			{
				// TODO: I believe you have to use the www class to read this on platforms like Android and WebGL // P4
				byte[] iniBytes = File.ReadAllBytes(Application.streamingAssetsPath + "/" + mirrorFileName);	
				File.WriteAllBytes(mirrorFilePath, iniBytes);
				copiedIni = true;
			}

			INIParser ini = new INIParser();
			ini.Open(mirrorFilePath);

			// Get the number of bytes we expect the file to be
			long iniExpectedBytes = ini.ReadValue("Metadata","Bytes", 0);
			long iniExpectedCrc32 = ini.ReadValue("Metadata","CRC32", 0);
			string iniExpectedMd5 = ini.ReadValue("Metadata","MD5", "");

			// Get all of the direct download links			
			List<string> fileLinkUrls = ini.GetListOfSectionValues("Links");

			// Get all of the other mirror links			
			List<string> mirrorLinkUrls = ini.GetListOfSectionValues("Mirrors");
			bool[] mirrorLinkUrlsChecked = new bool[100]; // TODO: magic number alert

			List<string> discoveredMirrorLinkUrls;
			List<string> allMirrorLinkUrls = new List<string>(mirrorLinkUrls);

			// Check to make sure we were able to load the pertinent information out of the INI
			if (iniExpectedBytes == 0 || fileLinkUrls == null)
			{
				if (copiedIni)
				{
					// INI file is malformed and we just copied it?
					Debug.LogError("Level download mirror INI file at ( " + mirrorFilePath + " ) is malformed. This application may not have permission to read or write that file.");
					yield return new WaitForSeconds(secondsBetweenSteps);
					goto failed;
					// TODO: consider only reading it from streaming assets in this case, since not all platforms will be file-write friendly // P3
				}
				// INI file is malformed
				Debug.LogError("Level download mirror INI file at ( " + mirrorFilePath + " ) is malformed. Please fix it or delete it to reset to defaults.");
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto failed;
			}

			int mirrorAttemptCycles = 0;
			int mirrorAttemptCyclesMax = 3;
			int fileLinkCount;
			int mirrorLinkCount;
			int discoveredMirrorLinkCount;

			yield return new WaitForSeconds(secondsBetweenSteps);
			goto processMirrorLinks;

		processMirrorLinks:
			Debug.Log("PHASE: PROCESS MIRROR LINKS");
			displayGuiStatus = "Checking mirror links...";
			// Where we go out and look for more potential places to download the desired file

			mirrorAttemptCycles++;
			fileLinkCount = fileLinkUrls.Count;
			mirrorLinkCount = mirrorLinkUrls.Count;
			discoveredMirrorLinkUrls = new List<string>();
			discoveredMirrorLinkCount = 0;
			int mirrorIndex = -1;

			// Download each of the other mirror link files, parse them, and see if we discover new links and mirrors inside.
			foreach(string mirror in mirrorLinkUrls)
			{
				mirrorIndex++;
				if (mirrorIndex >= 100 || mirrorLinkUrlsChecked[mirrorIndex])
				{
					continue;
				}
				mirrorLinkUrlsChecked[mirrorIndex] = true;
				if (mirror != null && mirror != "")
				{
					Debug.Log("Attempting to download mirror file from " + mirror);
					if (!mirror.StartsWith("http://") && !mirror.StartsWith("https://"))
					{
						Debug.LogError("Mirror download link doesn't start with http:// or https:// : " + mirror);
						continue;
					}

					WWW www = new WWW(mirror);
					yield return www;
					if (www.isDone)
					{
						string mirrorContents = www.text;
						// Debug.LogWarning(mirror + " : " + mirrorContents);
						if (mirrorContents == null || mirrorContents == "")
						{
							// The downloaded INI contents are a dud
							continue;
						}
						// Slightly magic-number-ish hardcoding here // P4
						if (!mirrorContents.Contains("[Metadata]"))
						{
							// The downloaded INI doesn't look like a mirror file
							continue;
						}

						INIParser mirrorIni = new INIParser();
						mirrorIni.OpenFromString(mirrorContents);

						// Get all of the direct download links			
						List<string> mirrorFileLinkUrls = mirrorIni.GetListOfSectionValues("Links");

						// Get all of the other mirror links			
						List<string> mirrorMirrorLinkUrls = mirrorIni.GetListOfSectionValues("Mirrors");

						// Add newly discovered file links
						// TODO: likely a way to speed this up // P4
						foreach(string newLink in mirrorFileLinkUrls)
						{
							if (!fileLinkUrls.Contains(newLink))
							{
								fileLinkUrls.Add(newLink);
							}
						}

						// Add newly discovered mirror links
						// TODO: likely a way to speed this up // P4
						foreach(string newLink in mirrorMirrorLinkUrls)
						{
							if (!allMirrorLinkUrls.Contains(newLink))
							{
								allMirrorLinkUrls.Add(newLink);
								discoveredMirrorLinkUrls.Add(newLink);
							}
						}

						// Report on findings
						Debug.Log("Found " + (fileLinkUrls.Count-fileLinkCount) + " new file links"); 
						Debug.Log("Found " + (discoveredMirrorLinkUrls.Count-discoveredMirrorLinkCount) + " new mirror links"); 

						fileLinkCount = fileLinkUrls.Count;
						discoveredMirrorLinkCount = discoveredMirrorLinkUrls.Count;

					}
					else
					{
						// WWW didn't return as done
						// TODO: likely need to put timeouts on the WWW requests here and elsewhere // P4
					}
				}
			}

			// Merge in newly discovered mirrors
			if (discoveredMirrorLinkCount > 0)
			{
				if (mirrorAttemptCycles <= mirrorAttemptCyclesMax)
				{
					mirrorLinkUrls = discoveredMirrorLinkUrls;
					yield return new WaitForSeconds(secondsBetweenSteps);
					goto processMirrorLinks;
				}
			}

			// Save all of the discovered file links and mirror links to the INI file
			int index = 1;
			foreach(string link in fileLinkUrls)
			{
				ini.WriteValue("Links", index.ToString(), link);
				index++;
			}
			index = 1;
			foreach(string link in allMirrorLinkUrls)
			{
				ini.WriteValue("Mirrors", index.ToString(), link);
				index++;
			}
			ini.Close(); // I believe this saves the ini file

			yield return new WaitForSeconds(secondsBetweenSteps);
			goto pickDownloadMirror;

		pickDownloadMirror:
			Debug.Log("PHASE: PICK DOWNLOAD MIRROR2");		
			displayGuiStatus = "Picking download mirror...";

			// Make sure there's more than one file link to download from
			if (fileLinkUrls.Count == 0)
			{
				Debug.LogError("Could not find any download URLs from INI.");
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto failed;
			}

			// Pick at random one of the URLs from the INI to download from
			// string downloadUrl = fileLinkUrls[0];
			System.Random random = new System.Random();
			bool[] fileLinkUrlsAttempted = new bool[fileLinkUrls.Count];
	        int randomDownloadIndex = random.Next(0, fileLinkUrls.Count);
			string downloadUrl = fileLinkUrls[randomDownloadIndex];
			fileLinkUrlsAttempted[randomDownloadIndex] = true;
			int fileLinkUrlsAttemptedCount = 1;

			if (downloadUrl != null && downloadUrl != "")
			{
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto downloadFiles;
			}

			Debug.LogError("Could not parse download URL from INI.");
			yield return new WaitForSeconds(secondsBetweenSteps);
			goto failed;

		pickAnotherDownloadMirror:
			displayGuiStatus = "Picking another download mirror...";

			if (fileLinkUrlsAttemptedCount < fileLinkUrls.Count)
			{
				while (fileLinkUrlsAttempted[randomDownloadIndex] == true)
				{
					randomDownloadIndex = random.Next(0, fileLinkUrls.Count);
					yield return new WaitForSeconds(secondsBetweenSteps);
				}
				downloadUrl = fileLinkUrls[randomDownloadIndex];
				fileLinkUrlsAttempted[randomDownloadIndex] = true;
				fileLinkUrlsAttemptedCount++;

				if (downloadUrl != null && downloadUrl != "")
				{
					if (!downloadUrl.StartsWith("http://") && !downloadUrl.StartsWith("https://"))
					{
						Debug.LogError("File download link doesn't start with http:// or https:// : " + downloadUrl);
					}
					else
					{
						yield return new WaitForSeconds(secondsBetweenSteps);
						goto downloadFiles;
					}
				}
			}

			downloadAttemptCycles++;
			if (downloadAttemptCycles < downloadAttemptCyclesMax)
			{
				fileLinkUrlsAttemptedCount = 0;
				fileLinkUrlsAttempted = new bool[fileLinkUrls.Count];
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto pickAnotherDownloadMirror;
			}

			Debug.LogError("We've run out of download attempts.");
			yield return new WaitForSeconds(secondsBetweenSteps);
			goto failed;

		downloadFiles:
			Debug.Log("PHASE: DOWNLOAD FILES");
			displayGuiStatus = "Downloading files...";

			// Pick one of the download hosts
			// Attempt the download
			if (downloader)
			{
				Destroy(downloader);
				yield return new WaitForSeconds(secondsBetweenSteps);
			}
			downloader = gameObject.AddComponent<SimpleZipDownloader>();
			downloader.expectedNumberOfBytes = iniExpectedBytes;
			downloader.docPathExtraction = zipFileContentsPath;
			downloader.StartDownload(downloadUrl);

			Debug.Log("Download attempt #" + (fileLinkUrlsAttemptedCount + downloadAttemptCycles * fileLinkUrls.Count) + " --> from " + downloadUrl);
			yield return new WaitForSeconds(2);

			// Until we download the .zip file we're expecting...
			while(downloader.downloadState != SimpleZipDownloader.DownloadState.Completed)
			{
				if (downloader.downloadState == SimpleZipDownloader.DownloadState.Failed)
				{
					Debug.LogError("Download has failed.");
					goto pickAnotherDownloadMirror;
					// yield break;
				}
				Debug.Log("Waiting for download to finish...");
				yield return new WaitForSeconds(secondsBetweenSteps);
			}

			yield return new WaitForSeconds(secondsBetweenSteps);
			goto verifyFiles;

		verifyFiles:
			Debug.Log("PHASE: VERIFY FILES");
			displayGuiStatus = "Verifying downloaded files...";

			// COULDDO: this check is currently redundant with code in SimpleZipDownloader.cs // P4
			// See if the download succeeded, and it's the expected filesize
			if (iniExpectedBytes == downloader.GetDownloadedNumberOfBytes())
			{
				// The downloaded file is the expected filesize
				Debug.Log("The downloaded file matches the expected number of bytes.");
			}
			else
			{
				Debug.LogError("The file we downloaded was not the expected number of bytes");
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto pickAnotherDownloadMirror;
			}

			// Do the CRC-32 comparison on the file
			uint computedCrc32 = Crc32Algorithm.Compute(downloader.GetZipData());
            if (iniExpectedCrc32 == computedCrc32)
			{
				// The downloaded file is the expected filesize
				Debug.Log("The downloaded file matches provided CRC-32.");
			}
			else
			{
				Debug.LogError("The file we downloaded does not match the provided CRC-32.");
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto pickAnotherDownloadMirror;
			}

			// Do the MD5 comparison of the file
			// Note: Yes I know this is redundant with the CRC-32 code, but I wanted to have working example code of both!
			byte[] hash;
			var md5 = System.Security.Cryptography.MD5.Create();
			md5.TransformFinalBlock(downloader.GetZipData(), 0, (int)iniExpectedBytes);
    		hash = md5.Hash;
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hash.Length; i++)
			{
	            sb.Append(hash[i].ToString("x2"));
			}
			var computedMd5 = sb.ToString();
			// Debug.Log("Computer MD5 = " + sb.ToString());
            if (iniExpectedMd5 == computedMd5)
			{
				// The downloaded file is the expected filesize
				Debug.Log("The downloaded file matches provided MD5.");
			}
			else
			{
				// If not, try another one of the download URLs
				Debug.LogError("The file we downloaded does not match the provided MD5.");
				yield return new WaitForSeconds(secondsBetweenSteps);
				goto pickAnotherDownloadMirror;
			}

			yield return new WaitForSeconds(secondsBetweenSteps);

			// Free up the memory from the downloaded .zip
			downloader.DeleteData();
			Destroy(downloader);

			yield return new WaitForSeconds(secondsBetweenSteps);

			goto extractFiles;

		extractFiles:
			Debug.Log("PHASE: EXTRACT FILES");
			displayGuiStatus = "Extracting downloaded files...";

			// Handle the scenario where the .zip exists but not yet the .pak files // P4			
			if (File.Exists(zipFilePath))
			{
				// Might also need to check to see if the zip file contents have already been extracted here // P3
				zipFileData = File.ReadAllBytes(zipFilePath);
				ZipFile.UnZip(zipFileContentsPath, zipFileData);
			}

			yield return new WaitForSeconds(secondsBetweenSteps);

			goto listFiles;

		listFiles:
			Debug.Log("PHASE: LIST DISCOVERED FILES");
			displayGuiStatus = "Listing downloaded files...";

			// TODO: Right now the SimpleZipDownloader does both the downloading and the unzipping at once // P4
			// Load at least the headers of the discovered .pak files
			// Create a list of all the known files, and where we think they're stored
			// TODO: Consider if we're going to load only from direct flat file on disk or only .pak or both // P3

			yield return new WaitForSeconds(secondsBetweenSteps);

			goto unpackFiles;

		unpackFiles:
			Debug.Log("PHASE: UNPACKING FILES");
			displayGuiStatus = "Unpacking downloaded files...";

			// Get some files out of the .pak file into their appropriate location
			pak = new FormatPak();
			pak.Load(pakFilePath);
			yield return new WaitForSeconds(secondsBetweenSteps);
			pak.Parse();
			yield return new WaitForSeconds(secondsBetweenSteps);

			goto listMaps;

		listMaps:
			// Remember the list of all other .bsp files for later
			bspMapNames = pak.GetListOfBspFiles();
			/***
			for (int i=0; i < bspMapNames.Length; i++)
			{
				Debug.Log(bspMapNames[i]);
			}
			***/
			yield return new WaitForSeconds(secondsBetweenSteps);

			goto loadFiles;

		loadFiles:
			Debug.Log("PHASE: LOAD FILES");
			displayGuiStatus = "Loading files...";

			// Make a new game object just for this new map
			mapGameObject = new GameObject();
			mapGameObject.name = "bsp"; // COULDDO: more detailed object name
			mapGameObject.transform.SetParent(gameObject.transform);

			// Start up our BSP MonoBehaviour
			FormatBspMB bspMB = mapGameObject.AddComponent<FormatBspMB>();
			bspMB.pathToBsp = bspFilePath;
			bspMB.saveFiles = saveFiles;
			bspMB.SetPak(pak);
			// COULDDO: Right now the FixedUpdate() in FormatBspMB will kick off visualization. Could make this more clear.

			yield return new WaitForSeconds(secondsBetweenSteps);

			goto visualize;

		visualize:
			Debug.Log("PHASE: VISUALIZE FILES");
			displayGuiStatus = "Visualizing level...";

			// Visualize the specific .bsp we want, using the palette

			while (!bspMB.visualized)
			{
				yield return new WaitForSeconds(1.0f); // actually waiting over time here
			}

			yield return new WaitForSeconds(secondsBetweenSteps);

			goto cleanup;

		cleanup:
			Debug.Log("PHASE: CLEAN-UP");
			displayGuiStatus = "Cleaning up...";

			yield return new WaitForSeconds(secondsBetweenSteps);

			if (downloader)
			{
				downloader.DeleteData();
			}
			downloader = null;
			pak.Unload();
			pak = null;
			zipFileData = null;

			bspMB.UnloadUnusedDependencies();
			bspMB = null;

			yield return new WaitForSeconds(secondsBetweenSteps);

			// Force garbage collection
			System.GC.Collect();
			yield return new WaitForSeconds(1.0f); // actually waiting a full second here

			// Force garbage collection
			System.GC.Collect();
			yield return new WaitForSeconds(secondsBetweenSteps);

			goto finished;

		finished:
			// We're done for now
			Debug.Log("PHASE: QUAKE BSP IN UNITY IS FINISHED!");
			displayGuiStatus = "Done!";
			yield return new WaitForSeconds(1.0f);
			displayGuiMode = 1;

			yield break;

		failed:
			Debug.LogError("Something in the boot process failed.");
			displayGuiStatus = "Something went wrong. Check the log files.";

			yield break;

	}

	private void loadMapNamed(string newMapName)
	{
		if (!newMapName.Equals(bspFilePath))
		{
			displayGuiMode = 0;
			bspFilePath = newMapName;
			StartCoroutine(Orchestrate(true));
		}
	}

	void OnGUI ()
	{
		// Customize the GUI style based on screen / window size
        int newFontSize = (int)(Screen.height / 40.0f);
		GUI.skin.GetStyle("label").richText    = true;
		GUI.skin.GetStyle("label").fontSize    = newFontSize;
		GUI.skin.GetStyle("label").fixedHeight = (int)(newFontSize * 2.0f);
		GUI.skin.GetStyle("button").fontSize = newFontSize;

		if (displayGuiMode == 0) // loading, cannot be hidden
		{
			// Display the UI
			GUILayout.Label(" <b>Quake BSP in Unity</b>");
			GUILayout.Label(" " + displayGuiStatus);
		}
		else if (displayGuiMode == 1) // full instructions, can be minimized
		{
			// Display the UI
			GUILayout.Label(" <b>Quake BSP in Unity</b>");
			// GUILayout.Button("I am a button");
			GUILayout.Label(" <i>Render Mode: </i> " + RenderModes.currentRenderMode);
			GUILayout.Label(" <i>Controls:</i>");
			GUILayout.Label(" Move mouse = look around.");
			GUILayout.Label(" Esc = unlock mouse. Click = lock mouse.");
			GUILayout.Label(" W/S = move forward/back. A/D = move left/right.");
			GUILayout.Label(" Q/E = move relative down/up. ");
			GUILayout.Label(" Ctrl/Space = move absolute down/up. ");
			GUILayout.Label(" Shift = speed-up movement.");
			GUILayout.Label(" M = select map.");
			GUILayout.Label(" R = cycle render mode.");
			GUILayout.Label(" Tab = toggle UI.");
			GUILayout.Label(" Ctrl+P = exit player.");
		}
		else if (displayGuiMode == 2) // minimized instructions, can be hidden
		{
			GUILayout.Label(" Tab = toggle UI.");
		}
		else if (displayGuiMode == 3) // no GUI / HUD at all!
		{
			GUILayout.Label(" ");
		}
		else if (displayGuiMode == 4) // select Map
		{
			GUILayout.Label(" <i>Select Map: </i> ");
			for (int i=0; i < bspMapNames.Length; i++)
			{
				if (GUILayout.Button(bspMapNames[i]) )
				{
					// Debug.Log("Clicked on new map button index " + i + " for " + bspMapNames[i]);
					loadMapNamed(bspMapNames[i]);
				}
			}
		}
    }

}
