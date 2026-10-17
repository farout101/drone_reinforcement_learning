using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneRL.Editor
{
    public static class DroneDemoSetup
    {
        public const string ScenePath = "Assets/Scenes/DroneLearning.unity";

        [InitializeOnLoadMethod]
        static void OnImport()
        {
            // One-time setup after the scripts compile. Never replace an unsaved scene.
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode &&
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene();
            };
        }

        [MenuItem("Drone RL/Open Demo")]
        public static void OpenDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene();
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void CreateScene()
        {
            bool canOpen = true;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) canOpen = false;
            Scene previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("Drone reinforcement learning demo");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<DroneDemo>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var entry in EditorBuildSettings.scenes) if (entry.path != ScenePath) scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            if (canOpen) EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Drone RL demo ready. Open Assets/Scenes/DroneLearning.unity and press Play.");
        }

        [MenuItem("Drone RL/Verify Learning")]
        public static void VerifyLearning()
        {
            Debug.Log(RunChecks());
        }

        public static string RunChecks()
        {
            int home=RescueMap.Home;
            var hit=DroneLearning.Move(DroneLearning.Encode(1,0,3),1,1,0,2);
            Check(hit.Collision&&hit.Done,"Low building must block the lowest flight level.");
            Check(!DroneLearning.Move(DroneLearning.Encode(1,1,3),1,1,0,2).Collision,"Drone can fly above a low roof.");
            Check(DroneLearning.Move(DroneLearning.Encode(7,2,2),1,1,0,2).Collision,"Tall building blocks all three levels.");
            Check(!DroneLearning.Blocked(3,0,6,2),"A collapsed ground bridge must not block aerial transit.");
            Check(DroneLearning.Move(DroneLearning.Encode(0,0,0),5,1,0,0).Collision,"Floor boundary.");
            Check(DroneLearning.Move(DroneLearning.Encode(0,2,0),4,1,0,0).Collision,"Ceiling boundary.");
            Check(DroneLearning.Move(DroneLearning.Encode(0,0,0),3,1,0,0).Collision,"Arena boundary.");
            var timeout=DroneLearning.Move(DroneLearning.Encode(0,0,0),0,DroneLearning.MaxSteps,0,0);
            Check(timeout.Done&&!timeout.Success&&!timeout.Collision,"Timeout termination.");
            int before=DroneLearning.Encode(1,0,0);
            var success=DroneLearning.Move(before,0,1,0,0);
            var agent=new DroneLearning();agent.Q[DroneLearning.Index(home,0,0)]=100;
            agent.Learn(before,0,success,0,0);
            Check(success.Success&&Math.Abs(agent.Q[DroneLearning.Index(before,0,0)]-DroneLearning.LearningRate*success.Reward)<0.0001f,
                "Goal reward and no terminal bootstrap.");
            Check(agent.Q[DroneLearning.Index(before,1,0)]==0&&agent.Q[DroneLearning.Index(before,0,1)]==0,"Goal/stage indexing isolation.");
            for(int stage=0;stage<3;stage++)CheckReachability(stage);
            Check(!RescueMap.Visible(DroneLearning.Encode(1,0,3),3,3,0.4f),"Buildings must occlude low sensor rays.");
            Check(RescueMap.Visible(DroneLearning.Encode(1,2,3),2,3,4.0f),"Roof survivor visible from an unobstructed high flight.");
            Check(!RescueMap.Visible(home,0,7,1),"Sensors must not discover distant survivors.");
            var knowledge=new RescueMission();Array.Clear(knowledge.Observed,0,knowledge.Observed.Length);
            Array.Clear(knowledge.Found,0,knowledge.Found.Length);
            Check(knowledge.MakeReport().people.Length==0&&knowledge.RoadStatus(0).status=="Unknown","Report cannot leak hidden people or hazards.");
            knowledge.Observed[3+6*9]=true;
            Check(knowledge.RoadStatus(0).status=="Blocked"&&knowledge.RoadStatus(0).reason=="Collapsed bridge","Observed damage establishes blockage.");
            Check(knowledge.RoadStatus(0).observedHazards.Length==1&&knowledge.RoadStatus(0).observedHazards[0].x==3,
                "Exported damage coordinates include only observed segments.");
            foreach(int tile in RescueMap.Roads[3].Tiles)knowledge.Observed[tile]=true;
            Check(knowledge.RoadStatus(3).status=="Passable","Only an entirely observed clear road is passable.");
            var failure=new RescueMission();var novice=new DroneLearning();
            for(int i=0;i<500&&!failure.Recovering&&!failure.Complete;i++)failure.Step(novice);
            Check(failure.Recovering,"Novice flight should exercise recovery.");failure.Relaunch();
            Check(failure.FlightSegments.Count==2&&failure.State==home&&failure.Failures==1,"Relaunch records a separate flight segment.");
            var first=new DroneLearning(17);var second=new DroneLearning(17);
            for(int i=0;i<3000;i++){first.TrainStep();second.TrainStep();}
            first.Evaluate(seed:991);
            for(int i=0;i<3000;i++){first.TrainStep();second.TrainStep();}
            Check(first.State==second.State&&first.GoalIndex==second.GoalIndex&&first.Episodes==second.Episodes&&first.Stage==second.Stage,
                "Evaluation preserves training state and randomness.");
            for(int i=0;i<first.Q.Length;i++)Check(first.Q[i]==second.Q[i],"Evaluation does not update the policy.");
            var reports=new List<string>();
            foreach(int seed in new[]{1337,42,2026})
            {
                agent=new DroneLearning(seed);int budget=0;
                while((agent.Stage<2||agent.StageEpisodes<30000)&&budget++<5000000)agent.TrainStep();
                Check(agent.Stage==2,"Curriculum must unlock rescue navigation.");
                var learned=agent.Evaluate(stage:2,seed:9001,trials:260);
                var random=agent.Evaluate(true,stage:2,seed:9001,trials:260);
                Check(learned.SuccessRate>=0.95f&&learned.SuccessRate>random.SuccessRate+0.5f,"Trained transit must beat random.");
                var mission=new RescueMission();budget=0;
                while(!mission.Complete&&budget++<5000){if(mission.Recovering)mission.Relaunch();else mission.Step(agent);}
                Check(mission.Complete&&mission.State==home&&mission.FoundCount==4,"Survey must find survivors and return home.");
                var report=mission.MakeReport();
                Check(report.returnedToBase&&report.people.Length==4&&report.roads[0].reason=="Collapsed bridge"&&
                    report.roads[1].reason=="Deep floodwater"&&report.roads[2].reason=="Road blocked by rubble"&&
                    report.roads[3].status=="Passable"&&report.roads[4].status=="Passable","Delivered report must match observed damage and clear routes.");
                int found=mission.FoundCount;mission.Observe();Check(mission.FoundCount==found,"Discoveries are idempotent.");
                var saved=agent.Save();var copy=new DroneLearning();copy.Load(saved);
                Check(copy.Stage==agent.Stage&&copy.Epsilon==agent.Epsilon&&copy.Evaluate(seed:9001).SuccessRate==agent.Evaluate(seed:9001).SuccessRate,"Policy snapshot round trip.");
                saved.version=2;bool rejected=false;
                try{copy.Load(saved);}catch(ArgumentException){rejected=true;}
                Check(rejected,"Older terrain policies must be rejected.");
                saved=agent.Save();saved.values[0]=float.NaN;rejected=false;
                try{copy.Load(saved);}catch(ArgumentException){rejected=true;}
                Check(rejected&&!float.IsNaN(copy.Q[0]),"Corrupt policy rejection must be atomic.");
                reports.Add($"seed={seed}: transit={learned.SuccessRate:P0}, survivors={mission.FoundCount}, coverage={mission.Coverage:P0}, mission moves={mission.Moves}, recoveries={mission.Failures}");
            }
            return "Rescue RL checks passed. "+string.Join("; ",reports);
        }

        static void CheckReachability(int stage)
        {
            var reached = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(DroneLearning.Encode(0, 0, 0)); reached.Add(queue.Peek());
            while (queue.Count > 0)
            {
                int state = queue.Dequeue();
                for (int action = 0; action < DroneLearning.Actions; action++)
                {
                    var t = DroneLearning.Move(state, action, 1, 0, stage);
                    if (!t.Collision && reached.Add(t.State)) queue.Enqueue(t.State);
                }
            }
            for (int goal = 0; goal < DroneLearning.GoalCount(stage); goal++)
                Check(reached.Contains(DroneLearning.Goals[goal]), $"Stage {stage + 1}, target {goal} must be reachable.");
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Drone RL verification failed: " + message);
        }
    }
}
