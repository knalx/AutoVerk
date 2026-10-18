namespace AutoVerk.Engine
{
    /// <summary>Steps in during any run when something happens: a monster in reach, low health.</summary>
    public abstract class Reflex
    {
        /// <summary>Defaults to the class name in snake_case.</summary>
        public virtual string Id => Skill.SnakeCase(GetType().Name);

        public bool Enabled { get; set; } = true;

        /// <summary>Every frame of a run, so keep it cheap. The skill to run now, or null.</summary>
        public abstract Skill Check();
    }
}
