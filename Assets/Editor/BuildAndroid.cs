using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace PCSim.Editor
{
    public static class BuildAndroid
    {
        [MenuItem("PC Simulator/Build Android APK")]
        public static void Build()
        {
            PlayerSettings.companyName="Danya";
            PlayerSettings.productName="PC Simulator";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.danya.pcsimulator");
            PlayerSettings.bundleVersion="0.1.0"; PlayerSettings.Android.bundleVersionCode=1;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.stripEngineCode=false;
            PlayerSettings.Android.forceInternetPermission=true;
            PlayerSettings.Android.useCustomKeystore=false;
            EditorUserBuildSettings.buildAppBundle=false;
            Directory.CreateDirectory("Builds/Android");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/Scenes/Apartment.unity"},locationPathName="Builds/Android/PC-Simulator.apk",
                target=BuildTarget.Android,options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Android build failed: "+report.summary.result);
            Debug.Log("APK built: Builds/Android/PC-Simulator.apk");
        }
    }
}
