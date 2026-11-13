using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Wardline
{
    public class MapTheme
    {
        public string id;
        public Color floor, wall, wallTrim, crate, platform, site, sky, fog, ambient, sun;
        public string floorTex, wallTex, crateTex = "crate";
        public float sunIntensity = 1.2f, fogStart = 30f, fogEnd = 140f;
        public Vector3 sunEuler = new Vector3(50, 35, 0);
        public bool ceiling, trees;
        public string ambience;

        public static MapTheme Get(string id)
        {
            switch (id)
            {
                case "urban": return new MapTheme { id = id, floor = new Color(0.42f, 0.42f, 0.43f), wall = new Color(0.56f, 0.55f, 0.53f), wallTrim = new Color(0.3f, 0.3f, 0.32f), crate = new Color(0.45f, 0.38f, 0.28f), platform = new Color(0.36f, 0.37f, 0.4f), site = new Color(0.55f, 0.48f, 0.2f),
                    sky = new Color(0.55f, 0.6f, 0.66f), fog = new Color(0.58f, 0.62f, 0.67f), ambient = new Color(0.5f, 0.52f, 0.56f), sun = new Color(0.95f, 0.95f, 1f), sunIntensity = 0.9f, floorTex = "panel", wallTex = "brick", fogStart = 25f, fogEnd = 120f, ambience = "ambient_wind" };
                case "arctic": return new MapTheme { id = id, floor = new Color(0.86f, 0.89f, 0.93f), wall = new Color(0.42f, 0.47f, 0.52f), wallTrim = new Color(0.75f, 0.3f, 0.12f), crate = new Color(0.35f, 0.42f, 0.35f), platform = new Color(0.5f, 0.53f, 0.56f), site = new Color(0.9f, 0.55f, 0.2f),
                    sky = new Color(0.78f, 0.84f, 0.9f), fog = new Color(0.82f, 0.86f, 0.92f), ambient = new Color(0.62f, 0.68f, 0.76f), sun = new Color(0.9f, 0.95f, 1f), sunIntensity = 1.1f, floorTex = "snow", wallTex = "panel", fogStart = 18f, fogEnd = 95f, sunEuler = new Vector3(28, 140, 0), ambience = "ambient_wind" };
                case "lab": return new MapTheme { id = id, floor = new Color(0.55f, 0.58f, 0.6f), wall = new Color(0.72f, 0.74f, 0.76f), wallTrim = new Color(0.2f, 0.45f, 0.5f), crate = new Color(0.3f, 0.33f, 0.36f), platform = new Color(0.4f, 0.43f, 0.46f), site = new Color(0.85f, 0.75f, 0.1f),
                    sky = new Color(0.05f, 0.06f, 0.07f), fog = new Color(0.1f, 0.12f, 0.13f), ambient = new Color(0.42f, 0.46f, 0.5f), sun = Color.white, sunIntensity = 0f, floorTex = "tile", wallTex = "panel", fogStart = 20f, fogEnd = 80f, ceiling = true, ambience = "ambient_hum" };
                case "tropical": return new MapTheme { id = id, floor = new Color(0.36f, 0.48f, 0.24f), wall = new Color(0.55f, 0.47f, 0.36f), wallTrim = new Color(0.3f, 0.24f, 0.16f), crate = new Color(0.33f, 0.4f, 0.24f), platform = new Color(0.45f, 0.36f, 0.24f), site = new Color(0.7f, 0.25f, 0.15f),
                    sky = new Color(0.62f, 0.78f, 0.82f), fog = new Color(0.55f, 0.68f, 0.6f), ambient = new Color(0.48f, 0.55f, 0.45f), sun = new Color(1f, 0.95f, 0.8f), sunIntensity = 1.15f, floorTex = "grass", wallTex = "planks", fogStart = 15f, fogEnd = 100f, trees = true, ambience = "ambient_jungle" };
                default: return new MapTheme { id = "desert", floor = new Color(0.82f, 0.7f, 0.5f), wall = new Color(0.84f, 0.73f, 0.55f), wallTrim = new Color(0.6f, 0.48f, 0.32f), crate = new Color(0.55f, 0.42f, 0.26f), platform = new Color(0.72f, 0.6f, 0.42f), site = new Color(0.75f, 0.3f, 0.15f),
                    sky = new Color(0.72f, 0.83f, 0.92f), fog = new Color(0.86f, 0.82f, 0.74f), ambient = new Color(0.66f, 0.6f, 0.52f), sun = new Color(1f, 0.93f, 0.78f), sunIntensity = 1.3f, floorTex = "sand", wallTex = "brick", fogStart = 40f, fogEnd = 160f, ambience = "ambient_wind" };
            }
        }
    }

    /// <summary>Parsed map file (Resources/Maps/*.txt). See Docs/MAP_FORMAT.md.</summary>
    public class MapDefinition
    {
        public string id, name, description, themeId;
        public string[] grid;
        public int width, height;
        public MapTheme Theme => MapTheme.Get(themeId);

        public char At(int x, int z) => z < 0 || z >= height || x < 0 || x >= width ? '#' : grid[z][x];

        public static MapDefinition Parse(string id, string text)
        {
            var def = new MapDefinition { id = id };
            var rows = new List<string>();
            bool body = false;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                if (!body)
                {
                    if (raw.Trim() == "---") { body = true; continue; }
                    int c = raw.IndexOf(':');
                    if (c < 0) continue;
                    string k = raw.Substring(0, c).Trim(), v = raw.Substring(c + 1).Trim();
                    if (k == "name") def.name = v; else if (k == "description") def.description = v; else if (k == "theme") def.themeId = v;
                }
                else if (raw.Trim().Length > 0) rows.Add(raw.TrimEnd());
            }
            def.height = rows.Count;
            foreach (var r in rows) def.width = Mathf.Max(def.width, r.Length);
            def.grid = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++) def.grid[i] = rows[i].PadRight(def.width, '#');
            return def;
        }
    }

    public static class MapLibrary
    {
        static List<MapDefinition> maps;
        static readonly string[] Order = { "desert_strike", "urban_assault", "arctic_base", "research_facility", "tropical_outpost" };

        public static List<MapDefinition> All
        {
            get
            {
                if (maps != null) return maps;
                maps = new List<MapDefinition>();
                var found = new Dictionary<string, TextAsset>();
                foreach (var ta in Resources.LoadAll<TextAsset>("Maps")) found[ta.name] = ta;
                foreach (var id in Order) if (found.TryGetValue(id, out var t)) { maps.Add(MapDefinition.Parse(id, t.text)); found.Remove(id); }
                foreach (var kv in found) maps.Add(MapDefinition.Parse(kv.Key, kv.Value.text));   // extra user maps
                return maps;
            }
        }

        static readonly Dictionary<string, Texture2D> previews = new Dictionary<string, Texture2D>();

        /// <summary>Top-down tactical preview generated from the layout (used in map select and the radar).</summary>
        public static Texture2D Preview(MapDefinition m)
        {
            if (previews.TryGetValue(m.id, out var t) && t) return t;
            const int P = 8;
            t = new Texture2D(m.width * P, m.height * P, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var th = m.Theme;
            var px = new Color32[t.width * t.height];
            for (int z = 0; z < m.height; z++)
            for (int x = 0; x < m.width; x++)
            {
                char c = m.At(x, z);
                Color col;
                switch (c)
                {
                    case '#': col = Color.Lerp(th.wall, Color.black, 0.6f); break;
                    case 'c': case 'w': col = Color.Lerp(th.crate, Color.black, 0.25f); break;
                    case 'h': col = Color.Lerp(th.crate, Color.black, 0.45f); break;
                    case 'o': col = th.trees ? new Color(0.15f, 0.3f, 0.12f) : Color.Lerp(th.wall, Color.black, 0.4f); break;
                    case 'p': col = Color.Lerp(th.floor, Color.white, 0.25f); break;
                    case 'r': col = Color.Lerp(th.floor, Color.white, 0.12f); break;
                    case 'A': col = Color.Lerp(th.floor, new Color(0.9f, 0.2f, 0.15f), 0.45f); break;
                    case 'B': col = Color.Lerp(th.floor, new Color(0.15f, 0.35f, 0.95f), 0.45f); break;
                    case 'R': col = Color.Lerp(th.floor, Team.Raiders.Tint(), 0.5f); break;
                    case 'W': col = Color.Lerp(th.floor, Team.Wardens.Tint(), 0.5f); break;
                    default: col = th.floor; break;
                }
                for (int py = 0; py < P; py++)
                for (int pxx = 0; pxx < P; pxx++)
                {
                    int ix = x * P + pxx, iy = (m.height - 1 - z) * P + py;   // texture origin is bottom-left; row 0 is north
                    Color cc = col;
                    if (c != '#' && (pxx == 0 || py == 0)) cc *= 0.94f;
                    px[iy * t.width + ix] = cc;
                }
            }
            t.SetPixels32(px);
            t.Apply();
            previews[m.id] = t;
            return t;
        }
    }

    /// <summary>Runtime representation of a built map: spawns, sites, tactical points, nav data.</summary>
    public class MapRuntime
    {
        public MapDefinition def;
        public GameObject root;
        public List<Vector3> raiderSpawns = new List<Vector3>(), wardenSpawns = new List<Vector3>();
        public List<Vector3> siteA = new List<Vector3>(), siteB = new List<Vector3>();
        public Vector3 siteACenter, siteBCenter;
        public Bounds siteABounds, siteBBounds;
        public Dictionary<char, List<Vector3>> lanes = new Dictionary<char, List<Vector3>>();
        public List<Vector3> guardSpots = new List<Vector3>(), allFloor = new List<Vector3>(), platforms = new List<Vector3>();
        public Vector3 raiderSpawnCenter, wardenSpawnCenter;
        public NavMeshDataInstance navInstance;
        public float raiderFacing, wardenFacing;

        public char laneA = '1', laneB = '3', laneMid = '2';

        public Vector3 SiteCenter(int site) => site == 0 ? siteACenter : siteBCenter;

        /// <summary>Works out which lane feeds which site, purely from geometry.</summary>
        public void IdentifyLanes()
        {
            var avg = new Dictionary<char, Vector3>();
            foreach (var kv in lanes)
            {
                Vector3 s = Vector3.zero; foreach (var p in kv.Value) s += p; avg[kv.Key] = s / kv.Value.Count;
            }
            if (avg.Count < 3) return;
            char bestA = laneA, bestB = laneB; float dA = float.MaxValue, dB = float.MaxValue;
            foreach (var kv in avg) { float d = Vector3.Distance(kv.Value, siteACenter); if (d < dA) { dA = d; bestA = kv.Key; } }
            foreach (var kv in avg) { if (kv.Key == bestA) continue; float d = Vector3.Distance(kv.Value, siteBCenter); if (d < dB) { dB = d; bestB = kv.Key; } }
            foreach (var kv in avg) if (kv.Key != bestA && kv.Key != bestB) laneMid = kv.Key;
            laneA = bestA; laneB = bestB;
        }

        /// <summary>Human-readable callout name for a position ("at A", "in mid", "B lane"...).</summary>
        public string AreaName(Vector3 p)
        {
            if (SiteAt(p) == 0) return "at A";
            if (SiteAt(p) == 1) return "at B";
            string best = "nearby"; float bd = float.MaxValue;
            void Check(Vector3 c, string name) { float d = Vector3.Distance(c.Flat(), p.Flat()); if (d < bd) { bd = d; best = name; } }
            Check(siteACenter, "near A"); Check(siteBCenter, "near B");
            Check(raiderSpawnCenter, "near Raider spawn"); Check(wardenSpawnCenter, "near Warden spawn");
            foreach (var kv in lanes)
            {
                string name = kv.Key == laneA ? "in A lane" : kv.Key == laneB ? "in B lane" : "in mid";
                foreach (var lp in kv.Value) Check(lp, name);
            }
            return best;
        }
        public List<Vector3> SiteCells(int site) => site == 0 ? siteA : siteB;
        public string SiteName(int site) => site == 0 ? "A" : "B";

        /// <summary>Returns 0 (A), 1 (B) or -1 if the position is not inside a bomb site.</summary>
        public int SiteAt(Vector3 p)
        {
            if (InSite(siteABounds, p)) return 0;
            if (InSite(siteBBounds, p)) return 1;
            return -1;
        }

        static bool InSite(Bounds b, Vector3 p) => p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && p.y < b.max.y + 3f;

        public bool InBuyZone(Team t, Vector3 p)
        {
            var list = t == Team.Raiders ? raiderSpawns : wardenSpawns;
            foreach (var s in list) if ((s - p).Flat().sqrMagnitude < MapBuilder.Cell * 2.2f * MapBuilder.Cell * 2.2f) return true;
            return false;
        }

        public List<Vector3> LanePoints(char lane) => lanes.TryGetValue(lane, out var l) ? l : null;

        public Vector3 RandomFloor() => allFloor[Random.Range(0, allFloor.Count)];

        public void Destroy()
        {
            if (navInstance.valid) NavMesh.RemoveNavMeshData(navInstance);
            if (root) Object.Destroy(root);
        }
    }

    /// <summary>
    /// Turns an ASCII layout into optimized geometry (merged wall runs, static batching), theme lighting,
    /// and a runtime-baked NavMesh so every map is navigable by bots without editor baking.
    /// </summary>
    public static class MapBuilder
    {
        public const float Cell = 3f;
        public const float WallHeight = 5.2f;
        public const float CrateHeight = 1.05f;
        public const float StackHeight = 2.3f;
        public const float PlatformHeight = 1.6f;

        public static Vector3 CellCenter(MapDefinition m, int x, int z) => new Vector3((x + 0.5f) * Cell, 0f, (m.height - 1 - z + 0.5f) * Cell);

        static bool IsWalk(char c) => ".ABRW123gprl".IndexOf(c) >= 0;

        public static MapRuntime Build(MapDefinition m, bool forMenu = false)
        {
            var rt = new MapRuntime { def = m };
            var th = m.Theme;
            rt.root = new GameObject("Map_" + m.id);
            var root = rt.root.transform;
            var geo = new GameObject("Geometry").transform; geo.SetParent(root, false);
            var props = new GameObject("Props").transform; props.SetParent(root, false);

            var floorMat = MaterialLib.Lit(th.floor, ProcTex.Get(th.floorTex), 0.1f);
            var wallMat = MaterialLib.Lit(th.wall, ProcTex.Get(th.wallTex), 0.08f);
            var trimMat = MaterialLib.Lit(th.wallTrim, null, 0.2f);
            var crateMat = MaterialLib.Lit(th.crate, ProcTex.Get(th.crateTex), 0.1f);
            var platMat = MaterialLib.Lit(th.platform, ProcTex.Get("panel"), 0.15f);
            var siteMat = MaterialLib.Lit(Color.Lerp(th.floor, th.site, 0.35f), ProcTex.Get(th.floorTex), 0.1f);
            var hazardMat = MaterialLib.Lit(th.site, ProcTex.Get("hazard"), 0.2f);
            var rng = new System.Random(m.id.GetHashCode());

            float W = m.width * Cell, H = m.height * Cell;

            // Floor slab
            MeshUtil.Block(geo, "Floor", new Vector3(W / 2, -0.25f, H / 2), new Vector3(W, 0.5f, H), floorMat);

            // Walls: merge horizontal runs of '#' into single boxes
            for (int z = 0; z < m.height; z++)
            {
                int x = 0;
                while (x < m.width)
                {
                    if (m.At(x, z) != '#') { x++; continue; }
                    int start = x;
                    while (x < m.width && m.At(x, z) == '#') x++;
                    int len = x - start;
                    Vector3 a = CellCenter(m, start, z), b = CellCenter(m, x - 1, z);
                    Vector3 c = (a + b) * 0.5f;
                    float h = WallHeight + (float)rng.NextDouble() * 0.8f;
                    var tint = Color.Lerp(th.wall, th.wallTrim, (float)rng.NextDouble() * 0.12f);
                    MeshUtil.Block(geo, "Wall", c + Vector3.up * h / 2, new Vector3(len * Cell, h, Cell), MaterialLib.Lit(tint, ProcTex.Get(th.wallTex), 0.08f));
                    // Cap trim breaks up the silhouette
                    MeshUtil.Block(geo, "Trim", c + Vector3.up * (h + 0.1f), new Vector3(len * Cell + 0.2f, 0.2f, Cell + 0.2f), trimMat, false);
                }
            }

            for (int z = 0; z < m.height; z++)
            for (int x = 0; x < m.width; x++)
            {
                char ch = m.At(x, z);
                Vector3 p = CellCenter(m, x, z);
                switch (ch)
                {
                    case 'c':
                    {
                        // A crate fills most of the cell but leaves a gap so the cell edge is still walkable around it.
                        float s = Cell * 0.72f;
                        MeshUtil.Block(geo, "Crate", p + Vector3.up * CrateHeight / 2, new Vector3(s, CrateHeight, s), crateMat, true, Quaternion.Euler(0, (float)rng.NextDouble() * 10f - 5f, 0));
                        break;
                    }
                    case 'h':
                    {
                        float s = Cell * 0.8f;
                        MeshUtil.Block(geo, "Stack", p + Vector3.up * CrateHeight / 2, new Vector3(s, CrateHeight, s), crateMat);
                        MeshUtil.Block(geo, "Stack2", p + Vector3.up * (CrateHeight + (StackHeight - CrateHeight) / 2), new Vector3(s * 0.85f, StackHeight - CrateHeight, s * 0.85f), crateMat, true, Quaternion.Euler(0, 12f, 0));
                        break;
                    }
                    case 'w':
                        MeshUtil.Block(geo, "LowWall", p + Vector3.up * 0.55f, new Vector3(Cell, 1.1f, Cell * 0.5f), wallMat);
                        break;
                    case 'o':
                        if (th.trees)
                        {
                            MeshUtil.Block(geo, "Trunk", p + Vector3.up * 2.5f, new Vector3(0.6f, 5f, 0.6f), MaterialLib.Lit(new Color(0.35f, 0.25f, 0.15f), ProcTex.Get("planks")));
                            var leaves = MaterialLib.Lit(new Color(0.18f, 0.38f + (float)rng.NextDouble() * 0.1f, 0.14f), ProcTex.Get("grass"));
                            for (int k = 0; k < 3; k++)
                            {
                                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                                Object.DestroyImmediate(canopy.GetComponent<Collider>());
                                canopy.transform.SetParent(props, false);
                                canopy.transform.position = p + new Vector3(((float)rng.NextDouble() - 0.5f) * 1.6f, 5f + k * 0.7f, ((float)rng.NextDouble() - 0.5f) * 1.6f);
                                canopy.transform.localScale = new Vector3(3.2f, 1.6f, 3.2f) * (1f - k * 0.2f);
                                canopy.GetComponent<Renderer>().sharedMaterial = leaves;
                            }
                            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            bush.transform.SetParent(geo, false);
                            bush.transform.position = p + Vector3.up * 0.6f;
                            bush.transform.localScale = new Vector3(2.2f, 1.6f, 2.2f);
                            bush.GetComponent<Renderer>().sharedMaterial = leaves;
                        }
                        else
                        {
                            MeshUtil.Block(geo, "Pillar", p + Vector3.up * WallHeight / 2, new Vector3(1.1f, WallHeight, 1.1f), wallMat);
                        }
                        break;
                    case 'p':
                        MeshUtil.Block(geo, "Platform", p + Vector3.up * PlatformHeight / 2, new Vector3(Cell, PlatformHeight, Cell), platMat);
                        rt.platforms.Add(p + Vector3.up * PlatformHeight);
                        break;
                    case 'r':
                        BuildRamp(m, x, z, geo, platMat);
                        break;
                    case 'A': case 'B':
                        MeshUtil.Block(geo, "SiteFloor", p + Vector3.up * 0.01f, new Vector3(Cell, 0.02f, Cell), siteMat, false);
                        break;
                    case 'l':
                    {
                        var lg = new GameObject("Lamp"); lg.transform.SetParent(props, false);
                        lg.transform.position = p + Vector3.up * (WallHeight - 0.6f);
                        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 14f; l.intensity = 1.8f; l.color = new Color(0.85f, 0.95f, 1f); l.shadows = LightShadows.None;
                        MeshUtil.Block(props, "LampFixture", p + Vector3.up * (WallHeight - 0.15f), new Vector3(1.2f, 0.1f, 0.3f), MaterialLib.Unlit(new Color(0.9f, 0.97f, 1f)), false);
                        break;
                    }
                }

                if (IsWalk(ch))
                {
                    Vector3 fp = ch == 'p' ? p + Vector3.up * PlatformHeight : p;
                    if (ch != 'r') rt.allFloor.Add(fp);
                    if (ch == 'R') rt.raiderSpawns.Add(p);
                    if (ch == 'W') rt.wardenSpawns.Add(p);
                    if (ch == 'A') rt.siteA.Add(p);
                    if (ch == 'B') rt.siteB.Add(p);
                    if (ch == 'g') rt.guardSpots.Add(p);
                    if (ch >= '1' && ch <= '3') { if (!rt.lanes.ContainsKey(ch)) rt.lanes[ch] = new List<Vector3>(); rt.lanes[ch].Add(p); }
                }
            }

            rt.siteACenter = Average(rt.siteA); rt.siteBCenter = Average(rt.siteB);
            rt.siteABounds = CellBounds(rt.siteA); rt.siteBBounds = CellBounds(rt.siteB);
            rt.raiderSpawnCenter = Average(rt.raiderSpawns); rt.wardenSpawnCenter = Average(rt.wardenSpawns);
            rt.raiderFacing = Quaternion.LookRotation((rt.wardenSpawnCenter - rt.raiderSpawnCenter).Flat()).eulerAngles.y;
            rt.wardenFacing = Quaternion.LookRotation((rt.raiderSpawnCenter - rt.wardenSpawnCenter).Flat()).eulerAngles.y;
            rt.IdentifyLanes();

            // Site markers: painted letters made of hazard boards on a nearby wall-facing side
            SiteMarker(geo, rt.siteACenter, "A", hazardMat);
            SiteMarker(geo, rt.siteBCenter, "B", hazardMat);

            if (th.ceiling)
                MeshUtil.Block(geo, "Ceiling", new Vector3(W / 2, WallHeight + 0.25f, H / 2), new Vector3(W, 0.5f, H), MaterialLib.Lit(th.wall * 0.6f, ProcTex.Get("panel")), true);

            SetupLighting(rt, th);

            if (!forMenu) BakeNavMesh(rt, W, H);
            StaticBatchingUtility.Combine(geo.gameObject);
            return rt;
        }

        static void BuildRamp(MapDefinition m, int x, int z, Transform parent, Material mat)
        {
            // Ramp rises toward the adjacent platform cell.
            Vector2Int dir = Vector2Int.zero;
            if (m.At(x, z - 1) == 'p') dir = new Vector2Int(0, -1);
            else if (m.At(x, z + 1) == 'p') dir = new Vector2Int(0, 1);
            else if (m.At(x + 1, z) == 'p') dir = new Vector2Int(1, 0);
            else if (m.At(x - 1, z) == 'p') dir = new Vector2Int(-1, 0);
            Vector3 c = CellCenter(m, x, z);
            Vector3 worldDir = new Vector3(dir.x, 0, -dir.y);   // grid z grows southwards
            float angle = Mathf.Atan2(PlatformHeight, Cell) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(Cell * Cell + PlatformHeight * PlatformHeight);
            const float thick = 0.4f;
            var rot = Quaternion.LookRotation(worldDir) * Quaternion.Euler(-angle, 0, 0);
            Vector3 center = c + Vector3.up * (PlatformHeight / 2) - rot * Vector3.up * (thick / 2);
            MeshUtil.Block(parent, "Ramp", center, new Vector3(Cell, thick, len), mat, true, rot);
            // Side skirt fills the wedge so the ramp reads as solid
            MeshUtil.Block(parent, "RampBase", c + Vector3.up * PlatformHeight * 0.22f + worldDir * Cell * 0.2f, new Vector3(Cell * 0.98f, PlatformHeight * 0.44f, Cell * 0.55f), mat, false, Quaternion.LookRotation(worldDir));
        }

        static void SiteMarker(Transform parent, Vector3 center, string letter, Material mat)
        {
            // A painted floor letter built from strips (A or B) so sites are recognisable at a glance.
            var root = new GameObject("Site" + letter).transform; root.SetParent(parent, false);
            root.position = center + Vector3.up * 0.03f;
            var paint = MaterialLib.Unlit(letter == "A" ? new Color(0.85f, 0.25f, 0.15f) : new Color(0.2f, 0.4f, 0.95f));
            void Strip(float x, float z, float w, float l, float rotY = 0)
                => MeshUtil.Block(root, "Paint", root.position + new Vector3(x, 0, z), new Vector3(w, 0.02f, l), paint, false, Quaternion.Euler(0, rotY, 0));
            if (letter == "A")
            {
                Strip(-0.55f, 0, 0.35f, 3f, 18f); Strip(0.55f, 0, 0.35f, 3f, -18f); Strip(0, -0.1f, 1.1f, 0.3f);
            }
            else
            {
                Strip(-0.6f, 0, 0.35f, 3f); Strip(0.05f, 1.35f, 1.3f, 0.3f); Strip(0.05f, 0f, 1.3f, 0.3f); Strip(0.05f, -1.35f, 1.3f, 0.3f);
                Strip(0.75f, 0.68f, 0.32f, 1.1f); Strip(0.75f, -0.68f, 0.32f, 1.1f);
            }
        }

        static void SetupLighting(MapRuntime rt, MapTheme th)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = th.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = th.fog;
            RenderSettings.fogStartDistance = th.fogStart;
            RenderSettings.fogEndDistance = th.fogEnd;
            RenderSettings.skybox = null;
            if (th.sunIntensity > 0f)
            {
                var sunGo = new GameObject("Sun"); sunGo.transform.SetParent(rt.root.transform, false);
                sunGo.transform.rotation = Quaternion.Euler(th.sunEuler);
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional; sun.color = th.sun; sun.intensity = th.sunIntensity;
                sun.shadows = GameApp.Settings.shadowQuality == 0 ? LightShadows.None : GameApp.Settings.shadowQuality == 1 ? LightShadows.Hard : LightShadows.Soft;
                sun.shadowStrength = 0.75f;
                RenderSettings.sun = sun;
            }
        }

        public static void BakeNavMesh(MapRuntime rt, float W, float H)
        {
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.45f;
            settings.agentSlope = 40f;
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(new Vector3(W / 2, 2f, H / 2), new Vector3(W + 2, 12f, H + 2));
            NavMeshBuilder.CollectSources(bounds, Layers.WorldMask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) { Debug.LogError("[Wardline] NavMesh build failed"); return; }
            rt.navInstance = NavMesh.AddNavMeshData(data);
        }

        static Vector3 Average(List<Vector3> pts)
        {
            if (pts.Count == 0) return Vector3.zero;
            Vector3 s = Vector3.zero; foreach (var p in pts) s += p; return s / pts.Count;
        }

        static Bounds CellBounds(List<Vector3> pts)
        {
            if (pts.Count == 0) return new Bounds();
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) b.Encapsulate(p);
            b.Expand(new Vector3(Cell + 1f, 0f, Cell + 1f));
            return b;
        }
    }
}
