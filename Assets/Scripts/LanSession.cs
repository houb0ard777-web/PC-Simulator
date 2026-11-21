using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using UnityEngine;
namespace PCSim
{
    public sealed class LanSession : MonoBehaviour
    {
        [Serializable] sealed class Packet { public string kind; public int id; public float x,y,z,yaw; }
        sealed class Message { public int generation, source; public string text; public bool gone; }
        sealed class Peer : IDisposable
        {
            public int id, generation;
            public TcpClient client;
            public BlockingCollection<string> output=new BlockingCollection<string>(8);
            int disposed;
            public void Dispose()
            { if(Interlocked.Exchange(ref disposed,1)!=0) return;output.CompleteAdding();client.Close(); }
        }
        public event Action<int,Vector3,float> PeerUpdated;
        public event Action<int> PeerLeft;
        public event Action<int> LocalAssigned;
        public Vector3 LocalPosition;
        public float LocalYaw;
        public string Status="Не подключено";
        public const int Port=47777;
        readonly ConcurrentDictionary<int,Peer> peers=new ConcurrentDictionary<int,Peer>();
        readonly ConcurrentQueue<Message> inbound=new ConcurrentQueue<Message>();
        readonly HashSet<int> visible=new HashSet<int>();
        TcpListener listener;
        bool host, connected;
        int generation, localId;
        float sendTimer;
        public void Host()
        {
            Stop();host=true;connected=true;localId=0;
            try
            {
                listener=new TcpListener(IPAddress.Any,Port);listener.Start(4);
                var current=listener;int epoch=generation;
                new Thread(()=>Accept(current,epoch)){IsBackground=true}.Start();
                Status="Комната создана: "+LocalAddresses()+":"+Port;LocalAssigned?.Invoke(0);
            }
            catch(Exception e) { Stop();Status="Сервер: "+e.Message; }
        }
        public void Join(string address)
        {
            Stop();
            if(!IPAddress.TryParse(address,out var ip)||ip.AddressFamily!=AddressFamily.InterNetwork) {Status="Укажи IPv4 хоста, например 192.168.1.10";return;}
            int epoch=generation;Status="Подключение…";
            new Thread(()=> {
                var client=new TcpClient();
                try
                {
                    var pending=client.BeginConnect(ip,Port,null,null);
                    if(!pending.AsyncWaitHandle.WaitOne(4000)) throw new IOException("Connection timeout");
                    client.EndConnect(pending);
                    if(epoch!=generation) {client.Close();return;}
                    var p=new Peer{id=0,client=client,generation=epoch};
                    if(!peers.TryAdd(0,p)) {p.Dispose();return;}
                    StartPeer(p);
                }
                catch(Exception e) {client.Close();inbound.Enqueue(new Message{generation=epoch,source=0,text="error:"+e.Message});}
            }){IsBackground=true}.Start();
        }
        void Accept(TcpListener server,int epoch)
        {
            while(epoch==generation)
            {
                try
                {
                    var client=server.AcceptTcpClient();int slot=0;
                    for(int i=1;i<4;i++) if(!peers.ContainsKey(i)) {slot=i;break;}
                    if(slot==0||epoch!=generation) {client.Close();continue;}
                    var peer=new Peer{id=slot,client=client,generation=epoch};
                    if(peers.TryAdd(slot,peer))
                    {
                        // No Unity serialization on worker threads.
                        peer.output.TryAdd("{\"kind\":\"welcome\",\"id\":"+slot+"}");StartPeer(peer);
                    }
                    else peer.Dispose();
                }
                catch { if(epoch==generation) inbound.Enqueue(new Message{generation=epoch,text="error:Listener stopped"});return; }
            }
        }
        void StartPeer(Peer p)
        {
            p.client.NoDelay=true;p.client.ReceiveTimeout=15000;p.client.SendTimeout=2000;
            new Thread(()=>Read(p)){IsBackground=true}.Start();
            new Thread(()=>Write(p)){IsBackground=true}.Start();
        }
        void Read(Peer p)
        {
            try
            {
                var stream=p.client.GetStream();var buffer=new byte[512];var line=new List<byte>(256);
                int count;
                while(p.generation==generation&&(count=stream.Read(buffer,0,buffer.Length))>0)
                {
                    for(int b=0;b<count;b++)
                    {
                        if(buffer[b]==10)
                        {
                            if(inbound.Count>128) throw new IOException("Message queue full");
                            inbound.Enqueue(new Message{generation=p.generation,source=p.id,text=Encoding.UTF8.GetString(line.ToArray())});line.Clear();
                        }
                        else {line.Add(buffer[b]);if(line.Count>2048) throw new IOException("Packet too large");}
                    }
                }
            }
            catch { }
            finally { p.Dispose();inbound.Enqueue(new Message{generation=p.generation,source=p.id,gone=true}); }
        }
        void Write(Peer p)
        {
            try
            {
                var stream=p.client.GetStream();
                foreach(var text in p.output.GetConsumingEnumerable())
                {var bytes=Encoding.UTF8.GetBytes(text+"\n");stream.Write(bytes,0,bytes.Length);}
            }
            catch { p.Dispose(); }
        }
        void Send(Peer p,string text)
        { try {if(!p.output.TryAdd(text)) p.Dispose();}catch(InvalidOperationException){} }
        void Update()
        {
            int budget=128;
            while(budget-->0&&inbound.TryDequeue(out var message))
            {
                if(message.generation!=generation) continue;
                if(message.gone)
                {
                    if(peers.TryRemove(message.source,out var peer)) peer.Dispose();
                    if(host) {DropAvatar(message.source);Broadcast(new Packet{kind="leave",id=message.source});}
                    else {Stop();Status="Соединение с хостом потеряно.";}
                    continue;
                }
                if(message.text.StartsWith("error:")) {Status=message.text;continue;}
                try
                {
                    var p=JsonUtility.FromJson<Packet>(message.text);
                    if(p==null) continue;
                    if(p.kind=="welcome"&&!host&&p.id>=1&&p.id<=3)
                    {localId=p.id;connected=true;Status="Подключено. Игрок "+(localId+1);LocalAssigned?.Invoke(localId);}
                    else if(p.kind=="leave"&&!host) DropAvatar(p.id);
                    else if(p.kind=="pose")
                    {
                        if(host) p.id=message.source;
                        if(p.id<0||p.id>3||p.id==localId||!Finite(p.x)||!Finite(p.y)||!Finite(p.z)||!Finite(p.yaw)||Mathf.Abs(p.x)>100||Mathf.Abs(p.z)>100||Mathf.Abs(p.y)>100) continue;
                        visible.Add(p.id);PeerUpdated?.Invoke(p.id,new Vector3(p.x,p.y,p.z),p.yaw);
                        if(host) Broadcast(p);
                    }
                }
                catch(Exception) { /* Ignore malformed JSON; bounded packet/queue prevents unbounded memory. */ }
            }
            if(!connected) return;
            sendTimer+=Time.unscaledDeltaTime;
            if(sendTimer<.1f) return;sendTimer=0;
            Broadcast(new Packet{kind="pose",id=localId,x=LocalPosition.x,y=LocalPosition.y,z=LocalPosition.z,yaw=LocalYaw});
        }
        static bool Finite(float n) {return !float.IsNaN(n)&&!float.IsInfinity(n);}
        void Broadcast(Packet p)
        {var json=JsonUtility.ToJson(p);foreach(var peer in peers.Values) Send(peer,json);}
        void DropAvatar(int id) {visible.Remove(id);PeerLeft?.Invoke(id);}
        public void Stop()
        {
            Interlocked.Increment(ref generation);connected=false;host=false;localId=0;
            if(listener!=null) {listener.Stop();listener=null;}
            foreach(var peer in peers.Values) peer.Dispose();peers.Clear();
            foreach(int id in visible.ToArray()) DropAvatar(id);
            Message ignored;while(inbound.TryDequeue(out ignored)){}
            Status="Не подключено";LocalAssigned?.Invoke(0);
        }
        void OnDestroy() {Stop();}
        public static string LocalAddresses()
        {
            try
            {
                var addresses=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up)
                    .SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address)
                    .Where(ip=>ip.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(ip)).Select(ip=>ip.ToString()).Distinct().ToArray();
                return addresses.Length==0?"IPv4 смотри в настройках Wi-Fi":string.Join(", ",addresses);
            }
            catch {return "IPv4 смотри в настройках Wi-Fi";}
        }
    }
}
