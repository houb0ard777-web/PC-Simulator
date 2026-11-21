using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace PCSim
{
    public sealed partial class SimApp : MonoBehaviour
    {
        public static SimApp Instance;
        public GameModel Model;
        public TouchWalker Walker;
        string page="", search="", brand="Все", kind="gpu", series="Все";
        int sort, minRam, maxPrice=350000, minPower, minRate;
        Vector2 shopScroll, inventoryScroll;
        Product selected;
        float saveTimer;
        public static long Now { get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); } }
        public bool MenuOpen { get { return page!=""; } }
        string SavePath { get { return Path.Combine(Application.persistentDataPath,"pcsim-v1.json"); } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if(Instance==null) new GameObject("PC Simulator").AddComponent<SimApp>(); }
        void Awake()
        {
            if(Instance!=null) { Destroy(gameObject); return; } Instance=this;
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            foreach(var path in new[]{SavePath,SavePath+".bak"})
            {
                if(!File.Exists(path)) continue;
                try { Model=new GameModel(catalog.items,JsonUtility.FromJson<Progress>(File.ReadAllText(path))); break; }
                catch(Exception ex) { Debug.LogWarning("Save unreadable: "+ex.Message); }
            }
            if(Model==null) Model=new GameModel(catalog.items);
            Application.targetFrameRate=60; Screen.sleepTimeout=SleepTimeout.NeverSleep;
            SimWorld.Build(this); InitExtra();
        }
        void Update()
        {
            Model.Deliver(Now); Model.Tick(Time.unscaledDeltaTime);
            Walker.MenuOpen=MenuOpen;
            if(Input.GetKeyDown(KeyCode.Escape)) Open("");
            if(!MenuOpen&&Input.GetKeyDown(KeyCode.E)) Use();
            if(Input.GetKeyDown(KeyCode.F1)) Open("Магазин");
            if(Input.GetKeyDown(KeyCode.F2)) Open("Сборка ПК");
            saveTimer+=Time.unscaledDeltaTime;
            if(saveTimer>5) { Save(); saveTimer=0; }
            UpdateExtra();
        }
        public void Save()
        {
            try
            {
                File.WriteAllText(SavePath+".tmp",JsonUtility.ToJson(Model.State,true));
                if(File.Exists(SavePath)) File.Copy(SavePath,SavePath+".bak",true);
                // Same-directory rename is atomic on Android. Keep a backup if interrupted before move.
                if(File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(SavePath+".tmp",SavePath);
            }
            catch(Exception ex) { Model.Notice="Не удалось сохранить: "+ex.Message; }
        }
        void OnApplicationPause(bool paused) { if(paused&&Model!=null) Save(); }
        void OnApplicationQuit() { if(Model!=null) Save(); ShutdownExtra(); }
        public void Open(string target)
        { page=target; if(Walker!=null) Walker.MenuOpen=MenuOpen; Cursor.lockState=CursorLockMode.None; }
        public void Use()
        {
            if(Physics.Raycast(Walker.View.ViewportPointToRay(new Vector3(.5f,.5f)),out var hit,3))
            {
                var target=hit.collider.GetComponentInParent<UseTarget>();
                if(target!=null) { Open(target.page); return; }
            }
            Model.Notice="Подойди к монитору, телевизору или столу доставки.";
        }
        void OnGUI()
        {
            var old=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            GUI.skin.label.fontSize=19; GUI.skin.button.fontSize=18; GUI.skin.textField.fontSize=19;
            GUI.skin.box.fontSize=20; GUI.skin.toggle.fontSize=18;
            GUI.Box(new Rect(0,0,1280,60),"");
            GUI.Label(new Rect(20,14,380,36),"PC SIMULATOR  /  "+Model.State.rubles.ToString("N0")+" ₽");
            GUI.Label(new Rect(400,14,270,36),(Model.State.microCrypto/1000000d).ToString("F6")+" кр");
            if(GUI.Button(new Rect(680,8,150,44),"ПКДоставка")) Open("Магазин");
            if(GUI.Button(new Rect(840,8,150,44),"Сборка ПК")) Open("Сборка ПК");
            if(GUI.Button(new Rect(1000,8,130,44),"Crypto")) Open("Crypto");
            if(GUI.Button(new Rect(1140,8,130,44),MenuOpen?"Закрыть":"Меню")) Open(MenuOpen?"":"Меню");
            GUI.Box(new Rect(15,662,1250,48),Model.Notice);
            if(!MenuOpen)
            {
                GUI.Label(new Rect(630,346,30,30),"+");
                if(GUI.Button(new Rect(1090,550,175,92),"Действие [E]")) Use();
                GUI.Label(new Rect(20,70,650,30),"WASD / касание слева — ходить · справа — обзор · Tab — мышь");
            }
            else
            {
                GUI.Box(new Rect(15,70,1250,580),"");
                GUILayout.BeginArea(new Rect(32,80,1210,555));
                GUILayout.Label(page);
                if(page=="Магазин") Shop();
                else if(page=="Сборка ПК") Assembly();
                else if(page=="Crypto") Crypto();
                else if(page=="Инвентарь") Inventory();
                else if(!DrawExtra(page)) MainMenu();
                GUILayout.EndArea();
            }
            GUI.matrix=old;
        }
        void MainMenu()
        {
            GUILayout.Label("Старт: 30 000 ₽. Монитор Starter 15″, 640×480, 60 Гц уже на столе.");
            foreach(var name in new[]{"Магазин","Инвентарь","Сборка ПК","Crypto","GoTube","Wi-Fi"})
                if(GUILayout.Button(name,GUILayout.Height(55))) Open(name);
            if(GUILayout.Button("Сохранить прогресс",GUILayout.Height(45))) { Save(); Model.Notice="Сохранено."; }
        }
        void Shop()
        {
            GUILayout.BeginHorizontal(); GUILayout.Label("Поиск",GUILayout.Width(70)); search=GUILayout.TextField(search,GUILayout.Width(280));
            foreach(var b in new[]{"Все","NVIDIA","AMD"}) if(GUILayout.Button((brand==b?"● ":"")+b)) brand=b;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach(var k in new[]{"gpu","cpu","ram","board","cooler","case","psu"}) if(GUILayout.Button((kind==k?"● ":"")+k)) {kind=k;series="Все";}
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Цена ≤ "+maxPrice+" ₽",GUILayout.Width(230)); maxPrice=(int)GUILayout.HorizontalSlider(maxPrice,1500,350000,GUILayout.Width(200));
            GUILayout.Label("VRAM ≥ "+minRam+" GB",GUILayout.Width(170)); minRam=(int)GUILayout.HorizontalSlider(minRam,0,32,GUILayout.Width(140));
            if(GUILayout.Button(new[]{"Цена ↑","Мощность ↑","Доход ↓"}[sort],GUILayout.Width(160))) sort=(sort+1)%3;
            if(GUILayout.Button("Сброс")) {search="";brand="Все";series="Все";maxPrice=350000;minRam=minPower=minRate=0;}
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Потребление ≥ "+minPower+" W",GUILayout.Width(210)); minPower=(int)GUILayout.HorizontalSlider(minPower,0,600,GUILayout.Width(160));
            GUILayout.Label("кр/с ≥ "+(minRate/1000000d).ToString("F4"),GUILayout.Width(210)); minRate=(int)GUILayout.HorizontalSlider(minRate,0,100000,GUILayout.Width(160));
            if(GUILayout.Button("Серия: "+series))
            {
                var groups=new[]{"Все"}.Concat(Model.Products.Values.Where(p=>p.kind==kind).Select(p=>p.series).Distinct().OrderBy(s=>s)).ToArray();
                series=groups[(Array.IndexOf(groups,series)+1)%groups.Length];
            }
            GUILayout.EndHorizontal();
            var products=Model.Products.Values.Where(p=>p.kind==kind&&p.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0&&(brand=="Все"||p.brand==brand)&&(series=="Все"||p.series==series)&&p.price<=maxPrice&&(p.kind!="gpu"||p.vram>=minRam)&&p.watts>=minPower&&p.microRate>=minRate);
            products=sort==0?products.OrderBy(p=>p.price):sort==1?products.OrderBy(p=>p.score):products.OrderByDescending(p=>p.microRate);
            GUILayout.Label(products.Count()+" товаров · всего 113 видеокарт · цены и доходность игровые");
            shopScroll=GUILayout.BeginScrollView(shopScroll,GUILayout.Height(300));
            foreach(var p in products)
            {
                GUILayout.BeginHorizontal(GUI.skin.box,GUILayout.Height(58));
                GUILayout.Label(p.name+"\n"+p.vram+" GB · "+p.watts+" W · "+(p.microRate/1000000d).ToString("F4")+" кр/с",GUILayout.Width(510));
                GUILayout.Label(p.price.ToString("N0")+" ₽",GUILayout.Width(170));
                if(GUILayout.Button("3D",GUILayout.Width(100),GUILayout.Height(45))) { selected=p; Preview(p); Open("Предпросмотр"); }
                if(GUILayout.Button("Заказать",GUILayout.Height(45))) { Model.Buy(p.id,Now); Save(); }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
        void Inventory()
        {
            inventoryScroll=GUILayout.BeginScrollView(inventoryScroll,GUILayout.Height(475));
            foreach(var i in Model.State.inventory.ToArray())
            {
                var p=Model.Product(i); GUILayout.BeginHorizontal(GUI.skin.box,GUILayout.Height(65));
                var status=i.status=="ordered"?"Доставка: "+Math.Max(0,i.due-Now)+" с":i.status=="boxed"?"В коробке":i.status=="installed"?"Установлено":"Распаковано";
                GUILayout.Label(p.name+" — "+status,GUILayout.Width(510));
                if(i.status=="boxed"&&GUILayout.Button("Распаковать",GUILayout.Height(45))) BeginUnbox(i);
                if(i.status=="ready"&&GUILayout.Button("Установить",GUILayout.Height(45))) {Model.Install(i.uid); RefreshPC();Save();}
                if(i.status=="installed"&&GUILayout.Button("Снять",GUILayout.Height(45))) {Model.Remove(p.kind);RefreshPC();Save();}
                if((i.status=="ready"||i.status=="boxed")&&GUILayout.Button("Продать "+(p.price*65/100)+" ₽",GUILayout.Height(45))) {Model.Sell(i.uid);RefreshPC();Save();}
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
        void Assembly()
        {
            GUILayout.Label("Монитор: Starter 15″ / 640×480 / 60 Гц. Один слот на каждый тип детали.");
            foreach(var k in new[]{"case","board","cpu","ram","cooler","psu","gpu"})
            {
                GUILayout.BeginHorizontal(); var p=Model.Part(k);
                GUILayout.Label(k+": "+(p==null?"пусто":p.name),GUILayout.Width(800));
                if(p!=null&&GUILayout.Button("Снять",GUILayout.Height(36))) {Model.Remove(k);RefreshPC();Save();}
                GUILayout.EndHorizontal();
            }
            var issue=Model.BootIssue(); GUILayout.Label(issue==""?"ПК готов к работе.":issue);
            if(GUILayout.Button("Открыть инвентарь",GUILayout.Height(48))) Open("Инвентарь");
        }
        void Crypto()
        {
            GUILayout.Label("CRYPTO  /  игровая криптовалюта");
            GUILayout.Label("Баланс: "+(Model.State.microCrypto/1000000d).ToString("F6")+" кр");
            GUILayout.Label("Скорость: "+(Model.Rate/1000000d).ToString("F4")+" кр/сек");
            GUILayout.Label("Добыча работает при запущенной игре и собранном ПК. Курс: 1 кр = 100 000 ₽.");
            if(GUILayout.Button(Model.State.mining?"Остановить майнинг":"Запустить майнинг",GUILayout.Height(70))) {Model.ToggleMining();Save();}
            if(GUILayout.Button("Обменять крипту на рубли",GUILayout.Height(65))) {Model.Exchange();Save();}
        }
        // Implemented by the expansion stage; these hooks keep core/UI responsibilities separate.
        partial void InitExtra(); partial void UpdateExtra(); partial void ShutdownExtra();
        partial void RefreshPC(); partial void Preview(Product p); partial void BeginUnbox(OwnedItem item);
        bool DrawExtra(string target) { bool handled=false; DrawExtraPage(target,ref handled); return handled; }
        partial void DrawExtraPage(string target,ref bool handled);
    }
    public sealed class UseTarget : MonoBehaviour { public string page; }
}
