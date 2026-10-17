using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneRL
{
    // Procedural low-poly diorama. Geometry and reports read the same RescueMap data.
    public sealed class RescueWorld : IDisposable
    {
        public Transform Drone { get; private set; }
        public Camera Camera { get; private set; }
        public RescueCamera Controls { get; private set; }
        public readonly List<Transform> People = new List<Transform>();
        readonly Transform root;
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> rotors = new List<Transform>();
        readonly List<LineRenderer> ripples = new List<LineRenderer>();
        readonly List<Building> buildings = new List<Building>();
        readonly Dictionary<Material,Material> ghosts = new Dictionary<Material,Material>();
        readonly List<LineRenderer> roadLines = new List<LineRenderer>();
        readonly Material ink, cream, roof, blue, amber, mint, white, water, wood, stone;
        LineRenderer trail;
        sealed class Building { public Renderer Body; public Renderer[] Parts; }
        public bool Overlays = true;
        public static Vector3 Cell(int state) => new Vector3(RescueMap.WorldX(DroneLearning.X(state)),
            RescueMap.FlightHeight(DroneLearning.Y(state)),RescueMap.WorldZ(DroneLearning.Z(state)));
        public static Vector3 GroundPoint(int x,int z,float lift=0) => new Vector3(RescueMap.WorldX(x),RescueMap.Surface(x,z)+lift,RescueMap.WorldZ(z));

        public RescueWorld(Transform parent)
        {
            root=parent;
            ink=Mat(new Color(0.09f,0.16f,0.19f)); cream=Mat(new Color(0.81f,0.77f,0.64f));
            roof=Mat(new Color(0.60f,0.29f,0.21f)); blue=Mat(new Color(0.22f,0.37f,0.41f));
            amber=Mat(new Color(1f,0.48f,0.12f),true); mint=Mat(new Color(0.29f,0.85f,0.72f),true);
            white=Mat(new Color(0.93f,0.94f,0.87f)); water=Mat(new Color(0.12f,0.47f,0.54f));
            water.SetFloat("_Smoothness",0.75f); stone=Mat(new Color(0.41f,0.45f,0.42f));
            wood=Mat(new Color(0.43f,0.33f,0.22f));
            BuildTerrain(); BuildRoads(); BuildVillage(); BuildBase(); BuildDrone(); BuildPeople();
            trail=Line("Flight trail",amber,0.045f);
            var cameraGO=new GameObject("Rescue camera"); cameraGO.transform.SetParent(root,false);
            Camera=cameraGO.AddComponent<Camera>(); Camera.tag="MainCamera";
            Camera.clearFlags=CameraClearFlags.SolidColor; Camera.backgroundColor=new Color(0.66f,0.78f,0.81f);
            Camera.fieldOfView=44; Camera.nearClipPlane=0.15f; Camera.farClipPlane=180;
            cameraGO.AddComponent<AudioListener>();
            Controls=cameraGO.AddComponent<RescueCamera>(); Controls.Drone=Drone;
            Controls.OcclusionUpdate=()=>FadeOccluders();
            var lightGO=new GameObject("Late morning sun"); lightGO.transform.SetParent(root,false);
            lightGO.transform.rotation=Quaternion.Euler(48,-35,0);
            var sun=lightGO.AddComponent<Light>(); sun.type=LightType.Directional; sun.color=new Color(1,0.93f,0.79f);
            sun.intensity=1.3f; sun.shadows=LightShadows.Soft; sun.shadowStrength=0.65f;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(0.65f,0.75f,0.78f);
            RenderSettings.ambientEquatorColor=new Color(0.47f,0.55f,0.53f);
            RenderSettings.ambientGroundColor=new Color(0.27f,0.31f,0.29f);
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogColor=Camera.backgroundColor;
            RenderSettings.fogStartDistance=65; RenderSettings.fogEndDistance=110;
        }
        Material Mat(Color color,bool emit=false)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;
            m.SetFloat("_Smoothness",0.15f);
            if(emit){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*0.18f);}
            materials.Add(m);return m;
        }
        GameObject Shape(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent??root,false);
            go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());return go;
        }
        LineRenderer Line(string name,Material mat,float width,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent??root,false);
            var l=go.AddComponent<LineRenderer>();l.sharedMaterial=mat;l.startWidth=l.endWidth=width;
            l.generateLightingData=true;
            l.numCapVertices=3;return l;
        }
        void Ring(LineRenderer line,Vector3 center,float radius,int points=48)
        {
            line.positionCount=points+1;
            for(int i=0;i<=points;i++){float a=i*Mathf.PI*2/points;line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
        }
        void BuildTerrain()
        {
            Shape("Deep water",PrimitiveType.Cube,new Vector3(0,-0.72f,0),new Vector3(110,0.1f,110),Mat(new Color(0.18f,0.40f,0.46f)));
            Shape("Earth section",PrimitiveType.Cube,new Vector3(0,-0.8f,0),new Vector3(23.5f,1.2f,23.5f),Mat(new Color(0.28f,0.30f,0.23f)));
            var groundMats=new[]{Mat(new Color(0.43f,0.55f,0.33f)),Mat(new Color(0.49f,0.59f,0.37f)),
                Mat(new Color(0.54f,0.61f,0.40f)),Mat(new Color(0.62f,0.58f,0.41f))};
            var vertices=new List<Vector3>();var triangles=new List<int>[4];for(int i=0;i<4;i++)triangles[i]=new List<int>();
            const int n=42;const float extent=11.75f;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float x0=-extent+x*extent*2/n,x1=-extent+(x+1)*extent*2/n;
                float z0=-extent+z*extent*2/n,z1=-extent+(z+1)*extent*2/n;
                var p=new[]{new Vector3(x0,RescueMap.Ground(x0,z0),z0),new Vector3(x0,RescueMap.Ground(x0,z1),z1),
                    new Vector3(x1,RescueMap.Ground(x1,z1),z1),new Vector3(x1,RescueMap.Ground(x1,z0),z0)};
                float shade=Mathf.PerlinNoise(x0*0.16f+31,z0*0.16f+23);
                int[] order={0,1,2,0,2,3};int group=RescueMap.Ground((x0+x1)*0.5f,(z0+z1)*0.5f)<0.25f?3:shade<0.43f?0:shade>0.57f?2:1;
                foreach(int k in order){triangles[group].Add(vertices.Count);vertices.Add(p[k]);}
            }
            var mesh=new Mesh{name="Faceted rescue terrain"};mesh.SetVertices(vertices);mesh.subMeshCount=4;
            for(int i=0;i<4;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();meshes.Add(mesh);
            var terrain=new GameObject("Floodplain terrain");terrain.transform.SetParent(root,false);
            terrain.AddComponent<MeshFilter>().sharedMesh=mesh;terrain.AddComponent<MeshRenderer>().sharedMaterials=groundMats;
            Shape("Floodwater",PrimitiveType.Cube,new Vector3(0,RescueMap.WaterLevel-0.025f,0),new Vector3(23.45f,0.05f,23.45f),water);
            var foam=Mat(new Color(0.40f,0.72f,0.73f));
            for(int i=0;i<7;i++)
            {
                var line=Line("Water ripples",foam,0.022f);ripples.Add(line);
            }
        }
        void BuildRoads()
        {
            var asphalt=Mat(new Color(0.34f,0.36f,0.31f));
            for(int z=0;z<9;z++)for(int x=0;x<9;x++)
            {
                if(!RescueMap.IsRoad(x,z))continue;
                if((z==2||z==6)&&x>=2&&x<=5)continue;
                float ground=RescueMap.Ground(RescueMap.WorldX(x),RescueMap.WorldZ(z));
                bool vertical=x==1||x==5;
                Shape("Road",PrimitiveType.Cube,new Vector3(RescueMap.WorldX(x),ground+0.025f,RescueMap.WorldZ(z)),
                    new Vector3(vertical?1.15f:2.5f,0.045f,vertical?2.5f:1.15f),asphalt);
            }
            for(int bridge=0;bridge<2;bridge++)for(int x=2;x<=5;x++)
            {
                int z=bridge==0?2:6;
                if(bridge==1&&(x==3||x==4))
                {
                    var broken=Shape("Collapsed bridge fragment",PrimitiveType.Cube,new Vector3(RescueMap.WorldX(x),0.4f,RescueMap.WorldZ(z)+0.3f),new Vector3(1.8f,0.12f,0.8f),wood);
                    broken.transform.Rotate(8,x==3?18:-13,12);continue;
                }
                Shape("Bridge deck",PrimitiveType.Cube,new Vector3(RescueMap.WorldX(x),0.77f,RescueMap.WorldZ(z)),new Vector3(2.48f,0.16f,1.55f),wood);
                for(int side=-1;side<=1;side+=2)
                {
                    Shape("Bridge rail",PrimitiveType.Cube,new Vector3(RescueMap.WorldX(x),1.13f,RescueMap.WorldZ(z)+side*0.68f),new Vector3(2.48f,0.06f,0.07f),cream);
                    Shape("Bridge post",PrimitiveType.Cube,new Vector3(RescueMap.WorldX(x)-0.95f,0.7f,RescueMap.WorldZ(z)+side*0.68f),new Vector3(0.1f,0.95f,0.1f),wood);
                }
            }
            for(int i=0;i<RescueMap.Roads.Length;i++)roadLines.Add(Line(RescueMap.Roads[i].Name+" assessment",mint,0.05f));
        }
        void BuildVillage()
        {
            var rng=new System.Random(51);
            var alternateWalls=Mat(new Color(0.65f,0.73f,0.68f));
            for(int z=0;z<9;z++)for(int x=0;x<9;x++)
            {
                int levels=RescueMap.BuildingLevels(x,z);if(levels==0)continue;
                var group=new GameObject("Damaged house "+x+","+z);group.transform.SetParent(root,false);
                group.transform.localPosition=new Vector3(RescueMap.WorldX(x),0,RescueMap.WorldZ(z));
                float height=RescueMap.RoofHeight(x,z);
                var walls=Shape("Walls",PrimitiveType.Cube,new Vector3(0,(height+0.18f)*0.5f-0.1f,0),new Vector3(2,height-0.18f,2),(x+z)%3==0?alternateWalls:cream,group.transform);
                Shape("Roof deck",PrimitiveType.Cube,new Vector3(0,height-0.09f,0),new Vector3(2.08f,0.18f,2.08f),roof,group.transform);
                Shape("Roof damage",PrimitiveType.Cube,new Vector3(0.5f,height+0.08f,0.55f),new Vector3(0.65f,0.16f,0.45f),stone,group.transform);
                if(x==6&&z==1)
                {
                    var fallen=Shape("Fallen roof panel",PrimitiveType.Cube,new Vector3(-0.65f,0.9f,-0.85f),new Vector3(0.8f,0.12f,1.1f),roof,group.transform);
                    fallen.transform.localRotation=Quaternion.Euler(52,10,-8);
                }
                for(int floor=0;floor<levels;floor++)for(int side=-1;side<=1;side+=2)
                {
                    Shape("Window",PrimitiveType.Cube,new Vector3(side*0.53f,1.15f+floor*2.4f,-1.015f),new Vector3(0.42f,0.60f,0.035f),blue,group.transform);
                    Shape("Window",PrimitiveType.Cube,new Vector3(1.015f,1.15f+floor*2.4f,side*0.53f),new Vector3(0.035f,0.60f,0.42f),blue,group.transform);
                }
                Shape("Door",PrimitiveType.Cube,new Vector3(-0.28f,0.74f,-1.02f),new Vector3(0.43f,1.0f,0.04f),ink,group.transform);
                buildings.Add(new Building{Body=walls.GetComponent<Renderer>(),Parts=group.GetComponentsInChildren<Renderer>()});
            }
            for(int i=0;i<12;i++)
            {
                float x=RescueMap.WorldX(7)+(float)rng.NextDouble()*1.7f-0.85f;
                float z=RescueMap.WorldZ(4)+(float)rng.NextDouble()*1.6f-0.8f;
                var rubble=Shape("Road rubble",PrimitiveType.Cube,new Vector3(x,0.65f,z),new Vector3(0.3f+(float)rng.NextDouble()*0.45f,0.28f,0.4f),i%2==0?stone:roof);
                rubble.transform.rotation=Quaternion.Euler(0,rng.Next(180),rng.Next(25));
            }
            var leaves=Mat(new Color(0.24f,0.41f,0.29f));var leaves2=Mat(new Color(0.35f,0.49f,0.31f));
            for(int i=0;i<23;i++)
            {
                float x=i%2==0?-11.0f:11.1f,z=-10.5f+(i/2)*1.9f;
                if(z>11)continue;
                float g=RescueMap.Ground(x,z);float h=1.0f+(float)rng.NextDouble()*0.8f;
                Shape("Tree trunk",PrimitiveType.Cylinder,new Vector3(x,g+h*0.5f,z),new Vector3(0.16f,h*0.5f,0.16f),wood);
                Shape("Tree canopy",PrimitiveType.Sphere,new Vector3(x,g+h+0.25f,z),new Vector3(1.25f,1.7f,1.2f),i%3==0?leaves2:leaves);
            }
        }
        void BuildBase()
        {
            Vector3 p=GroundPoint(1,1,0.05f);
            Shape("Rescue helipad",PrimitiveType.Cylinder,p,new Vector3(1.9f,0.06f,1.9f),ink);
            for(int side=-1;side<=1;side+=2)
                Shape("Helipad H",PrimitiveType.Cube,p+new Vector3(side*0.32f,0.07f,0),new Vector3(0.13f,0.025f,0.95f),white);
            Shape("Helipad H",PrimitiveType.Cube,p+Vector3.up*0.07f,new Vector3(0.7f,0.025f,0.14f),white);
            Shape("Rescue equipment",PrimitiveType.Cube,new Vector3(-10,0.95f,-9.8f),new Vector3(1.6f,0.8f,1.1f),cream);
            Shape("Medical stripe",PrimitiveType.Cube,new Vector3(-10,1.37f,-9.8f),new Vector3(0.8f,0.025f,0.2f),amber);
        }
        void BuildDrone()
        {
            Drone=new GameObject("RAVEN 01 / rescue drone").transform;Drone.SetParent(root,false);Drone.position=Cell(RescueMap.Home);
            Shape("Rescue shell",PrimitiveType.Cube,Vector3.zero,new Vector3(0.65f,0.22f,0.85f),white,Drone);
            Shape("Rescue orange top",PrimitiveType.Cube,new Vector3(0,0.13f,0),new Vector3(0.42f,0.06f,0.62f),amber,Drone);
            Shape("Rescue cross",PrimitiveType.Cube,new Vector3(0,0.166f,0),new Vector3(0.28f,0.012f,0.08f),white,Drone);
            Shape("Rescue cross",PrimitiveType.Cube,new Vector3(0,0.168f,0),new Vector3(0.08f,0.012f,0.28f),white,Drone);
            Shape("Search camera",PrimitiveType.Sphere,new Vector3(0,-0.18f,0.25f),Vector3.one*0.22f,ink,Drone);
            Shape("Camera lens",PrimitiveType.Sphere,new Vector3(0,-0.18f,0.35f),Vector3.one*0.10f,mint,Drone);
            for(int i=0;i<4;i++)
            {
                var pos=new Vector3(i%2==0?-0.55f:0.55f,0,i<2?-0.55f:0.55f);
                var arm=Shape("Arm",PrimitiveType.Cube,pos*0.5f,new Vector3(0.09f,0.08f,0.8f),ink,Drone);
                arm.transform.localRotation=Quaternion.Euler(0,i%2==0?(i<2?45:-45):(i<2?-45:45),0);
                Shape("Motor",PrimitiveType.Cylinder,pos,new Vector3(0.17f,0.08f,0.17f),ink,Drone);
                var rotor=Shape("Propeller",PrimitiveType.Cube,pos+Vector3.up*0.11f,new Vector3(0.72f,0.025f,0.06f),blue,Drone);rotors.Add(rotor.transform);
                var guard=Line("Propeller guard",ink,0.025f,Drone);guard.useWorldSpace=false;Ring(guard,pos+Vector3.up*0.08f,0.39f,24);
                Shape("Navigation lamp",PrimitiveType.Sphere,pos+new Vector3(0,-0.03f,0),Vector3.one*0.12f,i<2?amber:mint,Drone);
            }
        }
        void BuildPeople()
        {
            foreach(var person in RescueMap.People)
            {
                var p=new GameObject(person.Name).transform;p.SetParent(root,false);p.position=GroundPoint(person.X,person.Z,0.04f);People.Add(p);
                Shape("Life jacket",PrimitiveType.Capsule,new Vector3(0,0.46f,0),new Vector3(0.24f,0.38f,0.24f),amber,p);
                Shape("Head",PrimitiveType.Sphere,new Vector3(0,0.95f,0),Vector3.one*0.27f,cream,p);
                var arm=Shape("Waving arm",PrimitiveType.Cube,new Vector3(0.25f,0.77f,0),new Vector3(0.1f,0.45f,0.1f),amber,p);
                arm.transform.localRotation=Quaternion.Euler(0,0,-25);
            }
        }
        public void Animate(RescueMission mission)
        {
            foreach(var rotor in rotors)rotor.Rotate(0,1900*Time.unscaledDeltaTime,0);
            for(int i=0;i<ripples.Count;i++)
            {
                float z=-8+i*2.7f;float x=RescueMap.RiverCenter(z);
                Ring(ripples[i],new Vector3(x,RescueMap.WaterLevel+0.03f,z),0.28f+Mathf.Repeat(Time.unscaledTime*0.13f+i*0.21f,0.7f));
            }
            trail.enabled=Overlays&&mission!=null;
            foreach(var line in roadLines)line.enabled=Overlays&&mission!=null;
            if(mission==null)return;
            var points=mission.FlightSegments[mission.FlightSegments.Count-1];
            int start=Math.Max(0,points.Count-24);
            trail.positionCount=points.Count-start;
            for(int i=start;i<points.Count;i++)trail.SetPosition(i-start,Cell(points[i]));
            for(int i=0;i<roadLines.Count;i++)
            {
                var l=roadLines[i];l.enabled=Overlays;var road=RescueMap.Roads[i];var assessment=mission.RoadStatus(i);
                Color color=assessment.status=="Blocked"?new Color(1,0.35f,0.22f):assessment.status=="Passable"?new Color(0.2f,0.85f,0.61f):new Color(0.56f,0.64f,0.62f);
                // Each line owns a cheap color instance, created once on first use.
                if(l.sharedMaterial==mint)l.sharedMaterial=Mat(color,true);
                l.sharedMaterial.color=color;
                l.positionCount=road.Tiles.Length;
                for(int k=0;k<road.Tiles.Length;k++)l.SetPosition(k,GroundPoint(road.Tiles[k]%9,road.Tiles[k]/9,0.13f));
            }
        }
        Material Ghost(Material original)
        {
            if(ghosts.TryGetValue(original,out var ghost))return ghost;
            ghost=new Material(original);var color=ghost.color;color.a=0.18f;ghost.color=color;
            ghost.SetFloat("_Surface",1);ghost.SetFloat("_ZWrite",0);
            ghost.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);ghost.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");ghost.SetOverrideTag("RenderType","Transparent");ghost.renderQueue=3000;
            ghosts.Add(original,ghost);materials.Add(ghost);return ghost;
        }
        readonly Dictionary<Renderer,Material> originalMaterials=new Dictionary<Renderer,Material>();
        void FadeOccluders()
        {
            Vector3 dir=Drone.position-Camera.transform.position;float length=dir.magnitude;
            var ray=new Ray(Camera.transform.position,dir.normalized);
            foreach(var b in buildings)
            {
                bool fade=b.Body.bounds.IntersectRay(ray,out float distance)&&distance<length-0.4f;
                foreach(var renderer in b.Parts)
                {
                    if(!originalMaterials.TryGetValue(renderer,out var original)){original=renderer.sharedMaterial;originalMaterials[renderer]=original;}
                    renderer.sharedMaterial=fade?Ghost(original):original;
                }
            }
        }
        public void Dispose()
        {
            foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);
            foreach(var m in meshes)if(m!=null)UnityEngine.Object.Destroy(m);
        }
    }
}
