using Kitchen;
using Kitchen.Layouts;
using KitchenData;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>How an HQ shop room is laid out, worked out from the room's tiles alone.</summary>
    internal sealed class TycoonShopLayout
    {
        public int RoomID;
        public HashSet<(int x, int z)> Room;
        public List<(int x, int z)> Doors;
        public Vector3 Door, Centre, CounterAt, KeeperAt, Away;
        public List<(int x, int z)> Shelves = new List<(int x, int z)>();
        public int WallSpots, InnerSpots, Occupied;

        private static readonly (int dx, int dz)[] Steps = { (0, 1), (1, 0), (0, -1), (-1, 0) };

        internal static (int x, int z) Key(Vector3 p) => (Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));

        public static TycoonShopLayout PlanDense(TileManager tiles, (int x, int z) start, int maxShelves)
        {
            CLayoutRoomTile Tile(int x, int z) => tiles.GetTile(new IntVector3(x, 0, z));
            int roomID = Tile(start.x, start.z).RoomID;
            var room = new HashSet<(int x, int z)> { start };
            var queue = new Queue<(int x, int z)>();
            queue.Enqueue(start);
            while (queue.Count > 0 && room.Count < 400)
            {
                (int x, int z) p = queue.Dequeue();
                foreach ((int dx, int dz) in Steps)
                {
                    var n = (p.x + dx, p.z + dz);
                    if (room.Contains(n)) continue;
                    CLayoutRoomTile t = Tile(n.Item1, n.Item2);
                    if (t.Type == RoomType.NoRoom || t.RoomID != roomID) continue;
                    room.Add(n);
                    queue.Enqueue(n);
                }
            }
            if (room.Count < 6) return null;

            var doors = new List<(int x, int z)>();
            var walls = new HashSet<(int x, int z)>();
            float cx = 0f, cz = 0f;
            foreach ((int x, int z) t in room)
            {
                cx += t.x; cz += t.z;
                foreach ((int dx, int dz) in Steps)
                {
                    var n = (t.x + dx, t.z + dz);
                    if (room.Contains(n)) continue;
                    bool walkable = false;
                    try { walkable = tiles.CanReach(new IntVector3(t.x, 0, t.z), new IntVector3(n.Item1, 0, n.Item2), false); }
                    catch (System.Exception) { }
                    if (walkable && Tile(n.Item1, n.Item2).Type != RoomType.NoRoom) { if (!doors.Contains(t)) doors.Add(t); }
                    else walls.Add(t);
                }
            }
            Vector3 centre = new Vector3(cx / room.Count, 0f, cz / room.Count);
            if (doors.Count == 0) return null;
            Vector3 door = Vector3.zero;
            foreach ((int x, int z) d in doors) door += new Vector3(d.x, 0f, d.z);
            door /= doors.Count;
            var doorZone = new HashSet<(int x, int z)>();
            foreach ((int x, int z) d in doors)
            {
                doorZone.Add(d);
                foreach ((int dx, int dz) in Steps)
                {
                    var outside = (d.x + dx, d.z + dz);
                    if (room.Contains(outside)) continue;
                    bool walkable = false;
                    try { walkable = tiles.CanReach(new IntVector3(d.x, 0, d.z), new IntVector3(outside.Item1, 0, outside.Item2), false); }
                    catch (System.Exception) { }
                    if (walkable) doorZone.Add((d.x - dx, d.z - dz));
                }
            }
            float start0 = Mathf.Atan2(door.z - centre.z, door.x - centre.x);
            float Angle((int x, int z) t)
            {
                float a = Mathf.Atan2(t.z - centre.z, t.x - centre.x) - start0;
                while (a < 0f) a += 2f * Mathf.PI;
                return a;
            }
            List<(int x, int z)> wallOrder = walls.Where(w => !doorZone.Contains(w)).OrderBy(Angle).ToList();
            List<(int x, int z)> innerOrder = room.Where(t => !walls.Contains(t) && !doorZone.Contains(t))
                .OrderByDescending(t => (new Vector3(t.x, 0f, t.z) - centre).sqrMagnitude).ToList();

            TycoonShopLayout best = null;
            int bestScore = -1;
            foreach ((int x, int z) k in wallOrder)
                foreach ((int dx, int dz) in Steps)
                {
                    if (room.Contains((k.x - dx, k.z - dz))) continue;
                    var c = (x: k.x + dx, z: k.z + dz);
                    var f = (x: c.x + dx, z: c.z + dz);
                    if (!room.Contains(c) || !room.Contains(f) || doorZone.Contains(c) || doorZone.Contains(f)) continue;
                    var blocked = new HashSet<(int x, int z)> { k, c };
                    var shelves = new List<(int x, int z)>();
                    var none = new List<(int x, int z)>();
                    foreach ((int x, int z) w in wallOrder)
                    {
                        if (blocked.Contains(w) || w.Equals(f)) continue;
                        shelves.Add(w);
                        if (!AllReachable(room, blocked, shelves, none, doors, f)) shelves.RemoveAt(shelves.Count - 1);
                    }
                    int wallCount = shelves.Count;
                    foreach ((int x, int z) t in innerOrder)
                    {
                        if (blocked.Contains(t) || t.Equals(f)) continue;
                        shelves.Add(t);
                        if (!AllReachable(room, blocked, shelves, none, doors, f)) shelves.RemoveAt(shelves.Count - 1);
                    }
                    int score = wallCount * 1000 + shelves.Count;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = new TycoonShopLayout
                    {
                        RoomID = roomID, Room = room, Doors = doors, Door = door, Centre = centre,
                        KeeperAt = new Vector3(k.x, 0f, k.z), CounterAt = new Vector3(c.x, 0f, c.z), Away = new Vector3(-dx, 0f, -dz),
                        WallSpots = wallCount, InnerSpots = shelves.Count - wallCount,
                        Shelves = shelves.OrderBy(Angle).Take(maxShelves).ToList(),
                    };
                }
            return best;
        }

        public static TycoonShopLayout Plan(TileManager tiles, (int x, int z) start, int maxShelves, bool protectOccupants)
        {
            CLayoutRoomTile Tile(int x, int z) => tiles.GetTile(new IntVector3(x, 0, z));
            int roomID = Tile(start.x, start.z).RoomID;
            var room = new HashSet<(int x, int z)> { start };
            var queue = new Queue<(int x, int z)>();
            queue.Enqueue(start);
            while (queue.Count > 0 && room.Count < 400)
            {
                (int x, int z) p = queue.Dequeue();
                foreach ((int dx, int dz) in Steps)
                {
                    var n = (p.x + dx, p.z + dz);
                    if (room.Contains(n)) continue;
                    CLayoutRoomTile t = Tile(n.Item1, n.Item2);
                    if (t.Type == RoomType.NoRoom || t.RoomID != roomID) continue;
                    room.Add(n);
                    queue.Enqueue(n);
                }
            }
            if (room.Count < 4) return null;

            var doors = new List<(int x, int z)>();
            var walls = new HashSet<(int x, int z)>();
            float cx = 0f, cz = 0f;
            foreach ((int x, int z) t in room)
            {
                cx += t.x; cz += t.z;
                foreach ((int dx, int dz) in Steps)
                {
                    var n = (t.x + dx, t.z + dz);
                    if (room.Contains(n)) continue;
                    bool walkable = false;
                    try { walkable = tiles.CanReach(new IntVector3(t.x, 0, t.z), new IntVector3(n.Item1, 0, n.Item2), false); }
                    catch (System.Exception) { }
                    if (walkable && Tile(n.Item1, n.Item2).Type != RoomType.NoRoom) { if (!doors.Contains(t)) doors.Add(t); }
                    else walls.Add(t);
                }
            }
            Vector3 centre = new Vector3(Mathf.Round(cx / room.Count), 0f, Mathf.Round(cz / room.Count));
            if (!room.Contains(Key(centre))) centre = new Vector3(start.x, 0f, start.z);
            if (doors.Count == 0) doors.Add(Key(centre));
            Vector3 door = Vector3.zero;
            foreach ((int x, int z) d in doors) door += new Vector3(d.x, 0f, d.z);
            door /= doors.Count;

            var occupied = new HashSet<(int x, int z)>();
            foreach ((int x, int z) t in room)
                if (tiles.GetOccupant(new IntVector3(t.x, 0, t.z), OccupancyLayer.Default) != Entity.Null) occupied.Add(t);

            Vector3 toFar = centre - door;
            Vector3 away = Mathf.Abs(toFar.x) > Mathf.Abs(toFar.z) ? new Vector3(Mathf.Sign(toFar.x), 0f, 0f) : new Vector3(0f, 0f, toFar.z == 0f ? 1f : Mathf.Sign(toFar.z));
            Vector3 keeperAt = centre;
            while (room.Contains(Key(keeperAt + away)) && !(protectOccupants && occupied.Contains(Key(keeperAt + away)))) keeperAt += away;
            Vector3 counterAt = keeperAt - away;
            if (!room.Contains(Key(counterAt))) { counterAt = keeperAt; keeperAt = counterAt + away; }
            (int x, int z) front = Key(counterAt - away);
            if (protectOccupants && (occupied.Contains(Key(keeperAt)) || occupied.Contains(Key(counterAt)) || occupied.Contains(front) || !room.Contains(front)))
            {
                float best = -1f;
                foreach ((int x, int z) t in room)
                    foreach ((int dx, int dz) in Steps)
                    {
                        var c = (t.x - dx, t.z - dz);
                        var f = (t.x - 2 * dx, t.z - 2 * dz);
                        if (occupied.Contains(t) || !room.Contains(c) || occupied.Contains(c) || !room.Contains(f) || occupied.Contains(f) || doors.Contains(c)) continue;
                        float d = (new Vector3(t.x, 0f, t.z) - door).sqrMagnitude;
                        if (d <= best) continue;
                        best = d;
                        keeperAt = new Vector3(t.x, 0f, t.z);
                        away = new Vector3(dx, 0f, dz);
                    }
                counterAt = keeperAt - away;
                front = Key(counterAt - away);
            }

            var blocked = new HashSet<(int x, int z)> { Key(keeperAt), Key(counterAt) };
            var keepFree = new HashSet<(int x, int z)> { front };
            foreach ((int x, int z) d in doors)
            {
                keepFree.Add(d);
                foreach ((int dx, int dz) in Steps) keepFree.Add((d.x + dx, d.z + dz));
            }
            var mustTouch = new List<(int x, int z)>();
            if (protectOccupants)
                foreach ((int x, int z) o in occupied)
                {
                    blocked.Add(o);
                    mustTouch.Add(o);
                    foreach ((int dx, int dz) in Steps) keepFree.Add((o.x + dx, o.z + dz));
                }
            foreach ((int x, int z) t in Path(room, blocked, doors, front)) keepFree.Add(t);

            var wallSpots = new List<(int x, int z)>();
            var innerSpots = new List<(int x, int z)>();
            foreach ((int x, int z) t in room)
            {
                if (blocked.Contains(t) || keepFree.Contains(t) || occupied.Contains(t)) continue;
                (walls.Contains(t) ? wallSpots : innerSpots).Add(t);
            }
            int Angle((int x, int z) a, (int x, int z) b) =>
                Mathf.Atan2(a.z - centre.z, a.x - centre.x).CompareTo(Mathf.Atan2(b.z - centre.z, b.x - centre.x));
            wallSpots.Sort(Angle);
            innerSpots.Sort(Angle);

            var plan = new TycoonShopLayout
            {
                RoomID = roomID, Room = room, Doors = doors, Door = door, Centre = centre, CounterAt = counterAt, KeeperAt = keeperAt,
                Away = away, WallSpots = wallSpots.Count, InnerSpots = innerSpots.Count, Occupied = occupied.Count,
            };
            foreach ((int x, int z) spot in wallSpots.Concat(innerSpots))
            {
                if (plan.Shelves.Count >= maxShelves) break;
                plan.Shelves.Add(spot);
                if (!AllReachable(room, blocked, plan.Shelves, mustTouch, doors, front)) plan.Shelves.RemoveAt(plan.Shelves.Count - 1);
            }
            return plan;
        }

        public static TycoonShopLayout PlanDisplay(TileManager tiles, (int x, int z) start, int maxShelves, List<Vector3> solids)
        {
            TycoonShopLayout plan = Plan(tiles, start, 0, protectOccupants: true);
            if (plan == null) return null;
            HashSet<(int x, int z)> room = plan.Room;
            var solidTiles = new HashSet<(int x, int z)>();
            foreach (Vector3 p in solids) { var k = Key(p); if (room.Contains(k)) solidTiles.Add(k); }
            foreach ((int x, int z) t in room)
                if (tiles.GetOccupant(new IntVector3(t.x, 0, t.z), OccupancyLayer.Default) != Entity.Null) solidTiles.Add(t);
            plan.Occupied = solidTiles.Count;

            int Cheb((int x, int z) a, (int x, int z) b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.z - b.z));
            int Clearance((int x, int z) t)
            {
                int best = 99;
                foreach ((int x, int z) d in plan.Doors) best = Mathf.Min(best, Cheb(t, d));
                foreach ((int x, int z) o in solidTiles) best = Mathf.Min(best, Cheb(t, o));
                return best;
            }

            float cx = 0f, cz = 0f;
            foreach ((int x, int z) t in room) { cx += t.x; cz += t.z; }
            cx /= room.Count; cz /= room.Count;
            int bestScore = int.MinValue;
            (int x, int z) keeper = Key(plan.KeeperAt);
            (int dx, int dz) dir = (Mathf.RoundToInt(plan.Away.x), Mathf.RoundToInt(plan.Away.z));
            foreach ((int dx, int dz) in Steps)
            {
                int extreme = int.MinValue;
                foreach ((int x, int z) t in room) extreme = Mathf.Max(extreme, t.x * dx + t.z * dz);
                (int x, int z) pick = (0, 0);
                float pickLateral = float.MaxValue;
                foreach ((int x, int z) t in room)
                {
                    if (t.x * dx + t.z * dz != extreme || solidTiles.Contains(t) || plan.Doors.Contains(t)) continue;
                    var c = (t.x - dx, t.z - dz);
                    var f = (t.x - 2 * dx, t.z - 2 * dz);
                    if (!room.Contains(c) || !room.Contains(f) || solidTiles.Contains(c) || solidTiles.Contains(f)) continue;
                    float lateral = dx != 0 ? Mathf.Abs(t.z - cz) : Mathf.Abs(t.x - cx);
                    if (lateral < pickLateral) { pickLateral = lateral; pick = t; }
                }
                if (pickLateral == float.MaxValue) continue;
                int score = Clearance(pick) * 10 - Mathf.RoundToInt(pickLateral);
                if (score > bestScore) { bestScore = score; keeper = pick; dir = (dx, dz); }
            }
            Vector3 away = new Vector3(dir.dx, 0f, dir.dz);
            plan.Away = away;
            plan.KeeperAt = new Vector3(keeper.x, 0f, keeper.z);
            plan.CounterAt = plan.KeeperAt - away;
            (int x, int z) counter = Key(plan.CounterAt);
            (int x, int z) front = Key(plan.CounterAt - away);

            var blocked = new HashSet<(int x, int z)>(solidTiles) { keeper, counter };
            var mustTouch = new List<(int x, int z)>(solidTiles);
            (int px, int pz) perp = (-dir.dz, dir.dx);
            var spots = new List<((int x, int z) t, int depth, int lateral)>();
            foreach ((int x, int z) t in room)
            {
                if (blocked.Contains(t) || plan.Doors.Contains(t)) continue;
                bool nearDoor = false;
                foreach ((int x, int z) d in plan.Doors) if (Cheb(t, d) <= 1) { nearDoor = true; break; }
                if (nearDoor) continue;
                bool nearSolid = false;
                foreach ((int x, int z) o in solidTiles) if (Cheb(t, o) <= 2) { nearSolid = true; break; }
                if (nearSolid) continue;
                int depth = (keeper.x - t.x) * dir.dx + (keeper.z - t.z) * dir.dz;
                int lateral = (t.x - keeper.x) * perp.px + (t.z - keeper.z) * perp.pz;
                if (depth != 0 && depth != 2) continue;
                if (depth == 2 && lateral == 0) continue;
                spots.Add((t, depth, lateral));
            }
            spots.Sort((a, b) => a.depth != b.depth ? a.depth.CompareTo(b.depth) : Mathf.Abs(a.lateral).CompareTo(Mathf.Abs(b.lateral)));

            plan.Shelves.Clear();
            plan.WallSpots = 0;
            plan.InnerSpots = spots.Count;
            foreach (var spot in spots)
            {
                if (plan.Shelves.Count >= maxShelves) break;
                plan.Shelves.Add(spot.t);
                if (!AllReachable(room, blocked, plan.Shelves, mustTouch, plan.Doors, front)) plan.Shelves.RemoveAt(plan.Shelves.Count - 1);
            }
            return plan;
        }

        public HashSet<(int x, int z)> Region = new HashSet<(int x, int z)>();

        public static TycoonShopLayout PlanTopCorner(TileManager tiles, (int x, int z) start, int maxShelves, List<Vector3> solids, float minZ)
        {
            TycoonShopLayout plan = Plan(tiles, start, 0, protectOccupants: true);
            if (plan == null) return null;
            HashSet<(int x, int z)> room = plan.Room;
            var solidTiles = new HashSet<(int x, int z)>();
            foreach (Vector3 p in solids) { var k = Key(p); if (room.Contains(k)) solidTiles.Add(k); }
            foreach ((int x, int z) t in room)
                if (tiles.GetOccupant(new IntVector3(t.x, 0, t.z), OccupancyLayer.Default) != Entity.Null) solidTiles.Add(t);
            plan.Occupied = solidTiles.Count;
            foreach ((int x, int z) t in room) if (t.z >= minZ - 0.01f) plan.Region.Add(t);
            if (plan.Region.Count < 6) return null;

            int Cheb((int x, int z) a, (int x, int z) b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.z - b.z));
            int top = int.MinValue;
            float cx = 0f;
            foreach ((int x, int z) t in plan.Region) { top = Mathf.Max(top, t.z); cx += t.x; }
            cx /= plan.Region.Count;

            (int x, int z) keeper = (0, 0);
            float best = float.MaxValue;
            foreach ((int x, int z) t in plan.Region)
            {
                if (t.z != top || solidTiles.Contains(t) || plan.Doors.Contains(t)) continue;
                var c = (t.x, t.z - 1);
                var f = (t.x, t.z - 2);
                if (!room.Contains(c) || !room.Contains(f) || solidTiles.Contains(c) || solidTiles.Contains(f)) continue;
                float lateral = Mathf.Abs(t.x - cx);
                if (lateral < best) { best = lateral; keeper = t; }
            }
            if (best == float.MaxValue) return null;
            plan.Away = Vector3.forward;
            plan.KeeperAt = new Vector3(keeper.x, 0f, keeper.z);
            plan.CounterAt = plan.KeeperAt - plan.Away;
            (int x, int z) counter = Key(plan.CounterAt);
            (int x, int z) front = Key(plan.CounterAt - plan.Away);

            var blocked = new HashSet<(int x, int z)>(solidTiles) { keeper, counter };
            var mustTouch = new List<(int x, int z)>(solidTiles);
            int bottom = int.MaxValue, minX = int.MaxValue, maxX = int.MinValue;
            foreach ((int x, int z) t in plan.Region) { bottom = Mathf.Min(bottom, t.z); minX = Mathf.Min(minX, t.x); maxX = Mathf.Max(maxX, t.x); }
            bool IsWall((int x, int z) t) => t.z == top || t.x == minX || t.x == maxX;
            var spots = new List<(int x, int z)>();
            foreach ((int x, int z) t in plan.Region)
            {
                if (blocked.Contains(t) || t.Equals(front) || plan.Doors.Contains(t)) continue;
                bool near = false;
                foreach ((int x, int z) d in plan.Doors) if (Cheb(t, d) <= 1) { near = true; break; }
                if (!near) spots.Add(t);
            }
            int Rank((int x, int z) t) => t.z == top ? 0 : IsWall(t) ? 1 : 2;
            spots.Sort((a, b) =>
            {
                int ra = Rank(a), rb = Rank(b);
                if (ra != rb) return ra.CompareTo(rb);
                if (ra == 1) return b.z.CompareTo(a.z);
                int da = Mathf.Min(a.x - minX, maxX - a.x) + (top - a.z), db = Mathf.Min(b.x - minX, maxX - b.x) + (top - b.z);
                return da.CompareTo(db);
            });
            plan.InnerSpots = spots.Count;
            var cornerBlocked = new HashSet<(int x, int z)>(blocked);
            foreach ((int x, int z) t in room) if (!plan.Region.Contains(t)) cornerBlocked.Add(t);
            var from = new List<(int x, int z)>();
            foreach ((int x, int z) t in plan.Region)
                if (t.z == bottom && !cornerBlocked.Contains(t) && room.Contains((t.x, t.z - 1)) && !solidTiles.Contains((t.x, t.z - 1))) from.Add(t);
            if (from.Count == 0) foreach ((int x, int z) t in plan.Region) if (t.z == bottom && !cornerBlocked.Contains(t)) from.Add(t);
            HashSet<(int x, int z)> before = Reached(plan.Region, cornerBlocked, plan.Shelves, from);
            if (!before.Contains(front)) Mod.LogWarning("Discount shop: the desk's front can't be reached from the corner's entrance.");
            foreach ((int x, int z) spot in spots)
            {
                if (plan.Shelves.Count >= maxShelves) break;
                if (!before.Contains(spot)) continue;
                plan.Shelves.Add(spot);
                HashSet<(int x, int z)> after = Reached(plan.Region, cornerBlocked, plan.Shelves, from);
                bool ok = after.Count == before.Count - plan.Shelves.Count && after.Contains(front) && after.Count > 0;
                foreach ((int x, int z) sh in plan.Shelves)
                {
                    bool touch = false;
                    foreach ((int dx, int dz) in Steps) if (after.Contains((sh.x + dx, sh.z + dz))) { touch = true; break; }
                    if (!touch) { ok = false; break; }
                }
                if (!ok) plan.Shelves.RemoveAt(plan.Shelves.Count - 1);
            }
            Mod.LogInfo($"Discount shop: corner entrance tile(s) {string.Join(" ", from)}.");
            return plan;
        }

        public string Map(List<Vector3> solids)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            foreach ((int x, int z) t in Room) { minX = Mathf.Min(minX, t.x); maxX = Mathf.Max(maxX, t.x); minZ = Mathf.Min(minZ, t.z); maxZ = Mathf.Max(maxZ, t.z); }
            var solidTiles = new HashSet<(int x, int z)>();
            if (solids != null) foreach (Vector3 p in solids) solidTiles.Add(Key(p));
            var sb = new System.Text.StringBuilder("x " + minX + ".." + maxX + " left to right, z " + maxZ + " at the top:\n");
            for (int z = maxZ; z >= minZ; z--)
            {
                sb.Append(z.ToString().PadLeft(4)).Append(' ');
                for (int x = minX; x <= maxX; x++)
                {
                    var t = (x, z);
                    char c = !Room.Contains(t) ? ' ' : t.Equals(Key(KeeperAt)) ? 'K' : t.Equals(Key(CounterAt)) ? 'C' : Shelves.Contains(t) ? 'S'
                        : Doors.Contains(t) ? 'D' : solidTiles.Contains(t) ? '#' : '.';
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static HashSet<(int x, int z)> Reached(HashSet<(int x, int z)> room, HashSet<(int x, int z)> blocked, List<(int x, int z)> shelves, List<(int x, int z)> doors)
        {
            var solid = new HashSet<(int x, int z)>(blocked);
            foreach ((int x, int z) s in shelves) solid.Add(s);
            var seen = new HashSet<(int x, int z)>();
            var queue = new Queue<(int x, int z)>();
            foreach ((int x, int z) d in doors) if (!solid.Contains(d) && seen.Add(d)) queue.Enqueue(d);
            while (queue.Count > 0)
            {
                (int x, int z) p = queue.Dequeue();
                foreach ((int dx, int dz) in Steps)
                {
                    var n = (p.x + dx, p.z + dz);
                    if (room.Contains(n) && !solid.Contains(n) && seen.Add(n)) queue.Enqueue(n);
                }
            }
            return seen;
        }

        private static bool AllReachable(HashSet<(int x, int z)> room, HashSet<(int x, int z)> blocked, List<(int x, int z)> shelves,
            List<(int x, int z)> mustTouch, List<(int x, int z)> doors, (int x, int z) front)
        {
            HashSet<(int x, int z)> seen = Reached(room, blocked, shelves, doors);
            if (!seen.Contains(front)) return false;
            foreach ((int x, int z) s in shelves.Concat(mustTouch))
            {
                bool touch = false;
                foreach ((int dx, int dz) in Steps) if (seen.Contains((s.x + dx, s.z + dz))) { touch = true; break; }
                if (!touch) return false;
            }
            int free = 0;
            foreach ((int x, int z) t in room) if (!blocked.Contains(t) && !shelves.Contains(t)) free++;
            return seen.Count == free;
        }

        private static List<(int x, int z)> Path(HashSet<(int x, int z)> room, HashSet<(int x, int z)> blocked, List<(int x, int z)> doors, (int x, int z) to)
        {
            var from = new Dictionary<(int x, int z), (int x, int z)>();
            var queue = new Queue<(int x, int z)>();
            foreach ((int x, int z) d in doors) if (!from.ContainsKey(d)) { from[d] = d; queue.Enqueue(d); }
            while (queue.Count > 0)
            {
                (int x, int z) p = queue.Dequeue();
                if (p.Equals(to)) break;
                foreach ((int dx, int dz) in Steps)
                {
                    var n = (p.x + dx, p.z + dz);
                    if (!room.Contains(n) || blocked.Contains(n) || from.ContainsKey(n)) continue;
                    from[n] = p;
                    queue.Enqueue(n);
                }
            }
            var path = new List<(int x, int z)>();
            if (!from.ContainsKey(to)) return path;
            for ((int x, int z) p = to; ; p = from[p]) { path.Add(p); if (from[p].Equals(p)) break; }
            return path;
        }
    }
}
