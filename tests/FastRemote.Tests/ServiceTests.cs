using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FastRemote.Tests
{
    [TestClass]
    public class ServiceTests
    {
        static readonly DateTime Start=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
        static readonly ProbeOutcome Up=new ProbeOutcome { Up=true, LatencyMs=17 };
        static TaskCompletionSource<T> Signal<T>() => new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        [TestMethod]
        public async Task CacheForceExpiryAndInvalidation()
        {
            int calls=0;
            var now=Start;
            using var a=new Availability((h,p,t)=>{ calls++; return Task.FromResult(Up); },()=>now);
            Assert.AreEqual(Reach.Unknown,a.Get("server",3389).State);
            var first=await a.Check("Server",3389,false,default);
            now=now.AddSeconds(29);
            Assert.AreEqual(first.CheckedUtc,(await a.Check("SERVER",3389,false,default)).CheckedUtc);
            Assert.AreEqual(1,calls);
            now=now.AddSeconds(1);
            await a.Check("server",3389,false,default);
            await a.Check("server",3389,true,default);
            Assert.AreEqual(3,calls);
            a.Invalidate();
            Assert.AreEqual(Reach.Up,a.Get("server",3389).State);
            await a.Check("server",3389,false,default);
            await a.Check("server",3390,false,default);
            Assert.AreEqual(5,calls);
        }
        [TestMethod]
        public async Task ConcurrentChecksShareConnect()
        {
            int calls=0;
            var reply=Signal<ProbeOutcome>();
            using var a=new Availability((h,p,t)=>{ calls++; return reply.Task; },()=>Start);
            var first=a.Check("server",3389,false,default);
            var second=a.Check("SERVER",3389,true,default);
            Assert.AreEqual(Reach.Checking,a.Get("server",3389).State);
            reply.SetResult(Up);
            var results=await Task.WhenAll(first,second);
            Assert.AreEqual(1,calls);
            Assert.IsTrue(results.All(r=>r.State==Reach.Up && r.LatencyMs==17));
        }
        [TestMethod]
        public async Task TimeoutBackoffAndRecovery()
        {
            var pending=Signal<ProbeOutcome>();
            var response=pending.Task;
            using var a=new Availability((h,p,t)=>response,()=>Start) { Timeout=TimeSpan.FromMilliseconds(25) };
            TimeSpan Interval(int seconds) => a.NextInterval("h",3389,TimeSpan.FromSeconds(seconds));
            Assert.AreEqual(TimeSpan.FromSeconds(20),Interval(20));
            var timeout=await a.Check("h",3389,true,default);
            Assert.AreEqual(Reach.Down,timeout.State);
            Assert.AreEqual(ReachFailure.Timeout,timeout.Failure);
            Assert.AreEqual(1,timeout.Failures);
            Assert.IsNull(timeout.LatencyMs);
            pending.SetException(new InvalidOperationException());
            Assert.AreEqual(TimeSpan.FromSeconds(40),Interval(20));
            response=Task.FromResult(new ProbeOutcome { Failure=ReachFailure.Refused });
            for (int i=2;i<=5;i++)
            {
                Assert.AreEqual(i,(await a.Check("h",3389,true,default)).Failures);
                Assert.AreEqual(TimeSpan.FromSeconds(1<<Math.Min(i,4)),Interval(1));
            }
            Assert.AreEqual(TimeSpan.FromMinutes(5),Interval(20));
            response=Task.FromResult(Up);
            Assert.AreEqual(0,(await a.Check("h",3389,true,default)).Failures);
            Assert.AreEqual(TimeSpan.FromSeconds(20),Interval(20));
        }
        [TestMethod]
        public async Task CancellationRestoresPreviousResult()
        {
            var response=Task.FromResult(Up);
            using var a=new Availability((h,p,t)=>response,()=>Start);
            var first=await a.Check("h",3389,false,default);
            var pending=Signal<ProbeOutcome>();
            response=pending.Task;
            using var cancel=new CancellationTokenSource();
            var checking=a.Check("h",3389,true,cancel.Token);
            Assert.AreEqual(first.CheckedUtc,a.Get("h",3389).CheckedUtc);
            Assert.AreEqual(first.LatencyMs,a.Get("h",3389).LatencyMs);
            cancel.Cancel();
            try { await checking; Assert.Fail("Expected cancellation"); } catch (OperationCanceledException) {}
            Assert.AreSame(first,a.Get("h",3389));
            pending.SetResult(Up);
        }
        [TestMethod]
        public async Task BatchDeduplicatesAndBoundsConcurrencyAndCount()
        {
            var release=Signal<bool>();
            var starts=new List<string>();
            int active=0,peak=0;
            async Task<ProbeOutcome> Connect(string h,int p,CancellationToken t)
            {
                lock (starts) { starts.Add(h); peak=Math.Max(peak,++active); }
                try { await release.Task.ConfigureAwait(false); return Up; }
                finally { lock (starts) active--; }
            }
            using var a=new Availability(Connect,()=>Start) { MaxConcurrent=3 };
            var hosts=Enumerable.Range(0,60).Select(i=>"host"+i).ToArray();
            var pairs=hosts.SelectMany(h=>new[]{ h,h.ToUpperInvariant() }).Select(h=>new KeyValuePair<string,int>(h,3389));
            var batch=a.CheckMany(pairs,0,default);
            Assert.AreEqual(3,starts.Count);
            release.SetResult(true);
            await batch;
            Assert.AreEqual(3,peak);
            CollectionAssert.AreEqual(hosts.Take(50).ToArray(),starts);
        }

        const string Container="TscShellContainerClass", Credential="Credential Dialog Xaml Host";
        static WindowInfo Window(int pid,int handle,string cls,bool visible=true,string title="") =>
            new WindowInfo { ProcessId=pid, Handle=(IntPtr)handle, Class=cls, Visible=visible, Title=title };
        sealed class Windows : IWindowSource
        {
            public WindowInfo[] Items=Array.Empty<WindowInfo>();
            public List<int> Running=new List<int>();
            public List<int> Killed=new List<int>();
            public IntPtr Closed,Focused;
            public int Reads;
            public IReadOnlyList<WindowInfo> TopLevel() { Reads++; return Items; }
            public bool IsAlive(int pid) => Running.Contains(pid);
            public IReadOnlyList<int> Processes(string name) { Assert.AreEqual("mstsc",name); return Running; }
            public bool Focus(IntPtr window) { Focused=window; return true; }
            public bool Close(IntPtr window) { Closed=window; return true; }
            public bool Kill(int pid) { Killed.Add(pid); return true; }
        }
        [TestMethod]
        public void SessionLifecycle()
        {
            var now=Start;
            var w=new Windows { Running={ 1,2 } };
            var hub=new SessionHub(w,()=>now);
            var s=hub.Track("a","Alpha","host",1);
            var other=hub.Track("a","Alpha","host",2);
            Assert.AreSame(other,hub.Find("a"));
            int changes=0,ended=0;
            s.PropertyChanged+=(sender,e)=>{ Assert.IsNull(e.PropertyName); changes++; };
            hub.Ended+=session=>{ Assert.AreSame(s,session); Assert.IsFalse(hub.Sessions.Contains(s)); ended++; };
            var hidden=Window(1,11,Container,false);
            var dialog=Window(1,12,"#32770");
            var prompt=Window(1,13,Credential);
            w.Items=new[]{ hidden,dialog };
            hub.Poll();
            Assert.AreEqual(1,w.Reads);
            Assert.AreEqual(SessionState.Connecting,s.State);
            Assert.AreEqual(dialog.Handle,s.Window);
            Assert.AreEqual(TimeSpan.Zero,s.Duration(now));
            w.Items=new[]{ hidden,dialog,prompt };
            hub.Poll();
            Assert.AreEqual(SessionState.Authenticating,s.State);
            Assert.AreEqual(prompt.Handle,s.Window);
            Assert.IsTrue(hub.Focus(s));
            Assert.AreEqual(prompt.Handle,w.Focused);
            now=now.AddSeconds(3);
            var connected=now;
            w.Items=new[]{ prompt,Window(1,11,Container) };
            hub.Poll();
            Assert.AreEqual(SessionState.Connected,s.State);
            Assert.AreEqual(hidden.Handle,s.Window);
            Assert.AreEqual(connected,s.ConnectedUtc.Value);
            Assert.IsTrue(s.EverConnected);
            hub.Poll();
            Assert.AreEqual(3,changes);
            w.Items=new[]{ hidden,dialog };
            hub.Poll();
            Assert.AreEqual(SessionState.Interrupted,s.State);
            Assert.AreEqual(dialog.Handle,s.Window);
            now=now.AddSeconds(8);
            w.Items=new[]{ Window(1,11,Container) };
            hub.Poll();
            Assert.AreEqual(connected,s.ConnectedUtc.Value);
            Assert.AreEqual(TimeSpan.FromSeconds(8),s.Duration(now));
            w.Running.Remove(1);
            hub.Poll();
            hub.Poll();
            Assert.AreEqual(SessionState.Ended,s.State);
            Assert.AreEqual(IntPtr.Zero,s.Window);
            Assert.AreEqual(1,ended);
            Assert.AreEqual(8,w.Reads);
        }
        [TestMethod]
        public void AdoptionRequiresExactEndpointPrefixAndUntrackedMstsc()
        {
            var w=new Windows { Running={ 1,2,3,4,5 }, Items=new[]{
                Window(1,11,Container,true,"10.0.0.10 - Desktop"), Window(2,21,Container,true,"10.0.0.1 - Desktop"),
                Window(3,31,Container,false,"10.0.0.1 - Desktop"), Window(4,41,"#32770",true,"10.0.0.1 - Desktop"),
                Window(5,51,Container,true,"HOST - Desktop"), Window(5,52,Container,true,"host - Desktop"),
                Window(6,61,Container,true,"10.0.0.1 - Desktop") } };
            var hub=new SessionHub(w,()=>Start);
            hub.Track("existing","existing","10.0.0.1",2);
            var endpoints=new Dictionary<string,string> { ["a"]="10.0.0.1", ["b"]="host" };
            Assert.AreEqual(1,hub.Adopt(endpoints));
            Assert.AreEqual(0,hub.Adopt(endpoints));
            Assert.AreEqual(2,hub.Sessions.Count);
            var s=hub.Find("b");
            Assert.IsTrue(s.Adopted && s.EverConnected);
            Assert.AreEqual(Start,s.ConnectedUtc.Value);
            Assert.AreEqual(SessionState.Connected,s.State);
            Assert.AreEqual((IntPtr)51,s.Window);
        }
        [TestMethod]
        public void DisconnectTargetsContainerAndKillsOnlyTrackedProcesses()
        {
            var w=new Windows { Running={ 1,2,3 }, Items=new[]{ Window(1,11,"#32770"),Window(1,12,Container,false),Window(2,21,Credential) } };
            var hub=new SessionHub(w,()=>Start);
            var first=hub.Track("a","A","h",1);
            var second=hub.Track("b","B","i",2);
            hub.Poll();
            Assert.IsTrue(hub.Disconnect(first));
            Assert.IsFalse(hub.Disconnect(second));
            Assert.AreEqual((IntPtr)12,w.Closed);
            Assert.AreEqual(2,hub.DisconnectAll());
            CollectionAssert.AreEquivalent(new[]{ 1,2 },w.Killed);
        }
    }
}
