namespace AutoVerk.Engine
{
    /// <summary>How a skill is doing: still running, ended ok, or failed with a code and a reason for the player.</summary>
    public sealed class Result
    {
        public static readonly Result Running = new Result(null, null);
        public static readonly Result Success = new Result("ok", null);

        /// <param name="code">A short snake_case word a parent can check: not_found, stuck, timeout.</param>
        /// <param name="detail">A sentence for the player: "no tree within 30 m".</param>
        public static Result Fail(string code, string detail = null) =>
            new Result(string.IsNullOrEmpty(code) || code == "ok" ? "failed" : code, detail);

        private Result(string code, string detail)
        {
            Code = code;
            Detail = detail;
        }

        /// <summary>Null while running, "ok", or the failure code.</summary>
        public string Code { get; }
        public string Detail { get; }

        public bool IsRunning => Code == null;
        public bool Ok => Code == "ok";
        public bool Failed => Code != null && Code != "ok";

        public override string ToString() =>
            IsRunning ? "running" : Ok ? "ok" : Detail == null ? Code : $"{Code}: {Detail}";
    }
}
