// RenderModes.cs
// Used to help wrangle how we're rendering the scene -- normal, wireframe, unlit, lightmap only, etc.
using UnityEngine;

public enum RenderMode
{
	Default,
	Wireframe,
	Unlit,
	LightingOnly
} // Hey if you add here, update countOfRenderModes below!

public class RenderModes : MonoBehaviour
{
	public static int countOfRenderModes = 4;
	public static RenderMode currentRenderMode = (RenderMode)0;
	public static RenderMode lastRenderMode = (RenderMode)0;
	public bool isSkybox;
	private Camera thisCamera;

	public static void CycleRenderMode()
	{
		lastRenderMode = currentRenderMode;
		currentRenderMode += 1;
		if ((int)currentRenderMode >= countOfRenderModes)
		{
			currentRenderMode = (RenderMode)0;
		}
	}
	
	void Start ()
	{
		thisCamera = this.gameObject.GetComponent<Camera>();
	}

    void OnPreRender()
	{
		if (isSkybox)
		{
			if (currentRenderMode == RenderMode.Wireframe || currentRenderMode == RenderMode.LightingOnly)
			{
				thisCamera.clearFlags = CameraClearFlags.SolidColor;
			}
			else
			{
				thisCamera.clearFlags = CameraClearFlags.Skybox;
			}
		}
		else
		{
			if (currentRenderMode == RenderMode.Wireframe)
			{
				GL.wireframe = true;
			}
		}
    }

    void OnPostRender()
	{
		if (isSkybox)
		{
			// do nothing?
		}
		else
		{
			if (currentRenderMode == RenderMode.Wireframe)
			{
				GL.wireframe = false;
			}
		}
    }

}
