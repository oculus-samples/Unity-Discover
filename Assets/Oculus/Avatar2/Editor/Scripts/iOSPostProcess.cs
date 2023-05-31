#if UNITY_IOS

using System.IO;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEditor.Callbacks;

namespace Oculus.Avatar2
{
    public class AvatarSDK_iOS_PostProcess
    {


        private static readonly string[] xcFrameworksPaths =
        {
            "Oculus/Avatar2/Plugins/iOS/",
            "Internal/Plugins/iOS/"
        };

        private const string BuildPhaseName = "iOS Post Process";

        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromString(File.ReadAllText(projectPath));

            // Disable bitcode for Unity Framework
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();
            project.SetBuildProperty(frameworkTarget, "ENABLE_BITCODE", "NO");

            var projectSettings = project.WriteToString();
            File.WriteAllText(projectPath, projectSettings);



            XcodeXCFrameworkHelper.AddXCFrameworksToXcodeProject(pathToBuiltProject, xcFrameworksPaths, BuildPhaseName);
        }
    }
}

#endif
