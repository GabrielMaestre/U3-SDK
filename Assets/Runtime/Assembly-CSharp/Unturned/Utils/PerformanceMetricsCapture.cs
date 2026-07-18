////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDG.Unturned
{
	/// <summary>
	/// Opt-in local CSV capture for repeatable performance tests.
	/// Enable with -PerformanceMetrics and optionally -PerformanceMetricsSeconds=N.
	/// </summary>
	internal sealed class PerformanceMetricsCapture : MonoBehaviour
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void initialize()
		{
			if (!shouldCapture)
				return;

			beginCapture(captureDuration.hasValue ? captureDuration.value : 300, "performance");
		}

		/// <summary>
		/// Starts one opt-in capture after a test warm-up. Used by PerformanceStressScenario.
		/// </summary>
		internal static void beginAutomaticStressCapture(int duration)
		{
			if (instance != null)
			{
				UnturnedLog.warn("Performance stress capture skipped because another capture is active");
				return;
			}

			beginCapture(duration, "performance-stress");
		}

		private static void beginCapture(int duration, string fileNamePrefix)
		{
			GameObject gameObject = new GameObject("Performance Metrics Capture");
			DontDestroyOnLoad(gameObject);
			PerformanceMetricsCapture capture = gameObject.AddComponent<PerformanceMetricsCapture>();
			capture.initialize(duration, fileNamePrefix);
		}

		private void initialize(int requestedDuration, string fileNamePrefix)
		{
			instance = this;
			maximumDuration = Mathf.Max(1, requestedDuration);

			string directory = Path.Combine(Application.persistentDataPath, "PerformanceCaptures");
			Directory.CreateDirectory(directory);
			string path = Path.Combine(directory, $"{fileNamePrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
			writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
			writer.WriteLine("timestamp_utc,elapsed_s,scene,frames,fps_avg,frame_ms_avg,frame_ms_p50,frame_ms_p95,frame_ms_p99,frame_ms_max,main_thread_ms_avg,main_thread_ms_max,render_thread_ms_avg,render_thread_ms_max,cpu_frame_ms,gpu_frame_ms,gc_alloc_kib_avg,gc_alloc_kib_max,gc_used_mib,system_used_mib,draw_calls,batches,setpass_calls,triangles");
			writer.Flush();

			mainThreadTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", recorderCapacity);
			renderThreadTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", recorderCapacity);
			gcAllocatedInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", recorderCapacity);
			gcUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
			systemUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
			drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
			batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
			setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
			triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			Debug.Assert(getPercentileIndex(10, 0.95f) == 9, "Performance percentile calculation changed");
#endif
			UnturnedLog.info($"Performance metrics capture started: {path}");
		}

		private void Update()
		{
			float deltaTime = Time.unscaledDeltaTime;
			elapsed += deltaTime;
			windowElapsed += deltaTime;
			if (frameTimeCount < frameTimes.Length)
			{
				frameTimes[frameTimeCount++] = deltaTime * 1000.0f;
			}

			FrameTimingManager.CaptureFrameTimings();
			if (FrameTimingManager.GetLatestTimings(1, frameTiming) > 0)
			{
				cpuFrameTime = frameTiming[0].cpuFrameTime;
				gpuFrameTime = frameTiming[0].gpuFrameTime;
			}

			if (windowElapsed >= sampleInterval)
			{
				writeSample();
				windowElapsed = 0.0f;
				frameTimeCount = 0;
			}

			if (elapsed >= maximumDuration)
			{
				UnturnedLog.info("Performance metrics capture finished");
				Destroy(gameObject);
			}
		}

		private void writeSample()
		{
			if (frameTimeCount < 1)
				return;

			Array.Sort(frameTimes, 0, frameTimeCount);
			double frameTimeTotal = 0.0;
			for (int index = 0; index < frameTimeCount; ++index)
			{
				frameTimeTotal += frameTimes[index];
			}

			double frameTimeAverage = frameTimeTotal / frameTimeCount;
			getRecorderTimes(ref mainThreadTime, out double mainThreadAverage, out double mainThreadMaximum);
			getRecorderTimes(ref renderThreadTime, out double renderThreadAverage, out double renderThreadMaximum);
			getRecorderValues(ref gcAllocatedInFrame, 1.0 / 1024.0, out double gcAllocatedAverage, out double gcAllocatedMaximum);

			string sceneName = SceneManager.GetActiveScene().name.Replace(',', '_');
			writer.WriteLine(FormattableString.Invariant(
				$"{DateTime.UtcNow:O},{elapsed:F3},{sceneName},{frameTimeCount},{1000.0 / frameTimeAverage:F2},{frameTimeAverage:F3},{getPercentile(0.50f):F3},{getPercentile(0.95f):F3},{getPercentile(0.99f):F3},{frameTimes[frameTimeCount - 1]:F3},{mainThreadAverage:F3},{mainThreadMaximum:F3},{renderThreadAverage:F3},{renderThreadMaximum:F3},{cpuFrameTime:F3},{gpuFrameTime:F3},{gcAllocatedAverage:F3},{gcAllocatedMaximum:F3},{getLastValue(gcUsedMemory) / bytesPerMebibyte:F3},{getLastValue(systemUsedMemory) / bytesPerMebibyte:F3},{getLastValue(drawCalls)},{getLastValue(batches)},{getLastValue(setPassCalls)},{getLastValue(triangles)}"));
			writer.Flush();
		}

		private float getPercentile(float percentile)
		{
			return frameTimes[getPercentileIndex(frameTimeCount, percentile)];
		}

		private static int getPercentileIndex(int count, float percentile)
		{
			return Mathf.Clamp(Mathf.CeilToInt(count * percentile) - 1, 0, count - 1);
		}

		private static void getRecorderTimes(ref ProfilerRecorder recorder, out double average, out double maximum)
		{
			getRecorderValues(ref recorder, nanosecondsToMilliseconds, out average, out maximum);
		}

		private static void getRecorderValues(ref ProfilerRecorder recorder, double scale, out double average, out double maximum)
		{
			average = 0.0;
			maximum = 0.0;
			if (!recorder.Valid || recorder.Count < 1)
				return;

			int count = recorder.Count;
			for (int index = 0; index < count; ++index)
			{
				double value = recorder.GetSample(index).Value * scale;
				average += value;
				maximum = Math.Max(maximum, value);
			}
			average /= count;
			recorder.Reset();
			recorder.Start();
		}

		private static long getLastValue(ProfilerRecorder recorder)
		{
			return recorder.Valid ? recorder.LastValue : 0L;
		}

		private void OnDestroy()
		{
			if (isDisposed)
				return;

			isDisposed = true;
			mainThreadTime.Dispose();
			renderThreadTime.Dispose();
			gcAllocatedInFrame.Dispose();
			gcUsedMemory.Dispose();
			systemUsedMemory.Dispose();
			drawCalls.Dispose();
			batches.Dispose();
			setPassCalls.Dispose();
			triangles.Dispose();
			writer?.Dispose();
			if (instance == this)
				instance = null;
		}

		private const int recorderCapacity = 256;
		private const float sampleInterval = 1.0f;
		private const double nanosecondsToMilliseconds = 1.0 / 1_000_000.0;
		private const double bytesPerMebibyte = 1024.0 * 1024.0;
		private static readonly CommandLineFlag shouldCapture = new CommandLineFlag(false, "-PerformanceMetrics");
		private static readonly CommandLineInt captureDuration = new CommandLineInt("-PerformanceMetricsSeconds");
		private static PerformanceMetricsCapture instance;

		// ponytail: one-second fixed window avoids configuration and per-frame allocations.
		private readonly float[] frameTimes = new float[4096];
		private readonly FrameTiming[] frameTiming = new FrameTiming[1];
		private StreamWriter writer;
		private ProfilerRecorder mainThreadTime;
		private ProfilerRecorder renderThreadTime;
		private ProfilerRecorder gcAllocatedInFrame;
		private ProfilerRecorder gcUsedMemory;
		private ProfilerRecorder systemUsedMemory;
		private ProfilerRecorder drawCalls;
		private ProfilerRecorder batches;
		private ProfilerRecorder setPassCalls;
		private ProfilerRecorder triangles;
		private int frameTimeCount;
		private float elapsed;
		private float windowElapsed;
		private float maximumDuration;
		private double cpuFrameTime;
		private double gpuFrameTime;
		private bool isDisposed;
	}
}
