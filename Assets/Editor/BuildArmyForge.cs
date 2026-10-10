using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ArmyForge.Editor
{
    public static class BuildArmyForge
    {
        [MenuItem("Army Forge/Build Windows app")]
        public static void Windows()
        {
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = "Builds/Windows/ArmyForge.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + result.summary.result);
        }
    }
}
