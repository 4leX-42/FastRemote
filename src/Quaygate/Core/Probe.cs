using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Quaygate
{
    public sealed class Probe : IDisposable
    {
        readonly Dictionary<string,(DateTime Expires,bool Up)> cache = new Dictionary<string,(DateTime,bool)>(StringComparer.OrdinalIgnoreCase);
        readonly Func<string,int,CancellationToken,Task<bool>> connect;
        CancellationTokenSource active;
        public Probe() : this(ConnectOnce) {}
        internal Probe(Func<string,int,CancellationToken,Task<bool>> connect) { this.connect=connect; }
        static async Task<bool> ConnectOnce(string host,int port,CancellationToken token)
        {
            try
            {
                if (!IPAddress.TryParse(host.Trim('[',']'),out var address))
                    address=(await Dns.GetHostAddressesAsync(host)).FirstOrDefault(a=>a.AddressFamily==AddressFamily.InterNetwork ||
                        a.AddressFamily==AddressFamily.InterNetworkV6 && Socket.OSSupportsIPv6);
                token.ThrowIfCancellationRequested();
                if (address==null) return false;
                using var tcp=new TcpClient(address.AddressFamily);
                using var cancel=token.Register(tcp.Close);
                await tcp.ConnectAsync(address,port);
                return true;
            }
            catch(SocketException) { return false; }
            catch(ObjectDisposedException) when (token.IsCancellationRequested) { return false; }
        }
        public void Cancel() { active?.Cancel(); active=null; }
        public async Task<bool?> Selected(string host,int port)
        {
            Cancel();
            var key=Rdp.Endpoint(host,port);
            if (cache.TryGetValue(key,out var hit) && hit.Expires>DateTime.UtcNow) return hit.Up;
            using var source=new CancellationTokenSource();
            active=source;
            try
            {
                // Rapid keyboard navigation never starts a sequence of probes.
                await Task.Delay(180,source.Token);
                using var attempt=CancellationTokenSource.CreateLinkedTokenSource(source.Token);
                var pending=connect(host,port,attempt.Token);
                var timeout=Task.Delay(2000,source.Token);
                var finished=await Task.WhenAny(pending,timeout);
                bool up=false;
                if (finished==pending) up=await pending;
                else
                {
                    attempt.Cancel();
                    _=pending.ContinueWith(t=>{ var ignored=t.Exception; },TaskContinuationOptions.OnlyOnFaulted);
                }
                if (source.IsCancellationRequested) return null;
                cache[key]=(DateTime.UtcNow.AddSeconds(20),up);
                return up;
            }
            catch (OperationCanceledException) { return null; }
            catch (ObjectDisposedException) when (source.IsCancellationRequested) { return null; }
            finally { if (ReferenceEquals(active,source)) active=null; }
        }
        public void Dispose() => Cancel();
    }
}
