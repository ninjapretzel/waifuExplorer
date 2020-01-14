using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.Networking;

public class WaifuExplorer : MonoBehaviour {

	[Serializable] public class Girl {
		private string _id;
		public string id { 
			get {
				if (_id != null) { return _id; }
				return _id = seeds.ToString();
			}
		}
		public JsonArray seeds;
		public Texture2D big;
		public Texture2D small;
		public Girl(JsonArray seeds, string base64) {
			this.seeds = seeds;
			small = DecodeBase64Png(base64);
		}
		public Girl(JsonArray seeds, Texture2D small) {
			this.seeds = seeds;
			this.small = small;
		}
	}

	public List<Renderer> renderers;
	public Renderer primary;
	public Renderer previousGirl;
	public Renderer nextGirl;
	
	public List<Girl> grid;
	public List<string> history;
	public List<List<Girl>> grids;
	public Dictionary<string, Girl> allGirls;

	public GUISkin skin;


	
	
	public int historyIndex = -1;
	public int prevGirlsIndex = -1;
	
	
	public int step = 1;

	public JsonObject initialRequest;

	public string baseUrl = "https://api.waifulabs.com";
	public bool goOnStart = true;
	public Texture2D unsetTexture;
	public Texture2D loadingTexture;
	Texture2D pixel;

	bool browserOpen;
	string browserLocation;
	Vector2 browserDirScroll;
	Vector2 browserFileScroll;

	GUILayoutOption[] CORNER_BUTTON_OPTS = new GUILayoutOption[] { GUILayout.Width(260), GUILayout.Height(24) };

	void Awake() {
		renderers = new List<Renderer>();
		grids = new List<List<Girl>>();
		for (int i = 1; i <= 16; i++) {
			string name = $"Quad{i:d2}";
			GameObject o = GameObject.Find(name);
			if (o != null) {
				Renderer r = o.GetComponent<Renderer>();
				if (r != null) { renderers.Add(r); }
			}
		}
		initialRequest = new JsonObject("step", 0);
		allGirls = new Dictionary<string, Girl>();
		history = new List<string>();
		browserLocation = "waifus";
		if (!Directory.Exists("waifus")) { Directory.CreateDirectory("waifus"); }
		pixel = Resources.Load<Texture2D>("pixel");
	}

	void Start() {
		if (goOnStart) {
			InitialStep();
		}
	}
	
	void Update() {
		if (showingDialog || browserOpen) {
			return;
		}

		if (Input.GetMouseButtonDown(0)) {
			Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
			RaycastHit rayhit;

			if (Physics.Raycast(mouseRay, out rayhit)) {
				Renderer r = rayhit.collider.GetComponent<Renderer>();
				if (r != null) {
					if (r == previousGirl) {
						ClickedPrevious();
					} else if (r == nextGirl) {
						ClickedNext();	
					} else {
						int i = renderers.IndexOf(r);
						if (i > -1) {
							ClickedWaifu(i);

						}
					}
				}
			}
		}
	}

	Texture2D loaderTex = null;
	string lastLoaderTex = "";
	bool showingDialog = false;
	Vector4 prams = new Vector4(640, 400, 0, 0);
	void OnGUI() {
		GUI.skin = skin;
		if (browserOpen || showingDialog) {
			GUI.color = new Color(0,0,0,.5f);
			GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), pixel);
			GUI.color = Color.white;
		}

		if (showingDialog) { return; }

		if (GUILayout.Button("Open Waifu Dialog (no preview, fast)", CORNER_BUTTON_OPTS)) {
			OpenWaifuDialog();
		}

		if (GUILayout.Button("Toggle Waifu Browser (slow when populated)", CORNER_BUTTON_OPTS)) {
			browserOpen = !browserOpen;
		}
			
		if (browserOpen) {

			if (FileBrowser(ref browserLocation, ref browserDirScroll, ref browserFileScroll, prams)) {
				string seeds = FromLastSlash( browserLocation ).Replace(".png", "");
				if (allGirls.ContainsKey(seeds)) {
					PushWaifu(seeds);
				}
			} else {
				if (lastLoaderTex != browserLocation && browserLocation.EndsWith(".png")) {
					
					string seeds = FromLastSlash(browserLocation).Replace(".png", "");
					if (!allGirls.ContainsKey(seeds)) {
						
						try {
							JsonArray arr = Json.Parse<JsonArray>(seeds);
							byte[] data = File.ReadAllBytes(browserLocation);
							Texture2D tex = new Texture2D(8, 8);
							tex.LoadImage(data);
							loaderTex = tex;
							
							Girl g = new Girl(arr, tex);
							allGirls[g.id] = g;
							lastLoaderTex = browserLocation;

						} catch (Exception e) {
							Debug.LogWarning($"Failed to load girl file {e}");

						}
					} else {
						loaderTex = allGirls[seeds].small;
						lastLoaderTex = browserLocation;
					}
				} else {
					if (!browserLocation.EndsWith(".png")) {
						loaderTex = null;
						lastLoaderTex = browserLocation;
					}
				}
			}
			GUILayout.BeginVertical("box"); {
				GUILayout.Label("Preview:");
				GUILayout.Box(loaderTex != null ? new GUIContent(loaderTex) : new GUIContent(""), GUILayout.Height(210), GUILayout.Width(210));
			} GUILayout.EndVertical();
		} else {
			if (GUILayout.Button("Pick new waifus", CORNER_BUTTON_OPTS)) {
				InitialStep();
			}
			if (history.Count > 0) {
				if (GUILayout.Button("Change Palette", CORNER_BUTTON_OPTS)) {
					step = 1;
					RefreshSmall();
				}
				if (GUILayout.Button("Change Details", CORNER_BUTTON_OPTS)) {
					step = 2;
					RefreshSmall();
				}
				if (GUILayout.Button("Change Pose", CORNER_BUTTON_OPTS)) {
					step = 3;
					RefreshSmall();
				}

			}
			if (GUILayout.Button("Save Waifus", CORNER_BUTTON_OPTS)) {
				SaveGirls();
			}
		}
	}

	void OpenWaifuDialog() {
		OpenFileDialog dialog = new OpenFileDialog() ;
		// dialog.InitialDirectory = Directory.GetCurrentDirectory() + "/waifus";
		dialog.InitialDirectory = "waifus";
		dialog.Filter = "Waifu Pictures (*.png)|*.png|Im brave- all files (*.*)|*.*";
		showingDialog = true;
		dialog.ShowDialog((form, result) => { 
			//Debug.Log($"Show Dialog finished: {result}");
			if (result == DialogResult.OK) {
				var path = dialog.FileName;
				//Debug.Log($"Path: {path}");
				var seeds = FromLastSlash(path);
				//Debug.Log($"Fname: {seeds}");
				seeds = seeds.Substring(0, seeds.LastIndexOf('.'));
				//Debug.Log($"Fname2: {seeds}");
				try {
					JsonArray arr = Json.Parse<JsonArray>(seeds);
					byte[] data = File.ReadAllBytes(path);
					Texture2D tex = new Texture2D(8, 8);
					tex.LoadImage(data);
					Girl g = new Girl(arr, tex);
					allGirls[g.id] = g;

					PushWaifu(g.id);
				} catch (Exception e) {
					Debug.LogWarning($"Failed to load waifu file: {e}");
				}
			}
			showingDialog = false;
		});
	}

	void ClickedPrevious() {
		if (historyIndex-1 >= 0) {
			historyIndex--;
			RefreshBig();
		}

	}
	void ClickedNext() {
		if (historyIndex+1 < history.Count) {
			historyIndex++;
			RefreshBig();
		}
	}

	void ClickedWaifu(int i) {
		// JsonObject girlData = currentGirls.Get<JsonObject>(i);
		PushWaifu(grid[i].id);
	}


	void PushWaifu(string id) {
		history.Add(id);
		historyIndex = history.Count-1;
		RefreshBig();
	}

	void InitialStep() {
		for (int i = 0; i < renderers.Count; i++) {
			renderers[i].material.mainTexture = loadingTexture;
		}

		StartCoroutine(PostJson($"{baseUrl}/generate", initialRequest.ToString(), LoadNextGirlGridFromResponse));
	}

	void SaveGirls() {
		if (!Directory.Exists("waifus")) { Directory.CreateDirectory("waifus"); }
		int saved = 0;
		foreach (var pair in allGirls) {
			Girl girl = pair.Value;
			byte[] pngData = girl.small.EncodeToPNG();
			string filename = $"waifus/{girl.id}.png";
			
			if (!File.Exists(filename)) {
				File.WriteAllBytes(filename, pngData);
				saved++;
			}
		}
		Debug.Log($"Checked {allGirls.Count}, Saved {saved} waifus");
	}

	void RefreshBig() {
		Girl currentGirl = allGirls[history[historyIndex]];

		JsonObject request = new JsonObject("currentGirl", currentGirl.seeds, "step", step, "size", 512);
		if (!currentGirl.big) {
			primary.material.mainTexture = loadingTexture;
			StartCoroutine(PostJson($"{baseUrl}/generate_big", request.ToString(), (json) => {
				JsonObject response = Json.Parse<JsonObject>(json);
				currentGirl.big = DecodeBase64Png(response.Get<string>("girl"));
				primary.material.mainTexture = currentGirl.big;
			}));

		} else {
			primary.material.mainTexture = currentGirl.big;
		}

		request["size"] = 128;
		if (history.Count > 0) {
			int before = historyIndex - 1;
			if (before >= 0 && before < history.Count) {
				string prevId = history[before];
				Girl prev = allGirls.ContainsKey(prevId) ? allGirls[prevId] : null;
				if (prev.big == null) {
					previousGirl.material.mainTexture = loadingTexture;
					request["currentGirl"] = prev.seeds;

					StartCoroutine(PostJson($"{baseUrl}/generate_big", request.ToString(), (json) => {
						JsonObject response = Json.Parse<JsonObject>(json);
						prev.big = DecodeBase64Png(response.Get<string>("girl"));
						previousGirl.material.mainTexture = prev.big;
					}));
				} else {
					previousGirl.material.mainTexture = prev.big;
				}
			} else {
				previousGirl.material.mainTexture = unsetTexture;
			}
			
			int after = historyIndex + 1;
			if (after >= 0 && after < history.Count) {
				string nextId = history[after];
				Girl next = allGirls.ContainsKey(nextId) ? allGirls[nextId] : null;
				if (next.big == null) {
					nextGirl.material.mainTexture = loadingTexture;
					request["currentGirl"] = next.seeds;
					StartCoroutine(PostJson($"{baseUrl}/generate_big", request.ToString(), (json) => {
						JsonObject response = Json.Parse<JsonObject>(json);
						next.big = DecodeBase64Png(response.Get<string>("girl"));
						nextGirl.material.mainTexture = next.big;
					}));
				} else {
					nextGirl.material.mainTexture = next.big;
				}
			} else {
				nextGirl.material.mainTexture = unsetTexture;
			}
		}

	}

	void RefreshSmall() {
		for (int i = 0; i < renderers.Count; i++) {
			renderers[i].material.mainTexture = loadingTexture;
		}

		Girl currentGirl = allGirls[history[historyIndex]];
		JsonObject request = new JsonObject("currentGirl", currentGirl.seeds, "step", step);
		
		StartCoroutine(PostJson($"{baseUrl}/generate", request.ToString(), LoadNextGirlGridFromResponse));
	}

	void LoadNextGirlGridFromResponse(string json) {
		JsonObject response = Json.Parse<JsonObject>(json);
		JsonArray girls = response.Get<JsonArray>("newGirls");
		if (grid != null) {
			grids.Add(grid);
		}
		grid = LoadGrid(girls);
		ShowGrid(grid);
	}

	void ShowGrid(List<Girl> grid) {
		for (int i = 0; i < grid.Count; i++) {
			if (i < renderers.Count) {
				Renderer r = renderers[i];
				r.material.mainTexture = grid[i].small;
			}
		}
	}


	static Texture2D DecodeBase64Png(string pngData) {
		byte[] decoded = System.Convert.FromBase64String(pngData);
		Texture2D tex = new Texture2D(8, 8);
		// Texture2D tex = new Texture2D(16, 16, TextureFormat.DXT5, false);
		tex.LoadImage(decoded);
		return tex;
	}

	List<Girl> LoadGrid(JsonArray girls) {
		List<Girl> grid = new List<Girl>();

		for (int i = 0; i < girls.Count; i++) {
			JsonObject data = girls.Get<JsonObject>(i);
			JsonArray seeds = data.Get<JsonArray>("seeds");
			string id = seeds.ToString();
			if (allGirls.ContainsKey(id)) {
				grid.Add(allGirls[id]);
			} else {
				string base64 = data.Get<string>("image");
				Girl g = new Girl(seeds, base64);
				grid.Add(g);
				allGirls[id] = g;
			}
		}
		SaveGirls();
		return grid;
	}

	IEnumerator PostJson(string url, string payload, Action<string> callback) {
		using (var www = new UnityWebRequest(url, "POST")) {
			byte[] raw = Encoding.UTF8.GetBytes(payload);
			www.uploadHandler = new UploadHandlerRaw(raw);
			www.downloadHandler = new DownloadHandlerBuffer();
			www.SetRequestHeader("Content-Type", "application/json");

			yield return www.SendWebRequest();

			if (www.isNetworkError || www.isHttpError) {

				Debug.LogWarning("Network Error: " + www.error);
				try {
					byte[] rawGot = www.downloadHandler.data;
					string data = Encoding.UTF8.GetString(rawGot);

					Debug.LogWarning(data);
				} catch (Exception e) {
					Debug.LogWarning("Could not read data.");
				}

			} else {
				try {
					byte[] rawGot = www.downloadHandler.data;
					string json = Encoding.UTF8.GetString(rawGot);

					callback(json);

				} catch (Exception e) {
					Debug.LogWarning("Error during callback: " + e);
				}
			}
		}
	}


	public static bool FileBrowser(ref string location, ref Vector2 directoryScroll, ref Vector2 fileScroll, Vector4? prams = null) {
		bool complete;
		DirectoryInfo directoryInfo;
		DirectoryInfo directorySelection;
		FileInfo fileSelection;
		int contentWidth;
		Vector4 parameters = prams ?? new Vector4(420, 300, 0, 0);
		float width = parameters.x;
		float height = parameters.y;
		
		// Our return state - altered by the "Select" button
		complete = false;

		// Get the directory info of the current location
		fileSelection = new FileInfo(location);
		if ((fileSelection.Attributes & FileAttributes.Directory) == FileAttributes.Directory) {
			directoryInfo = new DirectoryInfo(location);
		} else {
			directoryInfo = fileSelection.Directory;
		}
		
		GUILayout.BeginVertical("box", GUILayout.Width(width)); {
		
			if (location != "/" && GUILayout.Button("Up one level", GUILayout.Width(width-10))) {
				directoryInfo = directoryInfo.Parent;
				location = directoryInfo.FullName;
			}
		
			// Handle the directories list
			GUILayout.BeginHorizontal("box", GUILayout.Height(height)); {
				GUILayout.BeginVertical( GUILayout.Width((width - 10) / 2) ); {
					GUILayout.Label("Directories:");
					directoryScroll = GUILayout.BeginScrollView(directoryScroll);
					directorySelection = SelectList(directoryInfo.GetDirectories(), null, DirToString);
					GUILayout.EndScrollView();
				} GUILayout.EndVertical();

				if (directorySelection != null) {
					// If a directory was selected, jump there
					location = directorySelection.FullName;
				}
		
				// Handle the files list
				GUILayout.BeginVertical(GUILayout.Width((width - 10) / 2)); {
					GUILayout.Label("Files:");
					fileScroll = GUILayout.BeginScrollView(fileScroll);
					fileSelection = SelectList(directoryInfo.GetFiles(), null, FileToString);
					GUILayout.EndScrollView();
				} GUILayout.EndVertical();

			}  GUILayout.EndHorizontal();

			if (fileSelection != null) {
				// If a file was selected, update our location to it
				location = fileSelection.FullName;
			}
		
			// The manual location box and the select button
			
			GUILayout.BeginHorizontal(); {
				location = GUILayout.TextArea(location);

				contentWidth = (int)GUI.skin.GetStyle("Button").CalcSize(new GUIContent("Select")).x;
				if (GUILayout.Button("Select", GUILayout.Width(contentWidth))) {
					complete = true;
				}
			} GUILayout.EndHorizontal();
			
		} GUILayout.EndVertical();
		return complete;
	}

	private static string DirToString(DirectoryInfo dir) {
		string s = dir.ToString();
		return Truncate(FromLastSlash(s));
	}
	private static string FileToString(FileInfo file) {
		string s = file.ToString();
		return Truncate(FromLastSlash(s));
	}
	private static string Truncate(string s, int length = 32) {
		if (s.Length > length) {
			return s.Substring(0, length) + "...";
		}
		return s;
	}
	private static string FromLastSlash(string s) {
		s = s.ToString().Replace('\\', '/');
		int ind = s.LastIndexOf("/");
		if (ind >= 0) {
			s = s.Substring(ind+1);
		}
		return s;
	}
	
	public static T SelectList<T>(IEnumerable<T> list, T selected, Func<T, string> toString, GUIStyle defaultStyle = null, GUIStyle selectedStyle = null) where T : class {
		if (defaultStyle == null) { defaultStyle = GUI.skin.GetStyle("Button"); }
		if (selectedStyle == null) { selectedStyle = GUI.skin.GetStyle("Button"); }
		TextAnchor defaultAlign = defaultStyle.alignment;
		TextAnchor selectedAlign = selectedStyle.alignment;
		defaultStyle.alignment = TextAnchor.MiddleLeft;
		selectedStyle.alignment = TextAnchor.MiddleLeft;
		foreach (T item in list) {
			if (item == null) { continue; }
			if (GUILayout.Button(toString(item), (item.Equals( selected)) ? selectedStyle : defaultStyle)) {
				if (item.Equals(selected)) {
					// Clicked an already selected item. Deselect.
					selected = null;
				} else {
					selected = item;
				}
			}
		}
		defaultStyle.alignment = defaultAlign;
		selectedStyle.alignment = selectedAlign;

		return selected;
	}

	
}
