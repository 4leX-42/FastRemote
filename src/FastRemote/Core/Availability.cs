using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FastRemote
{
    public enum Reach { Unknown, Checking, Up, Down }
    public enum ReachFailure { None, Dns, Timeout, Refused, Unreachable, Other }
    public sealed class ReachResult
    {
        public Reach State { get; set; }
        public ReachFailure Failure { get; set; }
        public int? LatencyMs { get; set; }
        public DateTime CheckedUtc { get; set; }
        public int Failures { get; set; }
    }
    public struct ProbeOutcome { public bool Up; public ReachFailure Failure; public int LatencyMs; }

    public sealed class Availability : IDisposable
    {
        sealed class Entry
        {
            internal ReachResult Result=new ReachResult();
            internal Task<ReachResult> Pending;
            internal long Generation=-1;
        }
        readonly object gate=new object();
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        readonly SynchronizationContext context=SynchronizationContext.Current;
        readonly Func<string,int,CancellationToken,Task<ProbeOutcome>> connect;
        readonly Func<DateTime> utcNow;
        SemaphoreSlim slots;
        long generation;
        bool disposed;

        public Availability() : this(Connect,()=>DateTime.UtcNow) {}
        internal Availability(Func<string,int,CancellationToken,Task<ProbeOutcome>> connect,Func<DateTime> utcNow)
        { this.connect=connect; this.utcNow=utcNow; }
        public TimeSpan CacheFor { get; set; }=TimeSpan.FromSeconds(30);
        public TimeSpan Timeout { get; set; }=TimeSpan.FromSeconds(2);
        public int MaxConcurrent { get; set; }=2;
        public event Action<string,ReachResult> Changed;

        public ReachResult Get(string host,int port)
        {
            lock (gate) return entries.TryGetValue(Rdp.Endpoint(host,port),out var e) ? e.Result : new ReachResult();
        }
        public void Invalidate() { lock (gate) generation++; }
        public TimeSpan NextInterval(string host,int port,TimeSpan baseInterval)
        {
            long factor=1L<<Math.Min(Get(host,port).Failures,4), cap=TimeSpan.FromMinutes(5).Ticks;
            return TimeSpan.FromTicks(baseInterval.Ticks>cap/factor ? cap : baseInterval.Ticks*factor);
        }
        void Publish(string key,ReachResult result)
        {
            var handler=Changed;
            if (handler==null) return;
            if (context==null) handler(key,result);
            else context.Post(_=>handler(key,result),null);
        }
        public Task<ReachResult> Check(string host,int port,bool force,CancellationToken token)
        {
            var key=Rdp.Endpoint(host,port);
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(Availability));
                if (token.IsCancellationRequested) return Task.FromCanceled<ReachResult>(token);
                if (!entries.TryGetValue(key,out var entry)) entries[key]=entry=new Entry();
                if (entry.Pending!=null) return token.CanBeCanceled ? WaitFor(entry.Pending,token) : entry.Pending;
                var previous=entry.Result;
                if (!force && entry.Generation==generation && utcNow()-previous.CheckedUtc<CacheFor)
                    return Task.FromResult(previous);
                slots ??= new SemaphoreSlim(MaxConcurrent);
                var done=new TaskCompletionSource<ReachResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                entry.Pending=done.Task;
                _=Run(host,port,key,entry,previous,generation,token,done);
                return done.Task;
            }
        }
        static async Task<ReachResult> WaitFor(Task<ReachResult> pending,CancellationToken token)
        {
            var canceled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration=token.Register(()=>canceled.TrySetResult(true));
            await Task.WhenAny(pending,canceled.Task).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return await pending.ConfigureAwait(false);
        }
        async Task Run(string host,int port,string key,Entry entry,ReachResult previous,long startedGeneration,
            CancellationToken caller,TaskCompletionSource<ReachResult> done)
        {
            using var active=CancellationTokenSource.CreateLinkedTokenSource(caller,lifetime.Token);
            try
            {
                entry.Result=new ReachResult { State=Reach.Checking, CheckedUtc=previous.CheckedUtc,
                    LatencyMs=previous.LatencyMs, Failures=previous.Failures };
                Publish(key,entry.Result);
                await slots.WaitAsync(active.Token).ConfigureAwait(false);
                ProbeOutcome outcome;
                try { outcome=await Attempt(host,port,active.Token).ConfigureAwait(false); }
                finally { slots.Release(); }
                lock (gate)
                {
                    active.Token.ThrowIfCancellationRequested();
                    var result=new ReachResult { State=outcome.Up ? Reach.Up : Reach.Down, CheckedUtc=utcNow(),
                        Failure=outcome.Up ? ReachFailure.None : outcome.Failure,
                        LatencyMs=outcome.Up ? outcome.LatencyMs : (int?)null, Failures=outcome.Up ? 0 : previous.Failures+1 };
                    entry.Result=result;
                    entry.Generation=startedGeneration;
                    done.SetResult(result);
                    entry.Pending=null;
                    Publish(key,result);
                }
            }
            catch (Exception error) when (!done.Task.IsCompleted)
            {
                lock (gate)
                {
                    entry.Result=previous;
                    entry.Pending=null;
                    if (error is OperationCanceledException) done.SetCanceled();
                    else done.SetException(error);
                    Publish(key,previous);
                }
            }
        }
        async Task<ProbeOutcome> Attempt(string host,int port,CancellationToken token)
        {
            using var attempt=CancellationTokenSource.CreateLinkedTokenSource(token);
            var delay=Task.Delay(Timeout,attempt.Token);
            try
            {
                token.ThrowIfCancellationRequested();
                var pending=connect(host,port,attempt.Token);
                if (await Task.WhenAny(pending,delay).ConfigureAwait(false)==pending)
                    return await pending.ConfigureAwait(false);
                _=pending.ContinueWith(t=>{ _=t.Exception; },CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                token.ThrowIfCancellationRequested();
                return new ProbeOutcome { Failure=ReachFailure.Timeout };
            }
            finally { attempt.Cancel(); }
        }
        public async Task CheckMany(IEnumerable<KeyValuePair<string,int>> endpoints,int spacingMs,CancellationToken token)
        {
            CancellationTokenSource batch;
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(Availability));
                batch=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);
            }
            using (batch)
            {
                var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pending=new List<Task<ReachResult>>();
                try
                {
                    batch.Token.ThrowIfCancellationRequested();
                    foreach (var pair in endpoints)
                    {
                        batch.Token.ThrowIfCancellationRequested();
                        if (!seen.Add(Rdp.Endpoint(pair.Key,pair.Value))) continue;
                        if (seen.Count>1 && spacingMs>0) await Task.Delay(spacingMs,batch.Token).ConfigureAwait(false);
                        if (pending.Count>=MaxConcurrent)
                        {
                            var first=await Task.WhenAny(pending).ConfigureAwait(false);
                            await first.ConfigureAwait(false);
                            pending.Remove(first);
                        }
                        pending.Add(Check(pair.Key,pair.Value,false,batch.Token));
                        if (seen.Count==50) break;
                    }
                }
                finally { await Task.WhenAll(pending).ConfigureAwait(false); }
            }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed=true;
            }
            lifetime.Cancel();
            lifetime.Dispose();
        }
        static async Task<ProbeOutcome> Connect(string host,int port,CancellationToken token)
        {
            try
            {
                if (!IPAddress.TryParse(host.Trim('[',']'),out var address))
                {
                    var addresses=await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
                    address=addresses.FirstOrDefault(a=>a.AddressFamily==AddressFamily.InterNetwork) ??
                        addresses.FirstOrDefault(a=>a.AddressFamily==AddressFamily.InterNetworkV6 && Socket.OSSupportsIPv6);
                }
                token.ThrowIfCancellationRequested();
                if (address==null) return new ProbeOutcome { Failure=ReachFailure.Dns };
                if (address.AddressFamily==AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
                    return new ProbeOutcome { Failure=ReachFailure.Unreachable };
                using var tcp=new TcpClient(address.AddressFamily);
                using var registration=token.Register(tcp.Close);
                var watch=Stopwatch.StartNew();
                await tcp.ConnectAsync(address,port).ConfigureAwait(false);
                return new ProbeOutcome { Up=true, LatencyMs=(int)watch.ElapsedMilliseconds };
            }
            catch (SocketException ex)
            {
                token.ThrowIfCancellationRequested();
                return new ProbeOutcome { Failure=ex.SocketErrorCode switch {
                    SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => ReachFailure.Dns,
                    SocketError.ConnectionRefused => ReachFailure.Refused,
                    SocketError.NetworkUnreachable or SocketError.HostUnreachable => ReachFailure.Unreachable,
                    SocketError.TimedOut => ReachFailure.Timeout, _ => ReachFailure.Other } };
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        }
    }
}
