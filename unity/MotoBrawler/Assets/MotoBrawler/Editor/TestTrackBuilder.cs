using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MotoBrawler.EditorTools
{
    /// <summary>
    /// Builds the test scene geometry from code (no ProBuilder needed):
    ///  - a 600 × 600 m flat ground with a grid texture (to read speed),
    ///  - a ~780 m closed road loop: a long straight, gentle sweepers, a 9 m hill,
    ///    a tight hairpin and an S-bend, as a mesh with solid sides (embankment),
    ///  - two ramps (10° and 16°) in the infield lined up with a crash wall,
    ///  - an angled wall for glancing-hit tests,
    ///  - a SpawnPoint on the start straight.
    /// Generated meshes / materials are saved under Assets/MotoBrawler/Generated.
    /// </summary>
    public static class TestTrackBuilder
    {
        public const float RoadWidth = 10f;
        private const float RoadLift = 0.05f;      // road sits just above the ground plane
        private const float SkirtBottom = -1f;     // road sides go down into the ground
        private const int SamplesPerSegment = 24;

        // Closed loop, clockwise seen from above. (x, height, z)
        private static readonly Vector3[] ControlPoints =
        {
            new Vector3(0, 0, -60),     // start straight (heading +Z)
            new Vector3(0, 0, 60),
            new Vector3(15, 0, 105),    // gentle right
            new Vector3(55, 0, 130),
            new Vector3(105, 4, 120),   // climb
            new Vector3(145, 9, 85),    // hill top
            new Vector3(155, 9, 35),
            new Vector3(150, 3, -10),   // descent
            new Vector3(135, 0, -32),
            new Vector3(112, 0, -36),   // tight hairpin
            new Vector3(95, 0, -55),
            new Vector3(108, 0, -78),
            new Vector3(130, 0, -95),   // S-bend
            new Vector3(125, 0, -125),
            new Vector3(90, 0, -145),
            new Vector3(40, 0, -145),   // long sweeper back to the start
            new Vector3(10, 0, -120),
        };

        public struct Layers
        {
            public int ground, road, wall;
        }

        /// <returns>The spawn point transform.</returns>
        public static Transform Build(Layers layers)
        {
            MotoSetupMenu.EnsureFolder(MotoSetupMenu.GeneratedFolder);

            var root = new GameObject("TestTrack").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Test Track");

            Material groundMat = MotoSetupMenu.GetOrCreateMaterial("Ground", new Color(0.36f, 0.52f, 0.33f), GetGridTexture(), 60f);
            Material roadMat = MotoSetupMenu.GetOrCreateMaterial("Road", new Color(0.22f, 0.22f, 0.24f));
            Material lineMat = MotoSetupMenu.GetOrCreateMaterial("RoadLine", new Color(0.95f, 0.95f, 0.9f));
            Material rampMat = MotoSetupMenu.GetOrCreateMaterial("Ramp", new Color(0.95f, 0.55f, 0.15f));
            Material wallMat = MotoSetupMenu.GetOrCreateMaterial("Wall", new Color(0.8f, 0.15f, 0.15f));

            // ---- Ground ----
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localPosition = new Vector3(75f, 0f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);   // Plane is 10 m → 600 m
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            ground.layer = layers.ground;
            ground.isStatic = true;

            // ---- Road ----
            List<Vector3> centre = SampleLoop(ControlPoints, SamplesPerSegment);
            Mesh roadMesh = SaveMesh(BuildRoadMesh(centre, RoadWidth), "RoadMesh");
            GameObject road = CreateMeshObject("Road", root, roadMesh, roadMat, layers.road, withCollider: true);
            road.isStatic = true;

            Mesh lineMesh = SaveMesh(BuildCentreLineMesh(centre), "RoadLineMesh");
            GameObject line = CreateMeshObject("CentreLine", road.transform, lineMesh, lineMat, layers.road, withCollider: false);
            line.isStatic = true;

            // ---- Ramps (rise toward +Z, lined up with the crash wall) ----
            Mesh rampLow = SaveMesh(BuildWedge(6f, 12f, 2.2f), "RampLowMesh");     // ≈10°
            Mesh rampHigh = SaveMesh(BuildWedge(6f, 12f, 3.5f), "RampHighMesh");   // ≈16°
            CreateMeshObject("Ramp_10deg", root, rampLow, rampMat, layers.ground, true).transform.localPosition = new Vector3(50f, 0f, -50f);
            CreateMeshObject("Ramp_16deg", root, rampHigh, rampMat, layers.ground, true).transform.localPosition = new Vector3(70f, 0f, -50f);

            // ---- Walls ----
            CreateWall("CrashWall", root, new Vector3(60f, 2f, 45f), Quaternion.identity, new Vector3(40f, 4f, 1f), wallMat, layers.wall);
            CreateWall("AngledWall", root, new Vector3(100f, 2f, 20f), Quaternion.Euler(0f, 45f, 0f), new Vector3(30f, 4f, 1f), wallMat, layers.wall);

            // ---- Spawn on the start straight, facing +Z ----
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetParent(root, false);
            spawn.localPosition = new Vector3(0f, RoadLift + 0.2f, -40f);
            spawn.localRotation = Quaternion.identity;

            return spawn;
        }

        // ---------------------------------------------------------------- spline

        private static List<Vector3> SampleLoop(Vector3[] p, int perSegment)
        {
            var result = new List<Vector3>(p.Length * perSegment);
            int n = p.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = p[(i - 1 + n) % n], p1 = p[i], p2 = p[(i + 1) % n], p3 = p[(i + 2) % n];
                for (int s = 0; s < perSegment; s++)
                    result.Add(CatmullRom(p0, p1, p2, p3, s / (float)perSegment));
            }
            return result;
        }

        /// <summary>Centripetal Catmull-Rom: no loops or cusps on uneven point spacing.</summary>
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            const float alpha = 0.5f;
            float t0 = 0f;
            float t1 = t0 + Mathf.Max(Mathf.Pow(Vector3.Distance(p0, p1), alpha), 1e-4f);
            float t2 = t1 + Mathf.Max(Mathf.Pow(Vector3.Distance(p1, p2), alpha), 1e-4f);
            float t3 = t2 + Mathf.Max(Mathf.Pow(Vector3.Distance(p2, p3), alpha), 1e-4f);
            float tt = Mathf.Lerp(t1, t2, t);

            Vector3 a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3;
            return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2;
        }

        private static Vector3 FlatRight(List<Vector3> c, int i)
        {
            int n = c.Count;
            Vector3 tangent = c[(i + 1) % n] - c[(i - 1 + n) % n];
            tangent.y = 0f;
            return Vector3.Cross(Vector3.up, tangent.normalized).normalized;
        }

        // ---------------------------------------------------------------- meshes

        private static Mesh BuildRoadMesh(List<Vector3> c, float width)
        {
            int n = c.Count;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float half = width * 0.5f;
            Vector3 lift = Vector3.up * RoadLift;

            var left = new Vector3[n + 1];
            var right = new Vector3[n + 1];
            var sideDir = new Vector3[n + 1];

            // Top surface (shared vertices → smooth normals). Last row duplicates the first for UVs.
            float distance = 0f;
            for (int i = 0; i <= n; i++)
            {
                int k = i % n;
                if (i > 0) distance += Vector3.Distance(c[k], c[(i - 1) % n]);
                Vector3 r = FlatRight(c, k);
                sideDir[i] = r;
                left[i] = c[k] - r * half + lift;
                right[i] = c[k] + r * half + lift;
                verts.Add(left[i]);
                verts.Add(right[i]);
                uvs.Add(new Vector2(0f, distance / width));
                uvs.Add(new Vector2(1f, distance / width));
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                AddQuad(tris, verts, a, a + 1, a + 3, a + 2, Vector3.up);
            }

            // Sides (separate vertices → hard edges), down into the ground so the hill is solid.
            for (int i = 0; i < n; i++)
            {
                AddSideQuad(verts, uvs, tris, left[i], left[i + 1], -sideDir[i]);
                AddSideQuad(verts, uvs, tris, right[i], right[i + 1], sideDir[i]);
            }

            var mesh = new Mesh { name = "Road" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddSideQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
                                        Vector3 top0, Vector3 top1, Vector3 outward)
        {
            int start = verts.Count;
            verts.Add(top0);
            verts.Add(top1);
            verts.Add(new Vector3(top1.x, SkirtBottom, top1.z));
            verts.Add(new Vector3(top0.x, SkirtBottom, top0.z));
            uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0f, 0f));
            AddQuad(tris, verts, start, start + 1, start + 2, start + 3, outward);
        }

        private static Mesh BuildCentreLineMesh(List<Vector3> c)
        {
            int n = c.Count;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const float halfWidth = 0.12f;
            Vector3 lift = Vector3.up * (RoadLift + 0.02f);

            // Dashes: two samples on, two off.
            for (int i = 0; i < n; i += 4)
            {
                int j = (i + 2) % n;
                Vector3 r0 = FlatRight(c, i) * halfWidth, r1 = FlatRight(c, j) * halfWidth;
                int s = verts.Count;
                verts.Add(c[i] - r0 + lift);
                verts.Add(c[i] + r0 + lift);
                verts.Add(c[j] + r1 + lift);
                verts.Add(c[j] - r1 + lift);
                AddQuad(tris, verts, s, s + 1, s + 2, s + 3, Vector3.up);
            }

            var mesh = new Mesh { name = "CentreLine" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Ramp wedge: sloped top rising toward +Z, vertical back face, two sides.</summary>
        private static Mesh BuildWedge(float width, float length, float height)
        {
            float w = width * 0.5f;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            Vector3 slopeNormal = new Vector3(0f, length, -height).normalized;
            AddFace(verts, tris, slopeNormal, new Vector3(-w, 0, 0), new Vector3(w, 0, 0), new Vector3(w, height, length), new Vector3(-w, height, length));
            AddFace(verts, tris, Vector3.forward, new Vector3(-w, 0, length), new Vector3(w, 0, length), new Vector3(w, height, length), new Vector3(-w, height, length));
            AddFace(verts, tris, Vector3.left, new Vector3(-w, 0, 0), new Vector3(-w, height, length), new Vector3(-w, 0, length));
            AddFace(verts, tris, Vector3.right, new Vector3(w, 0, 0), new Vector3(w, height, length), new Vector3(w, 0, length));

            var mesh = new Mesh { name = "Wedge" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Adds a flat-shaded triangle or quad (corners in perimeter order) with its own vertices.</summary>
        private static void AddFace(List<Vector3> verts, List<int> tris, Vector3 outward, params Vector3[] corners)
        {
            int s = verts.Count;
            verts.AddRange(corners);
            if (corners.Length == 3) AddTri(tris, verts, s, s + 1, s + 2, outward);
            else AddQuad(tris, verts, s, s + 1, s + 2, s + 3, outward);
        }

        /// <summary>Adds a quad (a,b,c,d in perimeter order), flipping the winding if needed so
        /// it faces 'outward' (Unity front faces are clockwise; physics raycasts are one-sided).</summary>
        private static void AddQuad(List<int> tris, List<Vector3> v, int a, int b, int c, int d, Vector3 outward)
        {
            AddTri(tris, v, a, b, c, outward);
            AddTri(tris, v, a, c, d, outward);
        }

        private static void AddTri(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(normal, outward) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material mat, int layer, bool withCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (withCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        private static void CreateWall(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 size, Material mat, int layer)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.SetLocalPositionAndRotation(pos, rot);
            wall.transform.localScale = size;
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
            wall.layer = layer;
            wall.isStatic = true;
        }

        private static Mesh SaveMesh(Mesh mesh, string assetName)
        {
            string path = $"{MotoSetupMenu.GeneratedFolder}/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);   // rebuild cleanly each time
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>White grid lines on transparent-white: tinted by the material colour.</summary>
        private static Texture2D GetGridTexture()
        {
            string path = $"{MotoSetupMenu.GeneratedFolder}/GridTexture.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing) return existing;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Grid", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool line = x < 2 || y < 2;
                bool checker = ((x / 32) + (y / 32)) % 2 == 0;
                pixels[y * size + x] = line ? new Color(1f, 1f, 1f) : checker ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.75f, 0.75f, 0.75f);
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }
    }
}
