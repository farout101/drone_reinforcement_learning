using System;
using System.Collections.Generic;

namespace DroneRL
{
    // The same deterministic 3D environment drives background training and visible flight.
    public sealed class DroneLearning
    {
        public const int Size = 9, Levels = 3, Cells = Size * Size * Levels;
        public const int Actions = 6, MaxSteps = 100, Stages = 3;
        public const float Discount = 0.97f, LearningRate = 0.22f;
        public static readonly int[] Goals = {
            Encode(1,0,1), Encode(2,0,2), Encode(1,0,3), Encode(2,0,1),
            Encode(1,2,1), Encode(4,2,1), Encode(7,2,1), Encode(1,2,4),
            Encode(4,2,4), Encode(7,2,4), Encode(1,2,7), Encode(4,2,7), Encode(7,2,7)
        };
        public static readonly string[] StageNames = { "Basic flight", "Survey transit", "Rescue navigation" };
        public readonly float[] Q = new float[Stages * Goals.Length * Cells * Actions];
        public readonly Queue<float> Returns = new Queue<float>();
        public readonly Queue<bool> Outcomes = new Queue<bool>();
        readonly Random random;
        public int Episodes { get; private set; }
        public int Stage { get; private set; }
        public int StageEpisodes { get; private set; }
        public int MasteryChecks { get; private set; }
        public int Successes { get; private set; }
        public int Collisions { get; private set; }
        public int Updates { get; private set; }
        public int State { get; private set; }
        public int GoalIndex { get; private set; }
        public float LastReturn { get; private set; }
        public float Epsilon => Math.Max(0.06f, (float)Math.Pow(0.998, StageEpisodes));
        int steps;
        float episodeReturn;

        public struct Transition
        {
            public int State;
            public float Reward;
            public bool Done, Success, Collision;
        }

        public struct Evaluation
        {
            public float SuccessRate, CollisionRate, AverageSteps, SuccessfulSteps, VerticalMoves;
            public int Trials;
        }

        public DroneLearning(int seed = 1337)
        {
            random = new Random(seed);
            BeginEpisode();
        }

        public static int Encode(int x, int y, int z) => x + z * Size + y * Size * Size;
        public static int X(int state) => state % Size;
        public static int Z(int state) => state / Size % Size;
        public static int Y(int state) => state / (Size * Size);
        public static int GoalCount(int stage) => stage == 2 ? Goals.Length : (stage + 1) * 4;
        public static int Index(int state, int goal, int stage, int action = 0)
            => ((stage * Goals.Length + goal) * Cells + state) * Actions + action;

        public static int ObstacleHeight(int x, int z) => RescueMap.BuildingLevels(x,z);

        public static bool Blocked(int x, int y, int z, int stage)
            => x < 0 || z < 0 || y < 0 || x >= Size || z >= Size || y >= Levels ||
               (stage == 2 && y < ObstacleHeight(x, z));

        public static int Distance(int state, int goal)
        {
            int target = Goals[goal];
            return Math.Abs(X(state) - X(target)) + Math.Abs(Y(state) - Y(target)) + Math.Abs(Z(state) - Z(target));
        }

        public static Transition Move(int state, int action, int step, int goal, int stage)
        {
            if (stage < 0 || stage >= Stages) throw new ArgumentOutOfRangeException(nameof(stage));
            if (goal < 0 || goal >= GoalCount(stage)) throw new ArgumentOutOfRangeException(nameof(goal));
            if (state < 0 || state >= Cells || Blocked(X(state), Y(state), Z(state), stage))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (action < 0 || action >= Actions) throw new ArgumentOutOfRangeException(nameof(action));
            int x = X(state), y = Y(state), z = Z(state);
            if (action == 0) z++;
            else if (action == 1) x++;
            else if (action == 2) z--;
            else if (action == 3) x--;
            else if (action == 4) y++;
            else y--;
            bool collision = Blocked(x, y, z, stage);
            int next = collision ? state : Encode(x, y, z);
            bool success = !collision && next == Goals[goal];
            bool done = collision || success || step >= MaxSteps;
            // Potential shaping is goal-conditioned and zero at ALL terminal states.
            float potential = -0.08f * Distance(state, goal);
            float nextPotential = done ? 0 : -0.08f * Distance(next, goal);
            float reward = -0.04f + Discount * nextPotential - potential;
            if (collision) reward -= 3;
            else if (success) reward += 8;
            else if (done) reward -= 1;
            return new Transition { State = next, Reward = reward, Done = done,
                Success = success, Collision = collision };
        }

        public int Choose(int state, int goal, int stage, float exploration, Random rng)
        {
            if (rng.NextDouble() < exploration) return rng.Next(Actions);
            int offset = Index(state, goal, stage), best = 0, ties = 1;
            for (int a = 1; a < Actions; a++)
            {
                float value = Q[offset + a], top = Q[offset + best];
                if (value > top + 0.00001f) { best = a; ties = 1; }
                else if (Math.Abs(value - top) <= 0.00001f && rng.Next(++ties) == 0) best = a;
            }
            return best;
        }

        public void Learn(int state, int action, Transition t, int goal, int stage)
        {
            int nextIndex = Index(t.State, goal, stage);
            float nextBest = Q[nextIndex];
            for (int a = 1; a < Actions; a++) nextBest = Math.Max(nextBest, Q[nextIndex + a]);
            float target = t.Reward + (t.Done ? 0 : Discount * nextBest);
            int index = Index(state, goal, stage, action);
            Q[index] += LearningRate * (target - Q[index]);
            Updates++;
        }

        public void TrainStep()
        {
            int action = Choose(State, GoalIndex, Stage, Epsilon, random);
            Transition t = Move(State, action, ++steps, GoalIndex, Stage);
            Learn(State, action, t, GoalIndex, Stage);
            State = t.State;
            episodeReturn += t.Reward;
            if (!t.Done) return;
            Episodes++; StageEpisodes++;
            if (t.Success) Successes++;
            if (t.Collision) Collisions++;
            LastReturn = episodeReturn;
            Returns.Enqueue(episodeReturn); Outcomes.Enqueue(t.Success);
            if (Returns.Count > 100) { Returns.Dequeue(); Outcomes.Dequeue(); }
            if (Stage < Stages - 1 && StageEpisodes >= 400 && StageEpisodes % 100 == 0)
            {
                MasteryChecks = Evaluate().SuccessRate >= 0.9f ? MasteryChecks + 1 : 0;
                if (MasteryChecks >= 3)
                {
                    // Transfer earlier goal-conditioned values; separate tables prevent map aliasing.
                    int tableSize = Goals.Length * Cells * Actions;
                    Array.Copy(Q, Stage * tableSize, Q, (Stage + 1) * tableSize, tableSize);
                    Stage++; StageEpisodes = 0; MasteryChecks = 0;
                    Returns.Clear(); Outcomes.Clear();
                }
            }
            BeginEpisode();
        }

        public static int Launch(Random rng, int goal)
        {
            int state;
            do { state = Encode(rng.Next(3), 0, rng.Next(3)); } while (state == Goals[goal]);
            return state;
        }

        public static int SampleStart(Random rng, int goal, int stage)
        {
            if (stage == 0) return Launch(rng, goal);
            int state;
            do
            {
                state = rng.NextDouble() < 0.5 ? Goals[rng.Next(GoalCount(stage))] : rng.Next(Cells);
            } while (state == Goals[goal] || Blocked(X(state),Y(state),Z(state),stage));
            return state;
        }

        void BeginEpisode()
        {
            GoalIndex = random.Next(GoalCount(Stage));
            State = SampleStart(random, GoalIndex, Stage);
            steps = 0; episodeReturn = 0;
        }

        public float RecentSuccess()
        {
            int count = 0;
            foreach (bool success in Outcomes) if (success) count++;
            return Outcomes.Count == 0 ? 0 : (float)count / Outcomes.Count;
        }

        // Independent episodes on the SAME fixed map; no policy, training RNG, or curriculum updates.
        public Evaluation Evaluate(bool randomPolicy = false, int trials = 120, int stage = -1, int seed = 7001)
        {
            if (trials <= 0) throw new ArgumentOutOfRangeException(nameof(trials));
            if (stage == -1) stage = Stage;
            if (stage < 0 || stage >= Stages) throw new ArgumentOutOfRangeException(nameof(stage));
            var rng = new Random(seed);
            int wins = 0, collisions = 0, moves = 0, successfulSteps = 0, vertical = 0;
            for (int i = 0; i < trials; i++)
            {
                int goal = i % GoalCount(stage), state = SampleStart(rng, goal, stage);
                for (int s = 1; s <= MaxSteps; s++)
                {
                    int action = Choose(state, goal, stage, randomPolicy ? 1 : 0, rng);
                    var t = Move(state, action, s, goal, stage);
                    moves++;
                    if (Y(state) != Y(t.State)) vertical++;
                    state = t.State;
                    if (t.Success) { wins++; successfulSteps += s; }
                    if (t.Collision) collisions++;
                    if (t.Done) break;
                }
            }
            return new Evaluation { SuccessRate = (float)wins / trials, CollisionRate = (float)collisions / trials,
                AverageSteps = (float)moves / trials, SuccessfulSteps = wins == 0 ? 0 : (float)successfulSteps / wins,
                VerticalMoves = (float)vertical / trials, Trials = trials };
        }

        [Serializable]
        public class Snapshot
        {
            public int version = 3;
            public float[] values;
            public int episodes, successes, collisions, updates, stage, stageEpisodes, masteryChecks;
        }

        public Snapshot Save() => new Snapshot { values = (float[])Q.Clone(), episodes = Episodes,
            successes = Successes, collisions = Collisions, updates = Updates, stage = Stage,
            stageEpisodes = StageEpisodes, masteryChecks = MasteryChecks };

        public void Load(Snapshot snapshot)
        {
            if (snapshot == null || snapshot.version != 3 || snapshot.values == null || snapshot.values.Length != Q.Length)
                throw new ArgumentException("Saved policy does not match the rescue terrain demo (version 3).");
            if (snapshot.stage < 0 || snapshot.stage >= Stages || snapshot.episodes < 0 ||
                snapshot.stageEpisodes < 0 || snapshot.stageEpisodes > snapshot.episodes || snapshot.successes < 0 ||
                snapshot.collisions < 0 || (long)snapshot.successes + snapshot.collisions > snapshot.episodes ||
                snapshot.updates < snapshot.episodes || snapshot.masteryChecks < 0 || snapshot.masteryChecks > 2)
                throw new ArgumentException("Saved curriculum data is invalid.");
            foreach (float value in snapshot.values)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Invalid Q-value.");
            Array.Copy(snapshot.values, Q, Q.Length);
            Episodes = snapshot.episodes; Successes = snapshot.successes; Collisions = snapshot.collisions;
            Updates = snapshot.updates; Stage = snapshot.stage; StageEpisodes = snapshot.stageEpisodes;
            MasteryChecks = snapshot.masteryChecks;
            Returns.Clear(); Outcomes.Clear();
            BeginEpisode();
        }
    }
}
