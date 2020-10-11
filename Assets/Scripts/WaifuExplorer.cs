using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.Networking;

public class WaifuExplorer : MonoBehaviour {
	/// <summary> Tightly packed struct to hold seed information. </summary>
	public unsafe struct Seed {
		/// <summary> Primary numbers for seed. Seem to be allowed anywhere in range [1,1000000) </summary>
		public fixed int nums[16];
		/// <summary> No idea what this does, it's always zero as far as I've seen. </summary>
		public int extra;
		/// <summary> Color summary, acts kinda as a uniquifier. </summary>
		public fixed double color[3];
		/// <summary> Construct a <see cref="Seed"/> from a <see cref="JsonArray"/> source </summary>
		/// <param name="data"><see cref="JsonArray"/> containing exactly 18 elements,
		/// 17 numbers and one array of 3 numbers at the end. </param>
		public Seed(JsonArray data) {
			if (data.Count == 18) {
				for (int i = 0; i < 16; i++) {
					nums[i] = data[i].intVal;
				}
				extra = data[16].intVal;
				JsonArray col = data[17] as JsonArray;
				if (col != null) {
					color[0] = col[0].doubleVal;
					color[1] = col[1].doubleVal;
					color[2] = col[2].doubleVal;
				}
			} else {
				for (int i = 0; i < 16; i++) {
					nums[i] = 0;
				}
				extra = 0;
				color[0] = 0;
				color[1] = 0;
				color[2] = 0;
			}
		}
		/// <summary> Converts this <see cref="Seed"/> back into its <see cref="JsonArray"/> form </summary>
		/// <returns> <see cref="JsonArray"/> containing same information </returns>
		public JsonArray ToJsonArray() {
			JsonArray main = new JsonArray();
			for (int i = 0; i < 16; i++) {
				main.Add(nums[i]);
			}
			main.Add(extra);
			JsonArray col = new JsonArray();
			main.Add(col);
			col.Add(color[0]);
			col.Add(color[1]);
			col.Add(color[2]);
			return main;
		}
		/// <summary> Converts this <see cref="Seed"/> back into plain JSON <see cref="string"/> form </summary>
		/// <returns> <see cref="string"/> containing same information </returns>
		public override string ToString() {
			StringBuilder str = new StringBuilder("[");
			for (int i = 0; i < 16; i++) {
				str.Append(nums[i]);
				str.Append(",");
			}
			str.Append(extra);
			str.Append(",[");
			str.Append(color[0]);
			str.Append(",");
			str.Append(color[1]);
			str.Append(",");
			str.Append(color[2]);
			str.Append("]]");

			return str.ToString();
		}
	}

	/// <summary> Class holding Girl data </summary>
	[Serializable] public class Girl {
		private string _id;
		public string id { 
			get {
				if (_id != null) { return _id; }
				return _id = seeds.ToString();
			}
		}
		private string _compressedID;
		public string compressedID {
			get {
				if (_compressedID != null) { return _compressedID; }
				return _compressedID = Pack.GZipBase64(seed).FilenameSafeBase64();
			}
		}
		public JsonArray seeds;
		public Seed seed;
		public Texture2D big;
		public Texture2D small;

		public Girl(JsonArray seeds, string base64) {
			this.seeds = seeds;
			small = DecodeBase64Png(base64);
			seed = new Seed(seeds);
		}
		public Girl(JsonArray seeds, Texture2D small) {
			this.seeds = seeds;
			this.small = small;
			seed = new Seed(seeds);
		}
	}

	const string GZIP_HEADER = "H4sIAAAAAAAAC";
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

	

	JsonArray UnpackSeedsFromFilename(string filename) {
		
		try {
			JsonArray array = (JsonArray)Json.Parse(filename);
			if (array.Count != 18) { throw new Exception(); }
			return array;
		} catch (Exception) { }
		
		Seed seed;
		if (Unpack.TryGZipBase64(filename.FilenameToBase64(), out seed)) {
			return seed.ToJsonArray();
		}

		return null;
	}

	private string LoadGirlFromFile(string path) {
		Debug.Log($"Attempting to load girl from {path}");
		var file = FromLastSlash(path);
		if (!file.StartsWith(GZIP_HEADER)) {
			file = GZIP_HEADER + file;
		}
		Debug.Log($"Actual file is now {file}");
		var seedText = file.Substring(0, file.LastIndexOf('.'));
		JsonArray arr = UnpackSeedsFromFilename(seedText);
		if (arr != null) {
			string gid = arr.ToString();
			Debug.Log($"got array! {gid}");
			if (!allGirls.ContainsKey(gid)) {
		
				try {
					byte[] data = File.ReadAllBytes(path);
					Texture2D tex = new Texture2D(8, 8);
					tex.LoadImage(data);
					Girl g = new Girl(arr, tex);
					allGirls[g.id] = g;
					return gid;
				} catch (Exception e) {
					Debug.LogWarning($"Failed to load waifu file: {e}");
					return null;
				}
			} else {
				return gid;
			}
		}
		return null;
	}


	bool showingDialog = false;
	bool browserOpen;
	string browserLocation;
	Vector2 browserDirScroll;
	Vector2 browserFileScroll;
	Vector4 prams = new Vector4(640, 400, 0, 0);
	Texture2D loaderTex = null;
	string lastLoaderTex = "";
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
				string id = LoadGirlFromFile( browserLocation );
				if (allGirls.ContainsKey(id)) {
					PushWaifu(id);
				}
			} else {
				if (lastLoaderTex != browserLocation && browserLocation.EndsWith(".png")) {
					string id = LoadGirlFromFile(browserLocation);
					if (id != null) {
						loaderTex = allGirls[id].small;
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
			if (GUILayout.Button("Wtf", CORNER_BUTTON_OPTS)) {
				RandomizeColorsOnBig();
			}
		}
	}

	void RandomizeColorsOnBig() {
		JsonArray deriveNewColors(JsonArray src) {
			JsonArray copy = new JsonArray();
			for (int i = 0; i < src.Count; i++) { copy[i] = src[i]; }
			JsonArray arr = new JsonArray();
			copy[copy.Count-1] = arr;
			arr.Add(UnityEngine.Random.value * 255);
			arr.Add(UnityEngine.Random.value * 255);
			arr.Add(UnityEngine.Random.value * 255);
			return copy;
		}
		Girl currentGirl = allGirls[history[historyIndex]];
		JsonArray derivedSeeds = deriveNewColors(currentGirl.seeds);

		MakeNewGirl(derivedSeeds);
		
	}
	void MakeNewGirl(JsonArray seeds) {
		JsonObject request = new JsonObject("currentGirl", seeds, "step", step, "size", 512);

		StartCoroutine(PostJson($"{baseUrl}/generate_big", request.ToString(), (json) => {
			JsonObject response = Json.Parse<JsonObject>(json);
			Girl newGirl = new Girl(seeds, DecodeBase64Png(response.Get<string>("girl")));
			allGirls[newGirl.id] = newGirl;

			PushWaifu(newGirl.id);

			//primary.material.mainTexture = currentGirl.big;
		}));
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
				string id = LoadGirlFromFile(path);
				if (id != null) {
					PushWaifu(id);
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
			string filename = girl.compressedID;
			if (filename.StartsWith(GZIP_HEADER)) {
				filename = filename.Substring(GZIP_HEADER.Length);
			}
			string filepath = $"waifus/{filename}.png";

			// string filename = $"waifus/{girl.id}.png";
			
			if (!File.Exists(filepath)) {
				File.WriteAllBytes(filepath, pngData);
				saved++;
			}
		}
		Debug.Log($"Checked {allGirls.Count}, Saved {saved} waifus");
	}

	void RefreshBig() {
		Girl currentGirl = allGirls[history[historyIndex]];
		

		/*string packedData = currentGirl.compressedID;
		byte[] base64Decoded = Unpack.RawBase64(packedData);
		byte[] unzipped = GZip.Decompress(base64Decoded);
		Seed unpackedSeed = Unsafe.FromBytes<Seed>(unzipped);
		JsonArray unpackedSeeds = unpackedSeed.ToJsonArray();
		string unpackedJson = unpackedSeeds.ToString();
		
		Debug.Log($"Using waifu. ID = {currentGirl.id}.png"
			+ $"\nRawSeed: {currentGirl.seed}"
			+ $"\nCompressed2: {currentGirl.compressedID}"
			+ $"\nReconstructed Seed: {unpackedSeed}" 
			+ $"\nfully unpacked: {unpackedJson}");
		*/
		

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

internal static class Helpers {
	internal static string FilenameSafeBase64(this string str) { return str.Replace('/', '-'); }
	internal static string FilenameToBase64(this string str) { return str.Replace('-', '/'); }
}
