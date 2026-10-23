using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Snake3D
{
    public sealed class InviteState { public string Room, Token, Error; public object Info; public bool Joining; }

    /// <summary>
    /// 線上對戰的資料與伺服器訊息處理（對應網頁版 online.js 的 init）。
    /// 伺服器位置一律從 Cfg / Net 取得；規則與快照由伺服器權威計算，這裡只負責顯示與送出輸入。
    /// </summary>
    public sealed class Online
    {
        readonly App app;
        public object Room;                        // 目前房間（JSON）
        public List<object> Rooms = new List<object>();
        public InviteState Invite;
        public string LobbyNotice = "";
        public int MySlot = -1;
        public bool InGame;
        public GamePage Game;
        public LobbyPage Lobby; public RoomPage RoomUi; public Ui.ModalHandle InviteModal; public Action InviteRefresh;
        public List<ChatMsg> ChatLog = new List<ChatMsg>();
        public event Action<ChatMsg> ChatPushed;
        public event Action ChatReset;
        public string AiAddLevel = "normal";

        static readonly Dictionary<string, string> REASONS = new Dictionary<string, string>
        {
            { "invalid", "inv.invalid" }, { "revoked", "inv.revoked" }, { "expired", "inv.expired" }, { "closed", "inv.closed" }, { "banned", "inv.banned" }, { "spec_full", "inv.spec_full" }
        };
        public static string ReasonText(string code) { string k; return code != null && REASONS.TryGetValue(code, out k) ? Loc.T(k) : ""; }

        public static string RoomName(string n)
        {
            var m = Regex.Match(n ?? "", "^(.*)的房間$");
            return m.Success && m.Groups[1].Length > 0 ? Loc.T("room.default", "name", m.Groups[1].Value) : (n ?? "");
        }

        public bool IsHost { get { return Room != null && Json.Bool(Json.Get(Room, "you"), "host"); } }

        public Online(App app)
        {
            this.app = app;
            Net.StatusChanged += OnStatus;
            Net.On("welcome", m =>
            {
                Room = Json.Get(m, "room");
                if (Room != null && app.Current is LobbyPage && Json.Str(Room, "phase") != "playing") app.Go(new RoomPage());
                else if (Room != null && app.Current is RoomPage && RoomUi != null) RoomUi.Paint();
                AskInvite();
            });
            Net.On("rooms", m => { Rooms = Json.Arr(m, "rooms") ?? new List<object>(); if (Lobby != null && app.Current == Lobby) Lobby.PaintList(); });
            Net.On("room", m =>
            {
                Room = Json.Get(m, "room");
                if (Room == null) { if (app.Current is RoomPage || (app.Current is GamePage && InGame)) LeaveToLobby(); return; }
                if (app.Current is RoomPage) { if (RoomUi != null) RoomUi.Paint(); }
                else if (app.Current is LobbyPage && Json.Str(Room, "phase") != "playing") app.Go(new RoomPage());
                else if (app.Current is GamePage && InGame && Json.Str(Room, "phase") == "lobby" && ResultOpen())
                {
                    app.CloseAllModals(); InGame = false; app.Go(new RoomPage()); app.Toast(Loc.T("net.newRound"), 4.2f);
                }
                if (InviteRefresh != null && InviteModal != null && InviteModal.Mask != null) InviteRefresh();
            });
            Net.On("joined", m =>
            {
                Invite = null; Platform.ReplaceUrl("?");
                string note = Json.Str(m, "note");
                if (note.Length > 0) app.Toast(Loc.Message(note), 4f);
                if (!(app.Current is GamePage)) app.Go(new RoomPage());
            });
            Net.On("joinFailed", m =>
            {
                string reason = Json.Str(m, "reason");
                if (Invite != null && Invite.Joining) { Invite.Joining = false; Invite.Error = reason; }
                else { string t = ReasonText(reason); app.Toast(t.Length > 0 ? t : Loc.T("net.cantJoin"), 3.6f); }
                if (Lobby != null && app.Current == Lobby) Lobby.PaintInvite();
            });
            Net.On("inviteInfo", m =>
            {
                if (Invite == null) return;
                if (Json.Bool(m, "ok")) Invite.Info = m; else Invite.Error = Json.Str(m, "reason");
                if (Lobby != null && app.Current == Lobby) Lobby.PaintInvite();
            });
            Net.On("invite", m => { if (InviteRefresh != null && InviteModal != null && InviteModal.Mask != null) InviteRefresh(); });
            Net.On("chatlog", m =>
            {
                ChatLog = new List<ChatMsg>();
                var l = Json.Arr(m, "msgs");
                if (l != null) foreach (var o in l) ChatLog.Add(ToChat(o));
                if (ChatReset != null) ChatReset();
            });
            Net.On("chat", m =>
            {
                var c = ToChat(Json.Get(m, "m"));
                ChatLog.Add(c); if (ChatLog.Count > 80) ChatLog.RemoveAt(0);
                if (ChatPushed != null) ChatPushed(c);
            });
            Net.On("closed", m =>
            {
                LobbyNotice = Loc.Message(Json.Str(m, "text", "") is string s && s.Length > 0 ? s : Loc.T("net.closed"));
                app.Toast(LobbyNotice, 4.2f);
                if (Lobby != null && app.Current == Lobby) Lobby.PaintNotice();
            });
            Net.On("kicked", m =>
            {
                string s = Json.Str(m, "text");
                app.Toast(Loc.Message(s.Length > 0 ? s : Loc.T("net.kickedToast")), 4.2f);
                LobbyNotice = Loc.Message(s.Length > 0 ? s : Loc.T("net.kickedNote"));
            });
            Net.On("error", m => { string s = Json.Str(m, "text"); app.Toast(Loc.Message(s.Length > 0 ? s : Loc.T("net.error")), 3.6f); });
            Net.On("start", m =>
            {
                app.CloseAllModals();
                InGame = true;
                var sl = Json.Get(m, "slot");
                MySlot = sl is double ? (int)(double)sl : -1;
                var gp = new GamePage(Json.Get(m, "game"), MySlot);
                Game = gp;
                app.Go(gp);
            });
            Net.On("snap", m => { if (Game != null && InGame && app.Current == Game) Game.OnSnap(Json.Get(m, "s")); });
            Net.On("result", m =>
            {
                if (!InGame || Game == null) return;
                Game.ShowResult(ResultInfo.FromJson(Json.Get(m, "result"), Json.Get(m, "players")));
            });
            Net.On("replaced", m => { app.Toast(Loc.T("net.replaced"), 5f); app.Banner(Loc.T("net.replacedBanner")); });
        }

        bool ResultOpen() { return Game != null; }

        static ChatMsg ToChat(object o) { return new ChatMsg { Role = Json.Str(o, "role"), Name = Json.Str(o, "name"), Text = Json.Str(o, "text") }; }

        public bool NeedsNet(Page p) { return p is LobbyPage || p is RoomPage || (p is GamePage && ((GamePage)p).kind == GamePage.Kind.Online); }

        // ---------- 連線橫幅 ----------
        string netDetailText;
        void OnStatus(string s, int detail)
        {
            RepaintBanner();
            if (Lobby != null && app.Current == Lobby) { Lobby.PaintStatus(); Lobby.PaintList(); Lobby.PaintInvite(); }
            if (s == "open") AskInvite();
        }

        public void RepaintBanner()
        {
            bool inOnline = app.Current is LobbyPage || app.Current is RoomPage || (app.Current is GamePage && InGame);
            if (!inOnline) { app.Banner(""); return; }
            switch (Net.Status)
            {
                case "waking": app.Banner(Loc.T("net.waking", "n", Net.StatusDetail)); break;
                case "connecting": app.Banner(Loc.T("net.connecting")); break;
                case "retrying": app.Banner(Loc.T("net.retrying")); break;
                case "offline": app.Banner(Loc.T("net.offline")); break;
                default: app.Banner(""); break;
            }
        }

        // ---------- 動作 ----------
        public void AskInvite()
        {
            if (Invite != null && Invite.Info == null && Invite.Error == null && Net.Connected)
                Net.Send(Net.Msg("inviteInfo", "room", Invite.Room, "token", Invite.Token));
        }

        public void ConfirmLeave()
        {
            app.ConfirmBox(Loc.T("leave.title"), Loc.T("leave.text"), Loc.T("leave.ok"), LeaveRoom, true);
        }

        void ResetRoomState() { Room = null; InGame = false; ChatLog = new List<ChatMsg>(); MySlot = -1; Game = null; }

        public void LeaveRoom() { Net.Send(Net.Msg("leave")); ResetRoomState(); app.Go(new LobbyPage()); }
        public void LeaveToHome() { Net.Send(Net.Msg("leave")); ResetRoomState(); app.Go(new HomePage()); }
        void LeaveToLobby() { app.CloseAllModals(); ResetRoomState(); if (app.Current is RoomPage || app.Current is GamePage) app.Go(new LobbyPage()); }
        public void BackToRoom() { InGame = false; app.Go(new RoomPage()); }
        public void Rematch() { Net.Send(Net.Msg("rematch")); InGame = false; app.Go(new RoomPage()); app.Toast(Loc.T("res.rematch"), 3.2f); }

        public void SendProfile() { Net.SetProfile(app.MyName(), Store.D.animal); Net.Send(Net.Msg("profile", "name", app.MyName(), "animal", Store.D.animal)); }

        public string InviteLink(string token)
        {
            string href = Platform.Href();
            int q = href.IndexOfAny(new[] { '?', '#' }); if (q >= 0) href = href.Substring(0, q);
            string u = href + "?room=" + Uri.EscapeDataString(Json.Str(Room, "id")) + "&invite=" + Uri.EscapeDataString(token);
            if (Cfg.Source == "query" && !string.IsNullOrEmpty(Cfg.ServerUrl)) u += "&server=" + Uri.EscapeDataString(Cfg.ServerUrl);
            return u;
        }
    }

    // =====================================================================
    //                              大廳
    // =====================================================================
    public sealed class LobbyPage : Page
    {
        RectTransform statusBox, noticeBox, inviteBox, profileBox, actionBox, listBox;
        public override bool Back() { App.I.Go(new HomePage()); return true; }

        static void Fill(RectTransform box, Action<RectTransform> build)
        {
            if (box == null) return;
            for (int i = box.childCount - 1; i >= 0; i--) { var c = box.GetChild(i).gameObject; c.SetActive(false); UnityEngine.Object.Destroy(c); }
            build(box);
        }

        public static void StatusInfo(out string style, out string text)
        {
            string s = Net.Status;
            if (Cfg.Status == "invalid") { style = "danger"; text = Loc.T("conn.bad"); return; }
            if (s == "unset" || Cfg.Status == "unset") { style = "danger"; text = Loc.T("conn.unset"); return; }
            switch (s)
            {
                case "open": style = "primary"; text = Loc.T("conn.open"); break;
                case "waking": style = "sun"; text = Loc.T("conn.waking"); break;
                case "offline": style = "danger"; text = Loc.T("conn.offline"); break;
                case "retrying": style = "sun"; text = Loc.T("conn.retry"); break;
                case "idle": style = "danger"; text = Loc.T("conn.idle"); break;
                default: style = "sun"; text = Loc.T("conn.ing"); break;
            }
        }

        public override void Build(RectTransform root)
        {
            var on = App.I.Online;
            on.Lobby = this; on.InGame = false;
            var p = Lay.Page(root, 1000);
            var col = p.Col;
            var bar = Ui.HBox(col, 10, 0, TextAnchor.MiddleLeft);
            Ui.Size(bar, minH: 60);
            Ui.IconBtn(bar, "back", () => { Sound.Sfx("click"); App.I.Go(new HomePage()); }, Loc.T("common.back"), 56);
            var tt = Ui.Label(bar, Loc.T("lobby.title"), 34, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false); Ui.Size(tt.rectTransform, flexW: 1, minW: 0);
            statusBox = Ui.HBox(bar, 6, 0, TextAnchor.MiddleRight);
            var rs = Ui.Obj("Reserve", bar); Ui.Size(rs, minW: 130, prefW: 130);

            if (Cfg.Status != "ok")
            {
                var card = Lay.Section(col, Loc.T("lobby.noserver"));
                Lay.Para(card, Cfg.Status == "invalid" ? Loc.T(Cfg.Error) : Loc.T("lobby.noserver.p"), true);
                Ui.Btn(card, Loc.T("lobby.solo"), () => App.I.Go(new SoloPage()), "pink", 0, 56, 24);
            }
            noticeBox = Ui.VBox(col, 8, 0);
            inviteBox = Ui.VBox(col, 8, 0);
            RectTransform a, b;
            Lay.Cols(col, out a, out b);
            var me = Lay.Section(a, Loc.T("lobby.me")); profileBox = Ui.VBox(me, 8, 0);
            var st = Lay.Section(a, Loc.T("lobby.start")); actionBox = Ui.VBox(st, 8, 0);
            var rooms = Lay.Section(b, Loc.T("lobby.rooms") + "  " + Loc.T("lobby.joinable")); listBox = Ui.VBox(rooms, 8, 0);
            PaintStatus(); PaintNotice(); PaintProfile(); PaintList(); PaintInvite();
            Net.OpenConn(App.I.MyName(), Store.D.animal);
            on.AskInvite();
        }

        public override void Close() { if (App.I.Online.Lobby == this) App.I.Online.Lobby = null; }

        public void PaintStatus()
        {
            Fill(statusBox, box => { string style, text; StatusInfo(out style, out text); Ui.Pill(box, text, style, 18); });
            PaintActions();
        }

        public void PaintNotice()
        {
            Fill(noticeBox, box =>
            {
                var n = App.I.Online.LobbyNotice;
                if (string.IsNullOrEmpty(n)) return;
                var card = Ui.Card(box, 8, 14, Data.Hex("#fff0b8"));
                Ui.Label(card, n, 22, Ui.Ink);
                Ui.Btn(card, Loc.T("lobby.gotit"), () => { App.I.Online.LobbyNotice = ""; PaintNotice(); }, "primary", 140, 48, 22);
            });
        }

        void PaintProfile()
        {
            Fill(profileBox, box =>
            {
                var row = Ui.HBox(box, 12, 0, TextAnchor.MiddleLeft);
                Ui.AnimalFace(row, Store.D.animal, 52);
                var v = Ui.VBox(row, 0, 0); Ui.Size(v, flexW: 1, minW: 0);
                Ui.Label(v, App.I.MyName(), 26, Ui.Ink, FontStyle.Bold, TextAnchor.UpperLeft, false);
                Ui.Label(v, Loc.T("lobby.mine"), 18, Ui.Muted, FontStyle.Normal, TextAnchor.UpperLeft, false);
                Ui.Btn(row, Loc.T("lobby.edit"), EditProfile, "sky", 90, 48, 20);
            });
        }

        void EditProfile()
        {
            var m = App.I.Modal(Loc.T("prof.title"), true, 600);
            Lay.ProfileEditor(m.Body, null);
            Ui.Btn(m.Body, Loc.T("common.done"), () => m.Close(), "primary", 0, 56, 24);
            m.OnClose = () => { App.I.Online.SendProfile(); if (App.I.Current == this) PaintProfile(); };
        }

        void PaintActions()
        {
            Fill(actionBox, box =>
            {
                bool ok = Net.Connected;
                var row = Ui.Wrap(box, 10);
                var q = Ui.Btn(row, Loc.T("lobby.quick"), () => Net.Send(Net.Msg("quick", "name", App.I.MyName(), "animal", Store.D.animal)), "pink", 200, 64, 24);
                var c = Ui.Btn(row, Loc.T("lobby.create"), CreateRoom, "orange", 200, 64, 24);
                q.interactable = ok; c.interactable = ok;
            });
        }

        void CreateRoom()
        {
            int max = 4; string roomName = "";
            var m = App.I.Modal(Loc.T("mk.title"), true, 520);
            var f1 = Ui.Field(m.Body, Loc.T("mk.name"));
            Ui.TextInput(f1, "", Loc.T("mk.placeholder", "name", App.I.MyName()), 20, v => roomName = v);
            var f2 = Ui.Field(m.Body, Loc.T("mk.max"));
            Ui.Stepper(f2, max, 2, 8, v => Loc.T("mk.n", "n", v), v => max = v);
            Ui.Label(m.Body, Loc.T("mk.hint"), 20, Ui.Muted);
            var row = Ui.Wrap(m.Body, 10);
            Ui.Btn(row, Loc.T("common.cancel"), () => m.Close(), "plain", 130, 56, 22);
            Ui.Btn(row, Loc.T("mk.ok"), () =>
            {
                m.Close();
                Net.Send(Net.Msg("create", "roomName", (roomName ?? "").Trim(), "max", max, "name", App.I.MyName(), "animal", Store.D.animal));
            }, "pink", 150, 56, 22);
        }

        public void PaintList()
        {
            Fill(listBox, box =>
            {
                var on = App.I.Online;
                if (!Net.Connected) { Lay.Para(box, Cfg.Status == "ok" ? Loc.T("lobby.after") : Loc.T("lobby.offline"), true); return; }
                if (on.Rooms.Count == 0) { Lay.Para(box, Loc.T("lobby.none"), true); return; }
                foreach (var r in on.Rooms)
                {
                    var card = Ui.Card(box, 6, 10, Ui.Paper, 14);
                    var title = Ui.Label(card, Online.RoomName(Json.Str(r, "name")), 26, Ui.Ink, FontStyle.Bold);
                    var chips = Ui.Wrap(card, 6);
                    var sets = Json.Get(r, "settings");
                    string phase = Json.Str(r, "phase");
                    Ui.Pill(chips, Loc.T("room.code", "id", Json.Str(r, "id")), "off", 17);
                    Ui.Pill(chips, Loc.T(phase == "lobby" ? "phase.lobby" : phase == "playing" ? "phase.playing" : "phase.finished"), phase == "lobby" ? "primary" : "pink", 17);
                    Ui.Pill(chips, Loc.T("room.count", "p", Json.Int(r, "players"), "m", Json.Int(r, "max")), "plain", 17);
                    if (Json.Int(r, "spectators") > 0) Ui.Pill(chips, Loc.T("room.spec", "n", Json.Int(r, "spectators")), "off", 17);
                    Ui.Pill(chips, Loc.T("room.host", "name", Json.Str(r, "host")), "off", 17);
                    string sp = Json.Str(sets, "speed", "normal"), ob = Json.Str(sets, "obstacles", "few");
                    Ui.Pill(chips, Loc.T("room.speed", "v", Names.Speed(sp)), "off", 17);
                    Ui.Pill(chips, Loc.T("room.rocks", "v", Names.Obstacle(ob)), "off", 17);
                    var btns = Ui.Wrap(card, 8);
                    string id = Json.Str(r, "id");
                    if (Json.Bool(r, "joinable")) Ui.Btn(btns, Loc.T("room.join"), () => Net.Send(Net.Msg("join", "room", id, "as", "player", "name", App.I.MyName(), "animal", Store.D.animal)), "primary", 110, 48, 20);
                    Ui.Btn(btns, Loc.T("room.watch"), () => Net.Send(Net.Msg("join", "room", id, "as", "spectator", "name", App.I.MyName(), "animal", Store.D.animal)), "sky", 110, 48, 20);
                }
            });
        }

        public void PaintInvite()
        {
            Fill(inviteBox, box =>
            {
                var inv = App.I.Online.Invite;
                if (inv == null) return;
                var card = Ui.Card(box, 8, 14, Data.Hex("#fff0b8"));
                UnityAction drop = () => { App.I.Online.Invite = null; Platform.ReplaceUrl("?"); PaintInvite(); };
                if (inv.Error != null)
                {
                    string t = Online.ReasonText(inv.Error);
                    Ui.Label(card, Loc.T("inv.bad"), 28, Ui.Ink, FontStyle.Bold);
                    Lay.Para(card, t.Length > 0 ? t : Loc.T("inv.badFallback"));
                    Ui.Btn(card, Loc.T("inv.back"), drop, "primary", 0, 52, 22);
                }
                else if (inv.Info == null)
                {
                    Ui.Label(card, Loc.T("inv.checking"), 28, Ui.Ink, FontStyle.Bold);
                    Lay.Para(card, Net.Connected ? Loc.T("inv.checkingWait") : Loc.T("inv.waiting"), true);
                    Ui.Btn(card, Loc.T("inv.decline"), drop, "plain", 0, 48, 22);
                }
                else
                {
                    var i = inv.Info;
                    bool spec = Json.Bool(i, "willSpectate");
                    string why = "";
                    if (spec && Json.Str(i, "role") != "spectator") why = Json.Str(i, "phase") != "lobby" ? Loc.T("inv.started") : Loc.T("inv.full");
                    Ui.Label(card, Loc.T("inv.title", "name", Online.RoomName(Json.Str(i, "name"))), 28, Ui.Ink, FontStyle.Bold);
                    var chips = Ui.Wrap(card, 6);
                    Ui.Pill(chips, Loc.T("room.code", "id", Json.Str(i, "room")), "off", 17);
                    Ui.Pill(chips, Loc.T("room.count", "p", Json.Int(i, "players"), "m", Json.Int(i, "max")), "plain", 17);
                    Ui.Pill(chips, spec ? Loc.T("inv.asSpec", "why", why) : Loc.T("inv.asPlayer"), spec ? "sun" : "primary", 17);
                    Lay.Para(card, Loc.T("inv.confirm") + (spec ? Loc.T("inv.specNote") : ""), true, 20);
                    // 邀請連結不會自動用儲存的暱稱進房：先讓對方確認或修改暱稱
                    Lay.ProfileEditor(card, null);
                    var row = Ui.Wrap(card, 10);
                    Ui.Btn(row, Loc.T("inv.decline"), drop, "plain", 130, 56, 22);
                    var go = Ui.Btn(row, spec ? Loc.T("inv.joinSpec") : Loc.T("inv.joinPlay"), () =>
                    {
                        inv.Joining = true;
                        Net.Send(Net.Msg("join", "room", Json.Str(i, "room"), "token", inv.Token, "name", App.I.MyName(), "animal", Store.D.animal));
                        PaintInvite();
                    }, "pink", 180, 56, 22);
                    go.interactable = Net.Connected && !inv.Joining;
                }
            });
        }
    }

    // =====================================================================
    //                              房間
    // =====================================================================
    public sealed class RoomPage : Page
    {
        Text titleT; RectTransform codeBox, seatsBox, specBox, actBox, setBox; ChatBox chat;
        Action<ChatMsg> push; Action reset;

        public override bool Back() { App.I.Online.ConfirmLeave(); return true; }

        static void Fill(RectTransform box, Action<RectTransform> build)
        {
            if (box == null) return;
            for (int i = box.childCount - 1; i >= 0; i--) { var c = box.GetChild(i).gameObject; c.SetActive(false); UnityEngine.Object.Destroy(c); }
            build(box);
        }

        public override void Build(RectTransform root)
        {
            var on = App.I.Online;
            on.RoomUi = this; on.InGame = false; on.Game = null;
            var p = Lay.Page(root, 1000);
            var col = p.Col;
            var bar = Ui.HBox(col, 10, 0, TextAnchor.MiddleLeft);
            Ui.Size(bar, minH: 60);
            Ui.IconBtn(bar, "back", () => { Sound.Sfx("click"); on.ConfirmLeave(); }, Loc.T("room.leave"), 56);
            titleT = Ui.Label(bar, Loc.T("room.title"), 34, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false); Ui.Size(titleT.rectTransform, flexW: 1, minW: 0);
            codeBox = Ui.HBox(bar, 6, 0, TextAnchor.MiddleRight);
            var rs = Ui.Obj("Reserve", bar); Ui.Size(rs, minW: 130, prefW: 130);

            RectTransform a, b;
            Lay.Cols(col, out a, out b);
            var s1 = Lay.Section(a, Loc.T("room.seats")); seatsBox = Ui.VBox(s1, 8, 0);
            specBox = Ui.VBox(a, 8, 0);
            var s3 = Lay.Section(a, Loc.T("room.chat"));
            chat = new ChatBox(s3, 220, t => Net.Send(Net.Msg("chat", "text", t)));
            chat.Set(on.ChatLog);
            push = m => { if (chat != null) chat.Push(m); };
            reset = () => { if (chat != null) chat.Set(on.ChatLog); };
            on.ChatPushed += push; on.ChatReset += reset;
            var s4 = Lay.Section(b, ""); actBox = Ui.VBox(s4, 10, 0);
            var s5 = Lay.Section(b, Loc.T("room.rules")); setBox = Ui.VBox(s5, 10, 0);
            Paint();
            Net.OpenConn(App.I.MyName(), Store.D.animal);
        }

        public override void Close()
        {
            var on = App.I.Online;
            if (push != null) on.ChatPushed -= push;
            if (reset != null) on.ChatReset -= reset;
            if (on.RoomUi == this) on.RoomUi = null;
            if (on.InviteModal != null && on.InviteModal.Mask != null) on.InviteModal.Close();
        }

        public void Paint()
        {
            var r = App.I.Online.Room;
            if (r == null)
            {
                titleT.text = Loc.T("room.entering");
                Fill(seatsBox, b => Lay.Para(b, Loc.T("room.syncing"), true));
                Fill(actBox, b => { }); Fill(setBox, b => { }); Fill(specBox, b => { }); Fill(codeBox, b => { });
                return;
            }
            string phase = Json.Str(r, "phase");
            titleT.text = Online.RoomName(Json.Str(r, "name"));
            Fill(codeBox, b =>
            {
                Ui.Pill(b, Loc.T("room.code", "id", Json.Str(r, "id")), "off", 17);
                Ui.Pill(b, Loc.T(phase == "lobby" ? "phase.lobby" : phase == "playing" ? "phase.playing" : "phase.finished"), phase == "lobby" ? "primary" : "pink", 17);
            });
            Fill(seatsBox, b => PaintSeats(b, r));
            Fill(specBox, b => PaintSpectators(b, r));
            Fill(actBox, b => PaintActions(b, r));
            Fill(setBox, b => PaintSettings(b, r));
        }

        void PaintSeats(RectTransform box, object r)
        {
            var you = Json.Get(r, "you"); bool host = Json.Bool(you, "host"); bool lobby = Json.Str(r, "phase") == "lobby";
            int mySeat = Json.Int(you, "seat", -1), hostSeat = Json.Int(r, "hostSeat", -1);
            var seats = Json.Arr(r, "seats") ?? new List<object>();
            foreach (var s in seats)
            {
                string kind = Json.Str(s, "kind"); int idx = Json.Int(s, "i");
                if (kind == "empty")
                {
                    var row = Ui.Card(box, 4, 10, Data.Hex("#fff3d6"), 14);
                    var hr = Ui.HBox(row, 8, 0, TextAnchor.MiddleLeft);
                    var l = Ui.Label(hr, Loc.T("seat.empty"), 24, Ui.Muted, FontStyle.Bold, TextAnchor.MiddleLeft, false); Ui.Size(l.rectTransform, flexW: 1, minW: 0);
                    if (host && lobby) Ui.Btn(hr, Loc.T("seat.addAi"), () => Net.Send(Net.Msg("addai", "level", App.I.Online.AiAddLevel)), "sky", 150, 48, 20);
                    continue;
                }
                bool ai = kind == "ai", mine = idx == mySeat;
                string animal = Json.Str(s, "animal", "cat");
                string nm = Names.P(kind, animal, Json.Str(s, "name"));
                var card = Ui.Card(box, 4, 10, mine ? Data.Hex("#fff0b8") : Ui.Paper, 14);
                var line = Ui.HBox(card, 8, 0, TextAnchor.MiddleLeft);
                Ui.AnimalFace(line, animal, 38);
                var nt = Ui.Label(line, nm, 24, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false); Ui.Size(nt.rectTransform, flexW: 1, minW: 0);
                if (ai)
                {
                    string lv = Json.Str(s, "level", "normal");
                    if (host && lobby)
                    {
                        var lvs = Ai.LEVEL_ORDER; var names = new string[lvs.Length];
                        for (int i = 0; i < lvs.Length; i++) names[i] = Names.Level(lvs[i]);
                        Ui.Seg(card, names, Array.IndexOf(lvs, lv), k => { App.I.Online.AiAddLevel = lvs[k]; Net.Send(Net.Msg("aiLevel", "seat", idx, "level", lvs[k])); }, 70);
                        Ui.Btn(card, Loc.T("seat.remove", "name", nm), () => Net.Send(Net.Msg("removeai", "seat", idx)), "plain", 0, 44, 20);
                    }
                    else Ui.Pill(line, Names.Level(lv), "off", 17);
                    continue;
                }
                var tags = Ui.Wrap(card, 6);
                if (idx == hostSeat) Ui.Pill(tags, Loc.T("seat.host"), "sun", 17);
                if (mine) Ui.Pill(tags, Loc.T("seat.you"), "plain", 17);
                bool ready = Json.Bool(s, "ready");
                if (idx != hostSeat && lobby) Ui.Pill(tags, ready ? Loc.T("seat.ready") : Loc.T("seat.notReady"), ready ? "primary" : "off", 17);
                if (!Json.Bool(s, "connected", true)) Ui.Pill(tags, Loc.T("seat.off"), "pink", 17);
                if (host && !mine && lobby)
                {
                    string who = nm;
                    Ui.Btn(tags, Loc.T("seat.kickOk"), () => App.I.ConfirmBox(Loc.T("seat.kickTitle", "name", who), Loc.T("seat.kickText"), Loc.T("seat.kickOk"), () => Net.Send(Net.Msg("kick", "seat", idx)), true), "danger", 0, 40, 18);
                }
            }
        }

        void PaintSpectators(RectTransform box, object r)
        {
            var list = Json.Arr(r, "spectators") ?? new List<object>();
            var card = Lay.Section(box, Loc.T("spec.title") + "  " + list.Count + "/" + Json.Int(r, "specCap"));
            if (list.Count == 0) { Lay.Para(card, Loc.T("spec.none"), true, 20); return; }
            var w = Ui.Wrap(card, 6);
            int mypid = Json.Int(Json.Get(r, "you"), "pid", -999);
            foreach (var s in list) Ui.Pill(w, Json.Str(s, "name") + (Json.Int(s, "pid", -1) == mypid ? Loc.T("res.youTag") : ""), Json.Int(s, "pid", -1) == mypid ? "plain" : "off", 18);
        }

        void PaintActions(RectTransform box, object r)
        {
            var you = Json.Get(r, "you"); string role = Json.Str(you, "role"); bool host = Json.Bool(you, "host");
            string phase = Json.Str(r, "phase"); bool lobby = phase == "lobby";
            var seats = Json.Arr(r, "seats") ?? new List<object>();
            int filled = 0; foreach (var s in seats) if (Json.Str(s, "kind") != "empty") filled++;
            bool free = filled < Json.Int(r, "max");
            Ui.Label(box, role == "player" ? (host ? Loc.T("you.host") : Loc.T("you.player")) : Loc.T("you.spec"), 28, Ui.Ink, FontStyle.Bold);
            if (phase == "playing") Lay.Para(box, Loc.T("room.playingNow"), true);
            else if (phase == "finished")
            {
                Lay.Para(box, host ? Loc.T("room.endedHost") : Loc.T("room.endedGuest"), true);
                if (host) Ui.Btn(box, Loc.T("res.again"), () => Net.Send(Net.Msg("rematch")), "pink", 0, 64, 26);
            }
            else if (role == "player" && host)
            {
                bool can = Json.Bool(r, "canStart");
                string why = filled < 2 ? Loc.T("room.fewPlayers") : (!can ? Loc.T("room.waitReady") : "");
                var b = Ui.Btn(box, Loc.T("room.startBtn"), () => Net.Send(Net.Msg("start")), "pink", 0, 64, 26); b.interactable = can;
                if (why.Length > 0) Lay.Para(box, why, true, 20);
            }
            else if (role == "player")
            {
                int seat = Json.Int(you, "seat", -1);
                bool ready = seat >= 0 && seat < seats.Count && Json.Bool(seats[seat], "ready");
                Ui.Btn(box, ready ? Loc.T("room.unready") : Loc.T("room.imReady"), () => Net.Send(Net.Msg("ready", "value", !ready)), ready ? "plain" : "primary", 0, 64, 26);
                Ui.Btn(box, Loc.T("room.toSpec"), () => Net.Send(Net.Msg("watch")), "plain", 0, 52, 22);
            }
            else
            {
                var b = Ui.Btn(box, free ? Loc.T("room.joinPlay") : Loc.T("room.seatsFull"), () => Net.Send(Net.Msg("sit")), "primary", 0, 64, 26);
                b.interactable = free && lobby;
                if (!lobby) Lay.Para(box, Loc.T("room.noSwitch"), true, 20);
            }
            if (host && phase != "playing") Ui.Btn(box, Loc.T("room.invite"), OpenInviteModal, "sky", 0, 52, 22);
            Ui.Btn(box, Loc.T("room.leave"), () => App.I.Online.ConfirmLeave(), "plain", 0, 52, 22);
        }

        void PaintSettings(RectTransform box, object r)
        {
            var st = Json.Get(r, "settings"); var you = Json.Get(r, "you");
            bool host = Json.Bool(you, "host") && Json.Str(r, "phase") == "lobby";
            string speed = Json.Str(st, "speed", "normal"), obs = Json.Str(st, "obstacles", "few"); int tl = Json.Int(st, "timeLimit"), th = Json.Int(st, "theme", -1);
            int max = Json.Int(r, "max");
            var seats = Json.Arr(r, "seats") ?? new List<object>();
            int filled = 0; foreach (var s in seats) if (Json.Str(s, "kind") != "empty") filled++;
            if (host)
            {
                var f = Ui.Field(box, Loc.T("room.max"));
                Ui.Stepper(f, max, Math.Max(2, filled), 8, v => Loc.T("mk.n", "n", v), v => Net.Send(Net.Msg("settings", "patch", new Dictionary<string, object> { { "max", v } })));
                Lay.RulesPanel(box, speed, obs, tl, th, false, (k, v) => Net.Send(Net.Msg("settings", "patch", new Dictionary<string, object> { { k, v } })));
            }
            else
            {
                var w = Ui.Wrap(box, 6);
                var sum = new List<string> { Loc.T("mk.n", "n", max) };
                sum.AddRange(Lay.RulesSummary(speed, obs, tl, th));
                foreach (var t in sum) Ui.Pill(w, t, "off", 18);
                if (!Json.Bool(you, "host")) Lay.Para(box, Loc.T("room.hostOnly"), true, 20);
            }
        }

        // ---------- 邀請連結（房主） ----------
        void OpenInviteModal()
        {
            var on = App.I.Online;
            if (on.InviteModal != null && on.InviteModal.Mask != null) return;
            string role = "player";
            var m = App.I.Modal(Loc.T("invm.title"), true, 680);
            on.InviteModal = m;
            Lay.Para(m.Body, Loc.T("invm.p"), true, 20);
            var f = Ui.Field(m.Body, Loc.T("invm.role"));
            Ui.Seg(f, new[] { Loc.T("invm.rolePlayer"), Loc.T("invm.roleSpec") }, 0, i => role = i == 0 ? "player" : "spectator");
            Ui.Btn(m.Body, Loc.T("invm.make"), () => Net.Send(Net.Msg("invite", "role", role)), "pink", 0, 52, 22);
            var list = Ui.VBox(m.Body, 8, 0);
            Action paint = () =>
            {
                Fill(list, box =>
                {
                    var invs = Json.Arr(on.Room, "invites");
                    if (invs == null || invs.Count == 0) { Lay.Para(box, Loc.T("invm.none"), true, 20); return; }
                    foreach (var iv in invs)
                    {
                        string url = on.InviteLink(Json.Str(iv, "token"));
                        var card = Ui.Card(box, 6, 10, Ui.Paper, 12);
                        Ui.Label(card, Json.Str(iv, "role") == "spectator" ? Loc.T("invm.spec") : Loc.T("invm.player"), 22, Ui.Ink, FontStyle.Bold);
                        Ui.Label(card, url, 18, Ui.Muted);
                        var row = Ui.Wrap(card, 8);
                        Ui.Btn(row, Loc.T("invm.copy"), () => App.I.Toast(Platform.Copy(url) ? Loc.T("invm.copied") : Loc.T("invm.copyFail")), "sky", 110, 44, 20);
                        if (Platform.CanShare) Ui.Btn(row, Loc.T("invm.share"), () => Platform.Share(Loc.T("app.title"), Loc.T("invm.shareText"), url), "primary", 110, 44, 20);
                    }
                });
            };
            on.InviteRefresh = paint; paint();
            var act = Ui.Wrap(m.Body, 10);
            Ui.Btn(act, Loc.T("invm.revokeAll"), () => App.I.ConfirmBox(Loc.T("invm.revokeTitle"), Loc.T("invm.revokeText"), Loc.T("invm.revokeOk"), () => Net.Send(Net.Msg("revoke")), true), "plain", 160, 52, 20);
            Ui.Btn(act, Loc.T("common.done"), () => m.Close(), "primary", 130, 52, 22);
        }
    }
}
