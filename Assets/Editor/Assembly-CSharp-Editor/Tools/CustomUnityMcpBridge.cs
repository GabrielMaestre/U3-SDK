using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDG.Unturned.Tools
{
	[InitializeOnLoad]
	internal static class CustomUnityMcpBridge
	{
		[Serializable]
		private sealed class BridgeConfig
		{
			public int port;
			public string token;
			public int editorPid;
		}

		[Serializable]
		private sealed class BridgeRequest
		{
			public string token;
			public string action;
			public string query;
			public string path;
			public string menuItem;
			public string mode;
			public int maxResults = 50;
			public int maxDepth = 4;
		}

		[Serializable]
		private sealed class BridgeResponse
		{
			public bool ok;
			public string result;
			public string error;
		}

		private sealed class PendingRequest
		{
			public string json;
			public string response;
			public readonly ManualResetEventSlim completed = new ManualResetEventSlim(false);
		}

		private static readonly ConcurrentQueue<PendingRequest> requests = new ConcurrentQueue<PendingRequest>();
		private static readonly string configPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UserSettings", "CustomUnityMcpBridge.json");
		private static TcpListener listener;
		private static Thread listenerThread;
		private static string sessionToken;

		static CustomUnityMcpBridge()
		{
			EditorApplication.update += ProcessRequests;
			AssemblyReloadEvents.beforeAssemblyReload += Stop;
			EditorApplication.quitting += Stop;
			EditorApplication.delayCall += Start;
		}

		[MenuItem("Tools/Custom Unity MCP/Start")]
		private static void Start()
		{
			if (listener != null)
				return;

			try
			{
				sessionToken = Guid.NewGuid().ToString("N");
				listener = new TcpListener(IPAddress.Loopback, 0);
				listener.Start();
				int port = ((IPEndPoint)listener.LocalEndpoint).Port;
				Directory.CreateDirectory(Path.GetDirectoryName(configPath));
				File.WriteAllText(configPath, JsonUtility.ToJson(new BridgeConfig
				{
					port = port,
					token = sessionToken,
					editorPid = Process.GetCurrentProcess().Id
				}, true));

				listenerThread = new Thread(ListenLoop)
				{
					IsBackground = true,
					Name = "Custom Unity MCP Bridge"
				};
				listenerThread.Start();
				UnityEngine.Debug.Log($"Custom Unity MCP bridge listening on 127.0.0.1:{port}");
			}
			catch (Exception exception)
			{
				Stop();
				UnityEngine.Debug.LogError($"Custom Unity MCP bridge failed to start: {exception.Message}");
			}
		}

		[MenuItem("Tools/Custom Unity MCP/Stop")]
		private static void Stop()
		{
			TcpListener activeListener = listener;
			listener = null;
			activeListener?.Stop();
			listenerThread = null;
			sessionToken = null;

			try
			{
				if (File.Exists(configPath))
					File.Delete(configPath);
			}
			catch (IOException)
			{
			}
		}

		private static void ListenLoop()
		{
			while (listener != null)
			{
				try
				{
					TcpClient client = listener.AcceptTcpClient();
					ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
				}
				catch (SocketException)
				{
					if (listener != null)
						UnityEngine.Debug.LogWarning("Custom Unity MCP bridge socket stopped unexpectedly.");
				}
				catch (ObjectDisposedException)
				{
				}
			}
		}

		private static void HandleClient(TcpClient client)
		{
			using (client)
			{
				client.ReceiveTimeout = 10000;
				client.SendTimeout = 10000;
				using (NetworkStream stream = client.GetStream())
				using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
				using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
				{
					string json = reader.ReadLine();
					if (string.IsNullOrEmpty(json))
						return;

					PendingRequest pending = new PendingRequest { json = json };
					requests.Enqueue(pending);
					if (!pending.completed.Wait(10000))
						pending.response = Error("Unity main thread timed out.");
					writer.WriteLine(pending.response);
				}
			}
		}

		private static void ProcessRequests()
		{
			while (requests.TryDequeue(out PendingRequest pending))
			{
				try
				{
					pending.response = Handle(pending.json);
				}
				catch (Exception exception)
				{
					pending.response = Error(exception.Message);
				}
				finally
				{
					pending.completed.Set();
				}
			}
		}

		private static string Handle(string json)
		{
			BridgeRequest request = JsonUtility.FromJson<BridgeRequest>(json);
			if (request == null || request.token != sessionToken)
				return Error("Unauthorized local MCP request.");

			switch (request.action)
			{
				case "status": return Success(GetStatus());
				case "list_scenes": return Success(ListScenes(request.maxResults));
				case "scene_hierarchy": return Success(GetSceneHierarchy(request.maxDepth));
				case "find_assets": return Success(FindAssets(request.query, request.maxResults));
				case "asset_info": return Success(GetAssetInfo(request.path));
				case "select_object": return Success(SelectObject(request.path));
				case "execute_menu_item": return Success($"executed={EditorApplication.ExecuteMenuItem(request.menuItem)}");
				case "set_play_mode": return Success(SetPlayMode(request.mode));
				case "refresh_assets": AssetDatabase.Refresh(); return Success("Asset database refreshed.");
				case "save_scenes": return Success($"saved={EditorSceneManager.SaveOpenScenes()}");
				default: return Error($"Unknown action: {request.action}");
			}
		}

		private static string GetStatus()
		{
			Scene scene = SceneManager.GetActiveScene();
			return $"unity={Application.unityVersion}\nproject={Directory.GetParent(Application.dataPath).FullName}\npid={Process.GetCurrentProcess().Id}\nplaying={EditorApplication.isPlaying}\npaused={EditorApplication.isPaused}\ncompiling={EditorApplication.isCompiling}\nupdating={EditorApplication.isUpdating}\nactiveScene={scene.path}";
		}

		private static string ListScenes(int requestedMax)
		{
			int max = Mathf.Clamp(requestedMax, 1, 500);
			string[] guids = AssetDatabase.FindAssets("t:Scene");
			StringBuilder result = new StringBuilder();
			for (int index = 0; index < guids.Length && index < max; ++index)
				result.AppendLine(AssetDatabase.GUIDToAssetPath(guids[index]));
			return result.Length > 0 ? result.ToString().TrimEnd() : "No scenes found.";
		}

		private static string GetSceneHierarchy(int requestedDepth)
		{
			int maxDepth = Mathf.Clamp(requestedDepth, 0, 12);
			Scene scene = SceneManager.GetActiveScene();
			StringBuilder result = new StringBuilder($"Scene: {scene.path}\n");
			foreach (GameObject root in scene.GetRootGameObjects())
				AppendTransform(result, root.transform, 0, maxDepth);
			return result.ToString().TrimEnd();
		}

		private static void AppendTransform(StringBuilder result, Transform transform, int depth, int maxDepth)
		{
			result.Append(' ', depth * 2).Append("- ").Append(transform.name).Append(" [").Append(transform.gameObject.activeSelf ? "active" : "inactive").AppendLine("]");
			if (depth >= maxDepth)
				return;
			for (int index = 0; index < transform.childCount; ++index)
				AppendTransform(result, transform.GetChild(index), depth + 1, maxDepth);
		}

		private static string FindAssets(string query, int requestedMax)
		{
			if (string.IsNullOrWhiteSpace(query))
				throw new ArgumentException("Asset query is required.");
			int max = Mathf.Clamp(requestedMax, 1, 500);
			string[] guids = AssetDatabase.FindAssets(query);
			StringBuilder result = new StringBuilder();
			for (int index = 0; index < guids.Length && index < max; ++index)
				result.AppendLine(AssetDatabase.GUIDToAssetPath(guids[index]));
			return result.Length > 0 ? result.ToString().TrimEnd() : "No assets found.";
		}

		private static string GetAssetInfo(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentException("Asset path is required.");
			UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
			if (asset == null)
				throw new FileNotFoundException("Asset not found.", path);
			string[] dependencies = AssetDatabase.GetDependencies(path, true);
			return $"name={asset.name}\ntype={asset.GetType().FullName}\npath={path}\ndependencies={dependencies.Length}\n{string.Join("\n", dependencies)}";
		}

		private static string SelectObject(string pathOrName)
		{
			if (string.IsNullOrWhiteSpace(pathOrName))
				throw new ArgumentException("Asset path or GameObject name is required.");

			UnityEngine.Object selected = AssetDatabase.LoadMainAssetAtPath(pathOrName);
			if (selected == null)
			{
				foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
				{
					if (candidate.scene.IsValid() && candidate.name == pathOrName)
					{
						selected = candidate;
						break;
					}
				}
			}

			if (selected == null)
				throw new InvalidOperationException($"Object not found: {pathOrName}");
			Selection.activeObject = selected;
			EditorGUIUtility.PingObject(selected);
			return $"selected={selected.name}\ntype={selected.GetType().FullName}";
		}

		private static string SetPlayMode(string mode)
		{
			switch (mode)
			{
				case "play": EditorApplication.isPlaying = true; break;
				case "stop": EditorApplication.isPlaying = false; break;
				case "pause": EditorApplication.isPaused = true; break;
				case "resume": EditorApplication.isPaused = false; break;
				default: throw new ArgumentException("Mode must be play, stop, pause, or resume.");
			}
			return $"mode={mode}";
		}

		private static string Success(string result) => JsonUtility.ToJson(new BridgeResponse { ok = true, result = result ?? string.Empty });
		private static string Error(string error) => JsonUtility.ToJson(new BridgeResponse { ok = false, error = error ?? "Unknown error." });
	}
}
