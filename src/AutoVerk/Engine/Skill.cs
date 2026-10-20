using System;
using System.Collections.Generic;
using System.Text;

namespace AutoVerk.Engine
{
    /// <summary>
    /// One job that ends ok or failed. Created with its parameters and run once. Write a <see cref="Primitive"/>
    /// to work the game, or a <see cref="Composite"/> to run other skills.
    /// </summary>
    public abstract class Skill
    {
        private RunContext _run;
        private int _depth;
        private bool _started;
        private double _startedAt;

        /// <summary>What it's doing now, for the window: "walking to Carrot (4 m)".</summary>
        public string Status { get; protected set; }

        /// <summary>One line when it ends: "30 Carrot, 2 skipped".</summary>
        public string Report { get; protected set; }

        /// <summary>How it ended, or null while it hasn't.</summary>
        public Result Result { get; private set; }

        /// <summary>What it works on, for the window: "Carrot · field carrots-4".</summary>
        public string Subject { get; protected set; }

        /// <summary>Label and value rows for the window, kept up while it runs and once it's done: ("Planted", "4").</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Facts => _facts;

        /// <summary>Once it's done, the window's heading instead of "Done": "Stopped: out of carrot seeds".</summary>
        public string Headline { get; protected set; }

        /// <summary>Once it's done, what to do next, under the buttons: "Get more seeds first."</summary>
        public string Advice { get; protected set; }

        /// <summary>A console line that carries on the job later, and its button: "Fill the rest". Null for none.</summary>
        public string AgainLine { get; protected set; }
        public string AgainLabel { get; protected set; }

        /// <summary>The registered skill it was started as, when it was; for the window's words.</summary>
        internal SkillInfo Info { get; set; }

        private readonly List<KeyValuePair<string, string>> _facts = new List<KeyValuePair<string, string>>();

        /// <summary>Sets a fact, in place if it's there, else at the end; null removes it.</summary>
        protected void SetFact(string label, string value)
        {
            int i = _facts.FindIndex(f => f.Key == label);
            if (value == null)
            {
                if (i >= 0) _facts.RemoveAt(i);
            }
            else if (i >= 0) _facts[i] = new KeyValuePair<string, string>(label, value);
            else _facts.Add(new KeyValuePair<string, string>(label, value));
        }

        protected void ClearFacts() => _facts.Clear();

        /// <summary>Seconds since it started (or restarted after a pause).</summary>
        protected double Elapsed => _run == null ? 0 : _run.Now - _startedAt;

        /// <summary>For the run log: "move_to Carrot (12 m)". Defaults to the class name in snake_case.</summary>
        public override string ToString() => SnakeCase(GetType().Name);

        /// <summary>The child skill running now, for the window's path.</summary>
        internal virtual Skill Child => null;

        internal RunContext CurrentRun => _run;
        internal int Depth => _depth;

        /// <summary>Called once a frame by the parent or the Runner. Returns the result, Running until it ends.</summary>
        internal Result Step(RunContext run, int depth)
        {
            if (Result != null) return Result;
            if (!_started)
            {
                _run = run;
                _depth = depth;
                _started = true;
                _startedAt = run.Now;
                run.Log(depth, ToString());
                var begun = OnStart();
                if (!begun.IsRunning) return Finish(begun);
            }
            var result = OnTick();
            return result.IsRunning ? result : Finish(result);
        }

        /// <summary>Pause or a reflex: the working primitive stops and starts again on the next Step.</summary>
        internal void Interrupt()
        {
            if (_started && Result == null) OnInterrupt();
        }

        /// <summary>Stop for good.</summary>
        internal void Cancel()
        {
            if (Result != null) return;
            if (_started) OnStop();
            Result = Result.Fail("cancelled");
        }

        internal abstract Result OnStart();
        internal abstract Result OnTick();
        internal abstract void OnInterrupt();
        /// <summary>Clean-up, however it ended.</summary>
        internal abstract void OnStop();

        /// <summary>A primitive starts again from Begin on its next Step.</summary>
        internal void Restart() => _started = false;

        private Result Finish(Result result)
        {
            OnStop();
            Result = result;
            _run.Log(_depth, $"{this} → {result}{(Report != null ? " · " + Report : "")}");
            return result;
        }

        internal static string SnakeCase(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(name[i]));
            }
            return sb.ToString();
        }
    }

    /// <summary>What a skill gets from the Runner while it runs: the clock, the log, a way to ask for a pause.</summary>
    internal sealed class RunContext
    {
        private readonly Func<double> _clock;
        private readonly Action<int, string> _log;

        public RunContext(Func<double> clock, Action<int, string> log)
        {
            _clock = clock;
            _log = log;
        }

        public double Now => _clock();
        public void Log(int depth, string text) => _log(depth, text);

        /// <summary>Set by a composite's Pause step; the Runner pauses after this frame.</summary>
        public string PauseRequest;
    }
}
