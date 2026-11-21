using System.Collections.Generic;
using UnityEngine;
namespace PCSim
{
    public static class SimWorld
    {
        static readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
        public static Transform PCMount;
        public static Material Surface(Color color)
        {
            if(!materials.TryGetValue(color,out var m))
            {
                var baseMat=Resources.Load<Material>("Surface");
                m=baseMat!=null?new Material(baseMat):new Material(Shader.Find("Standard"));
                m.color=color; m.SetFloat("_Glossiness",.35f); materials[color]=m;
            }
            return m;
        }
        public static GameObject Shape(string name,Transform parent,Vector3 pos,Vector3 size,Color color,PrimitiveType primitive=PrimitiveType.Cube,bool solid=true)
        {
            var g=GameObject.CreatePrimitive(primitive); g.name=name; g.transform.SetParent(parent,false);
            g.transform.localPosition=pos; g.transform.localScale=size; g.GetComponent<Renderer>().sharedMaterial=Surface(color);
            if(!solid) Object.Destroy(g.GetComponent<Collider>());
            return g;
        }
        public static TextMesh Label(string text,Transform parent,Vector3 pos,float scale=.045f)
        {
            var g=new GameObject("Label"); g.transform.SetParent(parent,false);g.transform.localPosition=pos;
            var t=g.AddComponent<TextMesh>(); t.font=Resources.GetBuiltinResource<Font>("Arial.ttf");
            g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
            t.text=text;t.characterSize=scale;t.fontSize=48;t.color=Color.white;
            t.anchor=TextAnchor.MiddleCenter; t.alignment=TextAlignment.Center;return t;
        }
        public static void Target(GameObject obj,string page) { obj.AddComponent<UseTarget>().page=page; }
        public static void Build(SimApp app)
        {
            RenderSettings.ambientLight=new Color(.55f,.6f,.7f);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;
            sun.transform.rotation=Quaternion.Euler(45,-35,0);sun.shadows=LightShadows.Soft;
            var room=new GameObject("Apartment").transform;
            Shape("Wood floor",room,new Vector3(0,-.12f,0),new Vector3(16,.24f,12),new Color(.3f,.22f,.16f));
            for(int x=-7;x<8;x++) Shape("Plank seam",room,new Vector3(x,.006f,0),new Vector3(.015f,.008f,12),new Color(.12f,.09f,.07f),solid:false);
            var wall=new Color(.7f,.72f,.73f);
            Shape("Left wall",room,new Vector3(-8,1.7f,0),new Vector3(.2f,3.4f,12),wall);
            Shape("Right wall",room,new Vector3(8,1.7f,0),new Vector3(.2f,3.4f,12),wall);
            Shape("Window sill",room,new Vector3(0,.6f,6),new Vector3(16,1.2f,.2f),wall);
            Shape("Window header",room,new Vector3(0,3.2f,6),new Vector3(16,.5f,.2f),wall);
            for(int x=-8;x<=8;x+=4) Shape("Window frame",room,new Vector3(x,2.1f,6),new Vector3(.12f,1.9f,.22f),Color.gray);
            // Transparent opening is blocked with an invisible collider, leaving a front door to the street.
            var glass=Shape("Window collider",room,new Vector3(0,2.1f,6),new Vector3(16,1.9f,.1f),Color.white);glass.GetComponent<Renderer>().enabled=false;
            foreach(var x in new[]{-4.6f,4.6f}) Shape("Entrance wall",room,new Vector3(x,1.7f,-6),new Vector3(6.8f,3.4f,.2f),wall);
            Shape("Door lintel",room,new Vector3(0,3,-6),new Vector3(2.4f,.8f,.2f),wall);
            Shape("Rug",room,new Vector3(0,.02f,0),new Vector3(4,.025f,3),new Color(.1f,.2f,.27f));
            Shape("Sofa seat",room,new Vector3(-5,.4f,-2),new Vector3(2.8f,.7f,1),new Color(.13f,.22f,.3f));
            Shape("Sofa back",room,new Vector3(-5,.9f,-2.45f),new Vector3(2.8f,.9f,.2f),new Color(.13f,.22f,.3f));
            Shape("Kitchen",room,new Vector3(6,.5f,-3),new Vector3(2,1,3),new Color(.23f,.24f,.25f));
            Shape("Countertop",room,new Vector3(6,1.04f,-3),new Vector3(2.1f,.09f,3.1f),Color.gray);
            Shape("Delivery table",room,new Vector3(-5,.7f,1),new Vector3(2.5f,.15f,1.5f),new Color(.4f,.25f,.12f));
            var delivery=Shape("ПКДоставка",room,new Vector3(-5,1.2f,1),new Vector3(.7f,.7f,.7f),new Color(.5f,.32f,.15f));Target(delivery,"Инвентарь");
            Label("ПКДоставка",room,new Vector3(-5,1.8f,.55f));
            PCMount=Station(new Vector3(-3.9f,0,4.4f),"Игрок 1");
            var tv=Shape("TV",room,new Vector3(5,1.8f,-.8f),new Vector3(2.8f,1.6f,.1f),Color.black);
            Target(tv,"GoTube");Label("GoTube",room,new Vector3(5,1.8f,-.87f),.09f);
            var city=new GameObject("City block").transform;
            Shape("City ground",city,new Vector3(0,-.35f,0),new Vector3(120,.4f,120),new Color(.28f,.3f,.27f));
            Shape("Road",city,new Vector3(0,-.11f,-15),new Vector3(110,.1f,7),new Color(.1f,.11f,.12f));
            for(int x=-50;x<50;x+=5) Shape("Road marking",city,new Vector3(x,-.05f,-15),new Vector3(2,.01f,.1f),Color.white,solid:false);
            for(int n=0;n<16;n++)
            {
                float x=(n%8-3.5f)*13, z=n<8?24:-32, height=9+(n*7%16);
                Shape("Building "+n,city,new Vector3(x,height/2,z),new Vector3(9,height,9),new Color(.2f+n*.006f,.23f,.28f));
                for(int f=2;f<height;f+=3) for(int w=-3;w<=3;w+=3)
                    Shape("Window",city,new Vector3(x+w,f,z+(n<8?-4.51f:4.51f)),new Vector3(1.1f,1.4f,.02f),new Color(.35f,.53f,.64f),solid:false);
            }
            var player=new GameObject("Player"); player.transform.position=new Vector3(-2,.1f,1);
            player.AddComponent<CharacterController>(); var walker=player.AddComponent<TouchWalker>();
            var camera=new GameObject("First person camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.SetParent(player.transform,false);camera.transform.localPosition=new Vector3(0,1.6f,0);
            camera.nearClipPlane=.05f;camera.farClipPlane=150; camera.fieldOfView=70;camera.backgroundColor=new Color(.42f,.63f,.8f);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.gameObject.AddComponent<AudioListener>();walker.View=camera;app.Walker=walker;
            Refresh(app.Model);
        }
        public static Transform Station(Vector3 pos,string owner)
        {
            var root=new GameObject("Station "+owner).transform;root.position=pos;
            Shape("Desk",root,new Vector3(0,.75f,0),new Vector3(2.4f,.12f,1.1f),new Color(.24f,.18f,.14f));
            foreach(float x in new[]{-1f,1f}) Shape("Desk leg",root,new Vector3(x,.35f,0),new Vector3(.1f,.7f,.8f),Color.black);
            Shape("Monitor stand",root,new Vector3(-.5f,.87f,.15f),new Vector3(.3f,.2f,.2f),Color.gray);
            var monitor=Shape("Starter monitor 640x480",root,new Vector3(-.5f,1.2f,.16f),new Vector3(.78f,.58f,.12f),Color.black);Target(monitor,"Crypto");
            Shape("Low resolution screen",root,new Vector3(-.5f,1.2f,.09f),new Vector3(.7f,.5f,.01f),new Color(.02f,.1f,.16f),solid:false);
            Label("CRYPTO\n640 × 480",root,new Vector3(-.5f,1.2f,.075f),.026f);
            Shape("Keyboard",root,new Vector3(-.5f,.84f,-.25f),new Vector3(.65f,.04f,.21f),Color.gray);
            Label(owner,root,new Vector3(0,1.9f,.1f),.05f);
            var mount=new GameObject("PC parts").transform;mount.SetParent(root,false);mount.localPosition=new Vector3(.55f,.85f,0);return mount;
        }
        public static void Refresh(GameModel model)
        {
            if(PCMount==null) return;
            foreach(Transform t in PCMount) Object.Destroy(t.gameObject);
            if(model.Part("case")!=null)
            {
                var c=Shape("ATX case",PCMount,new Vector3(0,.35f,0),new Vector3(.4f,.7f,.65f),new Color(.07f,.08f,.1f));Target(c,"Сборка ПК");
                // Open side shows the installed components, without a transparent-material dependency.
                Shape("Open side plate",PCMount,new Vector3(-.22f,.35f,0),new Vector3(.025f,.7f,.65f),Color.gray,solid:false);
            }
            if(model.Part("board")!=null) Shape("Motherboard",PCMount,new Vector3(-.24f,.4f,0),new Vector3(.02f,.42f,.45f),new Color(.08f,.25f,.12f),solid:false);
            if(model.Part("cpu")!=null) Shape("CPU",PCMount,new Vector3(-.26f,.5f,.03f),new Vector3(.03f,.08f,.08f),Color.gray,solid:false);
            if(model.Part("ram")!=null) Shape("RAM DIMM",PCMount,new Vector3(-.27f,.55f,.18f),new Vector3(.04f,.18f,.025f),new Color(.1f,.5f,.2f),solid:false);
            if(model.Part("cooler")!=null) Shape("CPU cooler",PCMount,new Vector3(-.3f,.5f,.03f),new Vector3(.08f,.14f,.14f),Color.gray,solid:false);
            if(model.Part("psu")!=null) Shape("PSU",PCMount,new Vector3(-.25f,.08f,.1f),new Vector3(.15f,.14f,.22f),Color.black,solid:false);
            if(model.Part("gpu")!=null)
            {
                var gpu=GPU(model.Part("gpu"),PCMount);gpu.transform.localPosition=new Vector3(-.28f,.29f,0);gpu.transform.localRotation=Quaternion.Euler(90,0,90);gpu.transform.localScale=Vector3.one*.55f;
            }
        }
        public static GameObject GPU(Product p,Transform parent)
        {
            var root=new GameObject(p.name);root.transform.SetParent(parent,false);
            var color=p.brand=="AMD"?new Color(.55f,.08f,.07f):new Color(.12f,.45f,.08f);
            float width=.38f+p.fans*.16f;
            Shape("PCB",root.transform,Vector3.zero,new Vector3(width,.3f,.025f),new Color(.04f,.12f,.06f),solid:false);
            Shape("Heatsink",root.transform,new Vector3(0,0,-.045f),new Vector3(width,.29f,.07f),Color.gray,solid:false);
            for(int f=0;f<p.fans;f++)
            {
                float x=(f-(p.fans-1)*.5f)*.23f;
                var fan=Shape("Fan "+f,root.transform,new Vector3(x,0,-.095f),new Vector3(.21f,.018f,.21f),new Color(.045f,.05f,.06f),PrimitiveType.Cylinder,false);
                fan.transform.localRotation=Quaternion.Euler(90,0,0);
                for(int b=0;b<7;b++)
                {
                    float a=b*Mathf.PI*2/7;
                    var blade=Shape("Blade",root.transform,new Vector3(x+Mathf.Sin(a)*.06f,Mathf.Cos(a)*.06f,-.119f),new Vector3(.04f,.075f,.012f),new Color(.15f,.16f,.18f),solid:false);
                    blade.transform.localRotation=Quaternion.Euler(0,0,-b*360/7+20);
                }
            }
            Shape("Brand stripe",root.transform,new Vector3(0,.145f,-.065f),new Vector3(width,.018f,.1f),color,solid:false);
            for(int c=0;c<18;c++) Shape("PCIe contact",root.transform,new Vector3(-.17f+c*.02f,-.175f,0),new Vector3(.013f,.035f,.013f),new Color(.85f,.64f,.15f),solid:false);
            Shape("IO bracket",root.transform,new Vector3(-width/2-.015f,0,0),new Vector3(.025f,.32f,.12f),Color.gray,solid:false);
            for(int o=0;o<4;o++) Shape(o==0?"HDMI":"DisplayPort",root.transform,new Vector3(-width/2-.031f,-.1f+o*.065f,-.02f),new Vector3(.008f,.034f,.05f),Color.black,solid:false);
            if(p.watts>75) Shape(p.watts>300?"High power connector":"PCIe power",root.transform,new Vector3(width*.3f,.17f,0),new Vector3(.09f,.04f,.04f),Color.black,solid:false);
            Label(p.name,root.transform,new Vector3(0,.23f,-.1f),.028f);
            return root;
        }
    }
}
