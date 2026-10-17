using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DroneRL
{
    public sealed class DroneDemo : MonoBehaviour
    {
        public DroneLearning Learner { get; private set; }
        public RescueMission Mission { get; private set; }
        public RescueMission.Report LastReport { get; private set; }
        public RescueWorld World { get; private set; }
        public int PreviewState => Mission==null?RescueMap.Home:Mission.State;
        public int PreviewEpisodes => CompletedMissions;
        public int CompletedMissions { get; private set; }
        public bool Training=true;
        public float TrainingStepsPerSecond=6000;
        const string SaveKey="DroneRL.RescuePolicy.v3";
        float trainBudget,moveTimer,completeTimer,noticeTimer;
        bool paused,trainingOpen,reportOpen,deliveredTab,exploring,reported;
        int stage=-1,nextEvaluation;
        string notice="";
        DroneLearning.Evaluation evaluation,baseline;
        GUIStyle title,heading,body,small,micro,button,selected,panel,big;
        readonly List<Texture2D> textures=new List<Texture2D>();
        Vector2 reportScroll;
        static readonly Color Mint=new Color(0.37f,0.87f,0.76f),Amber=new Color(1,0.64f,0.28f);
        static readonly Rect Header=new Rect(24,22,520,85),Stats=new Rect(560,22,584,85),MapPanel=new Rect(1168,138,248,278);
        static readonly Rect Footer=new Rect(24,750,680,126),Toolbar=new Rect(740,830,676,46);
        static readonly Rect TrainingPanel=new Rect(1048,440,368,374),ReportPanel=new Rect(24,128,464,600);
        public static Vector3 Cell(int state,float height=float.NaN)
        {var p=RescueWorld.Cell(state);if(!float.IsNaN(height))p.y=height;return p;}

        void Start()
        {
            Application.runInBackground=true;
            Learner=new DroneLearning();
            World=new RescueWorld(transform);
            World.Controls.PointerOverUI=PointerOverUI;
            RefreshEvaluation();
        }
        void RefreshEvaluation()
        {
            evaluation=Learner.Evaluate(seed:9001);baseline=Learner.Evaluate(true,seed:9001);
            nextEvaluation=Learner.Episodes+1000;
        }
        public void RestartSortie()
        {
            if(Learner.Stage<2){Notify("Rescue flights unlock after basic flight and survey training.");return;}
            Mission=new RescueMission();reported=false;moveTimer=0.6f;completeTimer=0;reportOpen=false;
            World.Drone.position=RescueWorld.Cell(RescueMap.Home);
        }
        public void ResetLearning()
        {
            Learner=new DroneLearning();Mission=null;LastReport=null;CompletedMissions=0;
            stage=-1;trainBudget=0;Training=true;paused=false;reported=false;reportOpen=false;
            World.Drone.position=RescueWorld.Cell(RescueMap.Home);
            RefreshEvaluation();Notify("Learning restarted. The drone is calibrating at base.");
        }
        void Notify(string text){notice=text;noticeTimer=7;}
        void Update()
        {
            if(Learner==null)return;
            noticeTimer-=Time.unscaledDeltaTime;
            if(paused)return;
            if(Training)
            {
                trainBudget+=Mathf.Min(Time.unscaledDeltaTime,0.25f)*TrainingStepsPerSecond;
                int count=Mathf.Min((int)trainBudget,5000);trainBudget-=count;
                for(int i=0;i<count;i++)Learner.TrainStep();
            }
            if(stage!=Learner.Stage)
            {
                stage=Learner.Stage;RefreshEvaluation();
                if(stage==2)RestartSortie();
                else {Mission=null;World.Drone.position=RescueWorld.Cell(RescueMap.Home);}
            }
            else if(Learner.Episodes>=nextEvaluation)RefreshEvaluation();
            if(Mission!=null)
            {
                moveTimer-=Time.unscaledDeltaTime;
                if(Mission.Complete)
                {
                    if(!reported)
                    {
                        reported=true;LastReport=Mission.MakeReport();CompletedMissions++;
                        reportOpen=true;deliveredTab=true;completeTimer=12;
                        Notify("Report delivered: "+LastReport.people.Length+" survivor locations recorded.");
                        Debug.Log("[Rescue] Returned to base. Survivors="+LastReport.people.Length+", coverage="+LastReport.coverage.ToString("P0")+", recoveries="+LastReport.recoveries);
                    }
                    completeTimer-=Time.unscaledDeltaTime;
                    if(completeTimer<=0)RestartSortie();
                }
                else if(moveTimer<=0)
                {
                    if(Mission.Recovering)
                    {
                        Mission.Relaunch();World.Drone.position=RescueWorld.Cell(Mission.State);moveTimer=0.8f;
                    }
                    else
                    {
                        int found=Mission.FoundCount;Mission.Step(Learner,exploring);
                        if(Mission.FoundCount>found)Notify("Survivor detected — coordinates added to field assessment.");
                        moveTimer=Mission.Recovering?1.4f:0.27f;
                    }
                }
                Vector3 destination=RescueWorld.Cell(Mission.State),before=World.Drone.position;
                World.Drone.position=Vector3.Lerp(before,destination,1-Mathf.Exp(-16*Time.unscaledDeltaTime));
                Vector3 motion=destination-before;
                var tilt=Quaternion.Euler(motion.z*8,0,-motion.x*8);
                World.Drone.rotation=Quaternion.Slerp(World.Drone.rotation,tilt,Time.unscaledDeltaTime*8);
            }
            else World.Drone.position=RescueWorld.Cell(RescueMap.Home)+Vector3.up*Mathf.Sin(Time.unscaledTime*2)*0.06f;
            World.Animate(Mission);
        }
        public string ExportReport()
        {
            if(LastReport==null)throw new InvalidOperationException("Return to base before exporting a delivered report.");
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../RescueReports"));Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"latest-report.json");File.WriteAllText(path,JsonUtility.ToJson(LastReport,true));
            Notify("Report exported to RescueReports/latest-report.json");return path;
        }
        bool PointerOverUI(Vector2 screen)
        {
            Vector2 p=new Vector2(screen.x*1440/Screen.width,(Screen.height-screen.y)*900/Screen.height);
            return Header.Contains(p)||Stats.Contains(p)||MapPanel.Contains(p)||Footer.Contains(p)||Toolbar.Contains(p)||
                (trainingOpen&&TrainingPanel.Contains(p))||(reportOpen&&ReportPanel.Contains(p));
        }
        Texture2D Tex(Color color,bool rounded=false)
        {
            int size=rounded?32:1;var t=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var c=color;
                if(rounded){float dx=Mathf.Max(6-x,x-(size-7)),dy=Mathf.Max(6-y,y-(size-7));if(dx>0&&dy>0&&dx*dx+dy*dy>36)c.a=0;}
                pixels[x+y*size]=c;
            }
            t.SetPixels(pixels);t.Apply();textures.Add(t);return t;
        }
        void Styles()
        {
            title=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            heading=new GUIStyle(GUI.skin.label){fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=Mint}};
            body=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true,normal={textColor=new Color(0.88f,0.92f,0.91f)}};
            small=new GUIStyle(body){fontSize=11,normal={textColor=new Color(0.65f,0.74f,0.75f)}};
            micro=new GUIStyle(small){fontSize=10,wordWrap=false};
            big=new GUIStyle(title){fontSize=24};
            panel=new GUIStyle{normal={background=Tex(new Color(0.055f,0.10f,0.13f,0.96f),true)},border=new RectOffset(7,7,7,7)};
            button=new GUIStyle(GUI.skin.button){fontSize=12,padding=new RectOffset(10,10,7,7),border=new RectOffset(7,7,7,7),
                normal={background=Tex(new Color(0.15f,0.23f,0.26f),true),textColor=Color.white},
                hover={background=Tex(new Color(0.22f,0.34f,0.36f),true),textColor=Color.white},
                active={background=Tex(Mint,true),textColor=new Color(0.04f,0.13f,0.14f)}};
            selected=new GUIStyle(button){normal={background=Tex(new Color(0.19f,0.45f,0.42f),true),textColor=Color.white}};
        }
        static void Fill(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        void Box(Rect r){GUI.Box(r,GUIContent.none,panel);}
        static void Segment(Vector2 a,Vector2 b,Color c,float width=2)
        {
            Vector2 delta=b-a;
            if(delta.sqrMagnitude<0.01f)return;
            if(Mathf.Abs(delta.y)<0.1f)Fill(new Rect(Mathf.Min(a.x,b.x),a.y-width/2,Mathf.Abs(delta.x),width),c);
            else if(Mathf.Abs(delta.x)<0.1f)Fill(new Rect(a.x-width/2,Mathf.Min(a.y,b.y),width,Mathf.Abs(delta.y)),c);
            else
            {
                int steps=Mathf.CeilToInt(delta.magnitude/width);
                for(int i=0;i<=steps;i++){var p=Vector2.Lerp(a,b,(float)i/steps);Fill(new Rect(p.x-width/2,p.y-width/2,width,width),c);}
            }
        }
        void Label(Rect rect,string text,GUIStyle style,Color? color=null)
        {var old=GUI.color;if(color.HasValue)GUI.color=color.Value;GUI.Label(rect,text,style);GUI.color=old;}
        bool Button(Rect r,string text,bool on=false)=>GUI.Button(r,text,on?selected:button);
        void KPI(float x,string value,string label)
        {
            Label(new Rect(x,40,132,32),value,big);Label(new Rect(x,75,132,18),label,micro);
        }
        void OnGUI()
        {
            if(Learner==null||World==null)return;if(title==null)Styles();
            var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
            Box(Header);Label(new Rect(42,34,460,36),"RAVEN / RESCUE",title);
            Label(new Rect(43,74,460,19),"FLOOD RESPONSE    /    AUTONOMOUS SURVEY",small);
            Box(Stats);KPI(582,(Mission?.FoundCount??0)+" / 4","SURVIVORS LOCATED");
            KPI(728,(Mission?.Coverage??0).ToString("P0"),"AREA OBSERVED");KPI(875,CompletedMissions.ToString(),"REPORTS DELIVERED");
            Label(new Rect(1020,43,112,26),paused?"PAUSED":Mission==null?"CALIBRATING":Mission.Complete?"AT BASE":"LIVE FLIGHT",heading);
            Label(new Rect(1020,75,112,20),"NAV "+evaluation.SuccessRate.ToString("P0"),small);
            DrawWorldTags();DrawMap();DrawFooter();DrawToolbar();
            if(trainingOpen)DrawTraining();if(reportOpen)DrawReport();
            GUI.matrix=old;
        }
        void DrawFooter()
        {
            Box(Footer);
            string phase=Mission==null?"CALIBRATING FLIGHT":Mission.Complete?"REPORT DELIVERED":Mission.Recovering?"RECOVERING":Mission.Returning?"RETURNING TO BASE":"SEARCH & ASSESS";
            Label(new Rect(42,764,640,24),phase,heading);
            string text=noticeTimer>0?notice:Mission==null?"Training stage "+(Learner.Stage+1)+" / 3 — "+DroneLearning.StageNames[Learner.Stage]:Mission.Event;
            Label(new Rect(42,794,630,36),text,body);
            Label(new Rect(42,844,440,20),Mission==null?"Training runs in the background; rescue flights unlock in stage 3.":
                "Move "+Mission.Moves+"  •  Flight level "+(DroneLearning.Y(Mission.State)+1)+" / 3  •  Recoveries "+Mission.Failures,small);
            if(Button(new Rect(493,837,92,28),paused?"Resume":"Pause",paused))paused=!paused;
            if(Button(new Rect(591,837,92,28),"New sortie"))RestartSortie();
            Fill(new Rect(24,879,680,20),new Color(0.055f,0.10f,0.13f,0.86f));
            Label(new Rect(33,882,662,17),"RMB orbit  ·  MMB pan  ·  Scroll zoom  ·  1 / 2 / 3 views  ·  R reset",micro);
        }
        void DrawToolbar()
        {
            Box(Toolbar);float x=750;
            string[] labels={"1  Isometric","2  Top-down","3  Follow","Overlays","Field report","Training"};
            for(int i=0;i<6;i++)
            {
                bool on=i==0?World.Controls.Mode==RescueCamera.View.Isometric:i==1?World.Controls.Mode==RescueCamera.View.Top:
                    i==2?World.Controls.Mode==RescueCamera.View.Follow:i==3?World.Overlays:i==4?reportOpen:trainingOpen;
                if(Button(new Rect(x,839,102,28),labels[i],on))
                {
                    if(i<3)World.Controls.SetView((RescueCamera.View)i);
                    else if(i==3)World.Overlays=!World.Overlays;
                    else if(i==4)reportOpen=!reportOpen;
                    else trainingOpen=!trainingOpen;
                }
                x+=110;
            }
        }
        Vector2 MapPoint(int state,Rect map)
            =>new Vector2(map.x+(DroneLearning.X(state)+0.5f)*map.width/9,map.y+(8-DroneLearning.Z(state)+0.5f)*map.height/9);
        void DrawMap()
        {
            Box(MapPanel);Label(new Rect(1184,150,190,20),"MISSION MAP",heading);Label(new Rect(1384,150,24,20),"N ↑",small);
            var map=new Rect(1184,179,216,216);float step=24;
            for(int z=0;z<9;z++)for(int x=0;x<9;x++)
            {
                bool known=Mission!=null&&Mission.Observed[x+z*9];
                Color c=!known?new Color(0.13f,0.21f,0.24f):RescueMap.HazardAt(x,z)==RescueMap.Hazard.DeepWater?new Color(0.18f,0.43f,0.49f):new Color(0.36f,0.47f,0.36f);
                if(known&&RescueMap.BuildingLevels(x,z)>0)c=new Color(0.55f,0.50f,0.42f);
                Fill(new Rect(map.x+x*step+1,map.y+(8-z)*step+1,step-2,step-2),c);
            }
            if(Mission!=null)
            {
                for(int i=0;i<RescueMap.Roads.Length;i++)
                {
                    var road=RescueMap.Roads[i];var r=Mission.RoadStatus(i);
                    Color c=r.status=="Blocked"?new Color(1,0.40f,0.27f):r.status=="Passable"?Mint:new Color(0.38f,0.49f,0.51f);
                    for(int k=1;k<road.Tiles.Length;k++)Segment(MapPoint(road.Tiles[k-1],map),MapPoint(road.Tiles[k],map),c,2);
                }
                var points=Mission.FlightSegments[Mission.FlightSegments.Count-1];
                for(int i=1;i<points.Count;i++)Segment(MapPoint(points[i-1],map),MapPoint(points[i],map),Amber,1);
                for(int i=0;i<Mission.Found.Length;i++)if(Mission.Found[i])
                {var p=RescueMap.People[i];var v=MapPoint(DroneLearning.Encode(p.X,0,p.Z),map);Label(new Rect(v.x-5,v.y-9,16,20),"+",heading,Amber);}
            }
            var home=MapPoint(RescueMap.Home,map);Label(new Rect(home.x-6,home.y-9,18,20),"H",small,Color.white);
            var drone=MapPoint(PreviewState,map);Label(new Rect(drone.x-6,drone.y-10,20,22),"▲",heading,Mint);
            Label(new Rect(1184,398,69,15),"Blocked",small,new Color(1,0.40f,0.27f));
            Label(new Rect(1258,398,65,15),"Clear",small,Mint);
            Label(new Rect(1324,398,80,15),"Unknown",small);
        }
        Vector2 Project(Vector3 point,out bool visible)
        {
            var p=World.Camera.WorldToScreenPoint(point);visible=p.z>0&&p.x>0&&p.x<Screen.width&&p.y>0&&p.y<Screen.height;
            return new Vector2(p.x*1440/Screen.width,(Screen.height-p.y)*900/Screen.height);
        }
        void Tag(Vector3 point,string text,Color color)
        {
            var p=Project(point,out bool visible);if(!visible)return;
            var r=new Rect(p.x-58,p.y-13,116,25);
            if(MapPanel.Overlaps(r)||Header.Overlaps(r)||Stats.Overlaps(r)||(reportOpen&&ReportPanel.Overlaps(r))||(trainingOpen&&TrainingPanel.Overlaps(r)))return;
            Fill(r,new Color(0.04f,0.10f,0.13f,0.87f));Label(new Rect(r.x+8,r.y+4,105,18),text,small,color);
        }
        void DrawWorldTags()
        {
            var p=Project(World.Drone.position,out bool visible);
            if(visible&&!Header.Contains(p)&&!Stats.Contains(p)&&!MapPanel.Contains(p)&&
                !(reportOpen&&ReportPanel.Contains(p))&&!(trainingOpen&&TrainingPanel.Contains(p)))
            {
                for(int side=-1;side<=1;side+=2)for(int up=-1;up<=1;up+=2)
                {
                    float x=p.x+side*18,y=p.y+up*18;
                    Fill(new Rect(side<0?x:x-7,y,7,2),Mint);
                    Fill(new Rect(x,up<0?y:y-7,2,7),Mint);
                }
            }
            Tag(World.Drone.position+Vector3.up*0.8f,"RAVEN 01",Mint);
            if(!World.Overlays)return;
            if(Mission!=null)for(int i=0;i<Mission.Found.Length;i++)if(Mission.Found[i])
                Tag(World.People[i].position+Vector3.up*1.65f,"SOS 0"+(i+1)+" / FOUND",Amber);
            if(Mission!=null&&!Mission.Complete)Tag(RescueWorld.Cell(DroneLearning.Goals[Mission.Goal])+Vector3.up*0.5f,
                Mission.Returning?"RETURN / BASE":"SURVEY POINT",Mint);
            Tag(RescueWorld.GroundPoint(1,1,0.5f),"RESCUE BASE",Color.white);
        }
        void DrawTraining()
        {
            Box(TrainingPanel);GUILayout.BeginArea(new Rect(1066,454,332,346));
            GUILayout.Label("NAVIGATION / Q-LEARNING",heading);
            GUILayout.Label("Stage "+(Learner.Stage+1)+" / 3 — "+DroneLearning.StageNames[Learner.Stage],body);
            GUILayout.Label($"Episodes {Learner.Episodes:N0}   Updates {Learner.Updates:N0}\nExploration {Learner.Epsilon:P0}   Recent success {Learner.RecentSuccess():P0}",small);
            GUILayout.Label($"Evaluation {evaluation.SuccessRate:P0}   Random {baseline.SuccessRate:P0}\nCollisions {evaluation.CollisionRate:P0}   Moves / success {evaluation.SuccessfulSteps:F1}",small);
            if(GUILayout.Button(Training?"Freeze learning":"Resume learning",button,GUILayout.Height(28))){Training=!Training;RefreshEvaluation();}
            GUILayout.Label($"Training speed: {TrainingStepsPerSecond:F0} steps/sec",small);
            TrainingStepsPerSecond=GUILayout.HorizontalSlider(TrainingStepsPerSecond,100,30000);
            exploring=GUILayout.Toggle(exploring,"Exploration in visible rescue flight");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Save",button))
            {PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(Learner.Save()));PlayerPrefs.Save();Notify("Rescue navigation policy saved.");}
            GUI.enabled=PlayerPrefs.HasKey(SaveKey);
            if(GUILayout.Button("Load",button))
            {
                try{Learner.Load(JsonUtility.FromJson<DroneLearning.Snapshot>(PlayerPrefs.GetString(SaveKey)));stage=-1;Mission=null;RefreshEvaluation();Notify("Rescue navigation policy loaded.");}
                catch(Exception ex){Notify(ex.Message);}
            }
            GUI.enabled=true;GUILayout.EndHorizontal();
            if(GUILayout.Button("Reset learning and mission",button,GUILayout.Height(26)))ResetLearning();
            var chart=GUILayoutUtility.GetRect(330,33);Fill(chart,new Color(0.03f,0.07f,0.09f));int index=0;
            foreach(float value in Learner.Returns)
            {float h=Mathf.Clamp01((value+6)/16)*30;Fill(new Rect(chart.x+index++*3.3f,chart.yMax-h,2,h),value>0?Mint:Amber);}
            GUILayout.Label("Navigation learns. Search planning and sensors use rules.",small);
            GUILayout.EndArea();
        }
        void DrawReport()
        {
            Box(ReportPanel);GUILayout.BeginArea(new Rect(44,143,424,568));
            GUILayout.Label("RESCUE / FIELD ASSESSMENT",heading);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Live assessment",deliveredTab?button:selected))deliveredTab=false;
            GUI.enabled=LastReport!=null;
            if(GUILayout.Button("Last delivery",deliveredTab?selected:button))deliveredTab=true;
            GUI.enabled=true;GUILayout.EndHorizontal();
            var r=deliveredTab?LastReport:Mission?.MakeReport();
            if(r==null)GUILayout.Label("No flight observations yet. Rescue sorties start after stage 3 unlocks.",body);
            else
            {
                reportScroll=GUILayout.BeginScrollView(reportScroll,GUILayout.Height(416));
                GUILayout.Label(r.returnedToBase?"DELIVERED AT RESCUE BASE":"LIVE — NOT YET DELIVERED",heading);
                GUILayout.Label($"Coverage {r.coverage:P0}  /  {r.moves} moves  /  {r.recoveries} recoveries",small);
                GUILayout.Space(8);GUILayout.Label("SURVIVOR LOCATIONS",heading);
                if(r.people.Length==0)GUILayout.Label("No survivors detected yet.",body);
                foreach(var p in r.people)GUILayout.Label($"+ {p.name}\n   Grid ({p.x}, {p.z}) • elevation {p.height:F1}",body);
                GUILayout.Space(10);GUILayout.Label("GROUND ROUTES FOR RESCUERS",heading);
                foreach(var road in r.roads)
                {
                    GUI.color=road.status=="Blocked"?new Color(1,0.58f,0.4f):road.status=="Passable"?Mint:Color.white;
                    GUILayout.Label(road.name+" / "+road.status,body);
                    GUI.color=Color.white;
                    GUILayout.Label(road.reason,small);GUILayout.Space(4);
                    if(road.observedHazards.Length>0)
                    {
                        var h=road.observedHazards[0];
                        GUILayout.Label("Observed damage at grid ("+h.x+", "+h.z+")"+(road.observedHazards.Length>1?" and "+(road.observedHazards.Length-1)+" more segments":""),small);
                    }
                }
                GUILayout.Space(8);GUILayout.Label("Simulation rules assess flood depth, rubble and bridge damage. Unknown routes remain unverified.",small);
                GUILayout.EndScrollView();
            }
            GUI.enabled=LastReport!=null;
            if(GUILayout.Button("Export delivered report / JSON",button,GUILayout.Height(30)))
            {try{ExportReport();}catch(Exception ex){Notify(ex.Message);}}
            GUI.enabled=true;
            if(GUILayout.Button("Close report",button,GUILayout.Height(27)))reportOpen=false;
            GUILayout.EndArea();
        }
        void OnDestroy()
        {
            World?.Dispose();foreach(var texture in textures)if(texture!=null)Destroy(texture);
        }
    }
}
