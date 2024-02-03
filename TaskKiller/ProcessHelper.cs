﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Navigation;
using Wox.Infrastructure;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.TaskKiller
{
    internal partial class ProcessHelper
    {
        private static readonly HashSet<string> _systemProcessList =
        [
            "conhost",
            "svchost",
            "idle",
            "system",
            "rundll32",
            "csrss",
            "lsass",
            "lsm",
            "smss",
            "wininit",
            "winlogon",
            "services",
            "spoolsv",
            "explorer",
        ];

        private static bool IsSystemProcess(Process p) => _systemProcessList.Contains(p.ProcessName.ToLower());

        /// <summary>
        /// Returns a ProcessResult for evey running non-system process whose name matches the given search
        /// </summary>
        public static List<ProcessResult> GetMatchingProcesses(string search)
        {
            var processes = Process.GetProcesses().Where(p => !IsSystemProcess(p)).ToList();

            if (string.IsNullOrWhiteSpace(search))
            {
                return processes.ConvertAll(p => new ProcessResult(p, 0));
            }

            List<ProcessResult> results = [];
            foreach (var p in processes)
            {
                var score = StringMatcher.FuzzySearch(search, p.ProcessName + p.Id).Score;
                if (score > 0)
                {
                    results.Add(new ProcessResult(p, score));
                }
            }
            return results;
        }

        /// <summary>
        /// Returns all non-system processes whose file path matches the given processPath
        /// </summary>
        public static IEnumerable<Process> GetSimilarProcesses(string processPath) =>
            Process.GetProcesses().Where(p => !IsSystemProcess(p) && TryGetProcessFilename(p) == processPath);

        public static void TryKill(Process p)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(50);
                }
            }
            catch (Exception e)
            {
                Log.Exception($"Failed to kill process {p.ProcessName}", e, typeof(ProcessHelper));
            }
        }

        public static string TryGetProcessFilename(Process p)
        {
            try
            {
                int capacity = 2000;
                StringBuilder builder = new(capacity);
                IntPtr ptr = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, p.Id);
                return QueryFullProcessImageName(ptr, 0, builder, ref capacity) ? builder.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            QueryLimitedInformation = 0x00001000
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            [In] IntPtr hProcess,
            [In] int dwFlags,
            [Out] StringBuilder lpExeName,
            ref int lpdwSize);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial IntPtr OpenProcess(
            ProcessAccessFlags processAccess,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            int processId);
    }
}
