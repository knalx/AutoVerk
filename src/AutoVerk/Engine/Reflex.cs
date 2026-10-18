namespace AutoVerk.Engine
{
    /// <summary>
    /// Steps in during any run when something happens: a monster in reach, low health. The window's Settings tab
    /// turns each one on or off.
    /// </summary>
    public abstract class Reflex
    {
        /// <summary>Defaults to the class name in snake_case.</summary>
        public virtual string Id => Skill.SnakeCase(GetType().Name);

        /// <summary>What it does, for the Settings tab: "Fight back when a monster comes at you".</summary>
        public virtual string Description => Id;

        public bool Enabled { get; set; } = true;

        /// <summary>Every frame of a run, so keep it cheap. The skill to run now, or null.</summary>
        public abstract Skill Check();
    }
}
