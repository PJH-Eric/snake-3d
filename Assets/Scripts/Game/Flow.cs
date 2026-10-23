using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snake3D
{
    /// <summary>開局流程（單機）：建立規則狀態、電腦大腦，進入對戰畫面。</summary>
    public static class Flow
    {
        static readonly System.Random seedRnd = new System.Random();
        static uint NewSeed() { var b = new byte[4]; seedRnd.NextBytes(b); return BitConverter.ToUInt32(b, 0); }

        public static void StartSolo()
        {
            var d = Store.D; var so = d.solo;
            var animals = Lay.PickAnimals(so.levels.Length, d.animal);
            var opt = new GameOptions { Seed = NewSeed(), Speed = so.speed, Obstacles = so.obstacles, TimeLimit = so.timeLimit, ThemeId = so.theme };
            opt.Players.Add(new PlayerInit { Slot = 0, Name = App.I.MyName(), Animal = d.animal, Kind = "human" });
            for (int i = 0; i < so.levels.Length; i++)
                opt.Players.Add(new PlayerInit { Slot = i + 1, Name = Names.Animal(animals[i]), Animal = animals[i], Kind = "ai", Level = so.levels[i] });
            var st = Rules.CreateGame(opt);
            var brains = new Dictionary<int, Brain>();
            foreach (var p in opt.Players)
                if (p.Kind == "ai") brains[p.Slot] = Ai.CreateBrain(p.Level, unchecked(opt.Seed + (uint)(p.Slot * 977)));
            App.I.LastRun = "solo";
            App.I.Go(new GamePage(GamePage.Kind.Solo, st, brains));
        }

        public static void StartChallenge()
        {
            var d = Store.D; var ch = d.challenge;
            var opt = new GameOptions { Seed = NewSeed(), Speed = ch.speed, Obstacles = ch.obstacles, ThemeId = ch.theme };
            opt.Players.Add(new PlayerInit { Slot = 0, Name = App.I.MyName(), Animal = d.animal, Kind = "human" });
            var st = Rules.CreateGame(opt);
            App.I.LastRun = "challenge";
            App.I.Go(new GamePage(GamePage.Kind.Challenge, st, new Dictionary<int, Brain>()));
        }

        public static void Restart() { if (App.I.LastRun == "challenge") StartChallenge(); else StartSolo(); }
        public static void BackToSetup() { if (App.I.LastRun == "challenge") App.I.Go(new ChallengePage()); else App.I.Go(new SoloPage()); }
    }
}
