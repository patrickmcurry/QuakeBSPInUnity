// TimerHelper.cs
// Used to measure how long certain actions take in the codebase

using System;
#if UNITY_EDITOR || UNITY_5 || UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

public class TimerHelper
{
    private bool autoStart = true;
    private bool isRunning;
    private string message;
    DateTime before;
    DateTime after;
    TimeSpan duration;

    public TimerHelper(string initMessage="")
    {
        message = initMessage;
        if (autoStart)
        {
            Start();
        }
    }

    public void Start()
    {
        if (!isRunning)
        {
            before = DateTime.Now;
            isRunning = true;
            Debug.Log("Timer start: " + message);
            return;
        }
        Debug.LogWarning("Timer start: " + message + " is already running.");
    }

    public void Stop()
    {
        if(isRunning)
        {
            after = DateTime.Now; 
            duration = after.Subtract(before);
            Debug.Log("Timer stop: " + message + " duration in milliseconds: " + duration.Milliseconds);
            isRunning = false;
            return;
        }
        Debug.LogWarning("Timer stop: " + message + " was not already running.");
    }

    public void Reset()
    {
        isRunning = false;
    }

    public void Restart()
    {
        if (isRunning)
        {
            Stop();
        }
        Reset();
        Start();
    }

} // end of TimerHelper.cs class

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
