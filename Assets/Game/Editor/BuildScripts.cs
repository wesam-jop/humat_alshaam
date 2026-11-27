using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Adventure3D.EditorTools
{
    /// <summary>
    /// Batch builds. Results (success, size, duration, or the exact blocker) are written to
    /// Reports/build-results.json so the verification report can separate "compiles", "builds" and
    /// "runs on device".
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Adventure3D.EditorTools.BuildScripts.BuildWindows
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Adventure3D.EditorTools.BuildScripts.BuildAll
    /// </summary>
    public static class BuildScripts
    {
        public const string OutputRoot = "Builds";
        public const string WindowsExe = OutputRoot + "/Windows/Adventure3D.exe";

        [Serializable]
        sealed class Result
        {
            public string target;
            public string status; // succeeded | failed | unavailable
            public string output;
            public long sizeBytes;
            public double seconds;
            public string detail;
        }

        [Serializable]
        sealed class ResultList { public List<Result> results = new List<Result>(); public string unityVersion; public string timeUtc; }

        static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        [MenuItem("Adventure3D/Build/Windows (x64)")]
        public static void BuildWindows() => RunAndExit(new[] { BuildWindowsInternal() });

        [MenuItem("Adventure3D/Build/All available platforms")]
        public static void BuildAll() => RunAndExit(new[]
        {
            BuildWindowsInternal(),
            BuildAndroidInternal(),
            ExportIosInternal(),
            PlayStationInternal(),
        });

        static void RunAndExit(Result[] results)
        {
            var list = new ResultList { unityVersion = Application.unityVersion, timeUtc = DateTime.UtcNow.ToString("o") };
            list.results.AddRange(results);
            Directory.CreateDirectory("Reports");
            File.WriteAllText("Reports/build-results.json", JsonUtility.ToJson(list, true), new UTF8Encoding(false));
            foreach (var r in results) Debug.Log($"[A3D] BUILD {r.target}: {r.status} {r.detail}");
            if (Application.isBatchMode)
                EditorApplication.Exit(results[0].status == "succeeded" ? 0 : 1);
        }

        static Result BuildWindowsInternal()
        {
            var r = new Result { target = "StandaloneWindows64", output = WindowsExe };
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                r.status = "unavailable";
                r.detail = "Windows Build Support module is not installed";
                return r;
            }
            return Build(r, BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, WindowsExe, BuildOptions.None);
        }

        static Result BuildAndroidInternal()
        {
            var r = new Result { target = "Android", output = OutputRoot + "/Android/Adventure3D.apk" };
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                r.status = "unavailable";
                r.detail = "Android Build Support (SDK/NDK/OpenJDK) module is not installed for this editor";
                return r;
            }
            return Build(r, BuildTarget.Android, BuildTargetGroup.Android, r.output, BuildOptions.None);
        }

        static Result ExportIosInternal()
        {
            var r = new Result { target = "iOS (Xcode project export)", output = OutputRoot + "/iOS" };
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                r.status = "unavailable";
                r.detail = "iOS Build Support module is not installed. Exporting the Xcode project works on Windows once installed; " +
                           "building the .ipa and running on a device require macOS + Xcode + an Apple developer account";
                return r;
            }
            return Build(r, BuildTarget.iOS, BuildTargetGroup.iOS, r.output, BuildOptions.None);
        }

        static Result PlayStationInternal() => new Result
        {
            target = "PlayStation (PS5 assumed, open decision)",
            status = "unavailable",
            detail = "Requires Sony partner registration, licensed PlayStation Unity build module, SDK and a dev kit; none are available in this environment",
        };

        static Result Build(Result r, BuildTarget target, BuildTargetGroup group, string path, BuildOptions options)
        {
            var started = DateTime.UtcNow;
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = path,
                    target = target,
                    targetGroup = group,
                    options = options,
                });
                r.seconds = (DateTime.UtcNow - started).TotalSeconds;
                r.status = report.summary.result == BuildResult.Succeeded ? "succeeded" : "failed";
                r.sizeBytes = (long)report.summary.totalSize;
                r.detail = $"result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings}";
            }
            catch (Exception e)
            {
                r.seconds = (DateTime.UtcNow - started).TotalSeconds;
                r.status = "failed";
                r.detail = e.Message;
            }
            return r;
        }
    }
}
