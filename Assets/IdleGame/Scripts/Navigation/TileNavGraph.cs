using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleTime.Navigation
{
    public enum NavMoveType { Walk, Hop, ClimbUp, ClimbDown }

    // One leg of a route returned by the pathfinder. Walk/Hop legs are driven by the
    // normal locomotion (which auto-hops gaps); Climb legs hand off to the ladder code.
    public struct NavStep
    {
        public NavMoveType Type;
        public Vector2 World;     // where this leg ends (Walk/Hop), or the ladder cell to grab (Climb)
        public float LadderX;     // column x for climb legs
    }

    // Builds a navigation graph over the terrain + ladder tilemaps and answers A* path
    // queries. Nodes are "stand cells" (an empty cell directly above solid ground).
    // Edges: Walk (adjacent stand cells), Hop (a stand cell across a small gap), and
    // Climb (a ladder's bottom access ↔ top access). Add this to the Grid (or any scene
    // object); it auto-finds the tilemaps. The gizmo draws the graph so junction/offset
    // assumptions can be eyeballed before trusting the routing.
    public sealed class TileNavGraph : MonoBehaviour
    {
        [Tooltip("Layers whose tilemaps count as solid ground/walkable surfaces. Set this to ONLY the " +
                 "solid Terrain layer(s) — keep decorative/background layers (e.g. Secondary) OUT, or their " +
                 "tiles become phantom walkable ground. The Ladder layer is auto-excluded in code.")]
        [SerializeField] private LayerMask terrainMask = ~0;
        [Tooltip("Ladder tilemap. Auto-found by the 'Ladder' layer if left empty.")]
        [SerializeField] private Tilemap ladderTilemap;
        [Tooltip("Widest gap (in cells) a Hop edge will bridge. Keep in sync with the player's maxHopDistance.")]
        [SerializeField] private int maxHopCells = 2;
        [Tooltip("How many cells above/below a ladder end to search for the platform stand cell it connects to.")]
        [SerializeField] private int maxLadderAccessScan = 3;
        [SerializeField] private bool drawGizmos = true;

        private struct Edge
        {
            public Vector3Int To;
            public NavMoveType Type;
            public float Cost;
            public Vector2 ClimbWorld;   // ladder cell to grab, for climb edges
            public float LadderX;
        }

        private Grid grid;
        private readonly HashSet<Vector3Int> groundCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> standCells = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector3Int, List<Edge>> edges = new Dictionary<Vector3Int, List<Edge>>();
        private bool built;

        // Last path, kept only for the gizmo.
        private readonly List<Vector3Int> lastPathCells = new List<Vector3Int>();

        private void Awake() => EnsureBuilt();

        [ContextMenu("Rebuild Nav Graph")]
        public void Rebuild()
        {
            built = false;
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (built) return;
            Build();
            built = true;
        }

        // ── Public query ─────────────────────────────────────────────────────────

        public bool TryFindPath(Vector2 fromWorld, Vector2 toWorld, List<NavStep> result)
        {
            result.Clear();
            lastPathCells.Clear();
            EnsureBuilt();
            if (standCells.Count == 0) return false;
            if (!TryNearestStandCell(fromWorld, out Vector3Int start)) return false;
            if (!TryNearestStandCell(toWorld, out Vector3Int goal)) return false;

            if (start == goal)
            {
                result.Add(new NavStep { Type = NavMoveType.Walk, World = toWorld });
                lastPathCells.Add(start);
                return true;
            }

            if (!RunAStar(start, goal, out var cameFrom)) return false;

            // Reconstruct cell path + edge legs from goal back to start.
            var legs = new List<(Vector3Int to, Edge edge)>();
            Vector3Int cursor = goal;
            lastPathCells.Add(goal);
            while (cursor != start)
            {
                var (prev, edge) = cameFrom[cursor];
                legs.Add((cursor, edge));
                cursor = prev;
                lastPathCells.Add(cursor);
            }
            legs.Reverse();
            lastPathCells.Reverse();

            foreach (var (to, edge) in legs)
            {
                result.Add(edge.Type == NavMoveType.ClimbUp || edge.Type == NavMoveType.ClimbDown
                    ? new NavStep { Type = edge.Type, World = edge.ClimbWorld, LadderX = edge.LadderX }
                    : new NavStep { Type = edge.Type, World = grid.GetCellCenterWorld(to) });
            }

            // Collapse runs of FLAT walking into one waypoint each. The graph emits a leg
            // per tile; without this the player stops/starts every cell, which stutters the
            // walk animation. Walk edges only ever join adjacent stand cells at the same
            // height, so merging Walk→Walk is always a straight, level run the locomotion
            // can follow. Hops and climbs stay as their own waypoints: a Hop changes
            // elevation (±1 level, and several can chain up a slope), so folding it into a
            // Walk produced a single diagonal leg from a low start to a high end — the
            // locomotion can't gain that height as a "walk", so it stalled at the ledge.
            // Keeping each hop/climb as a boundary makes the route hug the terrain instead.
            for (int i = result.Count - 1; i > 0; i--)
            {
                if (result[i].Type != NavMoveType.Walk || result[i - 1].Type != NavMoveType.Walk) continue;
                NavStep merged = result[i - 1];
                merged.World = result[i].World;   // extend to the farther point
                result[i - 1] = merged;
                result.RemoveAt(i);
            }

            // Finish exactly at the clicked x. If the last leg already walks, retarget it;
            // otherwise (arrived via a climb) append a short walk to the click.
            if (result.Count > 0)
            {
                NavStep last = result[result.Count - 1];
                if (last.Type == NavMoveType.Walk || last.Type == NavMoveType.Hop)
                {
                    last.World = new Vector2(toWorld.x, last.World.y);
                    result[result.Count - 1] = last;
                }
                else
                {
                    result.Add(new NavStep { Type = NavMoveType.Walk, World = toWorld });
                }
            }

            return true;
        }

        // ── A* ───────────────────────────────────────────────────────────────────

        private bool RunAStar(Vector3Int start, Vector3Int goal, out Dictionary<Vector3Int, (Vector3Int, Edge)> cameFrom)
        {
            cameFrom = new Dictionary<Vector3Int, (Vector3Int, Edge)>();
            var gScore = new Dictionary<Vector3Int, float> { [start] = 0f };
            var open = new List<Vector3Int> { start };

            while (open.Count > 0)
            {
                // Pick the open node with the lowest f = g + h (linear scan; maps are small).
                int bestIndex = 0;
                float bestF = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    float f = gScore[open[i]] + Heuristic(open[i], goal);
                    if (f < bestF) { bestF = f; bestIndex = i; }
                }

                Vector3Int current = open[bestIndex];
                if (current == goal) return true;
                open.RemoveAt(bestIndex);

                if (!edges.TryGetValue(current, out var outgoing)) continue;
                foreach (Edge edge in outgoing)
                {
                    float tentative = gScore[current] + edge.Cost;
                    if (gScore.TryGetValue(edge.To, out float known) && tentative >= known) continue;

                    cameFrom[edge.To] = (current, edge);
                    gScore[edge.To] = tentative;
                    if (!open.Contains(edge.To)) open.Add(edge.To);
                }
            }

            return false;
        }

        private static float Heuristic(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        // ── Graph construction ─────────────────────────────────────────────────────

        private void Build()
        {
            groundCells.Clear();
            standCells.Clear();
            edges.Clear();

            if (ladderTilemap == null) ladderTilemap = FindLadderTilemap();
            CollectGroundCells();
            if (grid == null) return;

            // Stand cell = empty cell directly above a ground tile.
            foreach (Vector3Int g in groundCells)
            {
                Vector3Int above = g + Vector3Int.up;
                if (!groundCells.Contains(above)) standCells.Add(above);
            }

            BuildWalkAndHopEdges();
            BuildLadderEdges();
        }

        private void CollectGroundCells()
        {
            int ladderLayer = LayerMask.NameToLayer("Ladder");

            foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
            {
                // Ladders are climb edges, never ground — exclude them here regardless of
                // the mask, so a terrainMask left on "Everything" can't turn a ladder column
                // into phantom walkable stand cells. BuildLadderEdges handles ladders.
                if (tilemap == ladderTilemap) continue;
                if (ladderLayer >= 0 && tilemap.gameObject.layer == ladderLayer) continue;

                if (((terrainMask.value >> tilemap.gameObject.layer) & 1) == 0) continue;

                // Only count tilemaps the player actually COLLIDES with as ground. Decorative
                // layers (signs, bushes, background) have tiles but no solid collider — the
                // player walks straight through them — so without this check the graph turned
                // them into phantom stand cells and routed onto/around them (e.g. the sign).
                // A non-trigger Collider2D on the tilemap is what the player's casts hit.
                if (!tilemap.TryGetComponent(out Collider2D solid) || solid.isTrigger) continue;

                if (grid == null) grid = tilemap.GetComponentInParent<Grid>();

                BoundsInt bounds = tilemap.cellBounds;
                foreach (Vector3Int cell in bounds.allPositionsWithin)
                {
                    if (tilemap.HasTile(cell)) groundCells.Add(new Vector3Int(cell.x, cell.y, 0));
                }
            }
        }

        private void BuildWalkAndHopEdges()
        {
            foreach (Vector3Int s in standCells)
            {
                // Walk: directly adjacent stand cells.
                foreach (int dir in new[] { -1, 1 })
                {
                    if (standCells.Contains(s + new Vector3Int(dir, 0, 0)))
                        AddEdge(s, s + new Vector3Int(dir, 0, 0), NavMoveType.Walk, 1f);
                }

                // Hop: nearest stand cell across a real gap, within maxHopCells and ±1 height.
                foreach (int dir in new[] { -1, 1 })
                {
                    for (int dist = 2; dist <= maxHopCells + 1; dist++)
                    {
                        bool landed = false;
                        for (int dy = 1; dy >= -1; dy--)
                        {
                            Vector3Int cand = s + new Vector3Int(dir * dist, dy, 0);
                            if (!standCells.Contains(cand)) continue;
                            if (!GapIsClear(s, dir, dist)) continue;
                            AddEdge(s, cand, NavMoveType.Hop, dist + 1f);
                            landed = true;
                            break;
                        }
                        if (landed) break;   // take the nearest landing, don't hop over platforms
                    }
                }
            }
        }

        // The intermediate columns must be empty at stand level and one above (headroom),
        // otherwise it's not a gap you can hop — there's ground or a wall in the way.
        private bool GapIsClear(Vector3Int from, int dir, int dist)
        {
            for (int k = 1; k < dist; k++)
            {
                Vector3Int mid = from + new Vector3Int(dir * k, 0, 0);
                if (groundCells.Contains(mid) || groundCells.Contains(mid + Vector3Int.up)) return false;
            }
            return true;
        }

        private void BuildLadderEdges()
        {
            if (ladderTilemap == null) return;

            // Group ladder tiles into columns, then into contiguous vertical runs.
            var columns = new Dictionary<int, List<int>>();
            BoundsInt bounds = ladderTilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!ladderTilemap.HasTile(cell)) continue;
                if (!columns.TryGetValue(cell.x, out List<int> ys)) { ys = new List<int>(); columns[cell.x] = ys; }
                ys.Add(cell.y);
            }

            foreach (var kv in columns)
            {
                int x = kv.Key;
                List<int> ys = kv.Value;
                ys.Sort();

                int runStart = 0;
                for (int i = 1; i <= ys.Count; i++)
                {
                    bool breakRun = i == ys.Count || ys[i] != ys[i - 1] + 1;
                    if (!breakRun) continue;

                    int yBottom = ys[runStart];
                    int yTop = ys[i - 1];
                    LinkLadderRun(x, yBottom, yTop);
                    runStart = i;
                }
            }
        }

        private void LinkLadderRun(int x, int yBottom, int yTop)
        {
            if (!TryFindAccessAbove(x, yTop, out Vector3Int topAccess)) return;
            if (!TryFindAccessBelow(x, yBottom, out Vector3Int bottomAccess)) return;
            if (topAccess == bottomAccess) return;

            float columnX = grid.GetCellCenterWorld(new Vector3Int(x, yTop, 0)).x;
            Vector2 topGrab = grid.GetCellCenterWorld(new Vector3Int(x, yTop, 0));
            Vector2 bottomGrab = grid.GetCellCenterWorld(new Vector3Int(x, yBottom, 0));
            float cost = Mathf.Abs(topAccess.y - bottomAccess.y) + 1f;

            AddEdge(bottomAccess, topAccess, NavMoveType.ClimbUp, cost, topGrab, columnX);
            AddEdge(topAccess, bottomAccess, NavMoveType.ClimbDown, cost, bottomGrab, columnX);
        }

        private bool TryFindAccessAbove(int x, int yTop, out Vector3Int access)
        {
            // Look at and above the ladder top for the nearest platform stand cell. Check
            // the ladder column first, then the columns either side — a ladder commonly
            // rises THROUGH a gap in the platform, so the cell you mount onto sits beside
            // the column, not on it. (Ladder tiles are no longer ground, so there's no
            // stand cell on the column itself to lean on anymore.)
            for (int k = -1; k <= maxLadderAccessScan; k++)
                if (TryStandCellNearColumn(x, yTop + k, out access)) return true;
            access = default;
            return false;
        }

        private bool TryFindAccessBelow(int x, int yBottom, out Vector3Int access)
        {
            // Same column-then-sides logic at the ladder base. Starts at k=0 so a stand
            // cell on the floor at the very bottom of the ladder still counts.
            for (int k = 0; k <= maxLadderAccessScan; k++)
                if (TryStandCellNearColumn(x, yBottom - k, out access)) return true;
            access = default;
            return false;
        }

        // The ladder column first, then x-1 / x+1 at the same row.
        private bool TryStandCellNearColumn(int x, int y, out Vector3Int access)
        {
            foreach (int dx in new[] { 0, -1, 1 })
            {
                access = new Vector3Int(x + dx, y, 0);
                if (standCells.Contains(access)) return true;
            }
            access = default;
            return false;
        }

        private void AddEdge(Vector3Int from, Vector3Int to, NavMoveType type, float cost,
                             Vector2 climbWorld = default, float ladderX = 0f)
        {
            if (!edges.TryGetValue(from, out List<Edge> list)) { list = new List<Edge>(); edges[from] = list; }
            list.Add(new Edge { To = to, Type = type, Cost = cost, ClimbWorld = climbWorld, LadderX = ladderX });
        }

        // ── Lookups ────────────────────────────────────────────────────────────────

        private bool TryNearestStandCell(Vector2 world, out Vector3Int result)
        {
            result = default;
            if (grid == null) return false;

            Vector3Int cell = grid.WorldToCell(world);
            cell.z = 0;

            // The click usually lands a hair above the surface — check the cell and the
            // ones just below/above before falling back to a global nearest search.
            for (int dy = 1; dy >= -2; dy--)
            {
                Vector3Int c = new Vector3Int(cell.x, cell.y + dy, 0);
                if (standCells.Contains(c)) { result = c; return true; }
            }

            float best = float.MaxValue;
            bool found = false;
            foreach (Vector3Int s in standCells)
            {
                float d = (grid.GetCellCenterWorld(s) - (Vector3)(Vector2)world).sqrMagnitude;
                if (d < best) { best = d; result = s; found = true; }
            }
            return found;
        }

        private Tilemap FindLadderTilemap()
        {
            int ladderLayer = LayerMask.NameToLayer("Ladder");
            if (ladderLayer < 0) return null;
            foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
                if (tilemap.gameObject.layer == ladderLayer) return tilemap;
            return null;
        }

        // ── Gizmo ────────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            if (!Application.isPlaying) { built = false; EnsureBuilt(); }
            if (grid == null) return;

            foreach (Vector3Int s in standCells)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(grid.GetCellCenterWorld(s), 0.08f);
            }

            foreach (var kv in edges)
            {
                Vector3 from = grid.GetCellCenterWorld(kv.Key);
                foreach (Edge e in kv.Value)
                {
                    Gizmos.color = e.Type switch
                    {
                        NavMoveType.Walk => new Color(0.5f, 0.5f, 0.5f, 0.6f),
                        NavMoveType.Hop => Color.yellow,
                        _ => Color.cyan,
                    };
                    Gizmos.DrawLine(from, grid.GetCellCenterWorld(e.To));
                }
            }

            Gizmos.color = Color.magenta;
            for (int i = 1; i < lastPathCells.Count; i++)
                Gizmos.DrawLine(grid.GetCellCenterWorld(lastPathCells[i - 1]), grid.GetCellCenterWorld(lastPathCells[i]));
        }
    }
}
