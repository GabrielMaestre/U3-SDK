////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace SDG.Unturned.Tools
{
	/// <summary>
	/// Loads a Unity Profiler capture (.data) and aggregates main-thread markers across all frames,
	/// writing a CSV ranked by total self time. Intended to attribute costs like
	/// Update.ScriptRunBehaviourUpdate from captures in ProfilerCaptures/ without manual frame-by-frame
	/// inspection. Loading a capture replaces the Profiler window's current data.
	/// </summary>
	public static class ProfilerCaptureAnalyzerTool
	{
		private class MarkerStats
		{
			public double totalSelfMs;
			public double maxSelfMs;
			public double totalGcBytes;
			public long calls;
			public int frames;
		}

		[MenuItem("Window/Unturned/Analyze Profiler Capture")]
		public static void AnalyzeCapture()
		{
			string startDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProfilerCaptures");
			if (!Directory.Exists(startDirectory))
			{
				startDirectory = Application.dataPath;
			}

			string capturePath = EditorUtility.OpenFilePanel("Select Profiler Capture", startDirectory, "data");
			AnalyzeCaptureFile(capturePath);
		}

		/// <summary>
		/// Dialog-free variant so automation (e.g. the local MCP bridge) can run the analysis.
		/// </summary>
		[MenuItem("Window/Unturned/Analyze Newest Profiler Capture")]
		public static void AnalyzeNewestCapture()
		{
			string captureDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProfilerCaptures");
			if (!Directory.Exists(captureDirectory))
			{
				Debug.LogError($"Capture directory not found: {captureDirectory}");
				return;
			}

			string newestCapture = null;
			System.DateTime newestTime = System.DateTime.MinValue;
			foreach (string candidate in Directory.GetFiles(captureDirectory, "*.data"))
			{
				System.DateTime writeTime = File.GetLastWriteTimeUtc(candidate);
				if (writeTime > newestTime)
				{
					newestTime = writeTime;
					newestCapture = candidate;
				}
			}

			if (newestCapture == null)
			{
				Debug.LogError($"No .data captures found in {captureDirectory}");
				return;
			}

			AnalyzeCaptureFile(newestCapture);
		}

		public static void AnalyzeCaptureFile(string capturePath)
		{
			if (string.IsNullOrEmpty(capturePath))
			{
				return;
			}

			if (!ProfilerDriver.LoadProfile(capturePath, false))
			{
				Debug.LogError($"Failed to load profiler capture: {capturePath}");
				return;
			}

			int firstFrame = ProfilerDriver.firstFrameIndex;
			int lastFrame = ProfilerDriver.lastFrameIndex;
			if (firstFrame < 0 || lastFrame < firstFrame)
			{
				Debug.LogError("Capture loaded but contains no frames.");
				return;
			}

			Dictionary<string, MarkerStats> statsByName = new Dictionary<string, MarkerStats>();
			// Reused across frames; names arrive interned per view so per-frame set stays cheap.
			HashSet<string> namesThisFrame = new HashSet<string>();
			List<int> childBuffer = new List<int>();
			Stack<int> pendingItems = new Stack<int>();
			double totalFrameMs = 0.0;
			int analyzedFrames = 0;

			try
			{
				for (int frameIndex = firstFrame; frameIndex <= lastFrame; frameIndex++)
				{
					if (frameIndex % 25 == 0 && EditorUtility.DisplayCancelableProgressBar(
						"Analyzing Profiler Capture",
						$"Frame {frameIndex - firstFrame + 1}/{lastFrame - firstFrame + 1}",
						(float) (frameIndex - firstFrame) / (lastFrame - firstFrame + 1)))
					{
						break;
					}

					using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(
						frameIndex,
						0, // main thread
						HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
						HierarchyFrameDataView.columnTotalTime,
						false))
					{
						if (view == null || !view.valid)
						{
							continue;
						}

						totalFrameMs += view.frameTimeMs;
						analyzedFrames++;
						namesThisFrame.Clear();

						pendingItems.Clear();
						pendingItems.Push(view.GetRootItemID());
						while (pendingItems.Count > 0)
						{
							int itemId = pendingItems.Pop();
							childBuffer.Clear();
							view.GetItemChildren(itemId, childBuffer);
							foreach (int childId in childBuffer)
							{
								pendingItems.Push(childId);

								string name = view.GetItemName(childId);
								if (!statsByName.TryGetValue(name, out MarkerStats stats))
								{
									stats = new MarkerStats();
									statsByName.Add(name, stats);
								}

								double selfMs = view.GetItemColumnDataAsDouble(childId, HierarchyFrameDataView.columnSelfTime);
								stats.totalSelfMs += selfMs;
								if (selfMs > stats.maxSelfMs)
								{
									stats.maxSelfMs = selfMs;
								}
								stats.totalGcBytes += view.GetItemColumnDataAsDouble(childId, HierarchyFrameDataView.columnGcMemory);
								stats.calls += (long) view.GetItemColumnDataAsDouble(childId, HierarchyFrameDataView.columnCalls);
								if (namesThisFrame.Add(name))
								{
									stats.frames++;
								}
							}
						}
					}
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			if (analyzedFrames < 1)
			{
				Debug.LogError("No valid main-thread frames found in capture.");
				return;
			}

			List<KeyValuePair<string, MarkerStats>> rankedMarkers = new List<KeyValuePair<string, MarkerStats>>(statsByName);
			rankedMarkers.Sort((lhs, rhs) => rhs.Value.totalSelfMs.CompareTo(lhs.Value.totalSelfMs));

			string csvPath = capturePath + ".markers.csv";
			StringBuilder csv = new StringBuilder(1024 * 1024);
			csv.AppendLine("marker,total_self_ms,self_ms_per_frame,max_self_ms,calls,calls_per_frame,gc_bytes_total,gc_bytes_per_frame,frames_present");
			foreach (KeyValuePair<string, MarkerStats> pair in rankedMarkers)
			{
				MarkerStats stats = pair.Value;
				csv.Append('"').Append(pair.Key.Replace("\"", "\"\"")).Append('"').Append(',');
				csv.Append(stats.totalSelfMs.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
				csv.Append((stats.totalSelfMs / analyzedFrames).ToString("F4", CultureInfo.InvariantCulture)).Append(',');
				csv.Append(stats.maxSelfMs.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
				csv.Append(stats.calls).Append(',');
				csv.Append(((double) stats.calls / analyzedFrames).ToString("F2", CultureInfo.InvariantCulture)).Append(',');
				csv.Append(stats.totalGcBytes.ToString("F0", CultureInfo.InvariantCulture)).Append(',');
				csv.Append((stats.totalGcBytes / analyzedFrames).ToString("F1", CultureInfo.InvariantCulture)).Append(',');
				csv.Append(stats.frames).AppendLine();
			}
			File.WriteAllText(csvPath, csv.ToString());

			StringBuilder summary = new StringBuilder();
			summary.AppendLine($"Analyzed {analyzedFrames} frames, avg frame {(totalFrameMs / analyzedFrames):F3} ms. Top self-time markers:");
			int summaryRows = Mathf.Min(25, rankedMarkers.Count);
			for (int index = 0; index < summaryRows; index++)
			{
				MarkerStats stats = rankedMarkers[index].Value;
				summary.AppendLine($"{index + 1,3}. {(stats.totalSelfMs / analyzedFrames),8:F4} ms/frame  {rankedMarkers[index].Key}");
			}
			summary.AppendLine($"Full ranking: {csvPath}");
			Debug.Log(summary.ToString());
		}
	}
}
