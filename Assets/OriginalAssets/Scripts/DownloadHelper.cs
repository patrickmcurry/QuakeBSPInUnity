// DownloadHelper.cs
// Script specifically for downloading stuff, not in use right now
// NOTE: Not currently used in the code anymore, but keeping for historical / documentation reasons

// Downloading
// https://learn.microsoft.com/en-us/dotnet/api/system.net.webclient.downloadfileasync?redirectedfrom=MSDN&view=net-7.0#overloads

// Zip stuff
// https://github.com/icsharpcode/SharpZipLib/tree/master/src/ICSharpCode.SharpZipLib

using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public class DownloadHelper
{
    public static bool Validator (object sender, X509Certificate certificate, X509Chain chain,
                                      SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    static void DownloadFile(string url, string localFilename)
    {
        ServicePointManager.ServerCertificateValidationCallback = Validator;
        WebClient client = new WebClient();
        client.DownloadFileCompleted += new System.ComponentModel.AsyncCompletedEventHandler( DownloadFileCompleted );
        client.DownloadFileAsync(new System.Uri(url), localFilename);
    }

    static void DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
    {
        if (e.Error == null)
        {
            // All Done
            Debug.Log("Download completed: " + e.ToString());
        }
        else
        {
            Debug.LogError("Download error: " + e.ToString() + " : " + e.Error);
        }
    }

    // Download the Quake 1 shareware .zip file from a list of reputible sources

    public static void DownloadQuakeZip()
    {
        string quakeZipDownloadUrl = "<see above>>";
        string quakeZipSaveUrl     = "QuakeData/quake106.zip";
        DownloadFile(quakeZipDownloadUrl, quakeZipSaveUrl);
    }
}
