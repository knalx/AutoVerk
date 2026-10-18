using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoVerk.Engine
{
    public enum RunState { Idle, Running, Paused, Finished }

    /// <summary>A guard fired: pause the run, or stop it with a code (died, left_world).</summary>
    public sealed class GuardHit
    {
        public bool Stop { get; private set; }
        public string Code { get; private set; }
        public string Reason { get; private set; }

        public static GuardHit Pause(string reason) => new GuardHit { Reason = reason };
        public static GuardHit End(string code, string reason) => new GuardHit { Stop = true, Code = code, Reason = reason };
    }

    /// <summary>A cheap check that runs every frame of a run. Returns null when all is well.</summary>
    public interface IGuard
    {
        GuardHit Check();
    }

    /// <summary>
    /// Runs one skill tree, a frame at a time: guards, then reflexes, then one step of the tree.
    /// A pause interrupts the working primitive; it starts again on resume.
    /// </summary>
    public sealed class Runner
    {
        private readonly Func<double> _clock;
        private readonly Action<string> _log;
        private readonly Func<IEnumerable<Reflex>> _reflexes;
        private RunContext _context;
        private Skill _reflexSkill;

        public Runner(Func<double> clock, Action<string> log, Func<IEnumerable<Reflex>> reflexes = null)
        {
            _clock = clock;
            _log = log ?? (_ => { });
            _reflexes = reflexes ?? (() => Registry.EnabledReflexes);
        }

        public List<IGuard> Guards { get; } = new List<IGuard>();

        public RunState State { get; private set; } = RunState.Idle;
        public Skill Root { get; private set; }
        public string PauseReason { get; private set; }
        /// <summary>How the last run ended, once Finished.</summary>
        public Result Outcome { get; private set; }

        /// <summary>The branch that's working now: "farm.gather › move_to Carrot".</summary>
        public string Path => string.Join(" › ", Branch().Select(s => s.ToString()));

        /// <summary>The deepest status text on the working branch.</summary>
        public string Status => Branch().Select(s => s.Status).LastOrDefault(s => s != null);

        public void Start(Skill root)
        {
            if (State == RunState.Running || State == RunState.Paused) Stop("a new run started");
            Root = root;
            Outcome = null;
            PauseReason = null;
            _reflexSkill = null;
            _context = new RunContext(_clock, (depth, text) => _log(new string(' ', depth * 2) + text));
            State = RunState.Running;
        }

        /// <summary>Main thread, once a frame.</summary>
        public void Tick()
        {
            if (State != RunState.Running) return;

            foreach (var guard in Guards)
            {
                var hit = guard.Check();
                if (hit == null) continue;
                if (hit.Stop) Finish(Result.Fail(hit.Code, hit.Reason));
                else Pause(hit.Reason);
                return;
            }

            if (_reflexSkill == null)
                foreach (var reflex in _reflexes())
                {
                    var skill = reflex.Check();
                    if (skill == null) continue;
                    Root.Interrupt();
                    _reflexSkill = skill;
                    break;
                }

            var active = _reflexSkill ?? Root;
            var result = active.Step(_context, _reflexSkill != null ? 1 : 0);

            if (_context.PauseRequest != null)
            {
                var reason = _context.PauseRequest;
                _context.PauseRequest = null;
                Pause(reason);
                return;
            }
            if (result.IsRunning) return;
            if (active == _reflexSkill) _reflexSkill = null;
            else Finish(result);
        }

        public void Pause(string reason)
        {
            if (State != RunState.Running) return;
            _reflexSkill?.Interrupt();
            Root.Interrupt();
            State = RunState.Paused;
            PauseReason = reason;
            _log($"paused: {reason}");
        }

        public void Resume()
        {
            if (State != RunState.Paused) return;
            State = RunState.Running;
            PauseReason = null;
            _log("resumed");
        }

        public void Stop(string reason)
        {
            if (State != RunState.Running && State != RunState.Paused) return;
            _reflexSkill?.Cancel();
            Root.Cancel();
            Finish(Result.Fail("stopped", reason));
        }

        private void Finish(Result outcome)
        {
            if (Root.Result == null) Root.Cancel();
            _reflexSkill?.Cancel();
            _reflexSkill = null;
            Outcome = outcome;
            State = RunState.Finished;
            _log($"finished: {outcome}");
        }

        private IEnumerable<Skill> Branch()
        {
            if (State != RunState.Running && State != RunState.Paused) yield break;
            for (var s = Root; s != null; s = s.Child) yield return s;
            for (var s = _reflexSkill; s != null; s = s.Child) yield return s;
        }
    }
}
