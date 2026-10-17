using System;

namespace DroneRL
{
    // One source of truth for the village art, aerial occupancy, and ground-route hazards.
    public static class RescueMap
    {
        public const float Spacing = 2.5f, WaterLevel = 0.28f;
        public static readonly int Home = DroneLearning.Encode(1, 0, 1);
        public static readonly int[] SurveyGoals = { 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        public enum Hazard { Clear, DeepWater, Rubble, CollapsedBridge }
        public struct Person
        {
            public string Name;
            public int X, Z;
            public Person(string name, int x, int z) { Name = name; X = x; Z = z; }
        }
        public static readonly Person[] People = {
            new Person("SOS 01 — rooftop", 2, 3), new Person("SOS 02 — upper roof", 6, 5),
            new Person("SOS 03 — stranded family", 7, 7), new Person("SOS 04 — west bank", 0, 7)
        };
        public sealed class Road
        {
            public string Name;
            public int[] Tiles;
            public Road(string name, params int[] tiles) { Name = name; Tiles = tiles; }
        }
        static int Tile(int x, int z) => x + z * DroneLearning.Size;
        public static readonly Road[] Roads = {
            new Road("North bridge", Tile(1,6),Tile(2,6),Tile(3,6),Tile(4,6),Tile(5,6),Tile(6,6),Tile(7,6)),
            new Road("Riverside road", Tile(5,3),Tile(5,4),Tile(5,5),Tile(5,6),Tile(5,7)),
            new Road("Eastern access", Tile(6,4),Tile(7,4),Tile(8,4)),
            new Road("South bridge", Tile(1,2),Tile(2,2),Tile(3,2),Tile(4,2),Tile(5,2),Tile(6,2),Tile(7,2)),
            new Road("West ridge road", Tile(1,1),Tile(1,2),Tile(1,3),Tile(1,4),Tile(1,5),Tile(1,6),Tile(1,7))
        };
        public static float WorldX(int x) => (x - 4) * Spacing;
        public static float WorldZ(int z) => (z - 4) * Spacing;
        public static float FlightHeight(int level) => 2 + level * 2.4f;
        public static float RiverCenter(float z) => -0.6f + 0.8f * (float)Math.Sin(z * 0.22f);
        public static float Ground(float x, float z)
        {
            float river = Math.Abs(x - RiverCenter(z));
            if (river < 2.7f) return -0.65f + 0.06f * (float)Math.Sin(z);
            if (x > 1.35f && x < 3.65f && z > -3.8f && z < 8.7f) return -0.25f;
            float bank = Math.Min(1, Math.Max(0, (river - 2.7f) / 1.2f));
            return -0.18f + bank * (0.70f + 0.12f * (float)Math.Sin(x * 0.6f) * (float)Math.Cos(z * 0.45f));
        }
        public static int BuildingLevels(int x, int z)
        {
            if (x == 2 && z == 3) return 1;
            if (x == 6 && z == 5) return 2;
            if (x == 7 && z == 7) return 1;
            if (x == 0 && z == 5) return 2;
            if (x == 6 && z == 1) return 2;
            if (x == 8 && z == 2) return 3;
            if (x == 2 && z == 7) return 2;
            if (x == 6 && z == 7) return 2;
            return 0;
        }
        public static float RoofHeight(int x, int z) => 0.6f + BuildingLevels(x, z) * 2.4f;
        public static bool IsRoad(int x, int z)
        {
            int tile = Tile(x,z);
            foreach (var road in Roads) foreach (int t in road.Tiles) if (t == tile) return true;
            return false;
        }
        public static float Surface(int x, int z)
        {
            if (BuildingLevels(x,z) > 0) return RoofHeight(x,z);
            if (z == 2 && x >= 2 && x <= 5) return 0.85f;
            return Math.Max(WaterLevel, Ground(WorldX(x), WorldZ(z)));
        }
        public static Hazard HazardAt(int x, int z)
        {
            if (z == 6 && (x == 3 || x == 4)) return Hazard.CollapsedBridge;
            if (x == 7 && z == 4) return Hazard.Rubble;
            if (z == 2 && x >= 2 && x <= 5) return Hazard.Clear;
            return WaterLevel - Ground(WorldX(x), WorldZ(z)) > 0.3f ? Hazard.DeepWater : Hazard.Clear;
        }
        public static string Reason(Hazard hazard)
        {
            if (hazard == Hazard.CollapsedBridge) return "Collapsed bridge";
            if (hazard == Hazard.Rubble) return "Road blocked by rubble";
            if (hazard == Hazard.DeepWater) return "Deep floodwater";
            return "Observed route clear";
        }
        public static bool Visible(int state, int x, int z, float targetHeight)
        {
            float sx = WorldX(DroneLearning.X(state)), sz = WorldZ(DroneLearning.Z(state));
            float sy = FlightHeight(DroneLearning.Y(state));
            float tx = WorldX(x), tz = WorldZ(z);
            if ((sx-tx)*(sx-tx)+(sz-tz)*(sz-tz) > 6.3f*6.3f) return false;
            for (int i = 1; i < 32; i++)
            {
                float t = i / 32f, px = sx + (tx-sx)*t, pz = sz + (tz-sz)*t, py = sy + (targetHeight-sy)*t;
                int cx = (int)Math.Round(px / Spacing + 4), cz = (int)Math.Round(pz / Spacing + 4);
                if (cx < 0 || cz < 0 || cx >= 9 || cz >= 9) continue;
                // Match the visible 2.0-wide footprints, including roof edges.
                if (BuildingLevels(cx,cz) > 0 && Math.Abs(px-WorldX(cx)) < 1.04f &&
                    Math.Abs(pz-WorldZ(cz)) < 1.04f && py < RoofHeight(cx,cz) + 0.02f) return false;
            }
            return true;
        }
    }
}
