using System.IO;
using UnityEditor;
using UnityEngine;

namespace YASS.Editor
{
    /// <summary>
    /// Saves what the Game view is showing to <c>Temp/YASS-Screenshot.png</c>. Development tool: it is how an
    /// automated session (MCP) can actually look at the game, including Screen Space Overlay UI, which does not
    /// appear in a camera render.
    /// </summary>
    public static class ScreenshotTool
    {
        public const string OutputPath = "Temp/YASS-Screenshot.png";

        [MenuItem("Tools/YASS/Capture Screenshot")]
        public static void Capture()
        {
            Directory.CreateDirectory("Temp");
            ScreenCapture.CaptureScreenshot(OutputPath);
            Debug.Log($"Screenshot requested at {OutputPath} (written at the end of the frame)");
        }
    }
}
