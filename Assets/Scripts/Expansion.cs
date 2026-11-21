using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;
namespace PCSim
{
    [System.Serializable] public class VideoEntry { public string title, youtube, directUrl; }
    [System.Serializable] public class VideoList { public VideoEntry[] videos; }
    public sealed partial class SimApp
    {
        Camera previewCamera;
        Transform previewStage;
        GameObject previewObject;
        RenderTexture previewImage;
        float rotation, tilt;
        AudioSource foley;
        AudioClip cardboard, plastic;
        OwnedItem opening;
        bool unboxing;
        VideoList videos;
        VideoPlayer video;
        LanSession lan;
        string hostAddress="192.168.1.100";
        Vector2 videoScroll;
        readonly Dictionary<string,Material> boxPrints=new Dictionary<string,Material>();
        Transform parcels;
        string parcelKey="";
        readonly Dictionary<int,GameObject> avatars=new Dictionary<int,GameObject>();
        readonly Dictionary<int,Transform> remoteStations=new Dictionary<int,Transform>();
        partial void InitExtra()
        {
            previewStage=new GameObject("Inspection studio").transform;previewStage.position=new Vector3(200,0,0);
            previewCamera=new GameObject("Product camera").AddComponent<Camera>();previewCamera.transform.position=new Vector3(200,0,-2.4f);
            previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.07f,.09f,.12f);
            previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=8;previewCamera.fieldOfView=38;
            previewImage=new RenderTexture(768,512,16);previewImage.Create();previewCamera.targetTexture=previewImage;
            var light=new GameObject("Studio light").AddComponent<Light>();light.type=LightType.Point;light.range=7;light.intensity=2;
            light.transform.position=new Vector3(199,2,-1);
            parcels=new GameObject("Delivered parcels").transform;
            foley=gameObject.AddComponent<AudioSource>();cardboard=Noise("Cardboard opening",.55f,1);plastic=Noise("Film peeling",.8f,2);
            videos=JsonUtility.FromJson<VideoList>(Resources.Load<TextAsset>("videos").text);
            video=gameObject.AddComponent<VideoPlayer>();video.playOnAwake=false;
            video.renderMode=VideoRenderMode.MaterialOverride;
            video.targetMaterialRenderer=GameObject.Find("TV").GetComponent<Renderer>();video.targetMaterialProperty="_MainTex";
            video.audioOutputMode=VideoAudioOutputMode.Direct;
            video.errorReceived+=(player,error)=>Model.Notice="GoTube: "+error;
            lan=gameObject.AddComponent<LanSession>();
            lan.PeerUpdated+=UpdatePeer;lan.PeerLeft+=RemovePeer;
            lan.LocalAssigned+=id=> {
                SimWorld.PCMount.parent.position=new Vector3(-3.9f+id*2.6f,0,4.4f);
                SimWorld.PCMount.parent.name="Local station "+id;
            };
        }
        partial void ShutdownExtra() { if(lan!=null) lan.Stop(); }
        partial void UpdateExtra()
        {
            var boxed=Model.State.inventory.Where(i=>i.status=="boxed").Take(12).ToArray();
            var key=string.Join(",",boxed.Select(i=>i.uid));
            if(key!=parcelKey)
            {
                parcelKey=key;
                foreach(Transform child in parcels) Destroy(child.gameObject);
                for(int n=0;n<boxed.Length;n++)
                {
                    var product=Model.Product(boxed[n]);
                    var parcel=SimWorld.Shape(product.name+" box",parcels,new Vector3(-5.8f+(n%3)*.65f,.95f+(n/3)*.3f,1),new Vector3(.6f,.28f,.5f),new Color(.55f,.34f,.17f));
                    SimWorld.Target(parcel,"Инвентарь");
                    SimWorld.Label(product.name,parcel.transform,new Vector3(0,0,-.51f),.025f);
                }
            }
            if(previewObject!=null&&!unboxing) previewObject.transform.localRotation=Quaternion.Euler(tilt,rotation,0);
            previewCamera.enabled=MenuOpen&&(page=="Предпросмотр"||page=="Распаковка");
            if(lan!=null) { lan.LocalPosition=Walker.transform.position;lan.LocalYaw=Walker.transform.eulerAngles.y; }
        }
        partial void RefreshPC() { SimWorld.Refresh(Model); }
        partial void Preview(Product p)
        {
            if(unboxing) return;
            if(previewObject!=null) {previewObject.SetActive(false);Destroy(previewObject);}
            rotation=tilt=0;
            previewObject=MakeProduct(p,previewStage);
        }
        GameObject MakeProduct(Product p,Transform parent)
        {
            if(p.kind=="gpu") return SimWorld.GPU(p,parent);
            var g=new GameObject(p.name);g.transform.SetParent(parent,false);
            Vector3 size=p.kind=="ram"?new Vector3(.65f,.18f,.04f):p.kind=="board"?new Vector3(.7f,.7f,.03f):p.kind=="cpu"?new Vector3(.3f,.3f,.04f):new Vector3(.5f,.5f,.35f);
            SimWorld.Shape(p.kind,g.transform,Vector3.zero,size,p.kind=="ram"||p.kind=="board"?new Color(.08f,.3f,.13f):Color.gray,solid:false);
            SimWorld.Label(p.name,g.transform,new Vector3(0,.42f,-.2f),.04f);return g;
        }
        partial void BeginUnbox(OwnedItem item)
        {
            if(unboxing) { Model.Notice="Текущая распаковка ещё не закончена.";return; }
            opening=item;Preview(Model.Product(item));
            if(previewObject!=null) {previewObject.SetActive(false);Destroy(previewObject);}
            previewObject=new GameObject("Sealed shipping box");previewObject.transform.SetParent(previewStage,false);
            var brown=new Color(.55f,.34f,.17f);
            SimWorld.Shape("Base",previewObject.transform,new Vector3(0,-.16f,.1f),new Vector3(1,.24f,.7f),brown,solid:false);
            SimWorld.Shape("Lid",previewObject.transform,new Vector3(0,0,.1f),new Vector3(1,.08f,.7f),brown,solid:false);
            // A labelled printed front with a small 3D product illustration is generated for every SKU.
            var p=Model.Product(item);
            var printed=BoxPrint(p);
            var front=SimWorld.Shape("Printed sleeve",previewObject.transform,new Vector3(0,-.12f,-.26f),new Vector3(.97f,.29f,.012f),p.brand=="AMD"?new Color(.6f,.07f,.05f):new Color(.16f,.48f,.07f),solid:false);
            front.GetComponent<Renderer>().sharedMaterial=printed;
            Open("Распаковка");
        }
        Material BoxPrint(Product p)
        {
            if(boxPrints.TryGetValue(p.id,out var cached)) return cached;
            var keep=previewObject;
            if(keep!=null) keep.SetActive(false);
            var art=MakeProduct(p,previewStage);
            var previous=RenderTexture.active;
            previewCamera.Render();RenderTexture.active=previewImage;
            var texture=new Texture2D(previewImage.width,previewImage.height,TextureFormat.RGB24,false);
            texture.name=p.name+" printed box artwork";
            texture.ReadPixels(new Rect(0,0,previewImage.width,previewImage.height),0,0);texture.Apply();
            RenderTexture.active=previous;
            art.SetActive(false);Destroy(art);
            if(keep!=null) keep.SetActive(true);
            var material=new Material(SimWorld.Surface(Color.white));material.mainTexture=texture;
            boxPrints[p.id]=material;return material;
        }
        IEnumerator OpenBox()
        {
            unboxing=true;foley.PlayOneShot(cardboard);
            var lid=previewObject.transform.Find("Lid");
            for(float t=0;t<1;t+=Time.deltaTime*1.5f) { if(lid!=null) lid.localRotation=Quaternion.Euler(-110*t,0,0);yield return null; }
            foley.PlayOneShot(plastic);
            yield return new WaitForSeconds(.8f);
            var item=opening;Model.Unbox(item.uid);Save();
            unboxing=false;opening=null;Preview(Model.Product(item));
        }
        static AudioClip Noise(string name,float duration,int seed)
        {
            const int rate=22050;var data=new float[(int)(duration*rate)];var random=new System.Random(seed);float last=0;
            for(int i=0;i<data.Length;i++)
            {
                last=last*.6f+(float)(random.NextDouble()*2-1)*.4f;
                data[i]=last*.35f*Mathf.Sin(i/(float)data.Length*Mathf.PI)*(seed==1?.5f+.5f*Mathf.Sin(i*.003f):1);
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        partial void DrawExtraPage(string target,ref bool handled)
        {
            if(target=="Предпросмотр"||target=="Распаковка")
            {
                handled=true;
                GUILayout.BeginHorizontal();GUILayout.Label(previewImage,GUILayout.Width(720),GUILayout.Height(450));
                GUILayout.BeginVertical();
                GUILayout.Label("Вращение");rotation=GUILayout.HorizontalSlider(rotation,-180,180);
                GUILayout.Label("Наклон");tilt=GUILayout.HorizontalSlider(tilt,-90,90);
                if(target=="Распаковка"&&opening!=null&&!unboxing&&GUILayout.Button("Открыть коробку\nи снять плёнку",GUILayout.Height(90))) StartCoroutine(OpenBox());
                GUILayout.Label("Процедурная модель PC Sim.\nОформление не является\nлицензированным ассетом производителя.");
                if(GUILayout.Button("Инвентарь",GUILayout.Height(50))) Open("Инвентарь");
                if(GUILayout.Button("Магазин",GUILayout.Height(50))) Open("Магазин");
                GUILayout.EndVertical();GUILayout.EndHorizontal();
            }
            else if(target=="GoTube")
            {
                handled=true;
                GUILayout.Label("7 видео · YouTube открывается во внешнем приложении.\nПрямые MP4 из videos.json воспроизводятся на 3D-телевизоре.");
                videoScroll=GUILayout.BeginScrollView(videoScroll,GUILayout.Height(410));
                foreach(var v in videos.videos)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);GUILayout.Label(v.title,GUILayout.Width(650));
                    if(GUILayout.Button("YouTube",GUILayout.Height(46))) Application.OpenURL(v.youtube);
                    GUI.enabled=!string.IsNullOrEmpty(v.directUrl);
                    if(GUILayout.Button("На телевизоре",GUILayout.Height(46))) {video.url=v.directUrl;video.Play();Open("");}
                    GUI.enabled=true;GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                if(GUILayout.Button("Остановить телевизор",GUILayout.Height(42))) video.Stop();
            }
            else if(target=="Wi-Fi")
            {
                handled=true;
                GUILayout.Label("Локальная сеть: до 4 игроков · TCP порт 47777");
                GUILayout.Label("IP этого устройства: "+LanSession.LocalAddresses());
                GUILayout.Label("Устройства должны быть в одной Wi-Fi сети без изоляции клиентов.");
                hostAddress=GUILayout.TextField(hostAddress,GUILayout.Height(48));
                if(GUILayout.Button("Создать комнату",GUILayout.Height(60))) lan.Host();
                if(GUILayout.Button("Подключиться по IP",GUILayout.Height(60))) lan.Join(hostAddress);
                if(GUILayout.Button("Отключиться",GUILayout.Height(50))) lan.Stop();
                GUILayout.Label(lan.Status);
                GUILayout.Label("Синхронизируются положение и поворот игроков. Деньги, покупки и ПК у каждого свои.");
            }
        }
        void UpdatePeer(int id,Vector3 pos,float yaw)
        {
            if(!avatars.TryGetValue(id,out var avatar))
            {
                avatar=new GameObject("Remote player "+id);avatars[id]=avatar;
                SimWorld.Shape("Body",avatar.transform,new Vector3(0,.85f,0),new Vector3(.45f,.6f,.3f),new Color(.15f,.3f,.65f),solid:false);
                SimWorld.Shape("Head",avatar.transform,new Vector3(0,1.5f,0),new Vector3(.3f,.3f,.3f),new Color(.72f,.5f,.34f),PrimitiveType.Sphere,false);
                foreach(float x in new[]{-.14f,.14f}) SimWorld.Shape("Leg",avatar.transform,new Vector3(x,.3f,0),new Vector3(.18f,.6f,.18f),Color.gray,solid:false);
                SimWorld.Label("Игрок "+(id+1),avatar.transform,new Vector3(0,1.9f,-.1f));
                // Remote workstations are visual; local purchases never overwrite another player's save.
                var station=SimWorld.Station(new Vector3(-3.9f+id*2.6f,0,4.4f),"Игрок "+(id+1));
                foreach(var use in station.parent.GetComponentsInChildren<UseTarget>()) Destroy(use);
                SimWorld.Shape("Remote case",station,Vector3.up*.35f,new Vector3(.4f,.7f,.65f),Color.gray,solid:false);
                remoteStations[id]=station.parent;
            }
            avatar.transform.position=Vector3.Lerp(avatar.transform.position,pos,.8f);avatar.transform.rotation=Quaternion.Euler(0,yaw,0);
        }
        void RemovePeer(int id)
        {
            if(avatars.TryGetValue(id,out var avatar)) Destroy(avatar);avatars.Remove(id);
            if(remoteStations.TryGetValue(id,out var station)) Destroy(station.gameObject);remoteStations.Remove(id);
        }
    }
}
