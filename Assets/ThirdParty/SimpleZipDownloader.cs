// SimpleZipDownloader.cs
// https://raw.githubusercontent.com/sankyprabhu/UnityZip/master/Assets/Script/SimpleZipDownloader.cs

using UnityEngine;
using System;
using System.Collections;
using System.IO;
using UnityEngine.UI;

/// <summary>
/// Downloading zipped image files and do unzip under persistence data path 
/// then load the specified image file.
/// </summary>
public class SimpleZipDownloader : MonoBehaviour
{
   // public MeshRenderer renderer;
    public string url;
    public string filename = "";
    public string docPathExtraction = "";
    public string imgFile = "";
    public GameObject rawImage;
    public DownloadState downloadState;
    public long numberOfBytes = 0; // PMC
    public long expectedNumberOfBytes = 0; // PMC

    public enum DownloadState
    {
        Uninitiated,
        Initiated,
        Completed,
        Failed
    }

    private byte[] data; // PMC made it private member variable

    delegate void OnFinish();

    void Start()
    {
        // PMC
        downloadState = DownloadState.Uninitiated;
        // End PMC
    }

    // PMC
    public void StartDownload()
    {
        StartCoroutine(Download(url, OnDownloadDone, true));
    }

    public void StartDownload(string newUrl)
    {
        url = newUrl;
        StartCoroutine(Download(url, OnDownloadDone, true));
    }

    public long GetDownloadedNumberOfBytes()
    {
        return numberOfBytes;
    }

    public byte[] GetZipData()
    {
        return data;
    }

    public void DeleteData()
    {
        data = null;
    }
    // End PMC

    /// <summary>
    /// Called when the downloaded zip file is unzipping is finished.
    /// </summary>
    void OnDownloadDone()
    {
        // PMC
        downloadState = DownloadState.Completed;
        // End PMC

        if (rawImage != null)
        {
            // load unzipped image file and assign it to the material's main texture.
            string path = Application.persistentDataPath + "/" + imgFile;

            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(1, 1);
            texture.LoadImage(bytes);
            rawImage.GetComponent<RawImage>().texture = texture;

           // renderer.material.mainTexture = Image.LoadPNG(path);
        }
    }

    /// <summary>
    /// Download ZIP file from the given URL and do calling passed delegate.
    /// 
    /// NOTE: This does not resolve an error such as '404 Not Found'.
    /// </summary>
    IEnumerator Download(string url, OnFinish onFinish, bool remove)
    {
        // PMC
        downloadState = DownloadState.Initiated;
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            Debug.LogError("File download link doesn't start with http:// or https:// : " + url);
        }
        // End PMC

        WWW www = new WWW(url);

        yield return www;

        if (www.isDone)
        {
            data = www.bytes;

            // PMC string file = UriHelper.GetFileName(url);
            // COULDDO: look into HTTP range requests, where you can download a fragment of a file
            // https://developer.mozilla.org/en-US/docs/Web/HTTP/Range_requests
            string file = filename;
            // End PMC
            Debug.Log("Downloading of " + file + " is completed.");

            string docPath = Application.persistentDataPath;
            docPath += "/" + file;

            Debug.Log("Downloaded file path: " + docPath);

            // PMC
            // numberOfBytes = new System.IO.FileInfo(docPath).Length;
            numberOfBytes = data.Length;
            if (expectedNumberOfBytes == 0)
            {
                Debug.Log("We aren't expecting a certain number of bytes");
            }
            else if (numberOfBytes == expectedNumberOfBytes)
            {
                Debug.Log("Downloaded expected number of bytes");
            }
            else
            {
                // We've downloaded a different number of byte than expected
                Debug.LogError("We've downloaded a different number of bytes than expected.");
                downloadState = DownloadState.Failed;
                yield break;
            }
            // End PMC

            ZipFile.UnZip(docPathExtraction, data);

            if (onFinish != null)
            {
                onFinish();
            }

            if (remove)
            {
                // delete zip file.
                // System.IO.File.Delete(docPath);
            }
        }
    }
}
