using System;
using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using Xunit;

namespace AutoVerk.Tests
{
    /// <summary>A primitive that ends with a scripted result after some ticks, and counts its calls.</summary>
    internal sealed class Fake : Primitive
    {
        private readonly string _name;
        private readonly Result _result;
        private readonly int _ticks;
        private int _ticked;
        public int Begun, Ended;

        public Fake(string name, Result result = null, int ticks = 1)
        {
            _name = name;
            _result = result ?? Result.Success;
            _ticks = ticks;
        }

        protected override Result Begin()
        {
            Begun++;
            _ticked = 0;
            return Result.Running;
        }

        protected override Result Tick() => ++_ticked >= _ticks ? _result : Result.Running;
        protected override void End() => Ended++;
        public override string ToString() => _name;
    }

    internal sealed class Script : Composite
    {
        private readonly Func<Script, IEnumerable<Step>> _run;
        public Script(Func<Script, IEnumerable<Step>> run) { _run = run; }
        protected override IEnumerable<Step> Run() => _run(this);
        public Result LastResult => Last;
        public static Step DoStep(Skill s) => Do(s);
        public static Step TryStep(Skill s) => Try(s);
        public static Step FailStep(string code) => Fail(code, "because");
        public static Step PauseStep(string reason) => Pause(reason);
        public static Step WaitStep(double s) => Wait(s);
        public void Fact(string label, string value) => SetFact(label, value);
    }

    internal sealed class Guard : IGuard
    {
        public GuardHit Hit;
        public GuardHit Check() => Hit;
    }

    internal sealed class Trigger : Reflex
    {
        public Func<Skill> Make;
        public override Skill Check()
        {
            var s = Make?.Invoke();
            Make = null;
            return s;
        }
    }

    public class EngineTests
    {
        private double _now;
        private readonly List<string> _log = new List<string>();
        private readonly List<Reflex> _reflexes = new List<Reflex>();

        private Runner NewRunner() => new Runner(() => _now, _log.Add, () => _reflexes);

        private static void RunToEnd(Runner runner, int maxFrames = 1000)
        {
            for (int i = 0; i < maxFrames && runner.State == RunState.Running; i++) runner.Tick();
        }

        [Fact]
        public void DoRunsStepsInOrderAndEndsOk()
        {
            var a = new Fake("a", ticks: 3);
            var b = new Fake("b");
            var runner = NewRunner();
            runner.Start(new Script(_ => new[] { Script.DoStep(a), Script.DoStep(b) }));
            RunToEnd(runner);
            Assert.True(runner.Outcome.Ok);
            Assert.Equal(1, a.Ended);
            Assert.Equal(1, b.Ended);
            Assert.Equal(new[] { "script", "  a", "  a → ok", "  b", "  b → ok", "script → ok", "finished: ok" }, _log);
        }

        [Fact]
        public void DoFailureEndsTheParentWithTheSameResult()
        {
            var after = new Fake("after");
            var runner = NewRunner();
            runner.Start(new Script(_ => new[] { Script.DoStep(new Fake("a", Result.Fail("stuck", "no closer"))), Script.DoStep(after) }));
            RunToEnd(runner);
            Assert.Equal("stuck", runner.Outcome.Code);
            Assert.Equal("no closer", runner.Outcome.Detail);
            Assert.Equal(0, after.Begun);
        }

        [Fact]
        public void TryCarriesOnAndKeepsTheResultInLast()
        {
            string seen = null;
            IEnumerable<Step> Body(Script s)
            {
                yield return Script.TryStep(new Fake("a", Result.Fail("gone")));
                seen = s.LastResult.Code;
            }
            var runner = NewRunner();
            runner.Start(new Script(Body));
            RunToEnd(runner);
            Assert.True(runner.Outcome.Ok);
            Assert.Equal("gone", seen);
        }

        [Fact]
        public void FailEndsTheSkillAtOnce()
        {
            var after = new Fake("after");
            var runner = NewRunner();
            runner.Start(new Script(_ => new[] { Script.FailStep("not_found"), Script.DoStep(after) }));
            RunToEnd(runner);
            Assert.Equal("not_found", runner.Outcome.Code);
            Assert.Equal(0, after.Begun);
        }

        [Fact]
        public void PauseInterruptsThePrimitiveAndResumeStartsItAgain()
        {
            var a = new Fake("a", ticks: 10);
            var runner = NewRunner();
            runner.Start(new Script(_ => new[] { Script.DoStep(a) }));
            for (int i = 0; i < 4; i++) runner.Tick();
            runner.Pause("you pressed W");
            Assert.Equal(RunState.Paused, runner.State);
            Assert.Equal(1, a.Ended);
            runner.Tick();
            Assert.Equal(1, a.Begun);

            runner.Resume();
            RunToEnd(runner);
            Assert.True(runner.Outcome.Ok);
            Assert.Equal(2, a.Begun);
            Assert.Equal(2, a.Ended);
        }

        [Fact]
        public void GuardsPauseOrStop()
        {
            var guard = new Guard();
            var runner = NewRunner();
            runner.Guards.Add(guard);
            runner.Start(new Script(_ => new[] { Script.DoStep(new Fake("a", ticks: 100)) }));
            runner.Tick();
            runner.Tick();

            guard.Hit = GuardHit.Pause("inventory open");
            runner.Tick();
            Assert.Equal("inventory open", runner.PauseReason);

            runner.Resume();
            guard.Hit = GuardHit.End("died", "the character died");
            runner.Tick();
            Assert.Equal(RunState.Finished, runner.State);
            Assert.Equal("died", runner.Outcome.Code);
        }

        [Fact]
        public void AReflexRunsOnTopAndTheInterruptedPrimitiveStartsAgain()
        {
            var work = new Fake("work", ticks: 5);
            var fight = new Fake("fight", ticks: 3);
            var trigger = new Trigger();
            _reflexes.Add(trigger);
            var runner = NewRunner();
            runner.Start(new Script(_ => new[] { Script.DoStep(work) }));
            runner.Tick();
            runner.Tick();

            trigger.Make = () => fight;
            runner.Tick();
            Assert.Contains("fight", runner.Path);
            RunToEnd(runner);

            Assert.True(runner.Outcome.Ok);
            Assert.Equal(1, fight.Ended);
            Assert.Equal(2, work.Begun);
        }

        [Fact]
        public void APauseStepPausesTheRunAndCarriesOnAfterResume()
        {
            var after = new Fake("after");
            var runner = NewRunner();
            runner.Start(new Script(_ => new[] { Script.PauseStep("no stamina to fight"), Script.DoStep(after) }));
            runner.Tick();
            Assert.Equal("no stamina to fight", runner.PauseReason);
            runner.Resume();
            RunToEnd(runner);
            Assert.True(runner.Outcome.Ok);
            Assert.Equal(1, after.Ended);
        }

        [Fact]
        public void WaitUsesTheClock()
        {
            var runner = NewRunner();
            runner.Start(new Script(_ => new[] { Script.WaitStep(2) }));
            for (int i = 0; i < 10; i++) runner.Tick();
            Assert.Equal(RunState.Running, runner.State);
            _now = 2.5;
            RunToEnd(runner);
            Assert.True(runner.Outcome.Ok);
        }

        [Fact]
        public void StopCancelsTheWorkingPrimitive()
        {
            var a = new Fake("a", ticks: 100);
            var runner = NewRunner();
            runner.Start(new Script(_ => new[] { Script.DoStep(a) }));
            runner.Tick();
            runner.Tick();
            Assert.Equal("script › a", runner.Path);
            runner.Stop("Stop pressed");
            Assert.Equal("stopped", runner.Outcome.Code);
            Assert.Equal(1, a.Ended);
            Assert.Equal("", runner.Path);
        }

        [Fact]
        public void ALoopOfInstantFailuresTakesOneStepAFrame()
        {
            int tries = 0;
            IEnumerable<Step> Body(Script s)
            {
                while (true)
                {
                    tries++;
                    yield return Script.TryStep(new Fake("a", Result.Fail("gone")));
                }
            }
            var runner = NewRunner();
            runner.Start(new Script(Body));
            for (int i = 0; i < 10; i++) runner.Tick();
            Assert.InRange(tries, 5, 11);
        }
    }

    // The registry is static: tests that change it run one at a time.
    [Collection("registry")]
    public class RegistryTests
    {
        private static SkillInfo Info() => new SkillInfo
        {
            Id = "test.gather",
            Description = "test",
            Params = { Param.Item("crop", "the crop"), Param.Number("radius", "metres", 50), Param.Bool("cleanup", "stones too") },
            Create = args => new Wait(args.Number("radius")),
        };

        [Fact]
        public void ParsesALineWithQuotes()
        {
            var id = Args.ParseLine("test.gather crop=\"Fine wood\" radius=40", out var values, out var error);
            Assert.Null(error);
            Assert.Equal("test.gather", id);
            Assert.Equal("Fine wood", values["crop"]);
            Assert.Equal("40", values["radius"]);
        }

        [Fact]
        public void FillsDefaultsAndChecksValues()
        {
            var args = Args.Check(Info(), new Dictionary<string, string> { ["crop"] = "Carrot" }, out var error);
            Assert.Null(error);
            Assert.Equal(50f, args.Number("radius"));
            Assert.False(args.Bool("cleanup"));

            Assert.Null(Args.Check(Info(), new Dictionary<string, string>(), out error));
            Assert.StartsWith("crop is required", error);
            Assert.Null(Args.Check(Info(), new Dictionary<string, string> { ["crop"] = "Carrot", ["radius"] = "far" }, out error));
            Assert.Equal("radius must be a number, not 'far'", error);
            Assert.Null(Args.Check(Info(), new Dictionary<string, string> { ["crop"] = "Carrot", ["size"] = "3" }, out error));
            Assert.StartsWith("unknown parameter size", error);
        }

        [Fact]
        public void FactsKeepTheirPlaceAsTheyChange()
        {
            var script = new Script(_ => new Step[0]);
            script.Fact("Spot", "1 of 20");
            script.Fact("Planted", "0");
            script.Fact("Spot", "2 of 20");
            Assert.Equal(new[] { "Spot=2 of 20", "Planted=0" }, script.Facts.Select(f => $"{f.Key}={f.Value}"));
            script.Fact("Spot", null);
            Assert.Equal(new[] { "Planted=0" }, script.Facts.Select(f => $"{f.Key}={f.Value}"));
        }

        [Fact]
        public void CreatesASkillFromALine()
        {
            Registry.Register(Info());
            try
            {
                var skill = Registry.FromLine("test.gather crop=Carrot radius=3", out var error);
                Assert.Null(error);
                Assert.Equal("wait 3 s", skill.ToString());
                Assert.Null(Registry.FromLine("test.nothing", out error));
                Assert.StartsWith("no skill 'test.nothing'", error);
            }
            finally
            {
                Registry.Unregister("test.gather");
            }
        }
    }
}
