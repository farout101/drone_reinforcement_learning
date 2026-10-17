using System;
using System.Collections.Generic;

namespace DroneRL
{
    public sealed class RescueMission
    {
        public readonly bool[] Observed = new bool[81];
        public readonly bool[] Found = new bool[RescueMap.People.Length];
        public readonly bool[] Surveyed = new bool[RescueMap.SurveyGoals.Length];
        public readonly List<List<int>> FlightSegments = new List<List<int>>();
        public int State { get; private set; } = RescueMap.Home;
        public int Goal { get; private set; } = 4;
        public int Moves { get; private set; }
        public int Failures { get; private set; }
        public bool Complete { get; private set; }
        public bool Returning { get; private set; }
        public bool Recovering { get; private set; }
        public string Event { get; private set; } = "Launching survey flight";
        public int FoundCount { get { int n=0; foreach(bool f in Found) if(f)n++; return n; } }
        public float Coverage { get { int n=0; foreach(bool v in Observed) if(v)n++; return n/81f; } }
        int legSteps;
        readonly Random rng = new Random(608);
        public RescueMission()
        {
            FlightSegments.Add(new List<int>{ State });
            Observe(); SelectGoal();
        }
        void SelectGoal()
        {
            int best = -1, distance = int.MaxValue;
            for(int i=0;i<Surveyed.Length;i++)
            {
                if(Surveyed[i]) continue;
                int d=DroneLearning.Distance(State,RescueMap.SurveyGoals[i]);
                if(d<distance) { distance=d; best=i; }
            }
            Returning = best < 0;
            Goal = Returning ? 0 : RescueMap.SurveyGoals[best];
            legSteps=0;
            Event=Returning ? "Survey complete — returning to rescue base" : "Surveying sector "+(best+1)+" / 9";
        }
        public void Observe()
        {
            for(int z=0;z<9;z++) for(int x=0;x<9;x++)
                if(RescueMap.Visible(State,x,z,RescueMap.Surface(x,z)+0.06f)) Observed[x+z*9]=true;
            for(int i=0;i<Found.Length;i++)
            {
                if(Found[i]) continue;
                var p=RescueMap.People[i];
                if(!RescueMap.Visible(State,p.X,p.Z,RescueMap.Surface(p.X,p.Z)+1.0f)) continue;
                Found[i]=true; Event="Survivor located: "+p.Name;
            }
        }
        public void Step(DroneLearning agent, bool explore=false)
        {
            if(Complete || Recovering) return;
            int action=agent.Choose(State,Goal,2,explore?agent.Epsilon:0,rng);
            var t=DroneLearning.Move(State,action,++legSteps,Goal,2);
            Moves++;
            State=t.State;
            FlightSegments[FlightSegments.Count-1].Add(State);
            Observe();
            if(t.Success)
            {
                if(Returning)
                {
                    Complete=true; Event="Back at base — rescue report delivered";
                }
                else
                {
                    for(int i=0;i<Surveyed.Length;i++) if(RescueMap.SurveyGoals[i]==Goal) Surveyed[i]=true;
                    SelectGoal();
                }
            }
            else if(t.Done)
            {
                Failures++; Recovering=true;
                Event=t.Collision?"Collision — recovering drone for relaunch":"Flight stalled — recovering for relaunch";
            }
        }
        public void Relaunch()
        {
            if(!Recovering) return;
            State=RescueMap.Home; Recovering=false;
            FlightSegments.Add(new List<int>{State});
            Observe(); SelectGoal();
        }
        public RoadFinding RoadStatus(int index)
        {
            var road=RescueMap.Roads[index];
            bool all=true;
            int seen=0;
            var hazards=new List<HazardFinding>();
            RescueMap.Hazard worst=RescueMap.Hazard.Clear;
            foreach(int tile in road.Tiles)
            {
                if(!Observed[tile]) { all=false; continue; }
                seen++;
                var hazard=RescueMap.HazardAt(tile%9,tile/9);
                if(hazard!=RescueMap.Hazard.Clear)hazards.Add(new HazardFinding{x=tile%9,z=tile/9,kind=RescueMap.Reason(hazard)});
                if(hazard>worst) worst=hazard;
            }
            return new RoadFinding { name=road.Name, status=worst!=RescueMap.Hazard.Clear?"Blocked":all?"Passable":"Unknown",
                tiles=(int[])road.Tiles.Clone(),observedTiles=seen,observedHazards=hazards.ToArray(),
                reason=worst!=RescueMap.Hazard.Clear?RescueMap.Reason(worst):all?"Entire route observed clear":"Route not fully observed" };
        }
        [Serializable] public class HazardFinding { public int x,z; public string kind; }
        [Serializable] public class RoadFinding
        {
            public string name,status,reason;
            public int[] tiles;
            public int observedTiles;
            public HazardFinding[] observedHazards;
        }
        [Serializable] public class PersonFinding { public string name; public int x,z; public float height; }
        [Serializable] public class Segment { public int[] cells; }
        [Serializable] public class Report
        {
            public string mission="Flood rescue / simulated hazard assessment";
            public string mapVersion="village-v1";
            public int gridSize=9;
            public float gridSpacing=RescueMap.Spacing;
            public bool returnedToBase;
            public int moves,recoveries;
            public float coverage;
            public PersonFinding[] people;
            public RoadFinding[] roads;
            public Segment[] flightSegments;
        }
        public Report MakeReport()
        {
            var people=new List<PersonFinding>();
            for(int i=0;i<Found.Length;i++) if(Found[i])
            {
                var p=RescueMap.People[i];
                people.Add(new PersonFinding{name=p.Name,x=p.X,z=p.Z,height=RescueMap.Surface(p.X,p.Z)});
            }
            var roads=new RoadFinding[RescueMap.Roads.Length];
            for(int i=0;i<roads.Length;i++) roads[i]=RoadStatus(i);
            var segments=new Segment[FlightSegments.Count];
            for(int i=0;i<segments.Length;i++) segments[i]=new Segment{cells=FlightSegments[i].ToArray()};
            return new Report {returnedToBase=Complete&&State==RescueMap.Home,moves=Moves,recoveries=Failures,
                coverage=Coverage,people=people.ToArray(),roads=roads,flightSegments=segments};
        }
    }
}
