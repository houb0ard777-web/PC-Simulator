using System;
using System.Collections.Generic;
using System.Linq;

namespace PCSim
{
    [Serializable] public class Product
    {
        public string id, name, kind, brand, series, socket, memory;
        public int price, microRate, watts, fans, score;
        public float vram;
    }
    [Serializable] public class Catalog { public Product[] items; }
    [Serializable] public class OwnedItem
    {
        public string uid, productId, status;
        public long due;
        public OwnedItem() { }
        public OwnedItem(string product, long deadline)
        { uid=Guid.NewGuid().ToString("N"); productId=product; status="ordered"; due=deadline; }
    }
    [Serializable] public class Progress
    {
        public int version=1;
        public long rubles=30000, microCrypto;
        public bool mining;
        public List<OwnedItem> inventory=new List<OwnedItem>();
    }
    // Domain rules are independent of scene objects and can be tested outside Unity.
    public sealed class GameModel
    {
        public readonly Dictionary<string, Product> Products;
        public Progress State;
        public string Notice="Старт: 30 000 ₽ и монитор 640×480. Собери свой первый ПК.";
        double remainder;
        public GameModel(Product[] products, Progress state=null)
        {
            Products=products.ToDictionary(p=>p.id);
            State=state??new Progress();
            Validate();
        }
        public Product Product(OwnedItem item) { return Products[item.productId]; }
        public OwnedItem Installed(string kind) { return State.inventory.FirstOrDefault(i=>i.status=="installed"&&Product(i).kind==kind); }
        public Product Part(string kind) { var i=Installed(kind); return i==null?null:Product(i); }
        public bool Say(string message) { Notice=message; return false; }
        public bool Buy(string id, long now)
        {
            if(!Products.ContainsKey(id)) return Say("Товар не найден.");
            var p=Products[id];
            if(State.inventory.Count>=128) return Say("Склад заполнен: продай лишние детали.");
            if(State.rubles<p.price) return Say("Не хватает рублей.");
            State.rubles-=p.price;
            State.inventory.Add(new OwnedItem(id, now+10));
            Notice="Заказ оформлен: "+p.name+". Доставка через 10 секунд.";
            return true;
        }
        public void Deliver(long now)
        { foreach(var i in State.inventory) if(i.status=="ordered"&&i.due<=now) i.status="boxed"; }
        public bool Unbox(string uid)
        {
            var i=State.inventory.Find(x=>x.uid==uid);
            if(i==null||i.status!="boxed") return Say("Дождись доставки.");
            i.status="ready"; Notice=Product(i).name+": распаковано."; return true;
        }
        public bool Install(string uid)
        {
            var i=State.inventory.Find(x=>x.uid==uid);
            if(i==null||i.status!="ready") return Say("Сначала распакуй деталь.");
            var p=Product(i); var board=p.kind=="board"?p:Part("board");
            var cpu=p.kind=="cpu"?p:Part("cpu"); var ram=p.kind=="ram"?p:Part("ram");
            if(board!=null&&cpu!=null&&board.socket!=cpu.socket) return Say("Сокет CPU не подходит к плате.");
            if(board!=null&&ram!=null&&board.memory!=ram.memory) return Say("Тип RAM не подходит к плате.");
            var old=Installed(p.kind); if(old!=null) old.status="ready";
            i.status="installed"; State.mining=false;
            Notice="Установлено: "+p.name; return true;
        }
        public void Remove(string kind)
        { var i=Installed(kind); if(i!=null) { i.status="ready"; State.mining=false; Notice="Деталь снята."; } }
        public bool Sell(string uid)
        {
            var i=State.inventory.Find(x=>x.uid==uid);
            if(i==null||i.status=="ordered"||i.status=="installed") return Say("Сними деталь перед продажей и дождись доставки.");
            State.rubles+=(long)Product(i).price*65/100; State.inventory.Remove(i);
            Notice="Продано за 65% цены магазина."; return true;
        }
        public string BootIssue()
        {
            foreach(var kind in new[]{"case","board","cpu","ram","cooler","psu","gpu"})
                if(Part(kind)==null) return "Не установлено: "+kind;
            if(Part("cpu").socket!=Part("board").socket) return "Несовместимый CPU";
            if(Part("ram").memory!=Part("board").memory) return "Несовместимая RAM";
            if(Part("cooler").score<Part("cpu").watts) return "Кулер слишком слабый";
            if(Part("psu").score<Part("gpu").watts+Part("cpu").watts+70) return "Блоку питания не хватает мощности";
            return "";
        }
        public int Rate { get { return BootIssue()==""?Part("gpu").microRate:0; } }
        public void Tick(double seconds)
        {
            if(!State.mining) return;
            if(BootIssue()!="") { State.mining=false; return; }
            remainder+=Rate*Math.Max(0,Math.Min(seconds,2));
            long earned=(long)remainder; State.microCrypto+=earned; remainder-=earned;
        }
        public bool ToggleMining()
        {
            if(State.mining) { State.mining=false; Notice="Crypto остановлен."; return true; }
            var issue=BootIssue(); if(issue!="") return Say(issue);
            State.mining=true; Notice="Crypto запущен."; return true;
        }
        public void Exchange()
        {
            // Fictional fixed rate: 1 кр = 100000 rubles. Keep sub-ruble remainder.
            var rubles=State.microCrypto/10;
            if(rubles<=0) { Say("Недостаточно крипты для обмена."); return; }
            State.microCrypto-=rubles*10; State.rubles+=rubles; Notice="Обменено: "+rubles+" ₽";
        }
        void Validate()
        {
            if(State.version!=1||State.rubles<0||State.microCrypto<0||State.inventory==null) throw new ArgumentException("Invalid save");
            var ids=new HashSet<string>(); var installed=new HashSet<string>();
            foreach(var i in State.inventory)
            {
                if(i==null||string.IsNullOrEmpty(i.uid)||!ids.Add(i.uid)||!Products.ContainsKey(i.productId)||!new[]{"ordered","boxed","ready","installed"}.Contains(i.status))
                    throw new ArgumentException("Invalid inventory");
                if(i.status=="installed"&&!installed.Add(Product(i).kind)) throw new ArgumentException("Duplicate slot");
            }
        }
    }
}
