using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Pilgrimage.UI
{
    // Only this adapter knows NGO. GameSession and LanRoom remain transport independent.
    public sealed class LanNetworkSession : MonoBehaviour
    {
        [Serializable] private sealed class Hello { public string fingerprint, name; }
        public event Action<LanEnvelope> Changed;
        public event Action<string> Closed;
        public LanRoom Authority { get; private set; }
        public LanEnvelope View { get; private set; }
        public bool Pending { get; private set; }
        public bool IsHost => manager != null && manager.IsHost;
        public bool Connected => manager != null && manager.IsConnectedClient;
        public ushort Port { get; private set; }
        public string Address { get; private set; }
        private NetworkManager manager;
        private GameObject networkObject;
        private readonly Dictionary<ulong, string> joining = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, float> requestWindow = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, int> requestCount = new Dictionary<ulong, int>();
        private AiSettings configuration;
        private long sequence, awaiting;
        private bool stopping, revealing, leaving;
        private bool? previousRunInBackground;
        private float deadline, pendingSince;
        private const string Commands = "pilgrimage.command.v1", Views = "pilgrimage.view.v1";
        private const int MaximumCommandCharacters = 4096;
        // A lost or rejected-without-reply request must never lock the local UI forever.
        private const float PendingTimeout = 6f;
        public static string LocalAddresses {
            get {
                // Avoid Dns.GetHostName: Mono can fail converting Korean Windows computer names.
                try {
                    string found=string.Join(" / ",NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up)
                        .SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address)
                        .Where(a=>a.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a)).Distinct().Select(a=>a.ToString()));
                    return found.Length==0?"127.0.0.1":found;
                } catch(Exception) { return "127.0.0.1 (LAN IP는 ipconfig에서 확인)"; }
            }
        }

        public bool Open(bool host, string address, ushort port, string room, string name, AiSettings ai, DemoRuleAssumptions rules, out string error)
        {
            error = null;
            if (manager != null || NetworkManager.Singleton != null) { error = "이미 네트워크 세션이 실행 중입니다."; return false; }
            if (port == 0 || (!host && (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork || address == "0.0.0.0"))) { error = "올바른 IPv4 주소와 1~65535 포트를 입력하세요."; return false; }
            Port = port; Address = host ? LocalAddresses : address; configuration = ai ?? new AiSettings();
            if (!previousRunInBackground.HasValue) previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            networkObject = new GameObject("Pilgrimage LAN Transport");
            // NGO requires a root object; this component owns and destroys it explicitly.
            var transport = networkObject.AddComponent<UnityTransport>();
            transport.SetConnectionData(host ? "127.0.0.1" : address, port, "0.0.0.0");
            transport.MaxPayloadSize = 262144;
            transport.MaxSendQueueSize = 4194304;
            transport.DisconnectTimeoutMS = 5000;
            transport.ConnectTimeoutMS = 500;
            transport.MaxConnectAttempts = 10;
            manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
                ConnectionApproval = true, ForceSamePrefabs = false, ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello { fingerprint = LanProtocol.Fingerprint, name = LanProtocol.CleanName(name) })) };
            manager.ConnectionApprovalCallback += Approve;
            manager.OnClientConnectedCallback += ConnectedClient;
            manager.OnClientDisconnectCallback += DisconnectedClient;
            manager.OnTransportFailure += TransportFailed;
            if (host) Authority = new LanRoom(NetworkManager.ServerClientId, room, name, configuration, rules);
            try {
                bool started = host ? manager.StartHost() : manager.StartClient();
                if (!started) { error = "연결을 시작하지 못했습니다. 포트 사용 여부를 확인하세요."; Shutdown(); return false; }
                manager.CustomMessagingManager.RegisterNamedMessageHandler(Commands, ReceiveCommand);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(Views, ReceiveView);
                deadline = Time.realtimeSinceStartup + 12;
                if (host) Publish();
                return true;
            } catch (Exception e) { error = "LAN 연결 오류: " + e.Message; Shutdown(); return false; }
        }
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; response.Pending = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId && Authority != null) { response.Approved = true; return; }
            Hello hello = null;
            try { if (request.Payload != null && request.Payload.Length <= 1024) hello = JsonUtility.FromJson<Hello>(Encoding.UTF8.GetString(request.Payload)); } catch (ArgumentException) { }
            response.Approved = Authority != null && Authority.CanJoin && Authority.Members.Count + joining.Count < 4 && hello?.fingerprint == LanProtocol.Fingerprint;
            response.Reason = hello?.fingerprint != LanProtocol.Fingerprint ? "게임 버전 또는 카드 데이터가 다릅니다." : "방이 가득 찼거나 이미 시작되었습니다.";
            if (response.Approved) joining[request.ClientNetworkId] = LanProtocol.CleanName(hello.name);
        }
        private void ConnectedClient(ulong id)
        {
            if (!IsHost) return;
            if (id != NetworkManager.ServerClientId) {
                if (!Authority.Join(id, joining.TryGetValue(id, out string name) ? name : "참가자", out string reason)) { manager.DisconnectClient(id, reason); return; }
                joining.Remove(id);
            }
            Publish();
        }
        private void DisconnectedClient(ulong id)
        {
            joining.Remove(id); requestWindow.Remove(id); requestCount.Remove(id);
            if (stopping) return;
            if (IsHost && id != NetworkManager.ServerClientId) { Authority.Disconnect(id); Publish(); }
            else Fail(string.IsNullOrWhiteSpace(manager?.DisconnectReason) ? "호스트와 연결이 종료되었습니다. 방이 닫혔습니다." : manager.DisconnectReason);
        }
        private void TransportFailed() => Fail("네트워크 연결 실패: IP·포트·방화벽을 확인하세요.");
        private void Update()
        {
            if (manager == null || stopping || leaving) return;
            if (Pending && Time.realtimeSinceStartup - pendingSince > PendingTimeout) {
                Pending = false;
                if (View != null) { View = new LanEnvelope { lobby = View.lobby, game = View.game, error = "호스트 응답이 없어 요청을 취소했습니다. 다시 시도하세요." }; Changed?.Invoke(View); }
            }
            if (!Connected && Time.realtimeSinceStartup > deadline) { Fail("접속 시간 초과: 호스트 IP·포트와 LAN 연결을 확인하세요."); return; }
            if (!IsHost || Authority == null) return;
            if (!Authority.Busy && Authority.StepAi()) Publish();
            if (Authority.Busy && !revealing) StartCoroutine(ResolveReveal());
        }
        private IEnumerator ResolveReveal()
        {
            revealing = true;
            try {
                var reveal=Authority.Reveal;
                float before=reveal.travelSeconds+reveal.cutInSeconds*.75f;
                yield return new WaitForSecondsRealtime(before);
                if (Authority == null || stopping) yield break;
                Authority.CompletePlay(out _); Publish();
                float remainingCutIn=reveal.cutInSeconds*.25f;
                float interval=Mathf.Clamp(configuration.revealInterval,.1f,10)*Mathf.Clamp(configuration.presentationTimeScale,.02f,2);
                yield return new WaitForSecondsRealtime(Mathf.Max(remainingCutIn+reveal.resultSeconds+.05f,interval-before));
                if (Authority == null || stopping) yield break;
                Authority.FinishReveal(); Publish();
            } finally { revealing = false; }
        }
        public bool Send(LanCommand command)
        {
            if (View?.lobby == null || manager == null || stopping || leaving || Pending) return false;
            command.session = View.lobby.session; command.sequence = ++sequence; command.version = View.lobby.version; command.round = View.game?.round ?? 0;
            if (command.kind == LanCommandKind.Play || command.kind == LanCommandKind.EndTurn || command.kind == LanCommandKind.Acknowledge) { Pending = true; awaiting = command.sequence; pendingSince = Time.realtimeSinceStartup; }
            if (IsHost) Process(NetworkManager.ServerClientId, command);
            else SendJson(Commands, NetworkManager.ServerClientId, JsonUtility.ToJson(command));
            return true;
        }
        private void ReceiveCommand(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || Authority == null) return;
            if (!requestWindow.TryGetValue(sender, out float time) || Time.realtimeSinceStartup-time>=1) { requestWindow[sender]=Time.realtimeSinceStartup; requestCount[sender]=0; }
            if (++requestCount[sender]>100) { manager.DisconnectClient(sender,"요청이 너무 많습니다."); return; }
            // Strings are UTF-16 plus a 4-byte length prefix.
            if (reader.Length > MaximumCommandCharacters * 2 + 16) { Reject(sender, 0, "요청이 너무 큽니다."); return; }
            LanCommand command;
            try {
                reader.ReadValueSafe(out string json);
                if (json == null || json.Length > MaximumCommandCharacters) { Reject(sender, 0, "요청이 너무 큽니다."); return; }
                command = JsonUtility.FromJson<LanCommand>(json);
            } catch (Exception e) { Debug.LogWarning("LAN: malformed command from " + sender + ": " + e.Message); manager.DisconnectClient(sender, "잘못된 네트워크 요청"); return; }
            Process(sender, command);
        }
        private void Reject(ulong sender, long reply, string reason)
            => Deliver(sender, new LanEnvelope { lobby = Authority.LobbyFor(sender), game = SafeView(sender), reply = reply, error = reason });
        private LanGameView SafeView(ulong recipient)
        {
            try { return Authority.ViewFor(recipient); }
            catch (Exception e) { Debug.LogException(e); return null; }
        }
        private void Process(ulong sender, LanCommand command)
        {
            int version = Authority.Version;
            bool started = Authority.Game != null;
            bool ok; string reason;
            // Any rule-code failure still produces a reply, so the sender's Pending lock is released.
            try { ok = Authority.Submit(sender, command, out reason, UnityEngine.Random.Range(1, int.MaxValue)); }
            catch (Exception e) { Debug.LogException(e); ok = false; reason = "호스트에서 요청을 처리하지 못했습니다. 다시 시도하세요."; }
            if (!started && Authority.Game != null && Debug.isDebugBuild) Debug.Log("LAN host started: faction seed="+Authority.AssignmentSeed+"; private game seed="+Authority.GameSeed);
            if (Authority.Version != version) Publish();
            Deliver(sender, new LanEnvelope { lobby = Authority.LobbyFor(sender), game = SafeView(sender), reply = command?.sequence ?? 0, error = ok ? null : reason });
        }
        // Host-only: removes a remote human. During a game the seat is handed to the host AI.
        public bool Kick(ulong connection)
        {
            if (!IsHost || Authority == null || stopping || leaving || connection == NetworkManager.ServerClientId) return false;
            if (!Authority.Members.Any(m => m.Connection == connection)) return false;
            if (manager.ConnectedClients.ContainsKey(connection)) manager.DisconnectClient(connection, "호스트가 방에서 내보냈습니다. 진행 중이던 좌석은 AI가 이어받습니다.");
            Authority.Disconnect(connection); Publish(); return true;
        }
        private void ReceiveView(ulong sender, FastBufferReader reader)
        {
            if (IsHost || sender != NetworkManager.ServerClientId || reader.Length > LanProtocol.MaximumJsonCharacters * 2 + 16) return;
            try {
                reader.ReadValueSafe(out string json);
                if (json.Length <= LanProtocol.MaximumJsonCharacters) Apply(JsonUtility.FromJson<LanEnvelope>(json));
            } catch (Exception e) when (e is ArgumentException || e is OverflowException || e is InvalidOperationException) { Fail("호스트 상태 메시지를 읽지 못했습니다."); }
        }
        private void SendJson(string channel, ulong recipient, string json)
        {
            if (json.Length > LanProtocol.MaximumJsonCharacters) { Debug.LogError("LAN snapshot exceeds protocol budget; not sent."); return; }
            using (var writer = new FastBufferWriter(Encoding.Unicode.GetByteCount(json) + 16, Allocator.Temp)) {
                writer.WriteValueSafe(json);
                manager.CustomMessagingManager.SendNamedMessage(channel, recipient, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
        private void Deliver(ulong recipient, LanEnvelope data)
        {
            // Even host presentation consumes a filtered view, never all private model piles.
            if (recipient == NetworkManager.ServerClientId) Apply(data);
            else if (manager.ConnectedClients.ContainsKey(recipient)) SendJson(Views, recipient, JsonUtility.ToJson(data));
        }
        private void Publish()
        {
            if (Authority == null || manager == null) return;
            foreach (var member in Authority.Members.ToArray()) Deliver(member.Connection, new LanEnvelope { lobby = Authority.LobbyFor(member.Connection), game = SafeView(member.Connection) });
        }
        private void Apply(LanEnvelope data)
        {
            if (data == null) return;
            if (data.closed) { Fail(data.error ?? "호스트가 방을 종료했습니다."); return; }
            if (View?.lobby != null && data.lobby != null && data.lobby.version < View.lobby.version) return;
            if (data.reply >= awaiting && data.reply != 0) Pending = false;
            View = data; Changed?.Invoke(data);
        }
        public void Leave() { if (stopping || leaving) return; leaving = true; StartCoroutine(LeaveRoom()); }
        private IEnumerator LeaveRoom()
        {
            if (IsHost) {
                foreach (ulong id in manager.ConnectedClientsIds.ToArray()) if (id != NetworkManager.ServerClientId)
                    SendJson(Views, id, JsonUtility.ToJson(new LanEnvelope { closed = true, error = "호스트가 방을 종료했습니다." }));
                yield return new WaitForSecondsRealtime(.2f);
            }
            Fail("방에서 나왔습니다.");
        }
        private void Fail(string reason) { if (stopping) return; Shutdown(); Closed?.Invoke(reason); }
        public void Shutdown()
        {
            stopping = true; StopAllCoroutines(); Pending = false; revealing = false;
            if (manager != null) {
                manager.ConnectionApprovalCallback -= Approve; manager.OnClientConnectedCallback -= ConnectedClient;
                manager.OnClientDisconnectCallback -= DisconnectedClient; manager.OnTransportFailure -= TransportFailed;
                manager.Shutdown();
            }
            if (networkObject != null) Destroy(networkObject);
            manager = null; Authority = null;
            if (previousRunInBackground.HasValue) { Application.runInBackground = previousRunInBackground.Value; previousRunInBackground = null; }
        }
        private void OnDestroy() => Shutdown();
    }
}
